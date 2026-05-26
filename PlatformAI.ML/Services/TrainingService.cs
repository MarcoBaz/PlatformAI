using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.ML;
using Microsoft.ML.Trainers.FastTree;
using PlatformAI.Infrastructure;
using PlatformAI.Infrastructure.Application;
using Microsoft.EntityFrameworkCore;

namespace PlatformAI.ML.Services;

// ═══════════════════════════════════════════════════════════════════════════════
// CONCETTO: TRAINING INCREMENTALE
//
// Invece di riaddestrare il modello da zero ogni volta (costoso e lento),
// usiamo un approccio incrementale:
//   1. Ricordiamo fino a dove abbiamo già processato i dati (checkpoint)
//   2. Carichiamo solo i dati nuovi dall'ultimo checkpoint
//   3. Li combiniamo con una finestra di dati storici recenti (contesto)
//   4. Riaddestriamo il modello su questa finestra aggiornata
//
// Questo simula l'apprendimento continuo: il modello si aggiorna man mano
// che arrivano nuovi dati di produzione, senza sprecare risorse.
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Servizio responsabile del training del modello ML di previsione della produzione.
///
/// Flusso principale:
///   LoadCheckpoint → LoadNewData → EnrichFeatures → Train → EvaluateMetrics → SaveModel
///
/// Il modello impara a prevedere QuantityProduced (label) a partire
/// da misurazioni fisiche e temporali della macchina (feature).
/// </summary>
public class TrainingService
{
    // MLContext è il punto di ingresso di ML.NET.
    // Il seed=42 garantisce riproducibilità: con gli stessi dati, lo split
    // train/test sarà sempre identico. Utile per confrontare esperimenti.
    private readonly MLContext _mlContext;

    private readonly IUnitOfWork _uow;
    private readonly ILogger<TrainingService> _logger;
    private readonly IncrementalTrainingConfig _config;
    private readonly BlobStoragePersistence _blobStorage;

    public TrainingService(IUnitOfWork uow, IConfiguration configuration, ILogger<TrainingService>? logger = null, IncrementalTrainingConfig? config = null)
    {
        _mlContext = new MLContext(seed: 42);
        _uow = uow;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<TrainingService>.Instance;
        _config = config ?? new IncrementalTrainingConfig();

        var blobConnectionString = configuration.GetConnectionString("BlobStorage")
            ?? throw new InvalidOperationException("ConnectionStrings:BlobStorage non configurata in appsettings.json");

        _blobStorage = new BlobStoragePersistence(_mlContext, blobConnectionString, _logger);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // PUBLIC API
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Esegue il training incrementale: legge i dati dal checkpoint, arricchisce
    /// le feature, addestra e valuta il modello, poi salva tutto su Blob Storage.
    /// </summary>
    public async Task<IncrementalTrainingResult> TrainIncrementalAsync(string tenantCode, IncrementalTrainingConfig? config = null, CancellationToken cancellationToken = default)
    {
        // Usa la configurazione passata al metodo; se null, torna alla configurazione iniettata via DI.
        var cfg = config ?? _config;

        var result = new IncrementalTrainingResult
        {
            TenantCode = tenantCode,
            StartTime = DateTime.UtcNow
        };

        try
        {
            // ── STEP 1: CHECKPOINT ─────────────────────────────────────────────
            // Il checkpoint è un "segnalibro": registra fino a quale data abbiamo
            // già processato i dati. Alla prima esecuzione non esiste, quindi
            // partiamo dall'epoch (1970) per caricare tutto lo storico disponibile.
            var checkpoint = await _blobStorage.LoadCheckpointAsync(tenantCode).ConfigureAwait(false);
            var lastProcessedDate = checkpoint?.LastProcessedDate ?? new DateTime(1970, 1, 1);

            _logger.LogInformation("Training incrementale tenant {tenant} - checkpoint {checkpoint}", tenantCode, checkpoint?.LastProcessedDate);

            // ── STEP 2: NUOVI DATI ─────────────────────────────────────────────
            // Carichiamo solo i record arrivati DOPO l'ultimo checkpoint.
            // Questo è il "delta" - i dati che il modello non ha ancora visto.
            var newData = LoadDataSinceCheckpoint(tenantCode, lastProcessedDate);

            if (newData == null || newData.Count == 0)
            {
                _logger.LogInformation("Nessun nuovo dato da processare per tenant {tenant}", tenantCode);
                result.Success = true;
                result.Message = "Nessun nuovo dato da processare";
                result.NewRecordsCount = 0;
                result.EndTime = DateTime.UtcNow;
                return result;
            }

            result.NewRecordsCount = newData.Count;
            _logger.LogInformation("Trovati {count} nuovi record per tenant {tenant}", newData.Count, tenantCode);

            // ── STEP 3: CONTESTO STORICO ───────────────────────────────────────
            // Aggiungere solo i dati nuovi potrebbe portare a un modello che
            // "dimentica" i pattern del passato (problema noto come "catastrophic
            // forgetting"). Per mitigarlo, includiamo una finestra di dati storici
            // recenti che serve da ancoraggio alla conoscenza pregressa.
            var historicalData = LoadHistoricalData(tenantCode, lastProcessedDate, cfg);
            _logger.LogInformation("Dati storici caricati: {count}", historicalData.Count);

            // ── STEP 4: UNIONE E VALIDAZIONE ───────────────────────────────────
            // Combiniamo i dati nuovi con quelli storici. Il downsampling degli
            // storici (HistoricalSamplingRatio) riduce il costo computazionale
            // mantenendo comunque una rappresentazione del passato.
            var allTrainingData = CombineDataWithWeights(historicalData, newData, cfg);

            // Soglia minima: sotto MinDataPoints il modello non avrebbe abbastanza
            // esempi per generalizzare. Rischieremmo overfitting estremo.
            if (allTrainingData.Count < cfg.MinDataPoints)
            {
                result.Success = false;
                result.Message = $"Dati insufficienti: {allTrainingData.Count} < {cfg.MinDataPoints} richiesti";
                _logger.LogWarning(result.Message);
                result.EndTime = DateTime.UtcNow;
                return result;
            }

            // Feature engineering: trasforma i dati grezzi in feature significative
            // (vedi metodo EnrichFeatures per i dettagli).
            var enriched = EnrichFeatures(allTrainingData);
            ValidateEnrichedData(enriched);

            // ── STEP 5: TRAINING ───────────────────────────────────────────────
            // Il cuore del processo: costruisce la pipeline ML, divide i dati in
            // train/test, addestra il modello e calcola le metriche di valutazione.
            var trainingResult = await PerformTrainingAsync(tenantCode, enriched, cfg, cancellationToken)
                .ConfigureAwait(false);

            // ── STEP 6: AGGIORNA CHECKPOINT ────────────────────────────────────
            // Il nuovo checkpoint è la data massima tra i dati appena processati.
            // La prossima esecuzione partirà da qui, evitando di riprocessare
            // dati già visti.
            var newCheckpointDate = newData.Max(x => x.LastModifiedDate);

            // ── STEP 7: PERSISTENZA ATOMICA ────────────────────────────────────
            // Prima il modello, poi il checkpoint. Se il salvataggio del modello
            // fallisce, il checkpoint non viene aggiornato: alla prossima esecuzione
            // il training verrà ripetuto con gli stessi dati. Questo garantisce
            // che non perdiamo mai un modello valido.
            await _blobStorage.SaveModelAsync(trainingResult.Model, tenantCode, trainingResult.ModelVersion)
                .ConfigureAwait(false);
            await _blobStorage.SaveCheckpointAsync(tenantCode, newCheckpointDate, trainingResult)
                .ConfigureAwait(false);

            result.Success = true;
            result.ModelVersion = trainingResult.ModelVersion;
            result.RSquared = trainingResult.RSquared;   // quanto il modello spiega la varianza (0-1, più alto è meglio)
            result.RMSE = trainingResult.RMSE;           // errore medio in unità di produzione (es. 5 = sbaglia di ±5 pezzi)
            result.MAE = trainingResult.MAE;             // come RMSE ma meno sensibile agli outlier
            result.TotalDataUsed = allTrainingData.Count;
            result.PreviousCheckpoint = checkpoint?.LastProcessedDate;
            result.NewCheckpoint = newCheckpointDate;
            result.Message = "Training completato con successo";

            _logger.LogInformation("Training completato tenant {tenant}: R2={r2:F4}, RMSE={rmse:F2}", tenantCode, result.RSquared, result.RMSE);
        }
        catch (OperationCanceledException)
        {
            result.Success = false;
            result.Message = "Cancellazione richiesta";
            _logger.LogWarning("Training cancellato per tenant {tenant}", tenantCode);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = ex.Message;
            _logger.LogError(ex, "Errore durante il training per tenant {tenant}", tenantCode);
        }
        finally
        {
            result.EndTime = DateTime.UtcNow;
        }

        return result;
    }

    /// <summary>
    /// Forza un retraining completo ignorando il checkpoint.
    /// Usato quando il modello è molto degradato (drift critico) e serve ripartire
    /// da tutti i dati storici disponibili invece che solo dal delta recente.
    /// </summary>
    public async Task<IncrementalTrainingResult> ForceFullTrainingAsync(string tenantCode, IncrementalTrainingConfig? config = null, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Forcing full training for tenant {tenant}", tenantCode);
        await ResetCheckpointAsync(tenantCode).ConfigureAwait(false);
        return await TrainIncrementalAsync(tenantCode, config, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TrainingCheckpoint?> GetCheckpointAsync(string tenantCode)
        => await _blobStorage.LoadCheckpointAsync(tenantCode).ConfigureAwait(false);

    public async Task ResetCheckpointAsync(string tenantCode)
    {
        // TODO: implementare la cancellazione del checkpoint su Blob Storage
        // (attualmente stub - ResetCheckpointAsync non fa nulla)
        await Task.CompletedTask;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // DATA LOADING
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Carica i record di produzione arrivati dopo l'ultimo checkpoint.
    /// Il filtro x.QuantityProduced > 0 esclude record non validi (macchina ferma).
    /// </summary>
    private List<ProductionData> LoadDataSinceCheckpoint(string tenantCode, DateTime lastProcessedDate)
    {
        var repo = _uow.Repository<ProductionData>();
        var query = repo.Query(x => x.LastModifiedDate > lastProcessedDate)
            .Include(x => x.Machine)
            .ThenInclude(m => m.ProductionLine)
            .ThenInclude(pl => pl.Department)
            .AsQueryable();
            // solo dati nuovi rispetto al checkpoint

        var list = query.Where(x=> x.Machine.ProductionLine.Department.TenantCode == tenantCode).OrderBy(x => x.LastModifiedDate).ToList();
        return list;
    }

    /// <summary>
    /// Carica dati storici precedenti al checkpoint come contesto di training.
    /// Serve a evitare il "catastrophic forgetting": il modello mantiene memoria
    /// del comportamento passato anche quando vengono aggiunti nuovi dati.
    /// </summary>
    private List<ProductionData> LoadHistoricalData(string tenantCode, DateTime beforeDate, IncrementalTrainingConfig cfg)
    {
        if (!cfg.IncludeHistoricalContext)
            return new List<ProductionData>();

        var repo = _uow.Repository<ProductionData>();

        // Finestra storica: da (beforeDate - HistoricalContextDays) fino a beforeDate
        var historicalCutoff = beforeDate.AddDays(-cfg.HistoricalContextDays);

        var query = repo.Query(x =>
            x.Machine.ProductionLine.Department.TenantCode == tenantCode &&
            x.LastModifiedDate > historicalCutoff &&
            x.LastModifiedDate <= beforeDate);

        var list = query.OrderBy(x => x.LastModifiedDate)
                        .Take(cfg.MaxHistoricalRecords)   // cap per non sovraccaricare la memoria
                        .ToList();

        return list;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // COMBINING / SAMPLING
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Unisce i nuovi dati con quelli storici.
    /// I nuovi dati vengono sempre inclusi per intero.
    /// Gli storici possono essere campionati (downsampling) tramite HistoricalSamplingRatio
    /// per ridurre il costo computazionale mantenendo la diversità dei dati.
    /// </summary>
    private List<ProductionData> CombineDataWithWeights(
        List<ProductionData> historical, List<ProductionData> newData, IncrementalTrainingConfig cfg)
    {
        var combined = new List<ProductionData>();

        // I nuovi dati hanno priorità: vengono aggiunti per primi
        combined.AddRange(newData);

        if (historical.Any() && cfg.IncludeHistoricalContext)
        {
            // Prendiamo i più recenti tra gli storici (più rilevanti per il modello)
            var sampled = historical
                .OrderByDescending(x => x.LastModifiedDate)
                .Take(cfg.MaxHistoricalRecords)
                .ToList();

            // Downsampling opzionale: se ratio=0.5 usiamo solo metà degli storici
            if (cfg.HistoricalSamplingRatio < 1.0)
            {
                var take = (int)Math.Ceiling(sampled.Count * cfg.HistoricalSamplingRatio);
                sampled = sampled.Take(take).ToList();
            }

            combined.AddRange(sampled);
        }

        // Riordiniamo per Timestamp: gli algoritmi ML su serie temporali
        // beneficiano di dati in ordine cronologico
        return combined.OrderBy(x => x.Timestamp).ToList();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // TRAINING
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Costruisce la pipeline ML, addestra il modello e calcola le metriche.
    ///
    /// CONCETTO: PIPELINE ML
    /// Una pipeline è una sequenza di trasformazioni applicata ai dati prima
    /// che arrivino all'algoritmo di training. In ML.NET ogni step è un
    /// IEstimator che produce un ITransformer quando viene fittato.
    ///
    /// La nostra pipeline fa:
    ///   1. CopyColumns    → rinomina QuantityProduced in "Label" (convenzione ML.NET)
    ///   2. Concatenate    → unisce tutte le feature in un unico vettore "Features"
    ///   3. NormalizeMinMax → porta tutte le feature in [0,1]
    ///   4. Trainer        → l'algoritmo che impara la relazione feature → label
    /// </summary>
    private async Task<TrainingResultInternal> PerformTrainingAsync(
        string tenantCode, List<ProductionDataEnriched> enrichedData, IncrementalTrainingConfig cfg, CancellationToken cancellationToken)
    {
        // Mappa i dati arricchiti nel DTO che ML.NET sa leggere (solo float, come richiede il framework)
        var mlData = enrichedData.Select(x => new ProductionDataMLEnriched
        {
            QuantityProduced      = x.QuantityProduced,
            CycleTime             = (float)x.CycleTime,
            EnergyConsumption     = (float)x.EnergyConsumption,
            Temperature           = (float)x.Temperature,
            ScrapQuantity         = x.ScrapQuantity,
            ScrapRate             = x.ScrapRate,             // feature derivata: ScrapQuantity / QuantityProduced
            EffectiveProductionRate = x.EffectiveProductionRate, // feature derivata: QuantityProduced / CycleTime
            EnergyPerUnit         = x.EnergyPerUnit,         // feature derivata: EnergyConsumption / QuantityProduced
            HourOfDay             = x.HourOfDay,             // feature temporale: cattura variazioni intragiornaliere
            DayOfWeek             = x.DayOfWeek,             // feature temporale: cattura variazioni settimanali
            IsWeekend             = x.IsWeekend,             // feature binaria: turni weekend spesso hanno pattern diversi
            Shift                 = x.Shift,                 // feature categorica: turno 1/2/3 (mattina/pomeriggio/notte)
            AvgQuantityLast3      = x.AvgQuantityLast3,      // rolling window: media degli ultimi 3 cicli (momentum)
            AvgCycleTimeLast3     = x.AvgCycleTimeLast3,
            AvgTemperatureLast3   = x.AvgTemperatureLast3,
            AvgEnergyLast3        = x.AvgEnergyLast3
        }).ToList();

        // Carica i dati in ML.NET come IDataView (struttura colonnare ottimizzata)
        var data = _mlContext.Data.LoadFromEnumerable(mlData);

        // ── TRAIN / TEST SPLIT ─────────────────────────────────────────────────
        // Dividiamo i dati in due set:
        //   - TrainSet (80%): il modello ci impara sopra
        //   - TestSet  (20%): serve solo per misurare le performance su dati mai visti
        //
        // PERCHÉ: se valutassimo il modello sugli stessi dati usati per il training,
        // otterremmo sempre metriche eccellenti (il modello ha già visto quei dati).
        // Il TestSet simula il comportamento reale su dati futuri.
        // Un grande divario tra metriche su TrainSet e TestSet indica overfitting.
        var split = _mlContext.Data.TrainTestSplit(data, testFraction: cfg.TestFraction);

        // ── DEFINIZIONE DELLE FEATURE ──────────────────────────────────────────
        // Lista esplicita di tutte le colonne che il modello userà per fare previsioni.
        // NON include "QuantityProduced" (quella è la label, la risposta da prevedere).
        var featureCols = new[]
        {
            // Feature fisiche dirette (misurate dalla macchina)
            nameof(ProductionDataMLEnriched.CycleTime),
            nameof(ProductionDataMLEnriched.EnergyConsumption),
            nameof(ProductionDataMLEnriched.Temperature),
            nameof(ProductionDataMLEnriched.ScrapQuantity),

            // Feature derivate (calcolate da noi in EnrichFeatures)
            // Catturano relazioni che le feature grezze non esprimono direttamente
            nameof(ProductionDataMLEnriched.ScrapRate),
            nameof(ProductionDataMLEnriched.EffectiveProductionRate),
            nameof(ProductionDataMLEnriched.EnergyPerUnit),

            // Feature temporali (estratte dal Timestamp)
            // Catturano pattern ciclici: ore di punta, turni, fine settimana
            nameof(ProductionDataMLEnriched.HourOfDay),
            nameof(ProductionDataMLEnriched.DayOfWeek),
            nameof(ProductionDataMLEnriched.IsWeekend),
            nameof(ProductionDataMLEnriched.Shift),

            // Rolling windows (medie degli ultimi 3 cicli)
            // Catturano il "momentum" della macchina: se i 3 cicli precedenti
            // erano lenti, probabilmente anche il prossimo lo sarà
            nameof(ProductionDataMLEnriched.AvgQuantityLast3),
            nameof(ProductionDataMLEnriched.AvgCycleTimeLast3),
            nameof(ProductionDataMLEnriched.AvgTemperatureLast3),
            nameof(ProductionDataMLEnriched.AvgEnergyLast3)
        };

        // ── COSTRUZIONE PIPELINE ───────────────────────────────────────────────
        IEstimator<ITransformer> pipeline =

            // Step 1: rinomina QuantityProduced → "Label" (nome atteso da ML.NET per la colonna target)
            _mlContext.Transforms.CopyColumns("Label", nameof(ProductionDataMLEnriched.QuantityProduced))

            // Step 2: concatena tutte le feature in un unico vettore numerico chiamato "Features"
            // ML.NET richiede che le feature siano in un vettore unico per poter addestrare il modello
            .Append(_mlContext.Transforms.Concatenate("Features", featureCols))

            // Step 3: normalizzazione Min-Max → porta ogni feature in [0, 1]
            // Formula: (valore - min) / (max - min)
            // PERCHÉ: feature con scale diverse (es. Temperature 70-90 vs HourOfDay 0-23)
            // potrebbero far sì che il modello assegni pesi sproporzionati alle più grandi.
            // Con FastTree (alberi decisionali) l'impatto è minore, ma con SDCA è essenziale.
            .Append(_mlContext.Transforms.NormalizeMinMax("Features"));

        // ── SCELTA DELL'ALGORITMO ──────────────────────────────────────────────
        switch (cfg.Trainer)
        {
            case TrainerType.FastTree:
                // FastTree è un algoritmo basato su alberi decisionali con Gradient Boosting.
                // Crea NumberOfTrees alberi in sequenza, ognuno corregge gli errori del precedente.
                //
                // PARAMETRI E OVERFITTING:
                //   NumberOfTrees: più alberi = modello più complesso. Aumenta la capacità ma
                //     rischia overfitting. Buon punto di partenza: 100-200.
                //   NumberOfLeaves: quante "foglie" (caselle terminali) ha ogni albero.
                //     Più foglie = albero più dettagliato = rischio overfitting. Partire da 20.
                //   MinimumExampleCountPerLeaf: ogni foglia deve avere almeno N esempi.
                //     Aumentarlo è il modo più diretto per ridurre l'overfitting.
                //   LearningRate: quanto ogni albero corregge il precedente.
                //     Troppo alto = overfitting veloce. Troppo basso = training lento. Default: 0.1
                pipeline = pipeline.Append(_mlContext.Regression.Trainers.FastTree(
                    new FastTreeRegressionTrainer.Options
                    {
                        NumberOfTrees               = cfg.NumberOfTrees,
                        NumberOfLeaves              = cfg.NumberOfLeaves,
                        MinimumExampleCountPerLeaf  = cfg.MinimumExampleCountPerLeaf,
                        LearningRate                = cfg.LearningRate,
                        LabelColumnName             = "Label",
                        FeatureColumnName           = "Features"
                    }));
                break;

            case TrainerType.Sdca:
                // SDCA (Stochastic Dual Coordinate Ascent) è una regressione lineare regolarizzata.
                // Più semplice di FastTree: cerca una relazione lineare tra feature e label.
                // Vantaggi: molto meno soggetto a overfitting, training veloce, interpretabile.
                // Svantaggi: non cattura relazioni non-lineari (es. la produzione che cala
                // molto rapidamente oltre una certa temperatura).
                // La regularizzazione L2 incorporata penalizza automaticamente la complessità.
                pipeline = pipeline.Append(_mlContext.Regression.Trainers.Sdca(
                    labelColumnName: "Label",
                    featureColumnName: "Features"));
                break;

            default:
                throw new NotSupportedException($"Trainer {cfg.Trainer} non supportato");
        }

        // ── FIT (TRAINING VERO E PROPRIO) ──────────────────────────────────────
        var model = pipeline.Fit(split.TrainSet);

        // ── VALUTAZIONE SU TRAIN SET ───────────────────────────────────────────
        // Valutiamo il modello anche sui dati con cui ha imparato.
        // Se le metriche train sono molto migliori di quelle test → overfitting.
        var trainPredictions = model.Transform(split.TrainSet);
        var trainMetrics = _mlContext.Regression.Evaluate(
            trainPredictions,
            labelColumnName: "Label",
            scoreColumnName: "Score");

        // ── VALUTAZIONE SUL TEST SET ───────────────────────────────────────────
        var testPredictions = model.Transform(split.TestSet);
        var testMetrics = _mlContext.Regression.Evaluate(
            testPredictions,
            labelColumnName: "Label",
            scoreColumnName: "Score");

        // ── DIAGNOSI OVERFITTING / UNDERFITTING ────────────────────────────────
        var r2Gap = trainMetrics.RSquared - testMetrics.RSquared;
        string diagnosis;
        if (trainMetrics.RSquared < 0.5 && testMetrics.RSquared < 0.5)
            diagnosis = "⚠️  UNDERFITTING — R² basso su entrambi i set. Il modello non riesce a catturare i pattern. Prova ad aggiungere feature o aumentare NumberOfTrees/NumberOfLeaves.";
        else if (r2Gap > 0.20)
            diagnosis = $"⚠️  OVERFITTING — Gap R² Train/Test = {r2Gap:F4}. Il modello memorizza i dati invece di generalizzare. Aumenta MinimumExampleCountPerLeaf o riduci NumberOfLeaves.";
        else if (r2Gap > 0.10)
            diagnosis = $"⚡ OVERFITTING LIEVE — Gap R² = {r2Gap:F4}. Accettabile ma tieni d'occhio. Considera di aumentare i dati di training.";
        else if (testMetrics.RSquared >= 0.80)
            diagnosis = "✅ BUONO — Il modello generalizza bene.";
        else if (testMetrics.RSquared >= 0.60)
            diagnosis = "🟡 ACCETTABILE — R² nella norma per dati industriali con poca storia.";
        else
            diagnosis = "🔴 DEBOLE — R² test basso. Potrebbe migliorare con più dati o feature più informative.";

        _logger.LogInformation(
            "══════════════════════════════════════════════════════════\n" +
            "  TRAINING REPORT — tenant: {tenant}\n" +
            "──────────────────────────────────────────────────────────\n" +
            "  Dataset    : {total} record totali  ({train} train / {test} test)\n" +
            "  Trainer    : {trainer}  (Trees={trees}, Leaves={leaves}, LR={lr})\n" +
            "──────────────────────────────────────────────────────────\n" +
            "  TRAIN SET  : R²={trainR2:F4}  RMSE={trainRmse:F2}  MAE={trainMae:F2}\n" +
            "  TEST SET   : R²={testR2:F4}  RMSE={testRmse:F2}  MAE={testMae:F2}\n" +
            "  Gap R²     : {gap:F4}\n" +
            "──────────────────────────────────────────────────────────\n" +
            "  {diagnosis}\n" +
            "══════════════════════════════════════════════════════════",
            tenantCode,
            mlData.Count,
            mlData.Count - (int)(mlData.Count * cfg.TestFraction),
            (int)(mlData.Count * cfg.TestFraction),
            cfg.Trainer,
            cfg.NumberOfTrees, cfg.NumberOfLeaves, cfg.LearningRate,
            trainMetrics.RSquared, trainMetrics.RootMeanSquaredError, trainMetrics.MeanAbsoluteError,
            testMetrics.RSquared, testMetrics.RootMeanSquaredError, testMetrics.MeanAbsoluteError,
            r2Gap,
            diagnosis);

        return await Task.FromResult(new TrainingResultInternal
        {
            Model        = model,
            ModelVersion = GenerateModelVersion(tenantCode),
            RSquared     = testMetrics.RSquared,
            RMSE         = testMetrics.RootMeanSquaredError,
            MAE          = testMetrics.MeanAbsoluteError,
            DataCount    = mlData.Count
        });
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // FEATURE ENGINEERING
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Trasforma i dati grezzi di produzione in feature significative per il modello.
    ///
    /// CONCETTO: FEATURE ENGINEERING
    /// I dati grezzi raramente sono nella forma ottimale per il ML.
    /// Trasformarli in rappresentazioni più informative (feature engineering)
    /// è spesso ciò che fa la differenza tra un modello mediocre e uno buono.
    ///
    /// Tipologie di feature che creiamo:
    ///   1. Feature derivate     → rapporti e combinazioni di misure grezze
    ///   2. Feature temporali    → informazioni estratte dal timestamp
    ///   3. Rolling windows      → medie mobili dei cicli precedenti (memoria a breve termine)
    /// </summary>
    private List<ProductionDataEnriched> EnrichFeatures(List<ProductionData> rawData)
    {
        var enriched = new List<ProductionDataEnriched>();

        // Ordiniamo per Timestamp: necessario per calcolare correttamente le rolling windows
        var ordered = rawData.OrderBy(x => x.Timestamp).ToList();

        for (int i = 0; i < ordered.Count; i++)
        {
            var current = ordered[i];
            var qty     = current.GetMetric("quantity_produced");
            var scrap   = current.GetMetric("scrap_quantity");
            var cycle   = current.GetMetric("cycle_time");
            var energy  = current.GetMetric("energy_consumption");
            var temp    = current.GetMetric("temperature");

            var item = new ProductionDataEnriched
            {
                // ── FEATURE GREZZE ─────────────────────────────────────────────
                QuantityProduced  = (int)qty,   // questa è la LABEL, non una feature
                ScrapQuantity     = (int)scrap,
                CycleTime         = cycle,
                EnergyConsumption = energy,
                Temperature       = temp,

                // ── FEATURE DERIVATE ───────────────────────────────────────────
                ScrapRate = qty > 0 ? (float)scrap / (float)qty : 0,

                EffectiveProductionRate = cycle > 0 ? (float)qty / (float)cycle : 0,

                EnergyPerUnit = qty > 0 ? (float)energy / (float)qty : 0,

                // ── FEATURE TEMPORALI ──────────────────────────────────────────
                // HourOfDay: cattura variazioni intragiornaliere
                // (es. la produzione cala nelle ultime ore del turno per fatica operatori)
                HourOfDay = current.Timestamp.Hour,

                // DayOfWeek: cattura variazioni settimanali
                // (es. il lunedì mattina le macchine sono "fredde" dopo il weekend)
                DayOfWeek = (int)current.Timestamp.DayOfWeek,

                // IsWeekend: binario, i turni di sabato/domenica hanno spesso
                // crew ridotti e ritmi di produzione diversi
                IsWeekend = (current.Timestamp.DayOfWeek == System.DayOfWeek.Saturday ||
                             current.Timestamp.DayOfWeek == System.DayOfWeek.Sunday) ? 1 : 0,

                // Shift: il turno di lavoro (1=mattina, 2=pomeriggio, 3=notte).
                // I turni notturni tipicamente hanno performance diverse dagli altri.
                Shift = current.Timestamp.Hour switch
                {
                    >= 6  and < 14 => 1,   // mattina
                    >= 14 and < 22 => 2,   // pomeriggio
                    _              => 3    // notte
                }
            };

            // ── ROLLING WINDOWS (dal 3° elemento in poi) ───────────────────────
            // Le medie degli ultimi 3 cicli catturano il "momentum" della macchina:
            // se i cicli precedenti erano lenti, molto probabilmente anche il prossimo lo sarà.
            // Questo introduce "memoria a breve termine" nel modello, che altrimenti
            // tratta ogni ciclo come indipendente dagli altri.
            //
            // NOTA: i primi 2 elementi non hanno abbastanza storia → le rolling
            // windows rimangono 0 (il default). Potrebbe essere migliorato con
            // un'imputation basata sulla media globale.
            if (i >= 2)
            {
                var last3 = ordered.Skip(i - 2).Take(3).ToList();
                item.AvgQuantityLast3    = (float)last3.Average(x => (double)x.GetMetric("quantity_produced"));
                item.AvgCycleTimeLast3   = (float)last3.Average(x => (double)x.GetMetric("cycle_time"));
                item.AvgTemperatureLast3 = (float)last3.Average(x => (double)x.GetMetric("temperature"));
                item.AvgEnergyLast3      = (float)last3.Average(x => (double)x.GetMetric("energy_consumption"));
            }

            enriched.Add(item);
        }

        return enriched;
    }

    /// <summary>
    /// Validazione base del dataset arricchito prima del training.
    /// TODO: aggiungere rimozione delle righe con NaN/Inf e imputation
    /// (sostituzione dei valori mancanti con media/mediana).
    /// </summary>
    private void ValidateEnrichedData(List<ProductionDataEnriched> enriched)
    {
        if (enriched == null || enriched.Count == 0)
            throw new InvalidOperationException("Dataset arricchito vuoto");

        // TODO: rimuovere righe con NaN o Inf (possono bloccare il training di FastTree)
        // TODO: imputation per i valori mancanti nelle rolling windows (i=0 e i=1)
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // MODEL PERSISTENCE
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Genera una versione univoca per il modello nel formato TENANT_YYYYMMDD_HHmmss.
    /// Usata per versionare i modelli su Blob Storage e permettere rollback.
    /// </summary>
    private string GenerateModelVersion(string tenantCode)
        => $"{tenantCode}_{DateTime.UtcNow:yyyyMMdd_HHmmss}";

    /// <summary>
    /// Esegue una singola previsione dato un set di feature.
    /// Carica il modello "latest" dal Blob Storage per il tenant specificato.
    ///
    /// NOTA: CreatePredictionEngine è comodo per previsioni singole ma non è
    /// thread-safe. In produzione ad alto throughput usare PredictionEnginePool.
    /// </summary>
    public ProductionPrediction? Predict(ProductionDataMLEnriched features, string tenantCode)
    {
        // Carica il modello più recente salvato su Blob Storage
        var model = _blobStorage.LoadLatestModel(tenantCode);
        if (model == null) return null;

        // Crea il motore di previsione: incapsula il modello per accettare
        // un singolo esempio di input e restituire la previsione (Score)
        var engine = _mlContext.Model.CreatePredictionEngine<ProductionDataMLEnriched, ProductionPrediction>(model);
        return engine.Predict(features);
    }
}
