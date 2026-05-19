using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PlatformAI.Analytics.DTO;
using PlatformAI.Core.Logic;
using PlatformAI.Core.Services;
using PlatformAI.Infrastructure.Application;
using PlatformAI.Infrastructure.VM;
using PlatformAI.NLP.Services;
using System.Text;
using System.Text.Json;
using ChatMessage = PlatformAI.NLP.Models.ChatMessage;


namespace PlatformAI.Controllers
{
    [Route("api/conversation")]
    [ApiController]
    public class ConversationController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ConversationLogic _conversationLogic;
        private readonly LLMStreamingService _llmStreamingService;

        public ConversationController(IAuthService authService, ConversationLogic conversationLogic,LLMStreamingService llmStreamingService)
        {
            _authService = authService;
            _conversationLogic = conversationLogic;
            _llmStreamingService = llmStreamingService;
        }

        [HttpGet("getall/{userId}")]
        public IActionResult GetAllConvesations([FromRoute] string userId)
        {
            var conversations =  _conversationLogic.GetAllConversationsAsync(Guid.Parse(userId)).Result;
            return Ok(conversations);
        }

        [HttpPost("save")]
        public async Task<IActionResult> SaveConversation([FromBody] ConversationRequest request)
        {
            var conversation = await _conversationLogic.SaveConversationAsync(request.Message, userId: Guid.Parse(request.UserId));
            return Ok(conversation);
        }

        /// <summary>
        /// Endpoint SSE per streaming delle risposte AI
        /// Il frontend può connettersi e ricevere i token in tempo reale
        /// </summary>
        [HttpPost("stream")]
        public async Task StreamConversation([FromBody] StreamingRequest request, CancellationToken cancellationToken)
        {
            // Imposta headers per Server-Sent Events
            Response.Headers.Append("Content-Type", "text/event-stream");
            Response.Headers.Append("Cache-Control", "no-cache");
            Response.Headers.Append("Connection", "keep-alive");
            Response.Headers.Append("X-Accel-Buffering", "no"); // Per NGINX

            try
            {
                // 1. Salva il messaggio dell'utente
                var userMessage = new MessageVM
                {
                    Id = Guid.NewGuid().ToString(),
                    ConversationId = request.ConversationId,
                    Content = request.Message,
                    IsAnswer = false,
                    IsLoading = false
                };

                var conversation = await _conversationLogic.SaveConversationAsync(
                    userMessage, 
                    Guid.Parse(request.UserId)
                );

                // 2. Prepara l'ID del messaggio AI
                var aiMessageId = Guid.NewGuid().ToString();
                
                // Invia evento di inizio con l'ID del messaggio AI
                await SendSSEEvent("start", new { 
                    conversationId = conversation.Id,
                    messageId = aiMessageId 
                });

                // 3. Costruisci la cronologia della conversazione per il contesto
                var chatHistory = conversation.Messages
                    .Where(m => m.Id != userMessage.Id)
                    .Select(m => new ChatMessage
                    {
                        Role = m.IsAnswer ? "assistant" : "user",
                        Content = m.Content
                    })
                    .ToList();

                // 4. Genera la risposta in streaming
                var fullResponse = new StringBuilder();
                var chunks = _llmStreamingService.GenerateStreamingResponseAsync( request.Message, chatHistory, cancellationToken);
                await foreach (var chunk in chunks)
                {
                    if (cancellationToken.IsCancellationRequested) break;

                    fullResponse.Append(chunk);
                    
                    // Invia ogni chunk come evento SSE
                    await SendSSEEvent("chunk", new { content = chunk });
                    
                    // Flush per inviare immediatamente
                    await Response.Body.FlushAsync(cancellationToken);
                }

                // 5. Salva il messaggio completo dell'AI
                var aiMessage = new MessageVM
                {
                    Id = aiMessageId,
                    ConversationId = conversation.Id,
                    Content = fullResponse.ToString(),
                    IsAnswer = true,
                    IsLoading = false
                };

                await _conversationLogic.SaveConversationAsync(aiMessage, Guid.Parse(request.UserId));

                // 6. Invia evento di completamento
                await SendSSEEvent("complete", new { 
                    messageId = aiMessageId,
                    content = fullResponse.ToString()
                });
            }
            catch (OperationCanceledException)
            {
                // Connessione chiusa dal client
                await SendSSEEvent("cancelled", new { });
            }
            catch (Exception ex)
            {
                await SendSSEEvent("error", new { message = ex.Message });
            }
        }

        /// <summary>
        /// Endpoint per chat con risposta completa (non streaming)
        /// Aspetta che l'AI generi l'intera risposta prima di restituirla
        /// </summary>
        [HttpPost("send")]
        public async Task<IActionResult> SendConversation([FromBody] StreamingRequest request, CancellationToken cancellationToken)
        {
            try
            {
                // 1. Salva il messaggio dell'utente
                var userMessage = new MessageVM
                {
                    Id = Guid.NewGuid().ToString(),
                    ConversationId = request.ConversationId,
                    Content = request.Message,
                    IsAnswer = false,
                    IsLoading = false
                };

                var conversation = await _conversationLogic.SaveConversationAsync( userMessage,Guid.Parse(request.UserId));

                // 2. Costruisci la cronologia della conversazione per il contesto
                var chatHistory = conversation.Messages
                    .Where(m => m.Id != userMessage.Id)
                    .Select(m => new ChatMessage
                    {
                        Role = m.IsAnswer ? "assistant" : "user",
                        Content = m.Content
                    })
                    .ToList();

                // 3. Genera la risposta completa (attende la risposta intera dal LLM)
                var fullResponse = await _llmStreamingService.SendToAzureOpenAIOnceAsync(request.Message, chatHistory, cancellationToken);
                
                //_llmStreamingService.GenerateCompleteResponseAsync(request.Message,chatHistory,cancellationToken);

                // 4. Salva il messaggio completo dell'AI
                var aiMessageId = Guid.NewGuid().ToString();
                var aiMessage = new MessageVM
                {
                    Id = aiMessageId,
                    ConversationId = conversation.Id,
                    Content = fullResponse,
                    IsAnswer = true,
                    IsLoading = false
                };

                var updatedConversation = await _conversationLogic.SaveConversationAsync(
                    aiMessage,
                    Guid.Parse(request.UserId)
                );

                // 5. Restituisce la risposta completa
                return Ok(new ConversationResponse
                {
                    ConversationId = conversation.Id,
                    MessageId = aiMessageId,
                    Content = fullResponse,
                    Conversation = updatedConversation
                });
            }
            catch (OperationCanceledException)
            {
                return StatusCode(499, new { message = "Request cancelled by client" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        /// <summary>
        /// Helper per inviare eventi SSE formattati
        /// </summary>
        private async Task SendSSEEvent(string eventType, object data)
        {
            var json = JsonSerializer.Serialize(data);
            var message = $"event: {eventType}\ndata: {json}\n\n";
            var bytes = Encoding.UTF8.GetBytes(message);
            await Response.Body.WriteAsync(bytes);
            await Response.Body.FlushAsync();
        }
    }

  
}
