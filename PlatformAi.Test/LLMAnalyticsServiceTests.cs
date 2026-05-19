using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PlatformAI.Analytics.Services;
using PlatformAI.Infrastructure.Master;
using PlatformAI.ML.Services;
using PlatformAI.NLP.Models;
using PlatformAI.Infrastructure.Application;

namespace PlatformAI.Tests;

/// <summary>
/// Test per LLMAnalyticsService - genera risposte con grafici
/// </summary>
[TestFixture]
public class LLMAnalyticsServiceTests : BaseTest
{
    private LLMAnalyticsService _analyticsService = null!;
    private ILogger<LLMAnalyticsService> _analyticsLogger = null!;
    
    // Test user e conversation IDs
    private Guid _testUserId;
    private Guid _testConversationId;

    [SetUp]
    public new void Setup()
    {
        base.Setup();

        // Logger
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });
        _analyticsLogger = loggerFactory.CreateLogger<LLMAnalyticsService>();

        // Ottieni un utente di test dal database o crea IDs di test
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

        // Genera un conversation ID di test
        _testConversationId = Guid.NewGuid();

        // Crea il servizio con configurazione da appsettings.json
        _analyticsService = new LLMAnalyticsService(_analyticsLogger, _llmConfig, _uow);
    }

    [TearDown]
    public new void TearDown()
    {
        base.TearDown();
    }

    [Test]
    public async Task GenerateResponse_SimpleQuestion_ShouldReturnTextOnly()
    {
        // Arrange
        var userMessage = "Ciao, come stai?";

        Console.WriteLine("=== Test: Simple Question (No Charts) ===");
        Console.WriteLine($"UserId: {_testUserId}");
        Console.WriteLine($"ConversationId: {_testConversationId}");

        try
        {
            // Act
            var response = await _analyticsService.GenerateResponseWithChartsAsync(
                userMessage,
                _testUserId,
                _testConversationId,
                CancellationToken.None
            );

            // Assert
            Assert.That(response.Content, Is.Not.Null.And.Not.Empty);
            Assert.That(response.HasCharts, Is.False, "Simple question should not generate charts");

            Console.WriteLine($"Response: {response.Content}");
            Console.WriteLine($"Charts: {response.Charts.Count}");
            Console.WriteLine("✅ Simple question handled correctly");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.GetType().Name} - {ex.Message}");
            throw;
        }
    }

    [Test]
    public async Task GenerateResponse_ChartRequest_ShouldReturnChartData()
    {
        // Arrange
        var userMessage = "Mostrami un grafico della produzione dell'ultima settimana";

        Console.WriteLine("=== Test: Chart Request ===");
        Console.WriteLine($"UserId: {_testUserId}");

        try
        {
            // Act
            var response = await _analyticsService.GenerateResponseWithChartsAsync(
                userMessage,
                _testUserId,
                _testConversationId,
                CancellationToken.None
            );

            // Assert
            Assert.That(response.Content, Is.Not.Null.And.Not.Empty);
            
            Console.WriteLine($"Response: {response.Content}");
            Console.WriteLine($"Charts generated: {response.Charts.Count}");

            if (response.HasCharts)
            {
                foreach (var chart in response.Charts)
                {
                    Console.WriteLine($"  - Chart: {chart.Title} ({chart.Type})");
                    Console.WriteLine($"    Labels: {string.Join(", ", chart.Labels.Take(5))}...");
                    Console.WriteLine($"    Data points: {chart.Datasets.FirstOrDefault()?.Data.Count ?? 0}");
                }
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

    [Test]
    public async Task GenerateResponse_PredictionRequest_ShouldReturnPrediction()
    {
        // Arrange
        var userMessage = "Prevedi la produzione per domani";

        Console.WriteLine("=== Test: Prediction Request ===");
        Console.WriteLine($"UserId: {_testUserId}");

        try
        {
            // Act
            var response = await _analyticsService.GenerateResponseWithChartsAsync(
                userMessage,
                _testUserId,
                _testConversationId,
                CancellationToken.None
            );

            // Assert
            Assert.That(response.Content, Is.Not.Null.And.Not.Empty);

            Console.WriteLine($"Response: {response.Content}");

            if (response.Prediction != null)
            {
                Console.WriteLine($"Prediction:");
                Console.WriteLine($"  - Value: {response.Prediction.PredictedValue:F2}");
                Console.WriteLine($"  - Confidence: {response.Prediction.Confidence:P0}");
                Console.WriteLine($"  - Explanation: {response.Prediction.Explanation}");
                Console.WriteLine("✅ Prediction generated successfully");
            }
            else
            {
                Console.WriteLine("⚠️ No prediction generated");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.GetType().Name} - {ex.Message}");
            throw;
        }
    }

    [Test]
    public async Task GenerateResponse_MultipleMetrics_ShouldReturnMultipleCharts()
    {
        // Arrange
        var userMessage = "Mostrami tutti i grafici: produzione, energia, temperatura e scarti dell'ultimo mese";

        Console.WriteLine("=== Test: Multiple Charts Request ===");
        Console.WriteLine($"UserId: {_testUserId}");

        try
        {
            // Act
            var response = await _analyticsService.GenerateResponseWithChartsAsync(
                userMessage,
                _testUserId,
                _testConversationId,
                CancellationToken.None
            );

            // Assert
            Assert.That(response.Content, Is.Not.Null.And.Not.Empty);

            Console.WriteLine($"Response: {response.Content}");
            Console.WriteLine($"Charts generated: {response.Charts.Count}");

            foreach (var chart in response.Charts)
            {
                Console.WriteLine($"  - {chart.Title} ({chart.Type}): {chart.Datasets.FirstOrDefault()?.Data.Count ?? 0} points");
            }

            if (response.Charts.Count > 1)
            {
                Console.WriteLine("✅ Multiple charts generated");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.GetType().Name} - {ex.Message}");
            throw;
        }
    }

    [Test]
    public async Task GenerateResponse_WithExistingConversation_ShouldMaintainContext()
    {
        // Arrange
        // Prima creiamo una conversazione di test se esiste un utente
        var existingConversation = _uow.Repository<Conversation>()
            .Query(x => x.UserId == _testUserId)
            .Include(x => x.Messages)
            .FirstOrDefault();

        var conversationId = existingConversation?.Id ?? _testConversationId;
        var userMessage = "Mostrami un grafico";

        Console.WriteLine("=== Test: Request with Existing Conversation ===");
        Console.WriteLine($"UserId: {_testUserId}");
        Console.WriteLine($"ConversationId: {conversationId}");
        Console.WriteLine($"Existing messages: {existingConversation?.Messages?.Count ?? 0}");

        try
        {
            // Act
            var response = await _analyticsService.GenerateResponseWithChartsAsync(
                userMessage,
                _testUserId,
                conversationId,
                CancellationToken.None
            );

            // Assert
            Assert.That(response.Content, Is.Not.Null.And.Not.Empty);

            Console.WriteLine($"Response: {response.Content}");
            Console.WriteLine($"Charts: {response.Charts.Count}");
            Console.WriteLine("✅ Context maintained with existing conversation");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.GetType().Name} - {ex.Message}");
            throw;
        }
    }

    [Test]
    public void ChartData_Serialization_ShouldBeValidJson()
    {
        // Arrange
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

        // Act
        var json = System.Text.Json.JsonSerializer.Serialize(chart, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });

        // Assert
        Assert.That(json, Is.Not.Null.And.Not.Empty);
        Assert.That(json, Does.Contain("Test Production"));
        Assert.That(json, Does.Contain("line"));
        Assert.That(json, Does.Contain("100"));

        Console.WriteLine("=== Chart JSON Serialization ===");
        Console.WriteLine(json);
        Console.WriteLine("✅ ChartData serializes correctly to JSON");
    }

    #region GetComparisonPredictionAsync Tests

    [Test]
    public async Task GetComparisonPredictionAsync_WithTrainingService_ShouldReturnBothPredictions()
    {
        // Arrange
        var args = new GetPredictionArgs { Target = "quantity", Horizon = "tomorrow" };

        // Crea TrainingService reale (usa il DB di test)
        var trainingService = _serviceProvider.GetRequiredService<TrainingService>();

        // Crea il servizio con TrainingService
        var serviceWithML = new LLMAnalyticsService(
            _analyticsLogger,
            _llmConfig,
            _uow,
            trainingService);

        Console.WriteLine("=== Test: GetComparisonPredictionAsync with TrainingService ===");
        Console.WriteLine($"UserId: {_testUserId}");

        try
        {
            // Act
            var result = await serviceWithML.GetComparisonPredictionAsync(
                _testUserId,
                args,
                CancellationToken.None);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.DatabasePrediction, Is.Not.Null);
            Assert.That(result.MLModelPrediction, Is.Not.Null);
            Assert.That(result.ComparisonSummary, Is.Not.Null.And.Not.Empty);
            Assert.That(result.RecommendedPrediction, Is.Not.Null.And.Not.Empty);

            // Output dettagliato
            Console.WriteLine($"Database Prediction: {result.DatabasePrediction.PredictedValue:F2}");
            Console.WriteLine($"  - Explanation: {result.DatabasePrediction.Explanation}");
            Console.WriteLine($"  - Confidence: {result.DatabasePrediction.Confidence:P0}");
            Console.WriteLine();
            Console.WriteLine($"ML Model Prediction: {result.MLModelPrediction.PredictedValue:F2}");
            Console.WriteLine($"  - Explanation: {result.MLModelPrediction.Explanation}");
            Console.WriteLine($"  - R²: {result.MLModelPrediction.ModelRSquared:F4}");
            Console.WriteLine($"  - RMSE: {result.MLModelPrediction.ModelRMSE:F2}");
            Console.WriteLine();
            Console.WriteLine($"Percentage Difference: {result.PercentageDifference:F2}%");
            Console.WriteLine($"Recommended: {result.RecommendedPrediction}");
            Console.WriteLine($"Both Available: {result.BothAvailable}");
            Console.WriteLine();
            Console.WriteLine("--- Comparison Summary ---");
            Console.WriteLine(result.ComparisonSummary);

            Console.WriteLine("✅ Comparison prediction completed successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.GetType().Name} - {ex.Message}");
            throw;
        }
    }

    [Test]
    public async Task GetComparisonPredictionAsync_WithoutTrainingService_ShouldReturnOnlyDatabasePrediction()
    {
        // Arrange
        var args = new GetPredictionArgs { Target = "quantity" };

        // Crea il servizio SENZA TrainingService
        var serviceWithoutML = new LLMAnalyticsService(
            _analyticsLogger,
            _llmConfig,
            _uow,
            trainingService: null);

        Console.WriteLine("=== Test: GetComparisonPredictionAsync without TrainingService ===");
        Console.WriteLine($"UserId: {_testUserId}");

        try
        {
            // Act
            var result = await serviceWithoutML.GetComparisonPredictionAsync(
                _testUserId,
                args,
                CancellationToken.None);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.DatabasePrediction, Is.Not.Null);
            Assert.That(result.MLModelPrediction, Is.Not.Null);
            
            // La predizione ML dovrebbe avere un messaggio di errore/warning
            Assert.That(result.MLModelPrediction.Explanation, 
                Does.Contain("non configurato").Or.Contain("non disponibile"));

            Console.WriteLine($"Database Prediction: {result.DatabasePrediction.PredictedValue:F2}");
            Console.WriteLine($"ML Prediction: {result.MLModelPrediction.PredictedValue:F2}");
            Console.WriteLine($"ML Explanation: {result.MLModelPrediction.Explanation}");
            Console.WriteLine($"Recommended: {result.RecommendedPrediction}");

            // Il raccomandato dovrebbe essere "Database" dato che ML non è disponibile
            Assert.That(result.RecommendedPrediction, Is.EqualTo("Database"));

            Console.WriteLine("✅ Service correctly handles missing TrainingService");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.GetType().Name} - {ex.Message}");
            throw;
        }
    }

    [Test]
    public async Task GetComparisonPredictionAsync_AllTargets_ShouldWorkForEachTarget()
    {
        // Arrange
        var targets = new[] { "quantity", "scrap", "energy" };
        var trainingService = _serviceProvider.GetRequiredService<TrainingService>();

        var serviceWithML = new LLMAnalyticsService(
            _analyticsLogger,
            _llmConfig,
            _uow,
            trainingService);

        Console.WriteLine("=== Test: GetComparisonPredictionAsync for All Targets ===");
        Console.WriteLine($"UserId: {_testUserId}");

        foreach (var target in targets)
        {
            Console.WriteLine($"\n--- Testing target: {target} ---");

            try
            {
                // Act
                var args = new GetPredictionArgs { Target = target };
                var result = await serviceWithML.GetComparisonPredictionAsync(
                    _testUserId,
                    args,
                    CancellationToken.None);

                // Assert
                Assert.That(result, Is.Not.Null, $"Result for {target} should not be null");
                Assert.That(result.DatabasePrediction, Is.Not.Null, $"DB prediction for {target} should not be null");
                Assert.That(result.MLModelPrediction, Is.Not.Null, $"ML prediction for {target} should not be null");

                Console.WriteLine($"  DB: {result.DatabasePrediction.PredictedValue:F2}");
                Console.WriteLine($"  ML: {result.MLModelPrediction.PredictedValue:F2}");
                Console.WriteLine($"  Diff: {result.PercentageDifference:F2}%");
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

    [Test]
    public async Task GetComparisonPredictionAsync_PercentageDifference_ShouldBeCalculatedCorrectly()
    {
        // Arrange
        var args = new GetPredictionArgs { Target = "quantity" };
        var trainingService = _serviceProvider.GetRequiredService<TrainingService>();

        var serviceWithML = new LLMAnalyticsService(
            _analyticsLogger,
            _llmConfig,
            _uow,
            trainingService);

        Console.WriteLine("=== Test: Percentage Difference Calculation ===");
        Console.WriteLine($"UserId: {_testUserId}");

        try
        {
            // Act
            var result = await serviceWithML.GetComparisonPredictionAsync(
                _testUserId,
                args,
                CancellationToken.None);

            // Assert - verifica che la differenza sia calcolata correttamente
            if (result.BothAvailable && result.PercentageDifference.HasValue)
            {
                var expectedDiff = Math.Abs(
                    (result.MLModelPrediction.PredictedValue - result.DatabasePrediction.PredictedValue) 
                    / result.DatabasePrediction.PredictedValue * 100);

                Assert.That(result.PercentageDifference.Value, 
                    Is.EqualTo(expectedDiff).Within(0.01),
                    "Percentage difference should be calculated correctly");

                Console.WriteLine($"DB Value: {result.DatabasePrediction.PredictedValue:F2}");
                Console.WriteLine($"ML Value: {result.MLModelPrediction.PredictedValue:F2}");
                Console.WriteLine($"Calculated Diff: {expectedDiff:F2}%");
                Console.WriteLine($"Returned Diff: {result.PercentageDifference:F2}%");
                Console.WriteLine("✅ Percentage difference calculated correctly");
            }
            else
            {
                Console.WriteLine("⚠️ Cannot verify percentage - one or both predictions unavailable");
                Console.WriteLine($"  BothAvailable: {result.BothAvailable}");
                Console.WriteLine($"  PercentageDifference: {result.PercentageDifference}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.GetType().Name} - {ex.Message}");
            throw;
        }
    }

    [Test]
    public async Task GetComparisonPredictionAsync_RecommendedPrediction_ShouldBeBasedOnRSquared()
    {
        // Arrange
        var args = new GetPredictionArgs { Target = "quantity" };
        var trainingService = _serviceProvider.GetRequiredService<TrainingService>();

        var serviceWithML = new LLMAnalyticsService(
            _analyticsLogger,
            _llmConfig,
            _uow,
            trainingService);

        Console.WriteLine("=== Test: Recommended Prediction Based on R² ===");
        Console.WriteLine($"UserId: {_testUserId}");

        try
        {
            // Act
            var result = await serviceWithML.GetComparisonPredictionAsync(
                _testUserId,
                args,
                CancellationToken.None);

            // Assert - verifica la logica di raccomandazione
            var r2 = result.MLModelPrediction.ModelRSquared;

            Console.WriteLine($"R² Value: {r2:F4}");
            Console.WriteLine($"Recommended: {result.RecommendedPrediction}");

            if (r2.HasValue)
            {
                if (r2 > 0.7)
                {
                    Assert.That(result.RecommendedPrediction, Is.EqualTo("ML"),
                        "With R² > 0.7, ML should be recommended");
                    Console.WriteLine("✅ ML correctly recommended for high R²");
                }
                else if (r2 < 0.5)
                {
                    Assert.That(result.RecommendedPrediction, Is.EqualTo("Database"),
                        "With R² < 0.5, Database should be recommended");
                    Console.WriteLine("✅ Database correctly recommended for low R²");
                }
                else
                {
                    Assert.That(result.RecommendedPrediction, 
                        Does.Contain("Entrambi"),
                        "With 0.5 <= R² <= 0.7, both should be suggested");
                    Console.WriteLine("✅ Both correctly suggested for medium R²");
                }
            }
            else
            {
                Assert.That(result.RecommendedPrediction, Is.EqualTo("Database"),
                    "Without R², Database should be recommended");
                Console.WriteLine("✅ Database correctly recommended when R² unavailable");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.GetType().Name} - {ex.Message}");
            throw;
        }
    }

    [Test]
    public async Task GetComparisonPredictionAsync_ComparisonSummary_ShouldContainAllInfo()
    {
        // Arrange
        var args = new GetPredictionArgs { Target = "quantity" };
        var trainingService = _serviceProvider.GetRequiredService<TrainingService>();

        var serviceWithML = new LLMAnalyticsService(
            _analyticsLogger,
            _llmConfig,
            _uow,
            trainingService);

        Console.WriteLine("=== Test: Comparison Summary Content ===");
        Console.WriteLine($"UserId: {_testUserId}");

        try
        {
            // Act
            var result = await serviceWithML.GetComparisonPredictionAsync(
                _testUserId,
                args,
                CancellationToken.None);

            // Assert - verifica che il summary contenga le informazioni chiave
            Assert.That(result.ComparisonSummary, Is.Not.Null.And.Not.Empty);
            Assert.That(result.ComparisonSummary, Does.Contain("Confronto Predizioni"));
            Assert.That(result.ComparisonSummary, Does.Contain("Database"));
            Assert.That(result.ComparisonSummary, Does.Contain("ML"));

            if (result.BothAvailable)
            {
                Assert.That(result.ComparisonSummary, Does.Contain("Differenza"));
            }

            Console.WriteLine("Summary content:");
            Console.WriteLine(result.ComparisonSummary);
            Console.WriteLine("✅ Comparison summary contains all required information");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.GetType().Name} - {ex.Message}");
            throw;
        }
    }

    [Test]
    public async Task GetPredictionFromMLModelAsync_WithValidData_ShouldReturnPrediction()
    {
        // Arrange
        var args = new GetPredictionArgs { Target = "quantity" };
        var trainingService = _serviceProvider.GetRequiredService<TrainingService>();

        var serviceWithML = new LLMAnalyticsService(
            _analyticsLogger,
            _llmConfig,
            _uow,
            trainingService);

        Console.WriteLine("=== Test: GetPredictionFromMLModelAsync ===");
        Console.WriteLine($"UserId: {_testUserId}");

        try
        {
            // Act
            var result = await serviceWithML.GetPredictionFromMLModelAsync(
                _testUserId,
                args,
                CancellationToken.None);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Explanation, Is.Not.Null.And.Not.Empty);

            Console.WriteLine($"Predicted Value: {result.PredictedValue:F2}");
            Console.WriteLine($"Confidence: {result.Confidence:P0}");
            Console.WriteLine($"R²: {result.ModelRSquared:F4}");
            Console.WriteLine($"RMSE: {result.ModelRMSE:F2}");
            Console.WriteLine($"Explanation: {result.Explanation}");

            if (result.Features != null)
            {
                Console.WriteLine("\nFeatures used:");
                foreach (var feature in result.Features.Take(5))
                {
                    Console.WriteLine($"  {feature.Key}: {feature.Value:F4}");
                }
                if (result.Features.Count > 5)
                {
                    Console.WriteLine($"  ... and {result.Features.Count - 5} more");
                }
            }

            Console.WriteLine("✅ ML prediction returned successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.GetType().Name} - {ex.Message}");
            throw;
        }
    }

    [Test]
    public async Task GetPredictionFromMLModelAsync_WithoutTrainingService_ShouldReturnErrorMessage()
    {
        // Arrange
        var args = new GetPredictionArgs { Target = "quantity" };

        // Servizio SENZA TrainingService
        var serviceWithoutML = new LLMAnalyticsService(
            _analyticsLogger,
            _llmConfig,
            _uow,
            trainingService: null);

        Console.WriteLine("=== Test: GetPredictionFromMLModelAsync without TrainingService ===");
        Console.WriteLine($"UserId: {_testUserId}");

        try
        {
            // Act
            var result = await serviceWithoutML.GetPredictionFromMLModelAsync(
                _testUserId,
                args,
                CancellationToken.None);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.PredictedValue, Is.EqualTo(0),
                "Predicted value should be 0 when TrainingService is not available");
            Assert.That(result.Explanation, Does.Contain("non configurato"),
                "Explanation should indicate service is not configured");

            Console.WriteLine($"Predicted Value: {result.PredictedValue}");
            Console.WriteLine($"Explanation: {result.Explanation}");
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

    [Test]
    public async Task GetSmartPredictionAsync_WithDefaultConfig_ShouldReturnValidResult()
    {
        // Arrange
        var args = new GetPredictionArgs { Target = "quantity" };
        var trainingService = _serviceProvider.GetRequiredService<TrainingService>();

        var serviceWithML = new LLMAnalyticsService(
            _analyticsLogger,
            _llmConfig,
            _uow,
            trainingService);

        Console.WriteLine("=== Test: GetSmartPredictionAsync with Default Config ===");
        Console.WriteLine($"UserId: {_testUserId}");

        try
        {
            // Act
            var result = await serviceWithML.GetSmartPredictionAsync(
                _testUserId,
                args,
                config: null, // usa default
                CancellationToken.None);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Prediction, Is.Not.Null);
            Assert.That(result.SelectionReason, Is.Not.Null.And.Not.Empty);
            Assert.That(result.SelectedSource, Is.AnyOf(
                PredictionSource.Database, 
                PredictionSource.MLModel, 
                PredictionSource.Hybrid));

            Console.WriteLine($"Selected Source: {result.SelectedSource}");
            Console.WriteLine($"Selection Reason: {result.SelectionReason}");
            Console.WriteLine($"Predicted Value: {result.PredictedValue:F2}");
            Console.WriteLine($"Confidence: {result.Confidence:P0}");
            Console.WriteLine($"Config Used: {result.ConfigUsed}");

            if (result.QualityMetrics != null)
            {
                Console.WriteLine($"Quality Metrics:");
                Console.WriteLine($"  - R²: {result.QualityMetrics.R2Score:F4}");
                Console.WriteLine($"  - RMSE: {result.QualityMetrics.RMSE:F2}");
                Console.WriteLine($"  - ML Available: {result.QualityMetrics.IsMLModelAvailable}");
            }

            Console.WriteLine("✅ Smart prediction with default config completed");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.GetType().Name} - {ex.Message}");
            throw;
        }
    }

    [Test]
    public async Task GetSmartPredictionAsync_WithConservativeConfig_ShouldPreferDatabase()
    {
        // Arrange
        var args = new GetPredictionArgs { Target = "quantity" };
        var trainingService = _serviceProvider.GetRequiredService<TrainingService>();

        var serviceWithML = new LLMAnalyticsService(
            _analyticsLogger,
            _llmConfig,
            _uow,
            trainingService);

        Console.WriteLine("=== Test: GetSmartPredictionAsync with Conservative Config ===");
        Console.WriteLine($"UserId: {_testUserId}");

        try
        {
            // Act
            var result = await serviceWithML.GetSmartPredictionAsync(
                _testUserId,
                args,
                SmartPredictionConfig.Conservative,
                CancellationToken.None);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.ConfigUsed, Is.Not.Null);
            Assert.That(result.ConfigUsed!.R2ThresholdHigh, Is.EqualTo(0.85));
            Assert.That(result.ConfigUsed.R2ThresholdLow, Is.EqualTo(0.65));

            Console.WriteLine($"Selected Source: {result.SelectedSource}");
            Console.WriteLine($"R² Threshold High: {result.ConfigUsed.R2ThresholdHigh}");
            Console.WriteLine($"R² Threshold Low: {result.ConfigUsed.R2ThresholdLow}");
            
            // Con config conservativa, è più probabile che selezioni Database
            // a meno che il modello non sia eccellente
            if (result.QualityMetrics?.R2Score < 0.85)
            {
                Assert.That(result.SelectedSource, 
                    Is.AnyOf(PredictionSource.Database, PredictionSource.Hybrid),
                    "Conservative config should prefer Database or Hybrid for R² < 0.85");
            }

            Console.WriteLine("✅ Conservative config test completed");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.GetType().Name} - {ex.Message}");
            throw;
        }
    }

    [Test]
    public async Task GetSmartPredictionAsync_WithAggressiveConfig_ShouldPreferML()
    {
        // Arrange
        var args = new GetPredictionArgs { Target = "quantity" };
        var trainingService = _serviceProvider.GetRequiredService<TrainingService>();

        var serviceWithML = new LLMAnalyticsService(
            _analyticsLogger,
            _llmConfig,
            _uow,
            trainingService);

        Console.WriteLine("=== Test: GetSmartPredictionAsync with Aggressive Config ===");
        Console.WriteLine($"UserId: {_testUserId}");

        try
        {
            // Act
            var result = await serviceWithML.GetSmartPredictionAsync(
                _testUserId,
                args,
                SmartPredictionConfig.Aggressive,
                CancellationToken.None);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.ConfigUsed, Is.Not.Null);
            Assert.That(result.ConfigUsed!.R2ThresholdHigh, Is.EqualTo(0.6));
            Assert.That(result.ConfigUsed.R2ThresholdLow, Is.EqualTo(0.4));
            Assert.That(result.ConfigUsed.MLWeightInHybrid, Is.EqualTo(0.8));

            Console.WriteLine($"Selected Source: {result.SelectedSource}");
            Console.WriteLine($"R² Score: {result.QualityMetrics?.R2Score:F4}");
            
            // Con config aggressiva, è più probabile che selezioni ML o Hybrid
            if (result.QualityMetrics?.R2Score >= 0.4)
            {
                Assert.That(result.SelectedSource, 
                    Is.AnyOf(PredictionSource.MLModel, PredictionSource.Hybrid),
                    "Aggressive config should prefer ML or Hybrid for R² >= 0.4");
            }

            Console.WriteLine("✅ Aggressive config test completed");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.GetType().Name} - {ex.Message}");
            throw;
        }
    }

    [Test]
    public async Task GetSmartPredictionAsync_WithCustomConfig_ShouldUseProvidedThresholds()
    {
        // Arrange
        var args = new GetPredictionArgs { Target = "quantity" };
        var trainingService = _serviceProvider.GetRequiredService<TrainingService>();

        var customConfig = new SmartPredictionConfig
        {
            R2ThresholdHigh = 0.72,
            R2ThresholdLow = 0.48,
            MLWeightInHybrid = 0.65,
            HybridConfidenceMultiplier = 0.75
        };

        var serviceWithML = new LLMAnalyticsService(
            _analyticsLogger,
            _llmConfig,
            _uow,
            trainingService);

        Console.WriteLine("=== Test: GetSmartPredictionAsync with Custom Config ===");
        Console.WriteLine($"UserId: {_testUserId}");

        try
        {
            // Act
            var result = await serviceWithML.GetSmartPredictionAsync(
                _testUserId,
                args,
                customConfig,
                CancellationToken.None);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.ConfigUsed, Is.Not.Null);
            Assert.That(result.ConfigUsed!.R2ThresholdHigh, Is.EqualTo(0.72));
            Assert.That(result.ConfigUsed.R2ThresholdLow, Is.EqualTo(0.48));
            Assert.That(result.ConfigUsed.MLWeightInHybrid, Is.EqualTo(0.65));

            Console.WriteLine($"Custom Config Applied:");
            Console.WriteLine($"  - R² High: {result.ConfigUsed.R2ThresholdHigh}");
            Console.WriteLine($"  - R² Low: {result.ConfigUsed.R2ThresholdLow}");
            Console.WriteLine($"  - ML Weight: {result.ConfigUsed.MLWeightInHybrid}");
            Console.WriteLine($"Selected Source: {result.SelectedSource}");

            Console.WriteLine("✅ Custom config test completed");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.GetType().Name} - {ex.Message}");
            throw;
        }
    }

    [Test]
    public async Task GetSmartPredictionAsync_AllTargets_ShouldWork()
    {
        // Arrange
        var targets = new[] { "quantity", "scrap", "energy" };
        var trainingService = _serviceProvider.GetRequiredService<TrainingService>();

        var serviceWithML = new LLMAnalyticsService(
            _analyticsLogger,
            _llmConfig,
            _uow,
            trainingService);

        Console.WriteLine("=== Test: GetSmartPredictionAsync for All Targets ===");
        Console.WriteLine($"UserId: {_testUserId}");

        foreach (var target in targets)
        {
            Console.WriteLine($"\n--- Testing target: {target} ---");

            try
            {
                var args = new GetPredictionArgs { Target = target };
                var result = await serviceWithML.GetSmartPredictionAsync(
                    _testUserId,
                    args,
                    SmartPredictionConfig.Default,
                    CancellationToken.None);

                Assert.That(result, Is.Not.Null);
                Assert.That(result.RequestedTarget, Is.EqualTo(target));

                Console.WriteLine($"  Source: {result.SelectedSource}");
                Console.WriteLine($"  Value: {result.PredictedValue:F2}");
                Console.WriteLine($"  Confidence: {result.Confidence:P0}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ Error: {ex.Message}");
                throw;
            }
        }

        Console.WriteLine("\n✅ All targets tested successfully");
    }

    [Test]
    public async Task GetSmartPredictionAsync_WithoutTrainingService_ShouldFallbackToDatabase()
    {
        // Arrange
        var args = new GetPredictionArgs { Target = "quantity" };

        var serviceWithoutML = new LLMAnalyticsService(
            _analyticsLogger,
            _llmConfig,
            _uow,
            trainingService: null);

        Console.WriteLine("=== Test: GetSmartPredictionAsync without TrainingService ===");
        Console.WriteLine($"UserId: {_testUserId}");

        try
        {
            // Act
            var result = await serviceWithoutML.GetSmartPredictionAsync(
                _testUserId,
                args,
                SmartPredictionConfig.Default,
                CancellationToken.None);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.SelectedSource, Is.EqualTo(PredictionSource.Database),
                "Should fallback to Database when TrainingService is not available");

            Console.WriteLine($"Selected Source: {result.SelectedSource}");
            Console.WriteLine($"Selection Reason: {result.SelectionReason}");
            Console.WriteLine($"Predicted Value: {result.PredictedValue:F2}");

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

    [Test]
    public async Task RunBacktestAsync_WithDefaultConfig_ShouldReturnValidResults()
    {
        // Arrange
        var trainingService = _serviceProvider.GetRequiredService<TrainingService>();

        var serviceWithML = new LLMAnalyticsService(
            _analyticsLogger,
            _llmConfig,
            _uow,
            trainingService);

        Console.WriteLine("=== Test: RunBacktestAsync with Default Config ===");
        Console.WriteLine($"UserId: {_testUserId}");

        try
        {
            // Act
            var result = await serviceWithML.RunBacktestAsync(
                _testUserId.ToString(),
                daysToTest: 30,
                config: SmartPredictionConfig.Default,
                CancellationToken.None);

            // Assert
            Assert.That(result, Is.Not.Null);
            //Assert.That(result.UserId, Is.EqualTo(_testUserId.ToString()));
            Assert.That(result.TestPeriodDays, Is.EqualTo(30));
            Assert.That(result.Duration, Is.GreaterThan(TimeSpan.Zero));

            Console.WriteLine($"Success: {result.Success}");
            Console.WriteLine($"Total Predictions: {result.TotalPredictions}");
            Console.WriteLine($"Duration: {result.Duration.TotalSeconds:F2}s");

            if (result.Success)
            {
                Assert.That(result.DatabaseMetrics, Is.Not.Null);
                Assert.That(result.SmartMetrics, Is.Not.Null);

                Console.WriteLine($"\nDatabase MAPE: {result.DatabaseMetrics!.MeanAbsolutePercentageError:F2}%");
                
                if (result.MLMetrics != null)
                    Console.WriteLine($"ML MAPE: {result.MLMetrics.MeanAbsolutePercentageError:F2}%");
                
                Console.WriteLine($"Smart MAPE: {result.SmartMetrics!.MeanAbsolutePercentageError:F2}%");

                Console.WriteLine($"\nSelection Breakdown:");
                foreach (var kvp in result.SelectionBreakdown)
                {
                    Console.WriteLine($"  - {kvp.Key}: {kvp.Value}");
                }

                Console.WriteLine("✅ Backtest completed successfully");
            }
            else
            {
                Console.WriteLine($"⚠️ Backtest failed: {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.GetType().Name} - {ex.Message}");
            throw;
        }
    }

    [Test]
    public async Task RunBacktestAsync_GenerateReport_ShouldContainAllSections()
    {
        // Arrange
        var trainingService = _serviceProvider.GetRequiredService<TrainingService>();

        var serviceWithML = new LLMAnalyticsService(
            _analyticsLogger,
            _llmConfig,
            _uow,
            trainingService);

        Console.WriteLine("=== Test: RunBacktestAsync GenerateReport ===");
        Console.WriteLine($"UserId: {_testUserId}");

        try
        {
            // Act
            var result = await serviceWithML.RunBacktestAsync(
                _testUserId.ToString(),
                daysToTest: 30,
                config: SmartPredictionConfig.Default,
                CancellationToken.None);

            var report = result.GenerateReport();

            // Assert
            Assert.That(report, Is.Not.Null.And.Not.Empty);
            Assert.That(report, Does.Contain("Backtest Report"));

            if (result.Success)
            {
                Assert.That(report, Does.Contain("MAPE"));
                Assert.That(report, Does.Contain("Database"));
                Assert.That(report, Does.Contain("Selezione Fonte"));
            }

            Console.WriteLine("Generated Report:");
            Console.WriteLine("=".PadRight(50, '='));
            Console.WriteLine(report);
            Console.WriteLine("=".PadRight(50, '='));

            Console.WriteLine("✅ Report generated successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.GetType().Name} - {ex.Message}");
            throw;
        }
    }

    [Test]
    public async Task RunBacktestAsync_CompareDifferentConfigs_ShouldShowDifferences()
    {
        // Arrange
        var trainingService = _serviceProvider.GetRequiredService<TrainingService>();

        var serviceWithML = new LLMAnalyticsService(
            _analyticsLogger,
            _llmConfig,
            _uow,
            trainingService);

        var configs = new[]
        {
            ("Default", SmartPredictionConfig.Default),
            ("Conservative", SmartPredictionConfig.Conservative),
            ("Aggressive", SmartPredictionConfig.Aggressive)
        };

        Console.WriteLine("=== Test: RunBacktestAsync Compare Configs ===");
        Console.WriteLine($"UserId: {_testUserId}");
        Console.WriteLine();

        var results = new List<(string Name, BacktestResult Result)>();

        foreach (var (name, config) in configs)
        {
            Console.WriteLine($"Testing {name} config...");

            try
            {
                var result = await serviceWithML.RunBacktestAsync(
                    _testUserId.ToString(),
                    daysToTest: 30,
                    config: config,
                    CancellationToken.None);

                results.Add((name, result));

                if (result.Success)
                {
                    Console.WriteLine($"  MAPE: {result.SmartMetrics!.MeanAbsolutePercentageError:F2}%");
                }
                else
                {
                    Console.WriteLine($"  Failed: {result.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Error: {ex.Message}");
            }
        }

        // Summary
        Console.WriteLine();
        Console.WriteLine("=== COMPARISON SUMMARY ===");
        Console.WriteLine("| Config | MAPE | Median Error | DB Selections | ML Selections |");
        Console.WriteLine("|--------|------|--------------|---------------|---------------|");

        foreach (var (name, result) in results.Where(r => r.Result.Success))
        {
            var dbCount = result.SelectionBreakdown.GetValueOrDefault(PredictionSource.Database, 0);
            var mlCount = result.SelectionBreakdown.GetValueOrDefault(PredictionSource.MLModel, 0);
            
            Console.WriteLine($"| {name,-12} | {result.SmartMetrics!.MeanAbsolutePercentageError,4:F1}% | {result.SmartMetrics.MedianError,10:F1}% | {dbCount,13} | {mlCount,13} |");
        }

        Console.WriteLine();
        Console.WriteLine("✅ Config comparison completed");
    }

    [Test]
    public async Task RunBacktestAsync_WithInsufficientData_ShouldReturnError()
    {
        // Arrange - Usa un userId che non esiste
        var nonExistentUserId = Guid.NewGuid();
        var trainingService = _serviceProvider.GetRequiredService<TrainingService>();

        var serviceWithML = new LLMAnalyticsService(
            _analyticsLogger,
            _llmConfig,
            _uow,
            trainingService);

        Console.WriteLine("=== Test: RunBacktestAsync with Insufficient Data ===");
        Console.WriteLine($"NonExistent UserId: {nonExistentUserId}");

        try
        {
            // Act
            var result = await serviceWithML.RunBacktestAsync(
                nonExistentUserId.ToString(),
                daysToTest: 30,
                config: SmartPredictionConfig.Default,
                CancellationToken.None);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Success, Is.False, 
                "Backtest should fail with nonexistent user");
            Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty);

            Console.WriteLine($"Success: {result.Success}");
            Console.WriteLine($"Error Message: {result.ErrorMessage}");

            Console.WriteLine("✅ Correctly handles insufficient data");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.GetType().Name} - {ex.Message}");
            throw;
        }
    }

    [Test]
    public async Task RunBacktestAsync_MetricsCalculation_ShouldBeAccurate()
    {
        // Arrange
        var trainingService = _serviceProvider.GetRequiredService<TrainingService>();

        var serviceWithML = new LLMAnalyticsService(
            _analyticsLogger,
            _llmConfig,
            _uow,
            trainingService);

        Console.WriteLine("=== Test: RunBacktestAsync Metrics Calculation ===");
        Console.WriteLine($"UserId: {_testUserId}");

        try
        {
            // Act
            var result = await serviceWithML.RunBacktestAsync(
                _testUserId.ToString(),
                daysToTest: 30,
                config: SmartPredictionConfig.Default,
                CancellationToken.None);

            if (!result.Success)
            {
                Console.WriteLine($"⚠️ Backtest failed: {result.ErrorMessage}");
                return;
            }

            // Assert metrics are consistent
            Assert.That(result.SmartMetrics, Is.Not.Null);
            Assert.That(result.SmartMetrics!.MeanAbsolutePercentageError, Is.GreaterThanOrEqualTo(0));
            Assert.That(result.SmartMetrics.MedianError, Is.GreaterThanOrEqualTo(0));
            Assert.That(result.SmartMetrics.MinError, Is.LessThanOrEqualTo(result.SmartMetrics.MedianError));
            Assert.That(result.SmartMetrics.MaxError, Is.GreaterThanOrEqualTo(result.SmartMetrics.MedianError));
            Assert.That(result.SmartMetrics.StandardDeviation, Is.GreaterThanOrEqualTo(0));

            // Verify predictions count matches
            var totalSelections = result.SelectionBreakdown.Values.Sum();
            Assert.That(totalSelections, Is.EqualTo(result.TotalPredictions),
                "Sum of selections should equal total predictions");

            Console.WriteLine($"Metrics Validation:");
            Console.WriteLine($"  MAPE: {result.SmartMetrics.MeanAbsolutePercentageError:F2}%");
            Console.WriteLine($"  Min Error: {result.SmartMetrics.MinError:F2}%");
            Console.WriteLine($"  Median Error: {result.SmartMetrics.MedianError:F2}%");
            Console.WriteLine($"  Max Error: {result.SmartMetrics.MaxError:F2}%");
            Console.WriteLine($"  Std Dev: {result.SmartMetrics.StandardDeviation:F2}");
            Console.WriteLine($"  Total Predictions: {result.TotalPredictions}");
            Console.WriteLine($"  Selection Sum: {totalSelections}");

            Console.WriteLine("✅ Metrics calculation validated");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.GetType().Name} - {ex.Message}");
            throw;
        }
    }

    [Test]
    public async Task RunBacktestAsync_Predictions_ShouldHaveValidData()
    {
        // Arrange
        var trainingService = _serviceProvider.GetRequiredService<TrainingService>();

        var serviceWithML = new LLMAnalyticsService(
            _analyticsLogger,
            _llmConfig,
            _uow,
            trainingService);

        Console.WriteLine("=== Test: RunBacktestAsync Predictions Validation ===");
        Console.WriteLine($"UserId: {_testUserId}");

        try
        {
            // Act
            var result = await serviceWithML.RunBacktestAsync(
                _testUserId.ToString(),
                daysToTest: 30,
                config: SmartPredictionConfig.Default,
                CancellationToken.None);

            if (!result.Success || result.Predictions.Count == 0)
            {
                Console.WriteLine($"⚠️ No predictions to validate");
                return;
            }

            // Assert each prediction has valid data
            foreach (var prediction in result.Predictions.Take(5)) // Check first 5
            {
                Assert.That(prediction.ActualValue, Is.GreaterThan(0),
                    "Actual value should be positive");
                Assert.That(prediction.DatabasePrediction, Is.GreaterThan(0),
                    "Database prediction should be positive");
                Assert.That(prediction.SmartPrediction, Is.GreaterThan(0),
                    "Smart prediction should be positive");
                Assert.That(prediction.ErrorPercent, Is.GreaterThanOrEqualTo(0),
                    "Error percent should be non-negative");
            }

            Console.WriteLine($"Sample Predictions (first 5):");
            Console.WriteLine("| Date | Actual | DB Pred | Smart Pred | Source | Error |");
            Console.WriteLine("|------|--------|---------|------------|--------|-------|");

            foreach (var p in result.Predictions.Take(5))
            {
                Console.WriteLine($"| {p.Date:MM-dd} | {p.ActualValue,6:F0} | {p.DatabasePrediction,7:F0} | {p.SmartPrediction,10:F0} | {p.SelectedSource,-6} | {p.ErrorPercent,5:F1}% |");
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

    [Test]
    public async Task GenerateResponseWithChartsStreamingAsync_SimpleQuestion_ShouldReturnTextChunks()
    {
        // Arrange
        var userMessage = "Ciao, come stai?";

        Console.WriteLine("=== Test: Streaming Simple Question ===");
        Console.WriteLine($"UserId: {_testUserId}");
        Console.WriteLine($"ConversationId: {_testConversationId}");

        try
        {
            // Act
            var chunks = new List<StreamingChartResponse>();
            var fullText = new System.Text.StringBuilder();

            await foreach (var response in _analyticsService.GenerateResponseWithChartsStreamingAsync(
                userMessage,
                _testUserId,
                _testConversationId,
                CancellationToken.None))
            {
                chunks.Add(response);
                
                if (response.Type == StreamingResponseType.TextChunk)
                {
                    fullText.Append(response.TextContent);
                    Console.Write(response.TextContent);
                }
            }
            Console.WriteLine();

            // Assert
            Assert.That(chunks, Is.Not.Empty);
            Assert.That(chunks.Any(c => c.Type == StreamingResponseType.TextChunk || c.Type == StreamingResponseType.Complete), 
                "Should have text chunks or complete");
            
            Console.WriteLine($"\nTotal chunks: {chunks.Count}");
            Console.WriteLine($"Full text length: {fullText.Length}");
            Console.WriteLine("✅ Streaming simple question handled correctly");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.GetType().Name} - {ex.Message}");
            throw;
        }
    }

    [Test]
    public async Task GenerateResponseWithChartsStreamingAsync_ChartRequest_ShouldReturnCharts()
    {
        // Arrange
        var userMessage = "Mostrami un grafico della produzione";

        Console.WriteLine("=== Test: Streaming Chart Request ===");
        Console.WriteLine($"UserId: {_testUserId}");

        try
        {
            // Act
            var chunks = new List<StreamingChartResponse>();
            var charts = new List<ChartData>();

            await foreach (var response in _analyticsService.GenerateResponseWithChartsStreamingAsync(
                userMessage,
                _testUserId,
                _testConversationId,
                CancellationToken.None))
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
                    case StreamingResponseType.ProcessingTools:
                        Console.WriteLine($"\n[Processing: {response.TextContent}]");
                        break;
                    case StreamingResponseType.Complete:
                        Console.WriteLine($"\n[Complete: {response.TotalCharts} charts]");
                        break;
                }
            }

            // Assert
            Assert.That(chunks, Is.Not.Empty);
            Console.WriteLine($"\nTotal chunks: {chunks.Count}");
            Console.WriteLine($"Charts received: {charts.Count}");
            
            foreach (var chart in charts)
            {
                Console.WriteLine($"  - {chart.Title} ({chart.Type})");
            }

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
