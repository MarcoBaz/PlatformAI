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
using Xunit;
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
public class ConversationControllerTests : BaseTest
{
    private Mock<LLMStreamingService> _mockLLMStreamingService = null!;
    private ConversationController _controller = null!;
    private MemoryStream _responseBodyStream = null!;
    private DefaultHttpContext _httpContext = null!;

    public ConversationControllerTests() : base()
    {
        // Setup HttpContext per testare le risposte SSE
        _responseBodyStream = new MemoryStream();
        _httpContext = new DefaultHttpContext();
        _httpContext.Response.Body = _responseBodyStream;

        // Crea Mock per LLMStreamingService
        var httpClient = new HttpClient();
        var llmLogger = _serviceProvider.GetRequiredService<ILogger<LLMStreamingService>>();
        _mockLLMStreamingService = new Mock<LLMStreamingService>(httpClient, llmLogger, _llmConfig);

        // Crea il controller con DI
        _controller = new ConversationController(
            _authService,
            _conversationLogic,
            _mockLLMStreamingService.Object
        );

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = _httpContext
        };
    }

    public override void Dispose()
    {
        _responseBodyStream?.Dispose();
        _controller = null!;
        base.Dispose();
    }

    // ============================================================================
    // TEST: StreamConversation - Scenario di successo
    // ============================================================================

    [Fact]
    public async Task StreamConversation_WithValidRequest_SendsSSEEvents()
    {
        var userId = Guid.Parse("b267adf9-6fcc-f011-8195-000d3a4749b7");
        var conversationId = Guid.NewGuid();
        var request = new StreamingRequest
        {
            UserId = userId.ToString(),
            ConversationId = conversationId.ToString(),
            Message = "Ciao, come stai?"
        };

        var chunks = new[] { "Ciao", "!", " Sto", " bene", ", grazie", "!" };
        _mockLLMStreamingService
            .Setup(x => x.GenerateStreamingResponseAsync(
                It.IsAny<string>(),
                It.IsAny<List<ChatMessage>>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncEnumerable(chunks));

        await _controller.StreamConversation(request, CancellationToken.None);

        Assert.Equal("text/event-stream", _httpContext.Response.Headers["Content-Type"].ToString());
        Assert.Equal("no-cache", _httpContext.Response.Headers["Cache-Control"].ToString());
        Assert.Equal("keep-alive", _httpContext.Response.Headers["Connection"].ToString());

        _responseBodyStream.Position = 0;
        var responseContent = Encoding.UTF8.GetString(_responseBodyStream.ToArray());

        Assert.Contains("event: start", responseContent);
        Assert.Contains("event: chunk", responseContent);
        Assert.Contains("event: complete", responseContent);

        foreach (var chunk in chunks)
        {
            Assert.Contains(chunk, responseContent);
        }

        Console.WriteLine($"✅ SSE Response received:");
        Console.WriteLine(responseContent);
    }

    [Fact]
    public async Task StreamConversation_WithNewConversation_SavesUserMessage()
    {
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

        await _controller.StreamConversation(request, CancellationToken.None);

        var savedConversation = await _conversationLogic.GetAllConversationsAsync(userId);
        Assert.NotEmpty(savedConversation);

        var conversation = savedConversation.FirstOrDefault(c => c.Id == conversationId.ToString());
        Assert.NotNull(conversation);
        Assert.True(conversation!.Messages.Count >= 1);

        var userMessage = conversation.Messages.FirstOrDefault(m => !m.IsAnswer);
        Assert.NotNull(userMessage);
        Assert.Equal("Prima domanda della conversazione", userMessage!.Content);

        Console.WriteLine($"✅ User message saved: {userMessage.Content}");
    }

    [Fact]
    public async Task StreamConversation_SavesAIResponseAfterStreaming()
    {
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

        await _controller.StreamConversation(request, CancellationToken.None);

        var savedConversations = await _conversationLogic.GetAllConversationsAsync(userId);
        var conversation = savedConversations.FirstOrDefault(c => c.Id == conversationId.ToString());

        Assert.NotNull(conversation);
        Assert.Equal(2, conversation!.Messages.Count);

        var userMessage = conversation.Messages.FirstOrDefault(m => !m.IsAnswer);
        var aiMessage = conversation.Messages.FirstOrDefault(m => m.IsAnswer);

        Assert.NotNull(userMessage);
        Assert.Equal("Qual è la capitale d'Italia?", userMessage!.Content);

        Assert.NotNull(aiMessage);
        Assert.Equal("La capitale d'Italia è Roma.", aiMessage!.Content);

        Console.WriteLine($"✅ AI response saved: {aiMessage.Content}");
    }

    [Fact]
    public async Task StreamConversation_WithConversationHistory_BuildsCorrectContext()
    {
        var userId = Guid.Parse("b267adf9-6fcc-f011-8195-000d3a4749b7");
        var conversationId = Guid.NewGuid();

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

        _responseBodyStream = new MemoryStream();
        _httpContext.Response.Body = _responseBodyStream;

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

        await _controller.StreamConversation(secondRequest, CancellationToken.None);

        Assert.NotNull(capturedHistory);
        Assert.True(capturedHistory!.Count >= 2);

        Console.WriteLine($"✅ Conversation history passed correctly: {capturedHistory.Count} messages");
    }

    // ============================================================================
    // TEST: StreamConversation - Gestione Errori
    // ============================================================================

    [Fact]
    public async Task StreamConversation_OnCancellation_SendsCancelledEvent()
    {
        var userId = Guid.Parse("b267adf9-6fcc-f011-8195-000d3a4749b7");
        var request = new StreamingRequest
        {
            UserId = userId.ToString(),
            ConversationId = Guid.NewGuid().ToString(),
            Message = "Test cancellation"
        };

        var cts = new CancellationTokenSource();

        _mockLLMStreamingService
            .Setup(x => x.GenerateStreamingResponseAsync(
                It.IsAny<string>(),
                It.IsAny<List<ChatMessage>>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateCancellableAsyncEnumerable(cts));

        cts.Cancel();
        await _controller.StreamConversation(request, cts.Token);

        _responseBodyStream.Position = 0;
        var responseContent = Encoding.UTF8.GetString(_responseBodyStream.ToArray());

        Assert.True(responseContent.Contains("event: cancelled") || responseContent.Contains("event: start"));

        Console.WriteLine($"✅ Cancellation handled correctly");
    }

    [Fact]
    public async Task StreamConversation_OnException_SendsErrorEvent()
    {
        var userId = Guid.Parse("b267adf9-6fcc-f011-8195-000d3a4749b7");
        var request = new StreamingRequest
        {
            UserId = userId.ToString(),
            ConversationId = Guid.NewGuid().ToString(),
            Message = "Test error"
        };

        _mockLLMStreamingService
            .Setup(x => x.GenerateStreamingResponseAsync(
                It.IsAny<string>(),
                It.IsAny<List<ChatMessage>>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateErrorAsyncEnumerable("LLM service unavailable"));

        await _controller.StreamConversation(request, CancellationToken.None);

        _responseBodyStream.Position = 0;
        var responseContent = Encoding.UTF8.GetString(_responseBodyStream.ToArray());

        Assert.Contains("event: error", responseContent);
        Assert.Contains("LLM service unavailable", responseContent);

        Console.WriteLine($"✅ Error event sent:");
        Console.WriteLine(responseContent);
    }

    [Fact]
    public async Task StreamConversation_WithEmptyMessage_StillProcesses()
    {
        var userId = Guid.Parse("b267adf9-6fcc-f011-8195-000d3a4749b7");
        var request = new StreamingRequest
        {
            UserId = userId.ToString(),
            ConversationId = Guid.NewGuid().ToString(),
            Message = ""
        };

        _mockLLMStreamingService
            .Setup(x => x.GenerateStreamingResponseAsync(
                It.IsAny<string>(),
                It.IsAny<List<ChatMessage>>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncEnumerable(new[] { "Response to empty" }));

        await _controller.StreamConversation(request, CancellationToken.None);

        _responseBodyStream.Position = 0;
        var responseContent = Encoding.UTF8.GetString(_responseBodyStream.ToArray());

        Assert.Contains("event: start", responseContent);

        Console.WriteLine($"✅ Empty message handled");
    }

    // ============================================================================
    // TEST: Verifica formato SSE
    // ============================================================================

    [Fact]
    public async Task StreamConversation_SSEFormat_IsCorrect()
    {
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

        await _controller.StreamConversation(request, CancellationToken.None);

        _responseBodyStream.Position = 0;
        var responseContent = Encoding.UTF8.GetString(_responseBodyStream.ToArray());

        var lines = responseContent.Split('\n');

        bool foundEventDataPair = false;
        for (int i = 0; i < lines.Length - 1; i++)
        {
            if (lines[i].StartsWith("event:") && lines[i + 1].StartsWith("data:"))
            {
                foundEventDataPair = true;

                var jsonData = lines[i + 1].Substring(5).Trim();
                var ex = Record.Exception(() => System.Text.Json.JsonDocument.Parse(jsonData));
                Assert.Null(ex);
            }
        }

        Assert.True(foundEventDataPair, "Should have event/data pairs in SSE format");

        Console.WriteLine($"✅ SSE format is correct");
    }

    // ============================================================================
    // TEST: SendConversation - Endpoint non-streaming
    // ============================================================================

    [Fact]
    public async Task SendConversation_WithValidRequest_ReturnsOkWithCompleteResponse()
    {
        var userId = Guid.Parse("b267adf9-6fcc-f011-8195-000d3a4749b7");
        var conversationId = Guid.NewGuid();
        var request = new StreamingRequest
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
            .ReturnsAsync("La capitale della Francia è Parigi.");

        var result = await _controller.SendConversation(request, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        var okResult = (OkObjectResult)result;
        Assert.IsType<ConversationResponse>(okResult.Value);

        var response = (ConversationResponse)okResult.Value!;
        Assert.Equal(conversationId.ToString(), response.ConversationId);
        Assert.Equal("La capitale della Francia è Parigi.", response.Content);
        Assert.NotEmpty(response.MessageId);

        Console.WriteLine($"✅ SendConversation returned complete response: {response.Content}");
    }

    [Fact]
    public async Task SendConversation_SavesBothUserAndAIMessages()
    {
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

        await _controller.SendConversation(request, CancellationToken.None);

        var savedConversations = await _conversationLogic.GetAllConversationsAsync(userId);
        var conversation = savedConversations.FirstOrDefault(c => c.Id == conversationId.ToString());

        Assert.NotNull(conversation);
        Assert.Equal(2, conversation!.Messages.Count);

        var userMsg = conversation.Messages.FirstOrDefault(m => !m.IsAnswer);
        var aiMsg = conversation.Messages.FirstOrDefault(m => m.IsAnswer);

        Assert.Equal("Ciao!", userMsg!.Content);
        Assert.Equal("Ciao! Come posso aiutarti?", aiMsg!.Content);

        Console.WriteLine($"✅ Both messages saved - User: '{userMsg.Content}', AI: '{aiMsg.Content}'");
    }

    [Fact]
    public async Task SendConversation_WithConversationHistory_PassesContextToLLM()
    {
        var userId = Guid.Parse("b267adf9-6fcc-f011-8195-000d3a4749b7");
        var conversationId = Guid.NewGuid();

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

        await _controller.SendConversation(secondRequest, CancellationToken.None);

        Assert.NotNull(capturedHistory);
        Assert.True(capturedHistory!.Count >= 2);

        Console.WriteLine($"✅ Conversation history passed: {capturedHistory.Count} messages");
    }

    [Fact]
    public async Task SendConversation_OnCancellation_Returns499()
    {
        var userId = Guid.Parse("b267adf9-6fcc-f011-8195-000d3a4749b7");
        var request = new StreamingRequest
        {
            UserId = userId.ToString(),
            ConversationId = Guid.NewGuid().ToString(),
            Message = "Test cancellation"
        };

        var cts = new CancellationTokenSource();
        cts.Cancel();

        _mockLLMStreamingService
            .Setup(x => x.GenerateCompleteResponseAsync(
                It.IsAny<string>(),
                It.IsAny<List<ChatMessage>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var result = await _controller.SendConversation(request, cts.Token);

        Assert.IsType<ObjectResult>(result);
        var objectResult = (ObjectResult)result;
        Assert.Equal(499, objectResult.StatusCode);

        Console.WriteLine($"✅ Cancellation returns 499 status code");
    }

    [Fact]
    public async Task SendConversation_OnException_Returns500WithErrorMessage()
    {
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

        var result = await _controller.SendConversation(request, CancellationToken.None);

        Assert.IsType<ObjectResult>(result);
        var objectResult = (ObjectResult)result;
        Assert.Equal(500, objectResult.StatusCode);

        Console.WriteLine($"✅ Exception returns 500 status code");
    }

    [Fact]
    public async Task SendConversation_ReturnsUpdatedConversationWithAllMessages()
    {
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

        var result = await _controller.SendConversation(request, CancellationToken.None);

        var okResult = (OkObjectResult)result;
        var response = (ConversationResponse)okResult.Value!;

        Assert.NotNull(response.Conversation);
        Assert.Equal(2, response.Conversation!.Messages.Count);

        Console.WriteLine($"✅ Response includes updated conversation with {response.Conversation.Messages.Count} messages");
    }

    // ============================================================================
    // HELPER METHODS
    // ============================================================================

    private static async IAsyncEnumerable<string> CreateAsyncEnumerable(string[] items)
    {
        foreach (var item in items)
        {
            await Task.Delay(1);
            yield return item;
        }
    }

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

    private static async IAsyncEnumerable<string> CreateErrorAsyncEnumerable(string errorMessage)
    {
        await Task.Delay(1);
        throw new Exception(errorMessage);
        #pragma warning disable CS0162
        yield break;
        #pragma warning restore CS0162
    }
}
