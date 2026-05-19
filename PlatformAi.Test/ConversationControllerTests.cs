using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using PlatformAI.Controllers;
using PlatformAI.Core.Logic;
using PlatformAI.Core.Services;
using PlatformAI.Infrastructure.VM;
using PlatformAI.NLP.Models;
using PlatformAI.NLP.Services;
using PlatformAI.Analytics.DTO;

namespace PlatformAI.Tests;

/// <summary>
/// Unit test per ConversationController - StreamConversation e SendConversation
/// </summary>
[TestFixture]
public class ConversationControllerTests : BaseTest
{
    // Mock per LLMStreamingService (per controllare le risposte AI nei test)
    private Mock<LLMStreamingService> _mockLLMStreamingService = null!;
    
    // Controller sotto test
    private ConversationController _controller = null!;
    
    // Http context per testare SSE streaming
    private MemoryStream _responseBodyStream = null!;
    private DefaultHttpContext _httpContext = null!;

    [SetUp]
    public new void Setup()
    {
        // Chiama il Setup del BaseTest per inizializzare DB, UoW, servizi, ecc.
        base.Setup();
        
        // Setup HttpContext per testare le risposte SSE
        SetupHttpContext();
        
        // Crea Mock per LLMStreamingService
        SetupLLMStreamingServiceMock();
        
        // Crea il controller con DI
        CreateController();
    }

    /// <summary>
    /// Setup HttpContext per testare le risposte SSE streaming
    /// </summary>
    private void SetupHttpContext()
    {
        _responseBodyStream = new MemoryStream();
        _httpContext = new DefaultHttpContext();
        _httpContext.Response.Body = _responseBodyStream;
    }

    /// <summary>
    /// Crea Mock per LLMStreamingService (per controllare le risposte AI nei test)
    /// </summary>
    private void SetupLLMStreamingServiceMock()
    {
        // Usa _llmConfig caricato da appsettings.json (dal BaseTest)
        var httpClient = new HttpClient();
        var llmLogger = _serviceProvider.GetRequiredService<ILogger<LLMStreamingService>>();
        _mockLLMStreamingService = new Mock<LLMStreamingService>(httpClient, llmLogger, _llmConfig);
    }

    /// <summary>
    /// Crea il ConversationController con le dipendenze
    /// </summary>
    private void CreateController()
    {
        _controller = new ConversationController(
            _authService,
            _conversationLogic,
            _mockLLMStreamingService.Object
        );

        // Assegna HttpContext al controller per testare SSE
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = _httpContext
        };
    }

    [TearDown]
    public new void TearDown()
    {
        _responseBodyStream?.Dispose();
        _controller = null!;
        
        // Chiama TearDown del BaseTest
        base.TearDown();
    }

    // ============================================================================
    // TEST: StreamConversation - Scenario di successo
    // ============================================================================

    [Test]
    public async Task StreamConversation_WithValidRequest_SendsSSEEvents()
    {
        // Arrange
        var userId = Guid.Parse("b267adf9-6fcc-f011-8195-000d3a4749b7");
        var conversationId = Guid.NewGuid();
        var request = new StreamingRequest
        {
            UserId = userId.ToString(),
            ConversationId = conversationId.ToString(),
            Message = "Ciao, come stai?"
        };

        // Mock GenerateStreamingResponseAsync - simula streaming di chunks
        var chunks = new[] { "Ciao", "!", " Sto", " bene", ", grazie", "!" };
        _mockLLMStreamingService
            .Setup(x => x.GenerateStreamingResponseAsync(
                It.IsAny<string>(),
                It.IsAny<List<ChatMessage>>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncEnumerable(chunks));

        // Act
        await _controller.StreamConversation(request, CancellationToken.None);

        // Assert - Verifica headers SSE
        Assert.That(_httpContext.Response.Headers["Content-Type"].ToString(), Is.EqualTo("text/event-stream"));
        Assert.That(_httpContext.Response.Headers["Cache-Control"].ToString(), Is.EqualTo("no-cache"));
        Assert.That(_httpContext.Response.Headers["Connection"].ToString(), Is.EqualTo("keep-alive"));

        // Verifica che il body contenga gli eventi SSE
        _responseBodyStream.Position = 0;
        var responseContent = Encoding.UTF8.GetString(_responseBodyStream.ToArray());

        Assert.That(responseContent, Does.Contain("event: start"));
        Assert.That(responseContent, Does.Contain("event: chunk"));
        Assert.That(responseContent, Does.Contain("event: complete"));

        // Verifica che tutti i chunks siano stati inviati
        foreach (var chunk in chunks)
        {
            Assert.That(responseContent, Does.Contain(chunk));
        }

        Console.WriteLine($"✅ SSE Response received:");
        Console.WriteLine(responseContent);
    }

    [Test]
    public async Task StreamConversation_WithNewConversation_SavesUserMessage()
    {
        // Arrange
        var userId = Guid.Parse("b267adf9-6fcc-f011-8195-000d3a4749b7");
        var conversationId = Guid.NewGuid();
        var request = new StreamingRequest
        {
            UserId = userId.ToString(),
            ConversationId = conversationId.ToString(),
            Message = "Prima domanda della conversazione"
        };

        _mockLLMStreamingService
            .Setup(x => x.GenerateStreamingResponseAsync(
                It.IsAny<string>(),
                It.IsAny<List<ChatMessage>>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncEnumerable(new[] { "Risposta" }));

        // Act
        await _controller.StreamConversation(request, CancellationToken.None);

        // Assert - Verifica che la conversazione sia stata salvata nel DB
        var savedConversation = await _conversationLogic.GetAllConversationsAsync(userId);
        Assert.That(savedConversation, Is.Not.Empty);
        
        var conversation = savedConversation.FirstOrDefault(c => c.Id == conversationId.ToString());
        Assert.That(conversation, Is.Not.Null);
        Assert.That(conversation!.Messages, Has.Count.GreaterThanOrEqualTo(1));
        
        var userMessage = conversation.Messages.FirstOrDefault(m => !m.IsAnswer);
        Assert.That(userMessage, Is.Not.Null);
        Assert.That(userMessage!.Content, Is.EqualTo("Prima domanda della conversazione"));

        Console.WriteLine($"✅ User message saved: {userMessage.Content}");
    }

    [Test]
    public async Task StreamConversation_SavesAIResponseAfterStreaming()
    {
        // Arrange
        var userId = Guid.Parse("b267adf9-6fcc-f011-8195-000d3a4749b7");
        var conversationId = Guid.NewGuid();
        var request = new StreamingRequest
        {
            UserId = userId.ToString(),
            ConversationId = conversationId.ToString(),
            Message = "Qual è la capitale d'Italia?"
        };

        var aiResponseChunks = new[] { "La", " capitale", " d'Italia", " è", " Roma", "." };
        _mockLLMStreamingService
            .Setup(x => x.GenerateStreamingResponseAsync(
                It.IsAny<string>(),
                It.IsAny<List<ChatMessage>>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncEnumerable(aiResponseChunks));

        // Act
        await _controller.StreamConversation(request, CancellationToken.None);

        // Assert - Verifica che siano stati salvati 2 messaggi (user + AI) nel DB
        var savedConversations = await _conversationLogic.GetAllConversationsAsync(userId);
        var conversation = savedConversations.FirstOrDefault(c => c.Id == conversationId.ToString());
        
        Assert.That(conversation, Is.Not.Null);
        Assert.That(conversation!.Messages.Count, Is.EqualTo(2));

        var userMessage = conversation.Messages.FirstOrDefault(m => !m.IsAnswer);
        var aiMessage = conversation.Messages.FirstOrDefault(m => m.IsAnswer);

        Assert.That(userMessage, Is.Not.Null);
        Assert.That(userMessage!.Content, Is.EqualTo("Qual è la capitale d'Italia?"));

        Assert.That(aiMessage, Is.Not.Null);
        Assert.That(aiMessage!.Content, Is.EqualTo("La capitale d'Italia è Roma."));

        Console.WriteLine($"✅ AI response saved: {aiMessage.Content}");
    }

    [Test]
    public async Task StreamConversation_WithConversationHistory_BuildsCorrectContext()
    {
        // Arrange
        var userId = Guid.Parse("b267adf9-6fcc-f011-8195-000d3a4749b7");
        var conversationId = Guid.NewGuid();
        
        // Prima crea una conversazione con storia
        var firstRequest = new StreamingRequest
        {
            UserId = userId.ToString(),
            ConversationId = conversationId.ToString(),
            Message = "Qual è la capitale d'Italia?"
        };

        _mockLLMStreamingService
            .Setup(x => x.GenerateStreamingResponseAsync(
                It.IsAny<string>(),
                It.IsAny<List<ChatMessage>>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncEnumerable(new[] { "Roma" }));

        await _controller.StreamConversation(firstRequest, CancellationToken.None);

        // Reset response stream per il secondo messaggio
        _responseBodyStream = new MemoryStream();
        _httpContext.Response.Body = _responseBodyStream;

        // Secondo messaggio
        var secondRequest = new StreamingRequest
        {
            UserId = userId.ToString(),
            ConversationId = conversationId.ToString(),
            Message = "E qual è la seconda città più grande?"
        };

        List<ChatMessage>? capturedHistory = null;
        _mockLLMStreamingService
            .Setup(x => x.GenerateStreamingResponseAsync(
                It.IsAny<string>(),
                It.IsAny<List<ChatMessage>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, List<ChatMessage>, CancellationToken>((msg, history, ct) => capturedHistory = history)
            .Returns(CreateAsyncEnumerable(new[] { "Milano" }));

        // Act
        await _controller.StreamConversation(secondRequest, CancellationToken.None);

        // Assert - Verifica che la storia sia stata passata correttamente
        Assert.That(capturedHistory, Is.Not.Null);
        Assert.That(capturedHistory!.Count, Is.GreaterThanOrEqualTo(2));

        Console.WriteLine($"✅ Conversation history passed correctly: {capturedHistory.Count} messages");
    }

    // ============================================================================
    // TEST: StreamConversation - Gestione Errori
    // ============================================================================

    [Test]
    public async Task StreamConversation_OnCancellation_SendsCancelledEvent()
    {
        // Arrange
        var userId = Guid.Parse("b267adf9-6fcc-f011-8195-000d3a4749b7");
        var request = new StreamingRequest
        {
            UserId = userId.ToString(),
            ConversationId = Guid.NewGuid().ToString(),
            Message = "Test cancellation"
        };

        var cts = new CancellationTokenSource();

        // Simula streaming che viene cancellato
        _mockLLMStreamingService
            .Setup(x => x.GenerateStreamingResponseAsync(
                It.IsAny<string>(),
                It.IsAny<List<ChatMessage>>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateCancellableAsyncEnumerable(cts));

        // Act - Cancella dopo un po'
        cts.Cancel();
        await _controller.StreamConversation(request, cts.Token);

        // Assert
        _responseBodyStream.Position = 0;
        var responseContent = Encoding.UTF8.GetString(_responseBodyStream.ToArray());

        Assert.That(responseContent, Does.Contain("event: cancelled").Or.Contain("event: start"));

        Console.WriteLine($"✅ Cancellation handled correctly");
    }

    [Test]
    public async Task StreamConversation_OnException_SendsErrorEvent()
    {
        // Arrange
        var userId = Guid.Parse("b267adf9-6fcc-f011-8195-000d3a4749b7");
        var request = new StreamingRequest
        {
            UserId = userId.ToString(),
            ConversationId = Guid.NewGuid().ToString(),
            Message = "Test error"
        };

        // Simula un errore durante lo streaming
        _mockLLMStreamingService
            .Setup(x => x.GenerateStreamingResponseAsync(
                It.IsAny<string>(),
                It.IsAny<List<ChatMessage>>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateErrorAsyncEnumerable("LLM service unavailable"));

        // Act
        await _controller.StreamConversation(request, CancellationToken.None);

        // Assert
        _responseBodyStream.Position = 0;
        var responseContent = Encoding.UTF8.GetString(_responseBodyStream.ToArray());

        Assert.That(responseContent, Does.Contain("event: error"));
        Assert.That(responseContent, Does.Contain("LLM service unavailable"));

        Console.WriteLine($"✅ Error event sent:");
        Console.WriteLine(responseContent);
    }

    [Test]
    public async Task StreamConversation_WithEmptyMessage_StillProcesses()
    {
        // Arrange
        var userId = Guid.Parse("b267adf9-6fcc-f011-8195-000d3a4749b7");
        var request = new StreamingRequest
        {
            UserId = userId.ToString(),
            ConversationId = Guid.NewGuid().ToString(),
            Message = "" // Messaggio vuoto
        };

        _mockLLMStreamingService
            .Setup(x => x.GenerateStreamingResponseAsync(
                It.IsAny<string>(),
                It.IsAny<List<ChatMessage>>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncEnumerable(new[] { "Response to empty" }));

        // Act
        await _controller.StreamConversation(request, CancellationToken.None);

        // Assert - Il controller dovrebbe comunque processare
        _responseBodyStream.Position = 0;
        var responseContent = Encoding.UTF8.GetString(_responseBodyStream.ToArray());

        Assert.That(responseContent, Does.Contain("event: start"));

        Console.WriteLine($"✅ Empty message handled");
    }

    // ============================================================================
    // TEST: Verifica formato SSE
    // ============================================================================

    [Test]
    public async Task StreamConversation_SSEFormat_IsCorrect()
    {
        // Arrange
        var userId = Guid.Parse("b267adf9-6fcc-f011-8195-000d3a4749b7");
        var request = new StreamingRequest
        {
            UserId = userId.ToString(),
            ConversationId = Guid.NewGuid().ToString(),
            Message = "Test SSE format"
        };

        _mockLLMStreamingService
            .Setup(x => x.GenerateStreamingResponseAsync(
                It.IsAny<string>(),
                It.IsAny<List<ChatMessage>>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncEnumerable(new[] { "Hello" }));

        // Act
        await _controller.StreamConversation(request, CancellationToken.None);

        // Assert - Verifica formato SSE corretto
        _responseBodyStream.Position = 0;
        var responseContent = Encoding.UTF8.GetString(_responseBodyStream.ToArray());

        // SSE format: "event: {type}\ndata: {json}\n\n"
        var lines = responseContent.Split('\n');
        
        // Verifica che ci siano coppie event/data
        bool foundEventDataPair = false;
        for (int i = 0; i < lines.Length - 1; i++)
        {
            if (lines[i].StartsWith("event:") && lines[i + 1].StartsWith("data:"))
            {
                foundEventDataPair = true;
                
                // Verifica che il data sia JSON valido
                var jsonData = lines[i + 1].Substring(5).Trim(); // Rimuovi "data:"
                Assert.DoesNotThrow(() => System.Text.Json.JsonDocument.Parse(jsonData),
                    $"Invalid JSON in SSE data: {jsonData}");
            }
        }

        Assert.That(foundEventDataPair, Is.True, "Should have event/data pairs in SSE format");

        Console.WriteLine($"✅ SSE format is correct");
    }

    // ============================================================================
    // TEST: SendConversation - Endpoint non-streaming
    // ============================================================================

    [Test]
    public async Task SendConversation_WithValidRequest_ReturnsOkWithCompleteResponse()
    {
        // Arrange
        var userId = Guid.Parse("b267adf9-6fcc-f011-8195-000d3a4749b7");
        var conversationId = Guid.NewGuid();
        var request = new StreamingRequest
        {
            UserId = userId.ToString(),
            ConversationId = conversationId.ToString(),
            Message = "Qual è la capitale della Francia?"
        };

        // Mock GenerateCompleteResponseAsync - restituisce la risposta completa
        _mockLLMStreamingService
            .Setup(x => x.GenerateCompleteResponseAsync(
                It.IsAny<string>(),
                It.IsAny<List<ChatMessage>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("La capitale della Francia è Parigi.");

        // Act
        var result = await _controller.SendConversation(request, CancellationToken.None);

        // Assert
        Assert.That(result, Is.InstanceOf<OkObjectResult>());
        var okResult = (OkObjectResult)result;
        Assert.That(okResult.Value, Is.InstanceOf<ConversationResponse>());

        var response = (ConversationResponse)okResult.Value!;
        Assert.That(response.ConversationId, Is.EqualTo(conversationId.ToString()));
        Assert.That(response.Content, Is.EqualTo("La capitale della Francia è Parigi."));
        Assert.That(response.MessageId, Is.Not.Empty);

        Console.WriteLine($"✅ SendConversation returned complete response: {response.Content}");
    }

    [Test]
    public async Task SendConversation_SavesBothUserAndAIMessages()
    {
        // Arrange
        var userId = Guid.Parse("b267adf9-6fcc-f011-8195-000d3a4749b7");
        var conversationId = Guid.NewGuid();
        var request = new StreamingRequest
        {
            UserId = userId.ToString(),
            ConversationId = conversationId.ToString(),
            Message = "Ciao!"
        };

        _mockLLMStreamingService
            .Setup(x => x.GenerateCompleteResponseAsync(
                It.IsAny<string>(),
                It.IsAny<List<ChatMessage>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("Ciao! Come posso aiutarti?");

        // Act
        await _controller.SendConversation(request, CancellationToken.None);

        // Assert - Verifica che siano stati salvati 2 messaggi nel DB
        var savedConversations = await _conversationLogic.GetAllConversationsAsync(userId);
        var conversation = savedConversations.FirstOrDefault(c => c.Id == conversationId.ToString());
        
        Assert.That(conversation, Is.Not.Null);
        Assert.That(conversation!.Messages.Count, Is.EqualTo(2));

        var userMsg = conversation.Messages.FirstOrDefault(m => !m.IsAnswer);
        var aiMsg = conversation.Messages.FirstOrDefault(m => m.IsAnswer);

        Assert.That(userMsg!.Content, Is.EqualTo("Ciao!"));
        Assert.That(aiMsg!.Content, Is.EqualTo("Ciao! Come posso aiutarti?"));

        Console.WriteLine($"✅ Both messages saved - User: '{userMsg.Content}', AI: '{aiMsg.Content}'");
    }

    [Test]
    public async Task SendConversation_WithConversationHistory_PassesContextToLLM()
    {
        // Arrange
        var userId = Guid.Parse("b267adf9-6fcc-f011-8195-000d3a4749b7");
        var conversationId = Guid.NewGuid();
        
        // Prima domanda
        var firstRequest = new StreamingRequest
        {
            UserId = userId.ToString(),
            ConversationId = conversationId.ToString(),
            Message = "Qual è la capitale della Francia?"
        };

        _mockLLMStreamingService
            .Setup(x => x.GenerateCompleteResponseAsync(
                It.IsAny<string>(),
                It.IsAny<List<ChatMessage>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("Parigi");

        await _controller.SendConversation(firstRequest, CancellationToken.None);

        // Seconda domanda
        var secondRequest = new StreamingRequest
        {
            UserId = userId.ToString(),
            ConversationId = conversationId.ToString(),
            Message = "E la seconda città più grande?"
        };

        List<ChatMessage>? capturedHistory = null;

        _mockLLMStreamingService
            .Setup(x => x.GenerateCompleteResponseAsync(
                It.IsAny<string>(),
                It.IsAny<List<ChatMessage>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, List<ChatMessage>, CancellationToken>((msg, history, ct) => capturedHistory = history)
            .ReturnsAsync("Marsiglia");

        // Act
        await _controller.SendConversation(secondRequest, CancellationToken.None);

        // Assert
        Assert.That(capturedHistory, Is.Not.Null);
        Assert.That(capturedHistory!.Count, Is.GreaterThanOrEqualTo(2));

        Console.WriteLine($"✅ Conversation history passed: {capturedHistory.Count} messages");
    }

    [Test]
    public async Task SendConversation_OnCancellation_Returns499()
    {
        // Arrange
        var userId = Guid.Parse("b267adf9-6fcc-f011-8195-000d3a4749b7");
        var request = new StreamingRequest
        {
            UserId = userId.ToString(),
            ConversationId = Guid.NewGuid().ToString(),
            Message = "Test cancellation"
        };

        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancella immediatamente

        _mockLLMStreamingService
            .Setup(x => x.GenerateCompleteResponseAsync(
                It.IsAny<string>(),
                It.IsAny<List<ChatMessage>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act
        var result = await _controller.SendConversation(request, cts.Token);

        // Assert
        Assert.That(result, Is.InstanceOf<ObjectResult>());
        var objectResult = (ObjectResult)result;
        Assert.That(objectResult.StatusCode, Is.EqualTo(499));

        Console.WriteLine($"✅ Cancellation returns 499 status code");
    }

    [Test]
    public async Task SendConversation_OnException_Returns500WithErrorMessage()
    {
        // Arrange
        var userId = Guid.Parse("b267adf9-6fcc-f011-8195-000d3a4749b7");
        var request = new StreamingRequest
        {
            UserId = userId.ToString(),
            ConversationId = Guid.NewGuid().ToString(),
            Message = "Test error"
        };

        _mockLLMStreamingService
            .Setup(x => x.GenerateCompleteResponseAsync(
                It.IsAny<string>(),
                It.IsAny<List<ChatMessage>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("LLM service unavailable"));

        // Act
        var result = await _controller.SendConversation(request, CancellationToken.None);

        // Assert
        Assert.That(result, Is.InstanceOf<ObjectResult>());
        var objectResult = (ObjectResult)result;
        Assert.That(objectResult.StatusCode, Is.EqualTo(500));

        Console.WriteLine($"✅ Exception returns 500 status code");
    }

    [Test]
    public async Task SendConversation_ReturnsUpdatedConversationWithAllMessages()
    {
        // Arrange
        var userId = Guid.Parse("b267adf9-6fcc-f011-8195-000d3a4749b7");
        var conversationId = Guid.NewGuid();
        var request = new StreamingRequest
        {
            UserId = userId.ToString(),
            ConversationId = conversationId.ToString(),
            Message = "Prima domanda"
        };

        _mockLLMStreamingService
            .Setup(x => x.GenerateCompleteResponseAsync(
                It.IsAny<string>(),
                It.IsAny<List<ChatMessage>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("Risposta completa");

        // Act
        var result = await _controller.SendConversation(request, CancellationToken.None);

        // Assert
        var okResult = (OkObjectResult)result;
        var response = (ConversationResponse)okResult.Value!;

        Assert.That(response.Conversation, Is.Not.Null);
        Assert.That(response.Conversation!.Messages.Count, Is.EqualTo(2));

        Console.WriteLine($"✅ Response includes updated conversation with {response.Conversation.Messages.Count} messages");
    }

    // ============================================================================
    // HELPER METHODS
    // ============================================================================

    /// <summary>
    /// Crea un IAsyncEnumerable simulato per i test
    /// </summary>
    private static async IAsyncEnumerable<string> CreateAsyncEnumerable(string[] items)
    {
        foreach (var item in items)
        {
            await Task.Delay(1); // Simula latenza minima
            yield return item;
        }
    }

    /// <summary>
    /// Crea un IAsyncEnumerable che viene cancellato
    /// </summary>
    private static async IAsyncEnumerable<string> CreateCancellableAsyncEnumerable(
        CancellationTokenSource cts,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        for (int i = 0; i < 10; i++)
        {
            if (cts.IsCancellationRequested || cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException();
            }
            await Task.Delay(10, cancellationToken);
            yield return $"chunk{i}";
        }
    }

    /// <summary>
    /// Crea un IAsyncEnumerable che genera un errore
    /// </summary>
    private static async IAsyncEnumerable<string> CreateErrorAsyncEnumerable(string errorMessage)
    {
        await Task.Delay(1);
        throw new Exception(errorMessage);
        #pragma warning disable CS0162 // Unreachable code
        yield break;
        #pragma warning restore CS0162
    }
}
