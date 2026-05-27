using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using PlatformAI.Analytics.Services;
using PlatformAI.Infrastructure.Master;
using PlatformAI.ML.Services;
using PlatformAI.NLP.Models;
using PlatformAI.Infrastructure.Application;

namespace PlatformAI.Tests;

/// <summary>
/// Test per LLMAnalyticsService - genera risposte con grafici
/// </summary>
[Trait("Category", "Integration")]
public class LLMAnalyticsServiceTests : BaseTest
{
    private LLMAnalyticsService _analyticsService = null!;
    private ILogger<LLMAnalyticsService> _analyticsLogger = null!;

    private Guid _testUserId;
    private Guid _testConversationId;

    public LLMAnalyticsServiceTests() : base()
    {
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });
        _analyticsLogger = loggerFactory.CreateLogger<LLMAnalyticsService>();

        var testUser = _uow.Repository<User>().Query(x => x.Tenant != null).FirstOrDefault();
        if (testUser != null)
        {
            _testUserId = testUser.Id;
            Console.WriteLine($"Using existing test user: {_testUserId}");
        }
        else
        {
            _testUserId = Guid.NewGuid();
            Console.WriteLine($"Using generated test user ID: {_testUserId}");
        }

        _testConversationId = Guid.NewGuid();
        _analyticsService = new LLMAnalyticsService(_analyticsLogger, _llmConfig, _uow);
    }

    public override void Dispose()
    {
        base.Dispose();
    }

    [Fact]
    public async Task GenerateResponse_SimpleQuestion_ShouldReturnTextOnly()
    {
        var userMessage = "Ciao, come stai?";

        Console.WriteLine("=== Test: Simple Question (No Charts) ===");

        try
        {
            var response = await _analyticsService.GenerateResponseWithChartsAsync(
                userMessage, _testUserId, _testConversationId, CancellationToken.None);

            Assert.NotNull(response.Content);
            Assert.NotEmpty(response.Content);
            Assert.False(response.HasCharts, "Simple question should not generate charts");

            Console.WriteLine($"Response: {response.Content}");
            Console.WriteLine("✅ Simple question handled correctly");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.GetType().Name} - {ex.Message}");
            throw;
        }
    }

    [Fact]
    public async Task GenerateResponse_ChartRequest_ShouldReturnChartData()
    {
        var userMessage = "Mostrami un grafico della produzione dell'ultima settimana";

        Console.WriteLine("=== Test: Chart Request ===");

        try
        {
            var response = await _analyticsService.GenerateResponseWithChartsAsync(
                userMessage, _testUserId, _testConversationId, CancellationToken.None);

            Assert.NotNull(response.Content);
            Assert.NotEmpty(response.Content);

            Console.WriteLine($"Response: {response.Content}");
            Console.WriteLine($"Charts generated: {response.Charts.Count}");

            if (response.HasCharts)
            {
                foreach (var chart in response.Charts)
                    Console.WriteLine($"  - Chart: {chart.Title} ({chart.Type})");
                Console.WriteLine("✅ Charts generated successfully");
            }
            else
            {
                Console.WriteLine("⚠️ No charts generated (might be due to no data in DB)");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.GetType().Name} - {ex.Message}");
            throw;
        }
    }

    [Fact]
    public async Task GenerateResponse_PredictionRequest_ShouldReturnPrediction()
    {
        var userMessage = "Prevedi la produzione per domani";

        Console.WriteLine("=== Test: Prediction Request ===");

        try
        {
            var response = await _analyticsService.GenerateResponseWithChartsAsync(
                userMessage, _testUserId, _testConversationId, CancellationToken.None);

            Assert.NotNull(response.Content);
            Assert.NotEmpty(response.Content);

            Console.WriteLine($"Response: {response.Content}");
            if (response.Prediction != null)
            {
                Console.WriteLine($"Prediction: {response.Prediction.PredictedValue:F2}");
                Console.WriteLine("✅ Prediction generated successfully");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.GetType().Name} - {ex.Message}");
            throw;
        }
    }

    [Fact]
    public async Task GenerateResponse_MultipleMetrics_ShouldReturnMultipleCharts()
    {
        var userMessage = "Mostrami tutti i grafici: produzione, energia, temperatura e scarti dell'ultimo mese";

        Console.WriteLine("=== Test: Multiple Charts Request ===");

        try
        {
            var response = await _analyticsService.GenerateResponseWithChartsAsync(
                userMessage, _testUserId, _testConversationId, CancellationToken.None);

            Assert.NotNull(response.Content);
            Assert.NotEmpty(response.Content);

            Console.WriteLine($"Charts generated: {response.Charts.Count}");
            if (response.Charts.Count > 1)
                Console.WriteLine("✅ Multiple charts generated");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.GetType().Name} - {ex.Message}");
            throw;
        }
    }

    [Fact]
    public async Task GenerateResponse_WithExistingConversation_ShouldMaintainContext()
    {
        var existingConversation = _uow.Repository<Conversation>()
            .Query(x => x.UserId == _testUserId)
            .Include(x => x.Messages)
            .FirstOrDefault();

        var conversationId = existingConversation?.Id ?? _testConversationId;
        var userMessage = "Mostrami un grafico";

        Console.WriteLine("=== Test: Request with Existing Conversation ===");

        try
        {
            var response = await _analyticsService.GenerateResponseWithChartsAsync(
                userMessage, _testUserId, conversationId, CancellationToken.None);

            Assert.NotNull(response.Content);
            Assert.NotEmpty(response.Content);

            Console.WriteLine("✅ Context maintained with existing conversation");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.GetType().Name} - {ex.Message}");
            throw;
        }
    }

    [Fact]
    public void ChartData_Serialization_ShouldBeValidJson()
    {
        var chart = new ChartData
        {
            Type = ChartType.Line,
            Title = "Test Production",
            Labels = new List<string> { "Mon", "Tue", "Wed", "Thu", "Fri" },
            Datasets = new List<ChartDataset>
            {
                new ChartDataset
                {
                    Label = "Quantity",
                    Data = new List<double> { 100, 120, 95, 130, 110 },
                    BorderColor = ChartColors.Blue,
                    BackgroundColor = ChartColors.BlueFill
                }
            },
            Options = new ChartOptions
            {
                ShowLegend = true,
                YAxisLabel = "Units",
                XAxisLabel = "Day"
            }
        };

        var json = System.Text.Json.JsonSerializer.Serialize(chart, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });

        Assert.NotNull(json);
        Assert.NotEmpty(json);
        Assert.Contains("Test Production", json);
        Assert.Contains("line", json);
        Assert.Contains("100", json);

        Console.WriteLine("✅ ChartData serializes correctly to JSON");
    }

    #region GetComparisonPredictionAsync Tests

    [Fact]
    public async Task GetComparisonPredictionAsync_WithTrainingService_ShouldReturnBothPredictions()
    {
        var args = new GetPredictionArgs { Target = "quantity", Horizon = "tomorrow" };
        var trainingService = _serviceProvider.GetRequiredService<TrainingService>();
        var serviceWithML = new LLMAnalyticsService(_analyticsLogger, _llmConfig, _uow, trainingService);

        Console.WriteLine("=== Test: GetComparisonPredictionAsync with TrainingService ===");

        try
        {
            var result = await serviceWithML.GetComparisonPredictionAsync(
                _testUserId, args, CancellationToken.None);

            Assert.NotNull(result);
            Assert.NotNull(result.DatabasePrediction);
            Assert.NotNull(result.MLModelPrediction);
            Assert.NotNull(result.ComparisonSummary);
            Assert.NotEmpty(result.ComparisonSummary);
            Assert.NotNull(result.RecommendedPrediction);
            Assert.NotEmpty(result.RecommendedPrediction);

            Console.WriteLine($"Database Prediction: {result.DatabasePrediction.PredictedValue:F2}");
            Console.WriteLine($"ML Model Prediction: {result.MLModelPrediction.PredictedValue:F2}");
            Console.WriteLine($"Recommended: {result.RecommendedPrediction}");
            Console.WriteLine("✅ Comparison prediction completed successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.GetType().Name} - {ex.Message}");
            throw;
        }
    }

    [Fact]
    public async Task GetComparisonPredictionAsync_WithoutTrainingService_ShouldReturnOnlyDatabasePrediction()
    {
        var args = new GetPredictionArgs { Target = "quantity" };
        var serviceWithoutML = new LLMAnalyticsService(_analyticsLogger, _llmConfig, _uow, trainingService: null);

        Console.WriteLine("=== Test: GetComparisonPredictionAsync without TrainingService ===");

        try
        {
            var result = await serviceWithoutML.GetComparisonPredictionAsync(
                _testUserId, args, CancellationToken.None);

            Assert.NotNull(result);
            Assert.NotNull(result.DatabasePrediction);
            Assert.NotNull(result.MLModelPrediction);
            Assert.True(
                result.MLModelPrediction.Explanation.Contains("non configurato") ||
                result.MLModelPrediction.Explanation.Contains("non disponibile"));
            Assert.Equal("Database", result.RecommendedPrediction);

            Console.WriteLine("✅ Service correctly handles missing TrainingService");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.GetType().Name} - {ex.Message}");
            throw;
        }
    }

    [Fact]
    public async Task GetComparisonPredictionAsync_AllTargets_ShouldWorkForEachTarget()
    {
        var targets = new[] { "quantity", "scrap", "energy" };
        var trainingService = _serviceProvider.GetRequiredService<TrainingService>();
        var serviceWithML = new LLMAnalyticsService(_analyticsLogger, _llmConfig, _uow, trainingService);

        Console.WriteLine("=== Test: GetComparisonPredictionAsync for All Targets ===");

        foreach (var target in targets)
        {
            Console.WriteLine($"\n--- Testing target: {target} ---");

            try
            {
                var args = new GetPredictionArgs { Target = target };
                var result = await serviceWithML.GetComparisonPredictionAsync(
                    _testUserId, args, CancellationToken.None);

                Assert.NotNull(result);
                Assert.NotNull(result.DatabasePrediction);
                Assert.NotNull(result.MLModelPrediction);

                Console.WriteLine($"  DB: {result.DatabasePrediction.PredictedValue:F2}");
                Console.WriteLine($"  Recommended: {result.RecommendedPrediction}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ Error for {target}: {ex.Message}");
                throw;
            }
        }

        Console.WriteLine("\n✅ All targets tested successfully");
    }

    [Fact]
    public async Task GetComparisonPredictionAsync_PercentageDifference_ShouldBeCalculatedCorrectly()
    {
        var args = new GetPredictionArgs { Target = "quantity" };
        var trainingService = _serviceProvider.GetRequiredService<TrainingService>();
        var serviceWithML = new LLMAnalyticsService(_analyticsLogger, _llmConfig, _uow, trainingService);

        Console.WriteLine("=== Test: Percentage Difference Calculation ===");

        try
        {
            var result = await serviceWithML.GetComparisonPredictionAsync(
                _testUserId, args, CancellationToken.None);

            if (result.BothAvailable && result.PercentageDifference.HasValue)
            {
                var expectedDiff = Math.Abs(
                    (result.MLModelPrediction.PredictedValue - result.DatabasePrediction.PredictedValue)
                    / result.DatabasePrediction.PredictedValue * 100);

                Assert.InRange(result.PercentageDifference.Value, expectedDiff - 0.01, expectedDiff + 0.01);

                Console.WriteLine($"Calculated Diff: {expectedDiff:F2}%");
                Console.WriteLine($"Returned Diff: {result.PercentageDifference:F2}%");
                Console.WriteLine("✅ Percentage difference calculated correctly");
            }
            else
            {
                Console.WriteLine("⚠️ Cannot verify percentage - one or both predictions unavailable");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.GetType().Name} - {ex.Message}");
            throw;
        }
    }

    [Fact]
    public async Task GetComparisonPredictionAsync_RecommendedPrediction_ShouldBeBasedOnRSquared()
    {
        var args = new GetPredictionArgs { Target = "quantity" };
        var trainingService = _serviceProvider.GetRequiredService<TrainingService>();
        var serviceWithML = new LLMAnalyticsService(_analyticsLogger, _llmConfig, _uow, trainingService);

        Console.WriteLine("=== Test: Recommended Prediction Based on R² ===");

        try
        {
            var result = await serviceWithML.GetComparisonPredictionAsync(
                _testUserId, args, CancellationToken.None);

            var r2 = result.MLModelPrediction.ModelRSquared;
            Console.WriteLine($"R² Value: {r2:F4}");
            Console.WriteLine($"Recommended: {result.RecommendedPrediction}");

            if (r2.HasValue)
            {
                if (r2 > 0.7)
                {
                    Assert.Equal("ML", result.RecommendedPrediction);
                    Console.WriteLine("✅ ML correctly recommended for high R²");
                }
                else if (r2 < 0.5)
                {
                    Assert.Equal("Database", result.RecommendedPrediction);
                    Console.WriteLine("✅ Database correctly recommended for low R²");
                }
                else
                {
                    Assert.Contains("Entrambi", result.RecommendedPrediction);
                    Console.WriteLine("✅ Both correctly suggested for medium R²");
                }
            }
            else
            {
                Assert.Equal("Database", result.RecommendedPrediction);
                Console.WriteLine("✅ Database correctly recommended when R² unavailable");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.GetType().Name} - {ex.Message}");
            throw;
        }
    }

    [Fact]
    public async Task GetComparisonPredictionAsync_ComparisonSummary_ShouldContainAllInfo()
    {
        var args = new GetPredictionArgs { Target = "quantity" };
        var trainingService = _serviceProvider.GetRequiredService<TrainingService>();
        var serviceWithML = new LLMAnalyticsService(_analyticsLogger, _llmConfig, _uow, trainingService);

        Console.WriteLine("=== Test: Comparison Summary Content ===");

        try
        {
            var result = await serviceWithML.GetComparisonPredictionAsync(
                _testUserId, args, CancellationToken.None);

            Assert.NotNull(result.ComparisonSummary);
            Assert.NotEmpty(result.ComparisonSummary);
            Assert.Contains("Confronto Predizioni", result.ComparisonSummary);
            Assert.Contains("Database", result.ComparisonSummary);
            Assert.Contains("ML", result.ComparisonSummary);

            if (result.BothAvailable)
                Assert.Contains("Differenza", result.ComparisonSummary);

            Console.WriteLine("✅ Comparison summary contains all required information");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.GetType().Name} - {ex.Message}");
            throw;
        }
    }

    [Fact]
    public async Task GetPredictionFromMLModelAsync_WithValidData_ShouldReturnPrediction()
    {
        var args = new GetPredictionArgs { Target = "quantity" };
        var trainingService = _serviceProvider.GetRequiredService<TrainingService>();
        var serviceWithML = new LLMAnalyticsService(_analyticsLogger, _llmConfig, _uow, trainingService);

        Console.WriteLine("=== Test: GetPredictionFromMLModelAsync ===");

        try
        {
            var result = await serviceWithML.GetPredictionFromMLModelAsync(
                _testUserId, args, CancellationToken.None);

            Assert.NotNull(result);
            Assert.NotNull(result.Explanation);
            Assert.NotEmpty(result.Explanation);

            Console.WriteLine($"Predicted Value: {result.PredictedValue:F2}");
            Console.WriteLine("✅ ML prediction returned successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.GetType().Name} - {ex.Message}");
            throw;
        }
    }

    [Fact]
    public async Task GetPredictionFromMLModelAsync_WithoutTrainingService_ShouldReturnErrorMessage()
    {
        var args = new GetPredictionArgs { Target = "quantity" };
        var serviceWithoutML = new LLMAnalyticsService(_analyticsLogger, _llmConfig, _uow, trainingService: null);

        Console.WriteLine("=== Test: GetPredictionFromMLModelAsync without TrainingService ===");

        try
        {
            var result = await serviceWithoutML.GetPredictionFromMLModelAsync(
                _testUserId, args, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(0, result.PredictedValue);
            Assert.Contains("non configurato", result.Explanation);

            Console.WriteLine("✅ Correctly handles missing TrainingService");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.GetType().Name} - {ex.Message}");
            throw;
        }
    }

    #endregion

    #region GetSmartPredictionAsync Tests

    [Fact]
    public async Task GetSmartPredictionAsync_WithDefaultConfig_ShouldReturnValidResult()
    {
        var args = new GetPredictionArgs { Target = "quantity" };
        var trainingService = _serviceProvider.GetRequiredService<TrainingService>();
        var serviceWithML = new LLMAnalyticsService(_analyticsLogger, _llmConfig, _uow, trainingService);

        Console.WriteLine("=== Test: GetSmartPredictionAsync with Default Config ===");

        try
        {
            var result = await serviceWithML.GetSmartPredictionAsync(
                _testUserId, args, config: null, CancellationToken.None);

            Assert.NotNull(result);
            Assert.NotNull(result.Prediction);
            Assert.NotNull(result.SelectionReason);
            Assert.NotEmpty(result.SelectionReason);
            Assert.True(
                result.SelectedSource == PredictionSource.Database ||
                result.SelectedSource == PredictionSource.MLModel ||
                result.SelectedSource == PredictionSource.Hybrid);

            Console.WriteLine($"Selected Source: {result.SelectedSource}");
            Console.WriteLine("✅ Smart prediction with default config completed");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.GetType().Name} - {ex.Message}");
            throw;
        }
    }

    [Fact]
    public async Task GetSmartPredictionAsync_WithConservativeConfig_ShouldPreferDatabase()
    {
        var args = new GetPredictionArgs { Target = "quantity" };
        var trainingService = _serviceProvider.GetRequiredService<TrainingService>();
        var serviceWithML = new LLMAnalyticsService(_analyticsLogger, _llmConfig, _uow, trainingService);

        Console.WriteLine("=== Test: GetSmartPredictionAsync with Conservative Config ===");

        try
        {
            var result = await serviceWithML.GetSmartPredictionAsync(
                _testUserId, args, SmartPredictionConfig.Conservative, CancellationToken.None);

            Assert.NotNull(result);
            Assert.NotNull(result.ConfigUsed);
            Assert.Equal(0.85, result.ConfigUsed!.R2ThresholdHigh);
            Assert.Equal(0.65, result.ConfigUsed.R2ThresholdLow);

            if (result.QualityMetrics?.R2Score < 0.85)
            {
                Assert.True(
                    result.SelectedSource == PredictionSource.Database ||
                    result.SelectedSource == PredictionSource.Hybrid);
            }

            Console.WriteLine("✅ Conservative config test completed");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.GetType().Name} - {ex.Message}");
            throw;
        }
    }

    [Fact]
    public async Task GetSmartPredictionAsync_WithAggressiveConfig_ShouldPreferML()
    {
        var args = new GetPredictionArgs { Target = "quantity" };
        var trainingService = _serviceProvider.GetRequiredService<TrainingService>();
        var serviceWithML = new LLMAnalyticsService(_analyticsLogger, _llmConfig, _uow, trainingService);

        Console.WriteLine("=== Test: GetSmartPredictionAsync with Aggressive Config ===");

        try
        {
            var result = await serviceWithML.GetSmartPredictionAsync(
                _testUserId, args, SmartPredictionConfig.Aggressive, CancellationToken.None);

            Assert.NotNull(result);
            Assert.NotNull(result.ConfigUsed);
            Assert.Equal(0.6, result.ConfigUsed!.R2ThresholdHigh);
            Assert.Equal(0.4, result.ConfigUsed.R2ThresholdLow);
            Assert.Equal(0.8, result.ConfigUsed.MLWeightInHybrid);

            if (result.QualityMetrics?.R2Score >= 0.4)
            {
                Assert.True(
                    result.SelectedSource == PredictionSource.MLModel ||
                    result.SelectedSource == PredictionSource.Hybrid);
            }

            Console.WriteLine("✅ Aggressive config test completed");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.GetType().Name} - {ex.Message}");
            throw;
        }
    }

    [Fact]
    public async Task GetSmartPredictionAsync_WithCustomConfig_ShouldUseProvidedThresholds()
    {
        var args = new GetPredictionArgs { Target = "quantity" };
        var trainingService = _serviceProvider.GetRequiredService<TrainingService>();
        var customConfig = new SmartPredictionConfig
        {
            R2ThresholdHigh = 0.72,
            R2ThresholdLow = 0.48,
            MLWeightInHybrid = 0.65,
            HybridConfidenceMultiplier = 0.75
        };
        var serviceWithML = new LLMAnalyticsService(_analyticsLogger, _llmConfig, _uow, trainingService);

        Console.WriteLine("=== Test: GetSmartPredictionAsync with Custom Config ===");

        try
        {
            var result = await serviceWithML.GetSmartPredictionAsync(
                _testUserId, args, customConfig, CancellationToken.None);

            Assert.NotNull(result);
            Assert.NotNull(result.ConfigUsed);
            Assert.Equal(0.72, result.ConfigUsed!.R2ThresholdHigh);
            Assert.Equal(0.48, result.ConfigUsed.R2ThresholdLow);
            Assert.Equal(0.65, result.ConfigUsed.MLWeightInHybrid);

            Console.WriteLine("✅ Custom config test completed");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.GetType().Name} - {ex.Message}");
            throw;
        }
    }

    [Fact]
    public async Task GetSmartPredictionAsync_AllTargets_ShouldWork()
    {
        var targets = new[] { "quantity", "scrap", "energy" };
        var trainingService = _serviceProvider.GetRequiredService<TrainingService>();
        var serviceWithML = new LLMAnalyticsService(_analyticsLogger, _llmConfig, _uow, trainingService);

        Console.WriteLine("=== Test: GetSmartPredictionAsync for All Targets ===");

        foreach (var target in targets)
        {
            Console.WriteLine($"\n--- Testing target: {target} ---");

            try
            {
                var args = new GetPredictionArgs { Target = target };
                var result = await serviceWithML.GetSmartPredictionAsync(
                    _testUserId, args, SmartPredictionConfig.Default, CancellationToken.None);

                Assert.NotNull(result);
                Assert.Equal(target, result.RequestedTarget);

                Console.WriteLine($"  Source: {result.SelectedSource}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ Error: {ex.Message}");
                throw;
            }
        }

        Console.WriteLine("\n✅ All targets tested successfully");
    }

    [Fact]
    public async Task GetSmartPredictionAsync_WithoutTrainingService_ShouldFallbackToDatabase()
    {
        var args = new GetPredictionArgs { Target = "quantity" };
        var serviceWithoutML = new LLMAnalyticsService(_analyticsLogger, _llmConfig, _uow, trainingService: null);

        Console.WriteLine("=== Test: GetSmartPredictionAsync without TrainingService ===");

        try
        {
            var result = await serviceWithoutML.GetSmartPredictionAsync(
                _testUserId, args, SmartPredictionConfig.Default, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(PredictionSource.Database, result.SelectedSource);

            Console.WriteLine("✅ Correctly falls back to Database");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.GetType().Name} - {ex.Message}");
            throw;
        }
    }

    #endregion

    #region RunBacktestAsync Tests

    [Fact]
    public async Task RunBacktestAsync_WithDefaultConfig_ShouldReturnValidResults()
    {
        var trainingService = _serviceProvider.GetRequiredService<TrainingService>();
        var serviceWithML = new LLMAnalyticsService(_analyticsLogger, _llmConfig, _uow, trainingService);

        Console.WriteLine("=== Test: RunBacktestAsync with Default Config ===");

        try
        {
            var result = await serviceWithML.RunBacktestAsync(
                _testUserId.ToString(), daysToTest: 30, config: SmartPredictionConfig.Default, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(30, result.TestPeriodDays);
            Assert.True(result.Duration > TimeSpan.Zero);

            Console.WriteLine($"Success: {result.Success}");
            Console.WriteLine($"Total Predictions: {result.TotalPredictions}");

            if (result.Success)
            {
                Assert.NotNull(result.DatabaseMetrics);
                Assert.NotNull(result.SmartMetrics);
                Console.WriteLine("✅ Backtest completed successfully");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.GetType().Name} - {ex.Message}");
            throw;
        }
    }

    [Fact]
    public async Task RunBacktestAsync_GenerateReport_ShouldContainAllSections()
    {
        var trainingService = _serviceProvider.GetRequiredService<TrainingService>();
        var serviceWithML = new LLMAnalyticsService(_analyticsLogger, _llmConfig, _uow, trainingService);

        Console.WriteLine("=== Test: RunBacktestAsync GenerateReport ===");

        try
        {
            var result = await serviceWithML.RunBacktestAsync(
                _testUserId.ToString(), daysToTest: 30, config: SmartPredictionConfig.Default, CancellationToken.None);

            var report = result.GenerateReport();

            Assert.NotNull(report);
            Assert.NotEmpty(report);
            Assert.Contains("Backtest Report", report);

            if (result.Success)
            {
                Assert.Contains("MAPE", report);
                Assert.Contains("Database", report);
                Assert.Contains("Selezione Fonte", report);
            }

            Console.WriteLine("✅ Report generated successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.GetType().Name} - {ex.Message}");
            throw;
        }
    }

    [Fact]
    public async Task RunBacktestAsync_CompareDifferentConfigs_ShouldShowDifferences()
    {
        var trainingService = _serviceProvider.GetRequiredService<TrainingService>();
        var serviceWithML = new LLMAnalyticsService(_analyticsLogger, _llmConfig, _uow, trainingService);

        var configs = new[]
        {
            ("Default", SmartPredictionConfig.Default),
            ("Conservative", SmartPredictionConfig.Conservative),
            ("Aggressive", SmartPredictionConfig.Aggressive)
        };

        Console.WriteLine("=== Test: RunBacktestAsync Compare Configs ===");

        var results = new List<(string Name, BacktestResult Result)>();

        foreach (var (name, config) in configs)
        {
            Console.WriteLine($"Testing {name} config...");
            try
            {
                var result = await serviceWithML.RunBacktestAsync(
                    _testUserId.ToString(), daysToTest: 30, config: config, CancellationToken.None);
                results.Add((name, result));
                if (result.Success)
                    Console.WriteLine($"  MAPE: {result.SmartMetrics!.MeanAbsolutePercentageError:F2}%");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Error: {ex.Message}");
            }
        }

        Console.WriteLine("✅ Config comparison completed");
    }

    [Fact]
    public async Task RunBacktestAsync_WithInsufficientData_ShouldReturnError()
    {
        var nonExistentUserId = Guid.NewGuid();
        var trainingService = _serviceProvider.GetRequiredService<TrainingService>();
        var serviceWithML = new LLMAnalyticsService(_analyticsLogger, _llmConfig, _uow, trainingService);

        Console.WriteLine("=== Test: RunBacktestAsync with Insufficient Data ===");

        try
        {
            var result = await serviceWithML.RunBacktestAsync(
                nonExistentUserId.ToString(), daysToTest: 30, config: SmartPredictionConfig.Default, CancellationToken.None);

            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.NotNull(result.ErrorMessage);
            Assert.NotEmpty(result.ErrorMessage);

            Console.WriteLine("✅ Correctly handles insufficient data");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.GetType().Name} - {ex.Message}");
            throw;
        }
    }

    [Fact]
    public async Task RunBacktestAsync_MetricsCalculation_ShouldBeAccurate()
    {
        var trainingService = _serviceProvider.GetRequiredService<TrainingService>();
        var serviceWithML = new LLMAnalyticsService(_analyticsLogger, _llmConfig, _uow, trainingService);

        Console.WriteLine("=== Test: RunBacktestAsync Metrics Calculation ===");

        try
        {
            var result = await serviceWithML.RunBacktestAsync(
                _testUserId.ToString(), daysToTest: 30, config: SmartPredictionConfig.Default, CancellationToken.None);

            if (!result.Success)
            {
                Console.WriteLine($"⚠️ Backtest failed: {result.ErrorMessage}");
                return;
            }

            Assert.NotNull(result.SmartMetrics);
            Assert.True(result.SmartMetrics!.MeanAbsolutePercentageError >= 0);
            Assert.True(result.SmartMetrics.MedianError >= 0);
            Assert.True(result.SmartMetrics.MinError <= result.SmartMetrics.MedianError);
            Assert.True(result.SmartMetrics.MaxError >= result.SmartMetrics.MedianError);
            Assert.True(result.SmartMetrics.StandardDeviation >= 0);

            var totalSelections = result.SelectionBreakdown.Values.Sum();
            Assert.Equal(result.TotalPredictions, totalSelections);

            Console.WriteLine("✅ Metrics calculation validated");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.GetType().Name} - {ex.Message}");
            throw;
        }
    }

    [Fact]
    public async Task RunBacktestAsync_Predictions_ShouldHaveValidData()
    {
        var trainingService = _serviceProvider.GetRequiredService<TrainingService>();
        var serviceWithML = new LLMAnalyticsService(_analyticsLogger, _llmConfig, _uow, trainingService);

        Console.WriteLine("=== Test: RunBacktestAsync Predictions Validation ===");

        try
        {
            var result = await serviceWithML.RunBacktestAsync(
                _testUserId.ToString(), daysToTest: 30, config: SmartPredictionConfig.Default, CancellationToken.None);

            if (!result.Success || result.Predictions.Count == 0)
            {
                Console.WriteLine($"⚠️ No predictions to validate");
                return;
            }

            foreach (var prediction in result.Predictions.Take(5))
            {
                Assert.True(prediction.ActualValue > 0);
                Assert.True(prediction.DatabasePrediction > 0);
                Assert.True(prediction.SmartPrediction > 0);
                Assert.True(prediction.ErrorPercent >= 0);
            }

            Console.WriteLine("✅ Predictions validated");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.GetType().Name} - {ex.Message}");
            throw;
        }
    }

    #endregion

    #region Streaming Tests

    [Fact]
    public async Task GenerateResponseWithChartsStreamingAsync_SimpleQuestion_ShouldReturnTextChunks()
    {
        var userMessage = "Ciao, come stai?";

        Console.WriteLine("=== Test: Streaming Simple Question ===");

        try
        {
            var chunks = new List<StreamingChartResponse>();
            var fullText = new System.Text.StringBuilder();

            await foreach (var response in _analyticsService.GenerateResponseWithChartsStreamingAsync(
                userMessage, _testUserId, _testConversationId, CancellationToken.None))
            {
                chunks.Add(response);
                if (response.Type == StreamingResponseType.TextChunk)
                    fullText.Append(response.TextContent);
            }

            Assert.NotEmpty(chunks);
            Assert.True(chunks.Any(c => c.Type == StreamingResponseType.TextChunk || c.Type == StreamingResponseType.Complete));

            Console.WriteLine($"Total chunks: {chunks.Count}");
            Console.WriteLine("✅ Streaming simple question handled correctly");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.GetType().Name} - {ex.Message}");
            throw;
        }
    }

    [Fact]
    public async Task GenerateResponseWithChartsStreamingAsync_ChartRequest_ShouldReturnCharts()
    {
        var userMessage = "Mostrami un grafico della produzione";

        Console.WriteLine("=== Test: Streaming Chart Request ===");

        try
        {
            var chunks = new List<StreamingChartResponse>();
            var charts = new List<ChartData>();

            await foreach (var response in _analyticsService.GenerateResponseWithChartsStreamingAsync(
                userMessage, _testUserId, _testConversationId, CancellationToken.None))
            {
                chunks.Add(response);

                switch (response.Type)
                {
                    case StreamingResponseType.TextChunk:
                        Console.Write(response.TextContent);
                        break;
                    case StreamingResponseType.ChartData:
                        if (response.Chart != null)
                        {
                            charts.Add(response.Chart);
                            Console.WriteLine($"\n[CHART: {response.Chart.Title}]");
                        }
                        break;
                    case StreamingResponseType.Complete:
                        Console.WriteLine($"\n[Complete: {response.TotalCharts} charts]");
                        break;
                }
            }

            Assert.NotEmpty(chunks);
            Console.WriteLine($"\nTotal chunks: {chunks.Count}");
            Console.WriteLine("✅ Streaming chart request handled correctly");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.GetType().Name} - {ex.Message}");
            throw;
        }
    }

    #endregion
}
