
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using PlatformAI.Infrastructure.Application;
using PlatformAI.ML;
using PlatformAI.ML.Services;

namespace PlatformAI.Tests;

[TestFixture]
public class TrainingServiceIntegrationTests : BaseTest
{
    private TrainingService _trainingService;



    [SetUp]
    public void TestSetup()
    {
        _trainingService = new TrainingService(_uow,_configuration, _logger);
    }


    [Test]
    public async Task TrainIncrementalAsync_WithRealData()
    {
        // Act – Esegui il training incrementale
        var result = await _trainingService.TrainIncrementalAsync(tenantCode);

        // Assert BASE
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.True, result.Message);

        // Il training incrementale può anche trovare 0 nuovi record,
        // quindi l'assert deve permettere >= 0
        Assert.That(result.NewRecordsCount, Is.GreaterThanOrEqualTo(0));

        // Deve sempre esistere un checkpoint dopo il training
        var checkpoint = await _trainingService.GetCheckpointAsync(tenantCode);
        Assert.That(checkpoint, Is.Not.Null, "Checkpoint non creato.");
        Assert.That(checkpoint!.LastProcessedDate, Is.Not.EqualTo(default(DateTime)),
            "Il checkpoint non contiene una LastProcessedDate valida.");

        // Se è stato usato training incrementale, la LastProcessedDate DEVE avere senso logico
        // Assert.That(checkpoint.LastTrainingDate, Is.GreaterThan(DateTime.UtcNow.AddMinutes(-5)),
        //     "La data del training non è coerente.");

        // Controlla versione modello (se viene tracciata)
        // if (!string.IsNullOrWhiteSpace(result.ModelVersion))
        //     Assert.That(result.ModelVersion, Does.StartWith("v"),
        //         "La versione del modello non è valida.");

        // Se sono stati elaborati nuovi record MANDATORY: ci deve essere un newcheckpoint
        if (result.NewRecordsCount > 0)
        {
            Assert.That(result.NewCheckpoint, Is.Not.Null,
                "Nuovo checkpoint deve essere valorizzato quando ci sono nuovi record.");
            Assert.That(result.NewCheckpoint!, Is.GreaterThan(checkpoint.LastProcessedDate.AddSeconds(-1)),
                "Nuovo checkpoint non aggiornato correttamente.");
        }

        Console.WriteLine("✅ Training completato");
        Console.WriteLine($"   Nuovi record: {result.NewRecordsCount}");
        Console.WriteLine($"   Checkpoint: {checkpoint!.LastProcessedDate:O}");
    }
    [Test]
    public async Task Predict_WithRealData_ReturnsValidScore()
    {
        // Assicurati che il modello esista
        var trainingResult = await _trainingService.TrainIncrementalAsync(tenantCode);
        Assume.That(trainingResult.Success, Is.True, "Training fallito, impossibile fare predizione.");

        // Prendi un record reale
        var sample = _uow.Repository<ProductionData>()
                         .Query(x => x.Id != Guid.Empty)
                         .OrderByDescending(x => x.LastModifiedDate)
                         .FirstOrDefault();

        Assume.That(sample, Is.Not.Null, "Nessun dato disponibile per predizione.");

        var features = new ProductionDataMLEnriched
        {
            // QuantityProduced = sample!.QuantityProduced,
            // CycleTime = (float)sample.CycleTime,
            // EnergyConsumption = (float)sample.EnergyConsumption,
            // Temperature = (float)sample.Temperature,
            // ScrapQuantity = sample.ScrapQuantity,
            // ScrapRate = sample.QuantityProduced > 0 ? (float)sample.ScrapQuantity / sample.QuantityProduced : 0,
            // EffectiveProductionRate = sample.CycleTime > 0 ? (float)sample.QuantityProduced / (float)sample.CycleTime : 0,
            // EnergyPerUnit = sample.QuantityProduced > 0 ? (float)sample.EnergyConsumption / sample.QuantityProduced : 0,
            HourOfDay = sample.Timestamp.Hour,
            DayOfWeek = (int)sample.Timestamp.DayOfWeek,
            IsWeekend = (sample.Timestamp.DayOfWeek == DayOfWeek.Saturday || sample.Timestamp.DayOfWeek == DayOfWeek.Sunday) ? 1 : 0,
            Shift = sample.Timestamp.Hour switch
            {
                >= 6 and < 14 => 1,
                >= 14 and < 22 => 2,
                _ => 3
            }
        };

        var prediction = _trainingService.Predict(features, tenantCode);
        Assert.That(prediction, Is.Not.Null, "Predizione fallita.");
        Assert.That(prediction!.Score, Is.GreaterThanOrEqualTo(0), "Valore predizione non valido.");

        Console.WriteLine($"✅ Predizione eseguita con successo: Score={prediction.Score:F2}");
    }

    // NOTA: non cancelliamo la cartella dei modelli per preservare checkpoint e training incrementale
    [TearDown]
    public void TestTearDown()
    {
        // opzionale: pulizia leggera o log
    }
}
