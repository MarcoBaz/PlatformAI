# 🎯 Guida al Fine Tuning - PlatformAI Prediction System

Questo documento descrive le strategie e le best practice per ottimizzare il sistema di predizione ibrido di PlatformAI.

## Indice

1. [Panoramica del Sistema](#panoramica-del-sistema)
2. [Fase 1: Baseline Assessment](#fase-1-baseline-assessment)
3. [Fase 2: Grid Search delle Soglie](#fase-2-grid-search-delle-soglie)
4. [Fase 3: Fine Tuning del Modello ML](#fase-3-fine-tuning-del-modello-ml)
5. [Fase 4: Monitoraggio Continuo](#fase-4-monitoraggio-continuo)
6. [Checklist e Troubleshooting](#checklist-e-troubleshooting)
7. [Esempi di Codice](#esempi-di-codice)

---

## Panoramica del Sistema

Il sistema di predizione utilizza un approccio **ibrido** che seleziona automaticamente la fonte migliore tra:

| Fonte | Descrizione | Quando Usarla |
|-------|-------------|---------------|
| **Database** | Media mobile ultimi 10 record (+2% trend) | R² < 0.5 o modello non disponibile |
| **ML Model** | FastTree/SDCA con 15 features | R² >= 0.75 |
| **Hybrid** | Media pesata (70% ML + 30% DB) | 0.5 <= R² < 0.75 |

### Architettura Decisionale

```
┌─────────────────────────────────────────────────────────────┐
│                    SMART PREDICTION FLOW                     │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│   Input: GetPredictionArgs (target, horizon)                │
│                    │                                        │
│                    ▼                                        │
│   ┌─────────────────────────────────┐                      │
│   │  GetComparisonPredictionAsync   │                      │
│   │  (esegue entrambe le predizioni)│                      │
│   └─────────────────────────────────┘                      │
│                    │                                        │
│                    ▼                                        │
│   ┌─────────────────────────────────┐                      │
│   │     Valuta R² del modello ML    │                      │
│   └─────────────────────────────────┘                      │
│                    │                                        │
│        ┌──────────┼──────────┐                             │
│        ▼          ▼          ▼                             │
│   ┌─────────┐ ┌─────────┐ ┌─────────┐                     │
│   │R² >= 0.75│ │0.5<=R²<0.75│ │R² < 0.5│                   │
│   │   ML    │ │  Hybrid │ │Database │                     │
│   └─────────┘ └─────────┘ └─────────┘                     │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

---

## Fase 1: Baseline Assessment

### Obiettivo
Stabilire le metriche di riferimento prima di qualsiasi ottimizzazione.

### Procedura

```csharp
// 1. Crea il servizio con TrainingService
var trainingService = serviceProvider.GetRequiredService<TrainingService>();
var analyticsService = new LLMAnalyticsService(
    logger, 
    llmConfig, 
    uow, 
    trainingService);

// 2. Esegui backtest con configurazione default
var baselineResult = await analyticsService.RunBacktestAsync(
    tenantCode: "TENANT-001",
    daysToTest: 60,  // Minimo 30, consigliato 60
    config: SmartPredictionConfig.Default
);

// 3. Analizza i risultati
Console.WriteLine(baselineResult.GenerateReport());

// 4. Salva le metriche di baseline
var baseline = new
{
    DatabaseMAPE = baselineResult.DatabaseMetrics?.MeanAbsolutePercentageError,
    MLMAPE = baselineResult.MLMetrics?.MeanAbsolutePercentageError,
    SmartMAPE = baselineResult.SmartMetrics?.MeanAbsolutePercentageError,
    Date = DateTime.UtcNow
};
```

### Metriche da Registrare

| Metrica | Target Ideale | Accettabile | Critico |
|---------|---------------|-------------|---------|
| **MAPE Smart** | < 5% | 5-10% | > 15% |
| **R² Modello ML** | > 0.8 | 0.6-0.8 | < 0.5 |
| **RMSE** | < 50 | 50-100 | > 100 |
| **Std Dev Errore** | < 5 | 5-10 | > 10 |

---

## Fase 2: Grid Search delle Soglie

### Obiettivo
Trovare la combinazione ottimale di parametri per `SmartPredictionConfig`.

### Parametri da Ottimizzare

| Parametro | Range Consigliato | Default | Impatto |
|-----------|-------------------|---------|---------|
| `R2ThresholdHigh` | 0.60 - 0.85 | 0.75 | Quando usare solo ML |
| `R2ThresholdLow` | 0.40 - 0.60 | 0.50 | Quando usare solo DB |
| `MLWeightInHybrid` | 0.50 - 0.80 | 0.70 | Peso ML in zona ibrida |
| `HybridConfidenceMultiplier` | 0.70 - 0.90 | 0.80 | Confidenza ibrida |

### Procedura Grid Search

```csharp
// Definisci lo spazio di ricerca
var r2HighValues = new[] { 0.65, 0.70, 0.75, 0.80, 0.85 };
var r2LowValues = new[] { 0.40, 0.45, 0.50, 0.55, 0.60 };
var mlWeightValues = new[] { 0.50, 0.60, 0.70, 0.80 };

var results = new List<GridSearchResult>();

foreach (var r2High in r2HighValues)
{
    foreach (var r2Low in r2LowValues.Where(l => l < r2High - 0.1)) // Gap minimo 0.1
    {
        foreach (var mlWeight in mlWeightValues)
        {
            var config = new SmartPredictionConfig
            {
                R2ThresholdHigh = r2High,
                R2ThresholdLow = r2Low,
                MLWeightInHybrid = mlWeight
            };

            var result = await analyticsService.RunBacktestAsync(
                "TENANT-001", 
                daysToTest: 60, 
                config: config);

            if (result.Success)
            {
                results.Add(new GridSearchResult
                {
                    Config = config,
                    MAPE = result.SmartMetrics!.MeanAbsolutePercentageError,
                    MedianError = result.SmartMetrics.MedianError,
                    MaxError = result.SmartMetrics.MaxError
                });
            }
        }
    }
}

// Trova la configurazione migliore
var bestConfig = results
    .OrderBy(r => r.MAPE)
    .ThenBy(r => r.MaxError)  // A parità di MAPE, preferisci errore max più basso
    .First();

Console.WriteLine($"Migliore configurazione trovata:");
Console.WriteLine($"  R²High: {bestConfig.Config.R2ThresholdHigh}");
Console.WriteLine($"  R²Low: {bestConfig.Config.R2ThresholdLow}");
Console.WriteLine($"  MLWeight: {bestConfig.Config.MLWeightInHybrid}");
Console.WriteLine($"  MAPE: {bestConfig.MAPE:F2}%");
```

### Configurazioni Predefinite

```csharp
// Conservativa - per ambienti critici dove l'errore è costoso
SmartPredictionConfig.Conservative
// R²High=0.85, R²Low=0.65, MLWeight=50%

// Default - bilanciata
SmartPredictionConfig.Default
// R²High=0.75, R²Low=0.50, MLWeight=70%

// Aggressiva - per massimizzare l'uso del ML
SmartPredictionConfig.Aggressive
// R²High=0.60, R²Low=0.40, MLWeight=80%
```

---

## Fase 3: Fine Tuning del Modello ML

### Quando Ottimizzare il Modello

- R² < 0.6 dopo il training iniziale
- RMSE > 100
- Performance degrada nel tempo (concept drift)

### Hyperparameters FastTree

```csharp
var mlConfig = new IncrementalTrainingConfig
{
    // === STRUTTURA ALBERO ===
    NumberOfTrees = 100,        // Range: 50-300
                                // ↑ più alberi = più accurato ma più lento
                                // Consiglio: inizia con 100, aumenta se R² < 0.7
    
    NumberOfLeaves = 40,        // Range: 20-100
                                // ↑ più foglie = più granularità
                                // Attenzione: troppo alto causa overfitting
    
    MinimumExampleCountPerLeaf = 5,  // Range: 1-20
                                      // ↑ più alto = più generalizzazione
                                      // ↓ più basso = più sensibile ai dettagli
    
    // === LEARNING ===
    LearningRate = 0.1,         // Range: 0.01-0.3
                                // ↓ più basso = training più stabile ma lento
                                // Consiglio: 0.05-0.1 per dati rumorosi
    
    // === DATI ===
    MinDataPoints = 50,         // Minimo record per training
    HistoricalContextDays = 30, // Giorni di storico da includere
    MaxHistoricalRecords = 500, // Limite record storici
    HistoricalSamplingRatio = 1.0, // 1.0 = usa tutti, 0.5 = usa 50%
    
    // === VALIDAZIONE ===
    TestFraction = 0.2,         // 20% per test, 80% per training
    
    // === TRAINER ===
    Trainer = TrainerType.FastTree  // o TrainerType.Sdca
};
```

### Strategia di Tuning Hyperparameters

```
┌────────────────────────────────────────────────────────────┐
│           DECISION TREE - HYPERPARAMETER TUNING            │
├────────────────────────────────────────────────────────────┤
│                                                            │
│  R² < 0.5?                                                │
│  ├─ Sì → Aumenta NumberOfTrees (150-200)                  │
│  │       Aumenta HistoricalContextDays (60)               │
│  │       Riduci LearningRate (0.05)                       │
│  │                                                        │
│  └─ No → R² > 0.9? (possibile overfitting)               │
│          ├─ Sì → Aumenta MinimumExampleCountPerLeaf (10)  │
│          │       Riduci NumberOfLeaves (30)               │
│          │       Aumenta TestFraction (0.25)              │
│          │                                                │
│          └─ No → Configurazione OK                        │
│                                                            │
│  RMSE troppo alto?                                        │
│  ├─ Sì → Verifica outliers nei dati                       │
│  │       Considera feature scaling                        │
│  │       Aumenta MinDataPoints                            │
│  │                                                        │
│  └─ No → Configurazione OK                                │
│                                                            │
└────────────────────────────────────────────────────────────┘
```

### Feature Engineering Avanzato

Le 15 features attuali del modello:

| Categoria | Feature | Descrizione |
|-----------|---------|-------------|
| **Base** | CycleTime | Tempo ciclo in minuti |
| | EnergyConsumption | Consumo kWh |
| | Temperature | Temperatura °C |
| | ScrapQuantity | Pezzi scartati |
| **Derivate** | ScrapRate | Scrap / Quantity |
| | EffectiveProductionRate | Quantity / CycleTime |
| | EnergyPerUnit | Energy / Quantity |
| **Temporali** | HourOfDay | Ora (0-23) |
| | DayOfWeek | Giorno (0-6) |
| | IsWeekend | Flag weekend |
| | Shift | Turno (1-3) |
| **Rolling** | AvgQuantityLast3 | Media ultimi 3 cicli |
| | AvgCycleTimeLast3 | Media tempo ciclo |
| | AvgTemperatureLast3 | Media temperatura |
| | AvgEnergyLast3 | Media energia |

#### Nuove Features da Considerare

```csharp
// Trend features
public float QuantityTrend { get; set; }  // (last - first) / first degli ultimi N
public float EnergyTrend { get; set; }

// Variabilità
public float QuantityStdDev { get; set; } // Deviazione standard ultimi N

// Stagionalità
public float MonthOfYear { get; set; }    // Per pattern stagionali
public float WeekOfYear { get; set; }

// Machine-specific
public float MachineAge { get; set; }     // Giorni dall'installazione
public float HoursSinceLastMaintenance { get; set; }
```

---

## Fase 4: Monitoraggio Continuo

### Sistema di Alerting

```csharp
public class PredictionMonitor
{
    private readonly double _mapeThreshold = 15.0;
    private readonly double _r2Threshold = 0.5;
    private readonly int _consecutiveDaysThreshold = 3;
    
    public async Task<MonitoringResult> CheckHealthAsync(
        string tenantCode, 
        LLMAnalyticsService service)
    {
        var result = new MonitoringResult { TenantCode = tenantCode };
        
        // 1. Verifica R² del modello
        var comparison = await service.GetComparisonPredictionAsync(
            tenantCode, 
            new GetPredictionArgs { Target = "quantity" });
        
        var r2 = comparison.MLModelPrediction.ModelRSquared;
        
        if (!r2.HasValue || r2.Value < _r2Threshold)
        {
            result.Alerts.Add(new Alert
            {
                Level = AlertLevel.Warning,
                Message = $"R² del modello basso: {r2:F3}. Considerare re-training.",
                Action = "Eseguire TrainingService.ForceFullTrainingAsync()"
            });
        }
        
        // 2. Verifica MAPE recente
        var backtest = await service.RunBacktestAsync(tenantCode, daysToTest: 7);
        
        if (backtest.Success && 
            backtest.SmartMetrics!.MeanAbsolutePercentageError > _mapeThreshold)
        {
            result.Alerts.Add(new Alert
            {
                Level = AlertLevel.Critical,
                Message = $"MAPE alto negli ultimi 7 giorni: {backtest.SmartMetrics.MeanAbsolutePercentageError:F2}%",
                Action = "Rivedere configurazione SmartPrediction e/o re-training"
            });
        }
        
        return result;
    }
}
```

### Schedule Consigliato

| Attività | Frequenza | Trigger Automatico |
|----------|-----------|-------------------|
| Backtest verifica | Settimanale | Lunedì 6:00 |
| Check R² modello | Giornaliero | Ogni predizione |
| Re-training incrementale | Settimanale | Domenica 2:00 |
| Full re-training | Mensile | 1° del mese o R² < 0.5 |
| Grid search configurazione | Trimestrale | Manuale |

### Logging delle Predizioni

```csharp
// Struttura per tracciare le predizioni nel tempo
public class PredictionLog
{
    public int Id { get; set; }
    public string TenantCode { get; set; }
    public DateTime PredictionTime { get; set; }
    public string Target { get; set; }  // quantity, scrap, energy
    
    // Predizione
    public double PredictedValue { get; set; }
    public PredictionSource Source { get; set; }
    public double? Confidence { get; set; }
    public double? ModelR2 { get; set; }
    
    // Valore reale (popolato dopo)
    public double? ActualValue { get; set; }
    public DateTime? ActualValueRecordedAt { get; set; }
    
    // Errore (calcolato)
    public double? AbsoluteError => ActualValue.HasValue 
        ? Math.Abs(PredictedValue - ActualValue.Value) 
        : null;
    
    public double? PercentageError => ActualValue.HasValue && ActualValue.Value > 0
        ? AbsoluteError / ActualValue.Value * 100
        : null;
}
```

---

## Checklist e Troubleshooting

### Checklist Pre-Deployment

- [ ] Backtest eseguito su almeno 30 giorni di dati
- [ ] MAPE < 10% su dati di test
- [ ] R² del modello ML > 0.6
- [ ] Grid search completato per le soglie
- [ ] Sistema di monitoring configurato
- [ ] Alerting attivo

### Troubleshooting Comune

| Problema | Possibile Causa | Soluzione |
|----------|-----------------|-----------|
| R² molto basso (< 0.3) | Dati insufficienti | Aumentare `MinDataPoints` e `HistoricalContextDays` |
| R² alto ma MAPE alto | Overfitting | Aumentare `TestFraction`, ridurre `NumberOfLeaves` |
| Predizioni sempre da DB | Soglie troppo alte | Ridurre `R2ThresholdHigh` |
| Errori spike occasionali | Outliers nei dati | Implementare data cleaning |
| Performance degradata | Concept drift | Eseguire `ForceFullTrainingAsync()` |
| Predizioni tutte uguali | Features non discriminanti | Aggiungere nuove features |

### Comandi Utili

```csharp
// Forzare re-training completo
await trainingService.ForceFullTrainingAsync("TENANT-001");

// Verificare stato checkpoint
var checkpoint = await trainingService.GetCheckpointAsync("TENANT-001");
Console.WriteLine($"Ultimo training: {checkpoint?.LastTrainingDate}");
Console.WriteLine($"R²: {checkpoint?.RSquared}, RMSE: {checkpoint?.RMSE}");

// Reset checkpoint (ripartire da zero)
await trainingService.ResetCheckpointAsync("TENANT-001");

// Backtest rapido (7 giorni)
var quickTest = await analyticsService.RunBacktestAsync("TENANT-001", 7);

// Smart prediction con configurazione custom
var customConfig = new SmartPredictionConfig
{
    R2ThresholdHigh = 0.70,
    R2ThresholdLow = 0.45,
    MLWeightInHybrid = 0.65
};
var prediction = await analyticsService.GetSmartPredictionAsync(
    "TENANT-001",
    new GetPredictionArgs { Target = "quantity" },
    customConfig);
```

---

## Esempi di Codice

### Esempio Completo: Setup e Ottimizzazione

```csharp
public class PredictionOptimizer
{
    private readonly LLMAnalyticsService _analyticsService;
    private readonly TrainingService _trainingService;
    private readonly ILogger _logger;

    public async Task<OptimizationResult> OptimizeForTenantAsync(string tenantCode)
    {
        var result = new OptimizationResult { TenantCode = tenantCode };

        // STEP 1: Verifica stato attuale
        _logger.LogInformation("Step 1: Verifica stato modello...");
        var checkpoint = await _trainingService.GetCheckpointAsync(tenantCode);
        
        if (checkpoint == null || checkpoint.RSquared < 0.5)
        {
            _logger.LogWarning("Modello assente o scarso. Eseguo full training...");
            await _trainingService.ForceFullTrainingAsync(tenantCode);
            checkpoint = await _trainingService.GetCheckpointAsync(tenantCode);
        }
        
        result.InitialR2 = checkpoint?.RSquared ?? 0;

        // STEP 2: Baseline con config default
        _logger.LogInformation("Step 2: Calcolo baseline...");
        var baseline = await _analyticsService.RunBacktestAsync(
            tenantCode, 60, SmartPredictionConfig.Default);
        
        result.BaselineMAPE = baseline.SmartMetrics?.MeanAbsolutePercentageError ?? 100;

        // STEP 3: Grid search
        _logger.LogInformation("Step 3: Grid search configurazioni...");
        var bestConfig = await FindBestConfigAsync(tenantCode);
        result.BestConfig = bestConfig.Config;
        result.OptimizedMAPE = bestConfig.MAPE;

        // STEP 4: Validazione finale
        _logger.LogInformation("Step 4: Validazione finale...");
        var validation = await _analyticsService.RunBacktestAsync(
            tenantCode, 30, bestConfig.Config);
        
        result.ValidationMAPE = validation.SmartMetrics?.MeanAbsolutePercentageError ?? 100;
        result.Improvement = result.BaselineMAPE - result.OptimizedMAPE;

        _logger.LogInformation(
            "Ottimizzazione completata. Miglioramento MAPE: {Improvement:F2}%", 
            result.Improvement);

        return result;
    }

    private async Task<(SmartPredictionConfig Config, double MAPE)> FindBestConfigAsync(
        string tenantCode)
    {
        var configs = GenerateConfigGrid();
        var results = new List<(SmartPredictionConfig, double)>();

        foreach (var config in configs)
        {
            var backtest = await _analyticsService.RunBacktestAsync(
                tenantCode, 60, config);
            
            if (backtest.Success)
            {
                results.Add((config, backtest.SmartMetrics!.MeanAbsolutePercentageError));
            }
        }

        return results.OrderBy(r => r.Item2).First();
    }

    private IEnumerable<SmartPredictionConfig> GenerateConfigGrid()
    {
        var r2HighValues = new[] { 0.65, 0.70, 0.75, 0.80 };
        var r2LowValues = new[] { 0.40, 0.45, 0.50, 0.55 };
        var mlWeightValues = new[] { 0.60, 0.70, 0.80 };

        foreach (var high in r2HighValues)
        foreach (var low in r2LowValues.Where(l => l < high - 0.1))
        foreach (var weight in mlWeightValues)
        {
            yield return new SmartPredictionConfig
            {
                R2ThresholdHigh = high,
                R2ThresholdLow = low,
                MLWeightInHybrid = weight
            };
        }
    }
}

public class OptimizationResult
{
    public string TenantCode { get; set; }
    public double InitialR2 { get; set; }
    public double BaselineMAPE { get; set; }
    public SmartPredictionConfig BestConfig { get; set; }
    public double OptimizedMAPE { get; set; }
    public double ValidationMAPE { get; set; }
    public double Improvement { get; set; }
}
```

---

## Appendice: Glossario

| Termine | Definizione |
|---------|-------------|
| **MAPE** | Mean Absolute Percentage Error - errore medio percentuale |
| **R²** | Coefficiente di determinazione - % varianza spiegata dal modello |
| **RMSE** | Root Mean Square Error - radice dell'errore quadratico medio |
| **Concept Drift** | Cambio nei pattern dei dati nel tempo |
| **Overfitting** | Modello troppo adattato ai dati di training |
| **Feature Engineering** | Creazione di nuove variabili dai dati grezzi |
| **Grid Search** | Ricerca esaustiva su griglia di parametri |
| **Backtest** | Test su dati storici per validare la strategia |

---

*Documento generato per PlatformAI - Ultimo aggiornamento: Dicembre 2024*