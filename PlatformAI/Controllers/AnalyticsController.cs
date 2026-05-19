using Microsoft.AspNetCore.Mvc;
using PlatformAI.Analytics.DTO;
using PlatformAI.Analytics.Services;
using PlatformAI.Core.Logic;
using PlatformAI.Infrastructure.VM;
using PlatformAI.NLP.Models;
using System.Text;
using System.Text.Json;
using ChatMessage = PlatformAI.NLP.Models.ChatMessage;

namespace PlatformAI.Controllers;

/// <summary>
/// Controller per le analisi AI con supporto per grafici e predizioni ML.
/// Supporta sia risposte complete che streaming SSE per l'UI.
/// </summary>
[Route("api/analytics")]
[ApiController]
public class AnalyticsController : ControllerBase
{
    private readonly LLMAnalyticsService _analyticsService;
    private readonly ConversationLogic _conversationLogic;
    private readonly ILogger<AnalyticsController> _logger;

    public AnalyticsController(
        LLMAnalyticsService analyticsService,
        ConversationLogic conversationLogic,
        ILogger<AnalyticsController> logger)
    {
        _analyticsService = analyticsService;
        _conversationLogic = conversationLogic;
        _logger = logger;
    }

    /// <summary>
    /// Endpoint SSE per streaming delle risposte AI con grafici.
    /// Restituisce token di testo, grafici e predizioni man mano che vengono generati.
    /// </summary>
    /// <remarks>
    /// Eventi SSE restituiti:
    /// - start: inizio della risposta con conversationId e messageId
    /// - text: chunk di testo dalla risposta AI
    /// - chart: dati di un grafico (JSON ChartData)
    /// - prediction: dati di una predizione ML (JSON PredictionData)
    /// - processing: indicatore che i tools sono in elaborazione
    /// - complete: risposta completata con statistiche finali
    /// - error: errore durante l'elaborazione
    /// - cancelled: richiesta annullata dal client
    /// </remarks>
    [HttpPost("stream")]
    public async Task StreamAnalytics([FromBody] StreamingRequest request, CancellationToken cancellationToken)
    {
        // Imposta headers per Server-Sent Events
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");
        Response.Headers.Append("X-Accel-Buffering", "no"); // Per NGINX

        try
        {
            
            _logger.LogInformation("Starting analytics stream for user {UserId}",
                request.UserId);

            // 1. Salva il messaggio dell'utente (se conversationId fornito)
            ConversationVM? conversation = null;
            string? aiMessageId = null;
            Guid userId = Guid.Parse(request.UserId);
            Guid conversationId = Guid.Parse(request.ConversationId);
            if (userId != Guid.Empty && conversationId != Guid.Empty)
            {
                var userMessage = new MessageVM
                {
                    Id = Guid.NewGuid().ToString(),
                    ConversationId = request.ConversationId,
                    Content = request.Message,
                    IsAnswer = false,
                    IsLoading = false
                };

                conversation = await _conversationLogic.SaveConversationAsync(
                    userMessage,
                    Guid.Parse(request.UserId)
                );

                aiMessageId = Guid.NewGuid().ToString();
            }
            else
            {
                _logger.LogInformation("No conversationId provided, skipping message save");
                  await SendSSEEvent("error", new { message = "No conversationId provided, skipping message save" });
            }

            // Invia evento di inizio
            await SendSSEEvent("start", new
            {
                conversationId = conversation?.Id ?? request.ConversationId,
                messageId = aiMessageId ?? Guid.NewGuid().ToString()
                //tenantCode = request.TenantCode
            });

            // 2. Costruisci la cronologia della conversazione per il contesto
            List<ChatMessage>? chatHistory = null;
            if (conversation != null)
            {
                chatHistory = conversation.Messages
                    .Where(m => !m.IsLoading)
                    .Select(m => new ChatMessage
                    {
                        Role = m.IsAnswer ? "assistant" : "user",
                        Content = m.Content
                    })
                    .ToList();
            }

            // 3. Genera la risposta in streaming con grafici
            var fullTextResponse = new StringBuilder();
            var chartsGenerated = new List<ChartData>();
            PredictionData? predictionGenerated = null;
            
            var responseStream = _analyticsService.GenerateResponseWithChartsStreamingAsync(request.Message, userId, conversationId,cancellationToken);
            await foreach (var response in responseStream)
            {
                if (cancellationToken.IsCancellationRequested) break;

                switch (response.Type)
                {
                    case StreamingResponseType.TextChunk:
                        fullTextResponse.Append(response.TextContent);
                        await SendSSEEvent("text", new { content = response.TextContent });
                        break;

                    case StreamingResponseType.ChartData:
                        if (response.Chart != null)
                        {
                            chartsGenerated.Add(response.Chart);
                            await SendSSEEvent("chart", response.Chart);
                        }
                        break;

                    case StreamingResponseType.PredictionData:
                        if (response.Prediction != null)
                        {
                            predictionGenerated = response.Prediction;
                            await SendSSEEvent("prediction", response.Prediction);
                        }
                        break;

                    case StreamingResponseType.ProcessingTools:
                        await SendSSEEvent("processing", new { message = response.TextContent });
                        break;

                    case StreamingResponseType.Error:
                        await SendSSEEvent("error", new { message = response.ErrorMessage });
                        break;

                    case StreamingResponseType.Complete:
                        // Gestito dopo il loop
                        break;
                }

                // Flush per inviare immediatamente
                await Response.Body.FlushAsync(cancellationToken);
            }

            // 4. Salva il messaggio completo dell'AI (se conversationId fornito)
            if (conversation != null && aiMessageId != null)
            {
                // Costruisci il contenuto completo con riferimenti ai grafici
                var finalContent = fullTextResponse.ToString();
                if (chartsGenerated.Count > 0)
                {
                    finalContent += $"\n\n[{chartsGenerated.Count} grafico/i generato/i]";
                }

                var aiMessage = new MessageVM
                {
                    Id = aiMessageId,
                    ConversationId = conversation.Id,
                    Content = fullTextResponse.ToString(),
                    IsAnswer = true,
                    IsLoading = false,
                    ChartsJson = chartsGenerated.Count > 0
                        ? System.Text.Json.JsonSerializer.Serialize(chartsGenerated,
                            new System.Text.Json.JsonSerializerOptions
                            {
                                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                            })
                        : null
                };

                await _conversationLogic.SaveConversationAsync(aiMessage, Guid.Parse(request.UserId));
            }

            // 5. Invia evento di completamento
            await SendSSEEvent("complete", new
            {
                messageId = aiMessageId,
                content = fullTextResponse.ToString(),
                chartsCount = chartsGenerated.Count,
                hasPrediction = predictionGenerated != null,
                chartIds = chartsGenerated.Select(c => c.Id).ToList()
            });

            _logger.LogInformation("Analytics stream completed: {ChartsCount} charts, prediction: {HasPrediction}",
                chartsGenerated.Count, predictionGenerated != null);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Analytics stream cancelled by client");
            await SendSSEEvent("cancelled", new { });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in analytics stream");
            await SendSSEEvent("error", new { message = ex.Message });
        }
    }

    /// <summary>
    /// Endpoint per analisi con risposta completa (non streaming).
    /// Attende che l'AI generi l'intera risposta con grafici prima di restituirla.
    /// </summary>
    [HttpPost("analyze")]
    public async Task<IActionResult> Analyze([FromBody] StreamingRequest request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting analytics for tenant {TenantCode}", request.UserId);

             var userId= Guid.Parse(request.UserId);
             var conversationId= Guid.Parse(request.ConversationId);

            // Genera la risposta con grafici
            var result = await _analyticsService.GenerateResponseWithChartsAsync(
                request.Message,
                userId,
                conversationId,
                cancellationToken
            );

            return Ok(new AnalyticsResponse
            {
                Content = result.Content,
                Charts = result.Charts,
                Prediction = result.Prediction,
                HasCharts = result.HasCharts
            });
        }
        catch (OperationCanceledException)
        {
            return StatusCode(499, new { message = "Request cancelled by client" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in analytics");
            return StatusCode(500, new { message = ex.Message });
        }
    }

    /// <summary>
    /// Endpoint per ottenere una smart prediction (sceglie automaticamente la fonte migliore)
    /// </summary>
    // [HttpPost("predict")]
    // public async Task<IActionResult> GetSmartPrediction([FromBody] StreamingRequest request, CancellationToken cancellationToken)
    // {
    //     try
    //     {
    //         var args = new GetPredictionArgs
    //         {
    //             Target = request.Target,
    //             Horizon = request.Horizon
    //         };

    //         var result = await _analyticsService.GetSmartPredictionAsync(
    //             request.TenantCode,
    //             args,
    //             request.Config,
    //             cancellationToken
    //         );

    //         return Ok(result);
    //     }
    //     catch (Exception ex)
    //     {
    //         _logger.LogError(ex, "Error in smart prediction");
    //         return StatusCode(500, new { message = ex.Message });
    //     }
    // }

    // /// <summary>
    // /// Endpoint per confrontare le predizioni (database vs ML)
    // /// </summary>
    // [HttpPost("predict/compare")]
    // public async Task<IActionResult> ComparePredictions([FromBody] PredictionRequest request, CancellationToken cancellationToken)
    // {
    //     try
    //     {
    //         var args = new GetPredictionArgs
    //         {
    //             Target = request.Target,
    //             Horizon = request.Horizon
    //         };

    //         var result = await _analyticsService.GetComparisonPredictionAsync(
    //             request.TenantCode,
    //             args,
    //             cancellationToken
    //         );

    //         return Ok(result);
    //     }
    //     catch (Exception ex)
    //     {
    //         _logger.LogError(ex, "Error in prediction comparison");
    //         return StatusCode(500, new { message = ex.Message });
    //     }
    // }

    /// <summary>
    /// Helper per inviare eventi SSE formattati
    /// </summary>
    private async Task SendSSEEvent(string eventType, object data)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
        var json = JsonSerializer.Serialize(data, options);
        var message = $"event: {eventType}\ndata: {json}\n\n";
        var bytes = Encoding.UTF8.GetBytes(message);
        await Response.Body.WriteAsync(bytes);
        await Response.Body.FlushAsync();
    }
}

#region Request/Response Models

/// <summary>
/// Request per lo streaming analytics
/// </summary>
public class AnalyticsStreamRequest
{
    /// <summary>ID dell'utente (opzionale, per salvare la conversazione)</summary>
    public string? UserId { get; set; }
    
    /// <summary>ID della conversazione (opzionale)</summary>
    public string? ConversationId { get; set; }
    
    /// <summary>Messaggio dell'utente</summary>
    public string Message { get; set; } = string.Empty;
    
    /// <summary>Codice del tenant per filtrare i dati di produzione</summary>
    public string TenantCode { get; set; } = string.Empty;
}



/// <summary>
/// Response per analisi completa
/// </summary>
public class AnalyticsResponse
{
    /// <summary>Risposta testuale dell'AI</summary>
    public string Content { get; set; } = string.Empty;
    
    /// <summary>Lista di grafici generati</summary>
    public List<ChartData> Charts { get; set; } = new();
    
    /// <summary>Predizione ML (se richiesta)</summary>
    public PredictionData? Prediction { get; set; }
    
    /// <summary>Indica se sono stati generati grafici</summary>
    public bool HasCharts { get; set; }
}

/// <summary>
/// Request per predizioni
/// </summary>
// public class PredictionRequest
// {
//     /// <summary>Codice del tenant</summary>
//     public string TenantCode { get; set; } = string.Empty;
    
//     /// <summary>Target della predizione: quantity, scrap, energy</summary>
//     public string Target { get; set; } = "quantity";
    
//     /// <summary>Orizzonte temporale: next_shift, tomorrow, next_week</summary>
//     public string? Horizon { get; set; }
    
//     /// <summary>Configurazione per smart prediction (opzionale)</summary>
//     public SmartPredictionConfig? Config { get; set; }
// }

#endregion
