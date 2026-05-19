using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PlatformAI.NLP.Models;
using PlatformAI.NLP.Services;

namespace PlatformAI.Tests;

/// <summary>
/// Test di integrazione per verificare la connessione Azure OpenAI
/// Carica la configurazione da appsettings.json tramite BaseTest
/// </summary>
[TestFixture]
public class AzureOpenAIConnectionTests : BaseTest
{
    private LLMStreamingService _llmService = null!;
    private ILogger<LLMStreamingService> _llmLogger = null!;

    [SetUp]
    public new void Setup()
    {
        // Chiama il Setup del BaseTest per caricare la configurazione da appsettings.json
        base.Setup();

        Console.WriteLine($"=== Configurazione caricata da appsettings.json ===");
        Console.WriteLine($"Provider: {_llmConfig.Provider}");
        Console.WriteLine($"Endpoint: {_llmConfig.Endpoint}");
        Console.WriteLine($"DeploymentName: {_llmConfig.DeploymentName}");
        Console.WriteLine($"ApiKey length: {_llmConfig.ApiKey?.Length ?? 0}");
        Console.WriteLine($"ApiVersion: {_llmConfig.ApiVersion}");

        // Logger reale per vedere l'output
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });
        _llmLogger = loggerFactory.CreateLogger<LLMStreamingService>();

        var httpClient = new HttpClient();
        _llmService = new LLMStreamingService(httpClient, _llmLogger, _llmConfig);
    }

    [TearDown]
    public new void TearDown()
    {
        base.TearDown();
    }

    [Test]
    public async Task AzureOpenAI_SimpleMessage_ShouldReturnResponse()
    {
        // Arrange
        var userMessage = "Ciao! Dimmi solo 'OK' se funziona.";

        Console.WriteLine("=== Test Azure OpenAI Connection ===");
        Console.WriteLine($"Sending message: {userMessage}");

        try
        {
            // Act
            var response = await _llmService.SendToAzureOpenAIOnceAsync(userMessage,null, CancellationToken.None);

            // Assert
            Console.WriteLine($"Response received: {response}");
            Assert.That(response, Is.Not.Null);
            Assert.That(response, Is.Not.Empty);

            Console.WriteLine("✅ Azure OpenAI connection successful!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.GetType().Name}");
            Console.WriteLine($"Message: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner: {ex.InnerException.Message}");
            }
            Console.WriteLine($"StackTrace: {ex.StackTrace}");
            throw;
        }
    }

    [Test]
    public async Task AzureOpenAI_WithHistory_ShouldReturnResponse()
    {
        // Arrange
        var history = new List<ChatMessage>
        {
            new() { Role = "user", Content = "Come ti chiami?" },
            new() { Role = "assistant", Content = "Sono un assistente AI." }
        };
        var userMessage = "Ripeti il tuo nome";

        Console.WriteLine("=== Test Azure OpenAI with History ===");

        try
        {
            // Act
            var response = await _llmService.SendToAzureOpenAIOnceAsync(
                userMessage,
                history,
                CancellationToken.None
            );

            // Assert
            Console.WriteLine($"Response: {response}");
            Assert.That(response, Is.Not.Null);
            Assert.That(response, Is.Not.Empty);

            Console.WriteLine("✅ Azure OpenAI with history successful!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.GetType().Name} - {ex.Message}");
            throw;
        }
    }

    [Test]
    public async Task AzureOpenAI_Streaming_ShouldReturnChunks()
    {
        // Arrange
        var userMessage = "Conta da 1 a 5";

        Console.WriteLine("=== Test Azure OpenAI Streaming ===");

        try
        {
            // Act
            var chunks = new List<string>();
            await foreach (var chunk in _llmService.GenerateStreamingResponseAsync(
                userMessage,
                null,
                CancellationToken.None))
            {
                Console.Write(chunk);
                chunks.Add(chunk);
            }
            Console.WriteLine();

            // Assert
            Assert.That(chunks.Count, Is.GreaterThan(0));
            var fullResponse = string.Join("", chunks);
            Console.WriteLine($"Full response: {fullResponse}");

            Console.WriteLine("✅ Azure OpenAI streaming successful!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.GetType().Name} - {ex.Message}");
            throw;
        }
    }

    [Test]
    public void Configuration_ShouldBeLoaded()
    {
        // Verifica che la configurazione sia stata caricata correttamente da appsettings.json
        Assert.That(_llmConfig.Provider, Is.EqualTo(LLMProvider.AzureOpenAI), "Provider should be AzureOpenAI");
        Assert.That(_llmConfig.Endpoint, Is.Not.Null.And.Not.Empty, "Endpoint should not be empty");
        Assert.That(_llmConfig.ApiKey, Is.Not.Null.And.Not.Empty, "ApiKey should not be empty");
        Assert.That(_llmConfig.DeploymentName, Is.Not.Null.And.Not.Empty, "DeploymentName should not be empty");

        Console.WriteLine("✅ Configuration loaded successfully from appsettings.json");
    }
}
