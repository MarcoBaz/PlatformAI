
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
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

        var sample = _uow.Repository<ProductionData>()
                         .Query(x => x.Id != Guid.Empty)
                         .OrderByDescending(x => x.LastModifiedDate)
                         .FirstOrDefault();

        if (sample == null)
        {
            // No data available for prediction, skipping
            return;
        }

        var features = new ProductionDataMLEnriched
        {
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
        Assert.NotNull(prediction);
        Assert.True(prediction!.Score >= 0, "Valore predizione non valido.");

        Console.WriteLine($"✅ Predizione eseguita con successo: Score={prediction.Score:F2}");
    }
}
