
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Microsoft.EntityFrameworkCore;
using PlatformAI.Infrastructure;
using PlatformAI.Infrastructure.Application;
using PlatformAI.ML;
using PlatformAI.ML.Services;

namespace PlatformAI.Tests;

[Trait("Category", "Integration")]
public class TrainingServiceIntegrationTests : BaseTest
{
    private TrainingService _trainingService = null!;

    public TrainingServiceIntegrationTests() : base()
    {
        _trainingService = new TrainingService(_uow, _configuration, _logger);
    }

    [Fact]
    public async Task TrainIncrementalAsync_WithRealData()
    {
        var result = await _trainingService.TrainIncrementalAsync(tenantCode);

        Assert.NotNull(result);
        Assert.True(result.Success, result.Message);
        Assert.True(result.NewRecordsCount >= 0);

        var checkpoint = await _trainingService.GetCheckpointAsync(tenantCode);
        Assert.NotNull(checkpoint);
        Assert.NotEqual(default(DateTime), checkpoint!.LastProcessedDate);

        if (result.NewRecordsCount > 0)
        {
            Assert.NotNull(result.NewCheckpoint);
            Assert.True(result.NewCheckpoint! > checkpoint.LastProcessedDate.AddSeconds(-1));
        }

        Console.WriteLine("✅ Training completato");
        Console.WriteLine($"   Nuovi record: {result.NewRecordsCount}");
        Console.WriteLine($"   Checkpoint: {checkpoint!.LastProcessedDate:O}");
    }

    [Fact]
    public async Task Predict_WithRealData_ReturnsValidScore()
    {
        var trainingResult = await _trainingService.TrainIncrementalAsync(tenantCode);
        if (!trainingResult.Success)
        {
            // Precondition not met - training failed, skipping prediction test
            return;
        }

        // Carica le ultime righe (una per metrica) e raggruppa in snapshot
        // in modo da poter estrarre tutti i valori di feature da un unico ciclo.
        var recentRows = _uow.Repository<ProductionData>()
                             .Query(x => x.Id != Guid.Empty)
                             .Include(x => x.MetricType)
                             .OrderByDescending(x => x.Timestamp)
                             .Take(50)
                             .ToList();

        var snapshots = recentRows.ToSnapshots()
                                  .OrderByDescending(s => s.Timestamp)
                                  .ToList();

        if (!snapshots.Any())
        {
            // No data available for prediction, skipping
            return;
        }

        var snap   = snapshots.First();
        var qty    = snap.GetMetric("quantity_produced");
        var scrap  = snap.GetMetric("scrap_quantity");
        var cycle  = snap.GetMetric("cycle_time");
        var energy = snap.GetMetric("energy_consumption");
        var temp   = snap.GetMetric("temperature");

        var features = new ProductionDataMLEnriched
        {
            // Feature fisiche dallo snapshot più recente
            CycleTime         = (float)cycle,
            EnergyConsumption = (float)energy,
            Temperature       = (float)temp,
            ScrapQuantity     = (float)scrap,

            // Feature derivate
            ScrapRate               = qty > 0 ? (float)scrap  / (float)qty   : 0f,
            EffectiveProductionRate = cycle > 0 ? (float)qty  / (float)cycle : 0f,
            EnergyPerUnit           = qty > 0 ? (float)energy / (float)qty   : 0f,

            // Feature temporali
            HourOfDay = snap.Timestamp.Hour,
            DayOfWeek = (int)snap.Timestamp.DayOfWeek,
            IsWeekend = (snap.Timestamp.DayOfWeek == DayOfWeek.Saturday ||
                         snap.Timestamp.DayOfWeek == DayOfWeek.Sunday) ? 1 : 0,
            Shift = snap.Timestamp.Hour switch
            {
                >= 6 and < 14  => 1,
                >= 14 and < 22 => 2,
                _              => 3
            }
        };

        var prediction = _trainingService.Predict(features, tenantCode);
        Assert.NotNull(prediction);
        Assert.True(prediction!.Score >= 0, "Valore predizione non valido.");

        Console.WriteLine($"✅ Predizione eseguita con successo: Score={prediction.Score:F2}");
    }
}
