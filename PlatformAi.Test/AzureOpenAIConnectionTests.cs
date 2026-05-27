using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Xunit;
using PlatformAI.NLP.Models;
using PlatformAI.NLP.Services;

namespace PlatformAI.Tests;

/// <summary>
/// Test di integrazione per verificare la connessione Azure OpenAI
/// </summary>
[Trait("Category", "Integration")]
public class AzureOpenAIConnectionTests : BaseTest
{
    private LLMStreamingService _llmService = null!;
    private ILogger<LLMStreamingService> _llmLogger = null!;

    public AzureOpenAIConnectionTests() : base()
    {
        Console.WriteLine($"=== Configurazione caricata da appsettings.json ===");
        Console.WriteLine($"Provider: {_llmConfig.Provider}");
        Console.WriteLine($"Endpoint: {_llmConfig.Endpoint}");
        Console.WriteLine($"DeploymentName: {_llmConfig.DeploymentName}");
        Console.WriteLine($"ApiKey length: {_llmConfig.ApiKey?.Length ?? 0}");
        Console.WriteLine($"ApiVersion: {_llmConfig.ApiVersion}");

        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });
        _llmLogger = loggerFactory.CreateLogger<LLMStreamingService>();

        var httpClient = new HttpClient();
        _llmService = new LLMStreamingService(httpClient, _llmLogger, _llmConfig);
    }

    public override void Dispose()
    {
        base.Dispose();
    }

    [Fact]
    public async Task AzureOpenAI_SimpleMessage_ShouldReturnResponse()
    {
        var userMessage = "Ciao! Dimmi solo 'OK' se funziona.";

        Console.WriteLine("=== Test Azure OpenAI Connection ===");
        Console.WriteLine($"Sending message: {userMessage}");

        try
        {
            var response = await _llmService.SendToAzureOpenAIOnceAsync(userMessage, null, CancellationToken.None);

            Console.WriteLine($"Response received: {response}");
            Assert.NotNull(response);
            Assert.NotEmpty(response);

            Console.WriteLine("✅ Azure OpenAI connection successful!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.GetType().Name}");
            Console.WriteLine($"Message: {ex.Message}");
            if (ex.InnerException != null)
                Console.WriteLine($"Inner: {ex.InnerException.Message}");
            throw;
        }
    }

    [Fact]
    public async Task AzureOpenAI_WithHistory_ShouldReturnResponse()
    {
        var history = new List<ChatMessage>
        {
            new() { Role = "user", Content = "Come ti chiami?" },
            new() { Role = "assistant", Content = "Sono un assistente AI." }
        };
        var userMessage = "Ripeti il tuo nome";

        Console.WriteLine("=== Test Azure OpenAI with History ===");

        try
        {
            var response = await _llmService.SendToAzureOpenAIOnceAsync(
                userMessage,
                history,
                CancellationToken.None
            );

            Console.WriteLine($"Response: {response}");
            Assert.NotNull(response);
            Assert.NotEmpty(response);

            Console.WriteLine("✅ Azure OpenAI with history successful!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.GetType().Name} - {ex.Message}");
            throw;
        }
    }

    [Fact]
    public async Task AzureOpenAI_Streaming_ShouldReturnChunks()
    {
        var userMessage = "Conta da 1 a 5";

        Console.WriteLine("=== Test Azure OpenAI Streaming ===");

        try
        {
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

            Assert.True(chunks.Count > 0);
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

    [Fact]
    public void Configuration_ShouldBeLoaded()
    {
        Assert.Equal(LLMProvider.AzureOpenAI, _llmConfig.Provider);
        Assert.NotNull(_llmConfig.Endpoint);
        Assert.NotEmpty(_llmConfig.Endpoint!);
        Assert.NotNull(_llmConfig.ApiKey);
        Assert.NotEmpty(_llmConfig.ApiKey!);
        Assert.NotNull(_llmConfig.DeploymentName);
        Assert.NotEmpty(_llmConfig.DeploymentName!);

        Console.WriteLine("✅ Configuration loaded successfully from appsettings.json");
    }
}
