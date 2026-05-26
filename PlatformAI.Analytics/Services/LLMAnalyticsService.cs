using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using PlatformAI.Infrastructure;
using PlatformAI.Infrastructure.Application;
using PlatformAI.Infrastructure.Master;
using PlatformAI.ML;
using PlatformAI.ML.Services;
using PlatformAI.NLP.Models;
using PlatformAI.NLP.Services;
using ChatMessage = PlatformAI.NLP.Models.ChatMessage;

namespace PlatformAI.Analytics.Services;

/// <summary>
/// Servizio LLM con capacità di generare grafici e integrare dati ML
/// Combina Azure OpenAI con dati di produzione per analisi avanzate
/// </summary>
public class LLMAnalyticsService
{
    private readonly ILogger<LLMAnalyticsService> _logger;
    private readonly LLMConfig _config;
    private readonly IUnitOfWork _uow;
    private readonly TrainingService? _trainingService;
    private readonly LLMStreamingService? _streamingService;

    // Tools/Functions disponibili per l'AI
    private static readonly ChatTool GetProductionDataTool = ChatTool.CreateFunctionTool(
        functionName: "get_production_data",
        functionDescription: "Recupera i dati di produzione per generare grafici. Usa questa funzione quando l'utente chiede grafici, trend, statistiche o analisi sui dati di produzione.",
        functionParameters: BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "metric": {
                    "type": "string",
                    "enum": ["quantity", "energy", "temperature", "scrap", "cycle_time", "efficiency", "all"],
                    "description": "Metrica da visualizzare: quantity=quantità prodotta, energy=consumo energetico, temperature=temperatura, scrap=scarti, cycle_time=tempo ciclo, efficiency=efficienza, all=tutte le metriche principali"
                },
                "period": {
                    "type": "string",
                    "enum": ["today", "week", "month", "quarter", "year", "custom"],
                    "description": "Periodo temporale per i dati"
                },
                "chart_type": {
                    "type": "string",
                    "enum": ["line", "bar", "pie", "doughnut", "radar", "scatter"],
                    "description": "Tipo di grafico da generare"
                },
                "group_by": {
                    "type": "string",
                    "enum": ["hour", "day", "week", "month", "shift", "machine"],
                    "description": "Come raggruppare i dati"
                }
                
            },
            "required": ["metric", "period"]
            
        }
        """)
    );
/*
 "language": {
                         "type": "string",
                        "enum": ["it", "en", "both"],
                        "description": "Lingua della risposta: it=italiano, en=inglese, both=entrambe"
                },
*/
    private static readonly ChatTool GetPredictionTool = ChatTool.CreateFunctionTool(
        functionName: "get_prediction",
        functionDescription: "Genera una predizione ML sulla produzione futura. Usa questa funzione quando l'utente chiede previsioni, stime future o predizioni.",
        functionParameters: BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "target": {
                    "type": "string",
                    "enum": ["quantity", "scrap", "energy"],
                    "description": "Cosa predire: quantity=quantità prodotta, scrap=scarti, energy=consumo energetico"
                },
                "horizon": {
                    "type": "string",
                    "enum": ["next_shift", "tomorrow", "next_week"],
                    "description": "Orizzonte temporale della predizione"
                }
            },
            "required": ["target"]
        }
        """)
    );

    /// <summary>
    /// Costruttore per DI con IOptions.
    /// TrainingService è obbligatorio: deve essere registrato in Program.cs.
    /// </summary>
    public LLMAnalyticsService(
        ILogger<LLMAnalyticsService> logger,
        IOptions<LLMConfig> options,
        IUnitOfWork uow,
        TrainingService trainingService,
        LLMStreamingService? streamingService = null)
        : this(logger, options.Value, uow, trainingService, streamingService)
    {
    }

    /// <summary>
    /// Costruttore diretto (per test).
    /// trainingService può essere null solo nei test unitari che non testano la predizione ML.
    /// </summary>
    public LLMAnalyticsService(
        ILogger<LLMAnalyticsService> logger,
        LLMConfig config,
        IUnitOfWork uow,
        TrainingService? trainingService = null,
        LLMStreamingService? streamingService = null)
    {
        _logger = logger;
        _config = config;
        _uow = uow;
        _trainingService = trainingService;
        _streamingService = streamingService;
    }

    /// <summary>
    /// Genera una risposta con grafici opzionali basati sui dati di produzione
    /// </summary>
    public async Task<LLMResponseWithCharts> GenerateResponseWithChartsAsync(
        string userMessage,
        Guid userId,
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating response with charts for User {UserId}: {Message}",
            userId, userMessage.Substring(0, Math.Min(50, userMessage.Length)));

        var result = new LLMResponseWithCharts();

        try
        {
             _logger.LogWarning("LLMStreamingService not available, falling back to non-streaming response");
             var conversationHistory = _uow.Repository<Conversation>().Query(x => x.Id == conversationId)
                .Include(x => x.Messages)
                .Select(x => x.Messages
                    .OrderBy(m => m.LastModifiedDate)
                    .Select(m => new ChatMessage
                    {
                        Role = m.IsAnswer ? "assistant" : "user",
                        Content = m.Content
                    })
                    .ToList()
                )
                .FirstOrDefault();

            var azureClient = new AzureOpenAIClient(
                new Uri(_config.Endpoint!),
                new AzureKeyCredential(_config.ApiKey!)
            );

            var chatClient = azureClient.GetChatClient(_config.DeploymentName);

            // Recupera il tenant dell'utente per iniettarlo nel system prompt
            var userForTenant = await _uow.Repository<User>()
                .Query(x => x.Id == userId)
                .Include(x => x.Tenant)
                .FirstOrDefaultAsync();

            // Costruisci messaggi con system prompt specializzato
            var messages = BuildMessagesWithAnalyticsContext(userMessage, conversationHistory, userForTenant?.Tenant?.Code);

            // Prima chiamata con tools
            var options = new ChatCompletionOptions
            {
                Temperature = 0.7f,
                MaxOutputTokenCount = 2000
            };
            options.Tools.Add(GetProductionDataTool);
            options.Tools.Add(GetPredictionTool);

            var response = await chatClient.CompleteChatAsync(messages, options, cancellationToken);
            var chatCompletion = response.Value;

            // Gestisci eventuali tool calls
            while (chatCompletion.FinishReason == ChatFinishReason.ToolCalls)
            {
                // Aggiungi la risposta dell'assistente con le tool calls
                messages.Add(new AssistantChatMessage(chatCompletion));

                // Processa ogni tool call
                foreach (var toolCall in chatCompletion.ToolCalls)
                {
                    _logger.LogInformation("Tool call: {Tool} with args: {Args}",
                        toolCall.FunctionName, toolCall.FunctionArguments);

                    string toolResult;

                    if (toolCall.FunctionName == "get_production_data")
                    {
                        var args = JsonSerializer.Deserialize<GetProductionDataArgs>(toolCall.FunctionArguments);
                        var chartData = await GetProductionChartDataAsync(userId, args!, cancellationToken);
                        result.Charts.AddRange(chartData);
                        toolResult = JsonSerializer.Serialize(new { success = true, charts_generated = chartData.Count });
                    }
                    else if (toolCall.FunctionName == "get_prediction")
                    {
                        var args = JsonSerializer.Deserialize<GetPredictionArgs>(toolCall.FunctionArguments);
                        var prediction = await GetPredictionFromMLModelAsync(userId, args!, cancellationToken);
                        result.Prediction = prediction;
                        toolResult = JsonSerializer.Serialize(new
                        {
                            predicted_value = prediction.PredictedValue,
                            confidence      = prediction.Confidence,
                            r_squared       = prediction.ModelRSquared,
                            rmse            = prediction.ModelRMSE,
                            explanation     = prediction.Explanation
                        });
                    }
                    else
                    {
                        toolResult = JsonSerializer.Serialize(new { error = "Unknown tool" });
                    }

                    messages.Add(new ToolChatMessage(toolCall.Id, toolResult));
                }

                // Continua la conversazione
                response = await chatClient.CompleteChatAsync(messages, options, cancellationToken);
                chatCompletion = response.Value;
            }

            // Estrai la risposta finale
            result.Content = chatCompletion.Content?.FirstOrDefault()?.Text ?? string.Empty;

            _logger.LogInformation("Response generated with {ChartCount} charts", result.Charts.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating response with charts");
            result.Content = $"Si è verificato un errore: {ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// Genera una risposta in streaming con grafici opzionali basati sui dati di produzione.
    /// Utilizza LLMStreamingService per restituire i token man mano che vengono generati.
    /// Supporta tool calls per la generazione di grafici e predizioni ML.
    /// </summary>
    /// <param name="userMessage">Messaggio dell'utente</param>
    /// <param name="tenantCode">Codice del tenant per filtrare i dati</param>
    /// <param name="conversationHistory">Cronologia della conversazione (opzionale)</param>
    /// <param name="cancellationToken">Token di cancellazione</param>
    /// <returns>IAsyncEnumerable di StreamingChartResponse contenente chunk di testo, grafici e predizioni</returns>
    public async IAsyncEnumerable<StreamingChartResponse> GenerateResponseWithChartsStreamingAsync( string userMessage, Guid userId, Guid conversationId,[EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var user = await _uow.Repository<User>().Query(x=>x.Id == userId).Include(x=>x.Tenant).FirstOrDefaultAsync();
        if (user == null)
        {
            yield return new StreamingChartResponse
            {
                Type = StreamingResponseType.Error,
                ErrorMessage = "Utente non trovato"
            };
            yield break;
        }
        // Carica la history della conversazione dal DB (stessa logica del path non-streaming)
        var conversationHistory = await _uow.Repository<Conversation>()
            .Query(x => x.Id == conversationId)
            .Include(x => x.Messages)
            .Select(x => x.Messages
                .OrderBy(m => m.LastModifiedDate)
                .Select(m => new ChatMessage
                {
                    Role    = m.IsAnswer ? "assistant" : "user",
                    Content = m.Content
                })
                .ToList()
            )
            .FirstOrDefaultAsync(cancellationToken);

        _logger.LogInformation("Starting streaming response with charts for tenant {Tenant}: {Message}",
            user.Tenant.Code, userMessage.Substring(0, Math.Min(50, userMessage.Length)));

        // Verifica che il servizio di streaming sia disponibile
        if (_streamingService == null)
        {
           
            // Fallback: usa il metodo non-streaming e restituisci tutto insieme
            var fallbackResult = await GenerateResponseWithChartsAsync(userMessage,userId, conversationId, cancellationToken);
            
            yield return new StreamingChartResponse
            {
                Type = StreamingResponseType.TextChunk,
                TextContent = fallbackResult.Content
            };

            foreach (var chart in fallbackResult.Charts)
            {
                yield return new StreamingChartResponse
                {
                    Type = StreamingResponseType.ChartData,
                    Chart = chart
                };
            }

            if (fallbackResult.Prediction != null)
            {
                yield return new StreamingChartResponse
                {
                    Type = StreamingResponseType.PredictionData,
                    Prediction = fallbackResult.Prediction
                };
            }

            yield return new StreamingChartResponse { Type = StreamingResponseType.Complete };
            yield break;
        }

        var charts = new List<ChartData>();
        PredictionData? prediction = null;

        // try
        // {
            var azureClient = new AzureOpenAIClient(
                new Uri(_config.Endpoint!),
                new AzureKeyCredential(_config.ApiKey!)
            );

            var chatClient = azureClient.GetChatClient(_config.DeploymentName);

            // Costruisci messaggi con system prompt specializzato
            var messages = BuildMessagesWithAnalyticsContext(userMessage, conversationHistory, user.Tenant.Code);

            // Configura opzioni con tools
            var options = new ChatCompletionOptions
            {
                Temperature = 0.7f,
                MaxOutputTokenCount = 2000
            };
            options.Tools.Add(GetProductionDataTool);
            options.Tools.Add(GetPredictionTool);

            bool continueLoop = true;
            bool isFirstResponse = true;

            while (continueLoop)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    yield return new StreamingChartResponse
                    {
                        Type = StreamingResponseType.Error,
                        ErrorMessage = "Operazione annullata"
                    };
                    yield break;
                }

                // Usa streaming per ottenere la risposta token per token
                var completionUpdates = chatClient.CompleteChatStreamingAsync(messages, options, cancellationToken);

                var accumulatedToolCalls = new Dictionary<int, (string Id, string Name, System.Text.StringBuilder Args)>();
                var responseBuilder = new System.Text.StringBuilder();
                ChatFinishReason? finishReason = null;

                await foreach (var update in completionUpdates)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    // Processa i chunk di testo
                    if (update.ContentUpdate.Count > 0)
                    {
                        var text = update.ContentUpdate[0].Text;
                        if (!string.IsNullOrEmpty(text))
                        {
                            responseBuilder.Append(text);
                            yield return new StreamingChartResponse
                            {
                                Type = StreamingResponseType.TextChunk,
                                TextContent = text
                            };
                        }
                    }

                    // Accumula le tool calls (arrivano in chunk)
                    if (update.ToolCallUpdates != null)
                    {
                        foreach (var toolCallUpdate in update.ToolCallUpdates)
                        {
                            if (!accumulatedToolCalls.ContainsKey(toolCallUpdate.Index))
                            {
                                accumulatedToolCalls[toolCallUpdate.Index] = (
                                    toolCallUpdate.ToolCallId ?? "",
                                    toolCallUpdate.FunctionName ?? "",
                                    new System.Text.StringBuilder()
                                );
                            }

                            if (toolCallUpdate.FunctionArgumentsUpdate != null)
                            {
                                accumulatedToolCalls[toolCallUpdate.Index].Args.Append(toolCallUpdate.FunctionArgumentsUpdate);
                            }
                        }
                    }

                    // Controlla il finish reason
                    if (update.FinishReason.HasValue)
                    {
                        finishReason = update.FinishReason;
                    }
                }

                // Se la risposta è terminata con tool calls, processale
                if (finishReason == ChatFinishReason.ToolCalls && accumulatedToolCalls.Count > 0)
                {
                    // Notifica che stiamo processando i dati
                    yield return new StreamingChartResponse
                    {
                        Type = StreamingResponseType.ProcessingTools,
                        TextContent = "Elaborazione dati in corso..."
                    };

                    // Costruisci il messaggio dell'assistente con le tool calls
                    var toolCallsList = accumulatedToolCalls.Values
                        .Where(tc => !string.IsNullOrEmpty(tc.Id) && !string.IsNullOrEmpty(tc.Name))
                        .Select(tc => ChatToolCall.CreateFunctionToolCall(tc.Id, tc.Name, BinaryData.FromString(tc.Args.ToString())))
                        .ToList();

                    if (toolCallsList.Count > 0)
                    {
                        // Nota: AssistantChatMessage con tool calls non supporta contenuto testuale aggiuntivo
                        // Il contenuto testuale eventualmente generato prima delle tool calls viene ignorato
                        // perché non è rilevante per il flusso di elaborazione delle tool calls
                        var assistantMessage = new AssistantChatMessage(toolCallsList);
                        messages.Add(assistantMessage);

                        // Processa ogni tool call
                        foreach (var tc in accumulatedToolCalls.Values)
                        {
                            if (string.IsNullOrEmpty(tc.Id) || string.IsNullOrEmpty(tc.Name))
                                continue;

                            _logger.LogInformation("Streaming tool call: {Tool} with args: {Args}",
                                tc.Name, tc.Args.ToString());

                            string toolResult;

                            if (tc.Name == "get_production_data")
                            {
                                var args = JsonSerializer.Deserialize<GetProductionDataArgs>(tc.Args.ToString());
                                var chartData = await GetProductionChartDataAsync(userId, args!, cancellationToken);
                                charts.AddRange(chartData);

                                // Restituisci i grafici man mano che vengono generati
                                foreach (var chart in chartData)
                                {
                                    yield return new StreamingChartResponse
                                    {
                                        Type = StreamingResponseType.ChartData,
                                        Chart = chart
                                    };
                                }

                                toolResult = JsonSerializer.Serialize(new { success = true, charts_generated = chartData.Count });
                            }
                            else if (tc.Name == "get_prediction")
                            {
                                var args = JsonSerializer.Deserialize<GetPredictionArgs>(tc.Args.ToString());

                                // Usa il modello ML reale. GetPredictionAsync (solo media DB)
                                // rimane disponibile per il confronto in GetComparisonPredictionAsync.
                                prediction = await GetPredictionFromMLModelAsync(userId, args!, cancellationToken);

                                // Restituisci la predizione
                                yield return new StreamingChartResponse
                                {
                                    Type = StreamingResponseType.PredictionData,
                                    Prediction = prediction
                                };

                                // Serializza solo i campi rilevanti per il contesto del LLM
                                // (evita di mandare tutto il dizionario Features all'AI)
                                toolResult = JsonSerializer.Serialize(new
                                {
                                    predicted_value = prediction.PredictedValue,
                                    confidence      = prediction.Confidence,
                                    r_squared       = prediction.ModelRSquared,
                                    rmse            = prediction.ModelRMSE,
                                    explanation     = prediction.Explanation
                                });
                            }
                            else
                            {
                                toolResult = JsonSerializer.Serialize(new { error = "Unknown tool" });
                            }

                            messages.Add(new ToolChatMessage(tc.Id, toolResult));
                        }

                        // Continua il loop per ottenere la risposta finale dell'AI
                        isFirstResponse = false;
                    }
                    else
                    {
                        continueLoop = false;
                    }
                }
                else
                {
                    // Risposta completata senza tool calls
                    continueLoop = false;
                }
            }

            // Segnala il completamento
            yield return new StreamingChartResponse
            {
                Type = StreamingResponseType.Complete,
                TotalCharts = charts.Count,
                HasPrediction = prediction != null
            };

            _logger.LogInformation("Streaming response completed with {ChartCount} charts", charts.Count);
        // }
        // catch (Exception ex)
        // {
        //     _logger.LogError(ex, "Error in streaming response with charts");
        //     yield return new StreamingChartResponse
        //     {
        //         Type = StreamingResponseType.Error,
        //         ErrorMessage = $"Si è verificato un errore: {ex.Message}"
        //     };
        // }
    }

    /// <summary>
    /// Costruisce i messaggi con contesto analytics.
    /// Il SystemChatMessage inietta: data/ora UTC corrente, tenant attivo, lingua forzata a italiano.
    /// La history è limitata agli ultimi 10 messaggi per contenere il numero di token.
    /// Ruoli sconosciuti nella history vengono ignorati (evita SystemChatMessage a metà lista).
    /// </summary>
    private List<OpenAI.Chat.ChatMessage> BuildMessagesWithAnalyticsContext(string userMessage,List<ChatMessage>? history,string? tenantCode = null)
    {
        var now = DateTime.UtcNow;

        var messages = new List<OpenAI.Chat.ChatMessage>
        {
            new SystemChatMessage($"""
                Sei un assistente AI specializzato nell'analisi dei dati di produzione industriale.
                Rispondi sempre in italiano, indipendentemente dalla lingua dell'utente.
                Data e ora corrente (UTC): {now:yyyy-MM-dd HH:mm}
                {(tenantCode != null ? $"Tenant attivo: {tenantCode}" : "")}

                CAPACITÀ:
                - Puoi generare grafici sui dati di produzione (quantità, energia, temperatura, scarti, tempi ciclo)
                - Puoi fare predizioni sulla produzione futura usando modelli ML
                - Puoi analizzare trend e anomalie

                COMPORTAMENTO:
                - Quando l'utente chiede grafici, trend, statistiche o analisi, usa la funzione get_production_data
                - Quando l'utente chiede previsioni o predizioni, usa la funzione get_prediction
                - Spiega sempre i dati in modo chiaro e professionale
                - Se generi grafici, descrivi brevemente cosa mostrano
                - Usa il formato markdown per formattare le risposte
                - Se i dati non sono disponibili, spiegalo chiaramente senza inventare valori

                METRICHE DISPONIBILI:
                - Quantità prodotta (quantity)
                - Consumo energetico in kWh (energy)
                - Temperatura in °C (temperature)
                - Quantità scarti (scrap)
                - Tempo ciclo in minuti (cycle_time)
                - Efficienza produttiva (efficiency)
                """)
        };

        // Aggiungi history (max 10 messaggi per contenere i token)
        if (history != null)
        {
            foreach (var h in history.TakeLast(10))
            {
                // Ruoli sconosciuti vengono ignorati: inserire un SystemChatMessage
                // a metà lista causa errori con alcune versioni dell'API Azure OpenAI.
                OpenAI.Chat.ChatMessage? msg = h.Role.ToLower() switch
                {
                    "user"      => new UserChatMessage(h.Content),
                    "assistant" => new AssistantChatMessage(h.Content),
                    _           => null
                };
                if (msg != null) messages.Add(msg);
            }
        }

        messages.Add(new UserChatMessage(userMessage));
        return messages;
    }

    /// <summary>
    /// Recupera i dati di produzione e genera grafici
    /// </summary>
    private async Task<List<ChartData>> GetProductionChartDataAsync(
        Guid userId,
        GetProductionDataArgs args,
        CancellationToken cancellationToken)
    {
        var charts = new List<ChartData>();

        try
        {
            var user = await _uow.Repository<User>().Query(x=>x.Id == userId).Include(x=>x.Tenant).FirstOrDefaultAsync();
            if (user == null)
            {
                _logger.LogWarning("User not found for ID {UserId}", userId);
                return charts;
            }
            var repo = _uow.Repository<ProductionData>();

            // Determina il range temporale
            var (startDate, endDate) = GetDateRange(args.Period);

            // Query dati
            var data = repo.Query(x =>
                x.Machine.ProductionLine.Department.TenantCode == user.Tenant.Code &&
                x.Timestamp >= startDate &&
                x.Timestamp <= endDate)
                .OrderBy(x => x.Timestamp)
                .Include(x => x.Machine)
                .ToList();

            if (!data.Any())
            {
                _logger.LogWarning("No production data found for tenant {Tenant} in period {Period}",
                    user.Tenant.Code, args.Period);
                return charts;
            }

            // Raggruppa i dati
            var groupedData = GroupData(data, args.GroupBy ?? "day");

            // Genera grafici in base alla metrica richiesta
            var chartType = args.ChartType ?? "line";

            if (args.Metric == "all")
            {
                charts.Add(CreateChart("Quantità Prodotta",       groupedData, d => (double)d.Sum(x => x.GetMetric("quantity_produced")),        chartType, ChartColors.Blue,   ChartColors.BlueFill));
                charts.Add(CreateChart("Consumo Energetico (kWh)", groupedData, d => (double)d.Average(x => x.GetMetric("energy_consumption")),   chartType, ChartColors.Green,  ChartColors.GreenFill));
                charts.Add(CreateChart("Temperatura Media (°C)",  groupedData, d => (double)d.Average(x => x.GetMetric("temperature")),          chartType, ChartColors.Red,    ChartColors.RedFill));
                charts.Add(CreateChart("Scarti",                  groupedData, d => (double)d.Sum(x => x.GetMetric("scrap_quantity")),            "bar",     ChartColors.Orange, ChartColors.OrangeFill));
            }
            else
            {
                var chart = args.Metric switch
                {
                    "quantity"    => CreateChart("Quantità Prodotta",        groupedData, d => (double)d.Sum(x => x.GetMetric("quantity_produced")),       chartType, ChartColors.Blue,   ChartColors.BlueFill),
                    "energy"      => CreateChart("Consumo Energetico (kWh)", groupedData, d => (double)d.Average(x => x.GetMetric("energy_consumption")),  chartType, ChartColors.Green,  ChartColors.GreenFill),
                    "temperature" => CreateChart("Temperatura Media (°C)",   groupedData, d => (double)d.Average(x => x.GetMetric("temperature")),         chartType, ChartColors.Red,    ChartColors.RedFill),
                    "scrap"       => CreateChart("Scarti",                   groupedData, d => (double)d.Sum(x => x.GetMetric("scrap_quantity")),           chartType, ChartColors.Orange, ChartColors.OrangeFill),
                    "cycle_time"  => CreateChart("Tempo Ciclo Medio (min)",  groupedData, d => (double)d.Average(x => x.GetMetric("cycle_time")),          chartType, ChartColors.Purple, ChartColors.PurpleFill),
                    "efficiency"  => CreateChart("Efficienza (%)",           groupedData, d => CalculateEfficiency(d),                                     chartType, ChartColors.Green,  ChartColors.GreenFill),
                    _             => CreateChart("Quantità Prodotta",        groupedData, d => (double)d.Sum(x => x.GetMetric("quantity_produced")),       chartType, ChartColors.Blue,   ChartColors.BlueFill)
                };
                charts.Add(chart);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting production chart data");
        }

        return charts;
    }

    /// <summary>
    /// Predizione basata su media mobile del database (nessun modello ML).
    /// NON chiamare direttamente dal flusso NLP/tool call: usare GetPredictionFromMLModelAsync.
    /// Questo metodo è usato solo da GetComparisonPredictionAsync per il confronto DB vs ML.
    /// </summary>
    private async Task<PredictionData> GetPredictionAsync(
        Guid userId,
        GetPredictionArgs args,
        CancellationToken cancellationToken)
    {
        var prediction = new PredictionData();

        try
        {
            var user = await _uow.Repository<User>().Query(x=>x.Id == userId).Include(x=>x.Tenant).FirstOrDefaultAsync();
            if (user == null)
            {
                _logger.LogWarning("User not found for ID {UserId}", userId);
                prediction.Explanation = "Utente non trovato per generare la predizione.";
                return prediction;
            }
            var repo = _uow.Repository<ProductionData>();

            // Recupera gli ultimi dati per calcolare le features
            var recentData = repo.Query(x =>
                x.Machine.ProductionLine.Department.TenantCode == user.Tenant.Code)
                .OrderByDescending(x => x.Timestamp)
                .Take(10)
                .ToList();

            if (recentData.Any())
            {
                var avgQuantity  = (double)recentData.Average(x => x.GetMetric("quantity_produced"));
                var avgEnergy    = (double)recentData.Average(x => x.GetMetric("energy_consumption"));
                var avgTemp      = (double)recentData.Average(x => x.GetMetric("temperature"));
                var avgScrap     = (double)recentData.Average(x => x.GetMetric("scrap_quantity"));
                var avgCycleTime = (double)recentData.Average(x => x.GetMetric("cycle_time"));

                // Predizione semplice basata su media mobile (in produzione userebbe il modello ML)
                prediction.PredictedValue = args.Target switch
                {
                    "quantity" => avgQuantity * 1.02, // Leggero incremento stimato
                    "scrap" => avgScrap,
                    "energy" => avgEnergy,
                    _ => avgQuantity
                };

                prediction.Confidence = 0.85; // Placeholder
                prediction.Features = new Dictionary<string, double>
                {
                    ["avg_quantity_recent"] = avgQuantity,
                    ["avg_energy_recent"] = avgEnergy,
                    ["avg_temperature_recent"] = avgTemp,
                    ["avg_scrap_recent"] = avgScrap,
                    ["avg_cycle_time_recent"] = avgCycleTime
                };

                prediction.Explanation = args.Target switch
                {
                    "quantity" => $"Basandomi sui dati recenti (media {avgQuantity:F0} unità), prevedo una produzione di circa {prediction.PredictedValue:F0} unità.",
                    "scrap" => $"Il tasso di scarto medio recente è {avgScrap:F0} unità. Prevedo un valore simile.",
                    "energy" => $"Il consumo energetico medio è {avgEnergy:F1} kWh. Prevedo un consumo simile.",
                    _ => "Predizione basata su media mobile dei dati recenti."
                };
            }
            else
            {
                prediction.Explanation = "Dati insufficienti per generare una predizione accurata.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating prediction");
            prediction.Explanation = $"Errore nella generazione della predizione: {ex.Message}";
        }

        return prediction;
    }

    /// <summary>
    /// Genera una predizione utilizzando il modello ML addestrato.
    /// Usa il TrainingService per interrogare il modello FastTree/Sdca.
    /// </summary>
    /// <param name="tenantCode">Codice del tenant</param>
    /// <param name="args">Argomenti della predizione (target, horizon)</param>
    /// <param name="cancellationToken">Token di cancellazione</param>
    /// <returns>PredictionData con i risultati del modello ML</returns>
    public async Task<PredictionData> GetPredictionFromMLModelAsync( Guid userId, GetPredictionArgs args, CancellationToken cancellationToken = default)
    {
        var prediction = new PredictionData();
        var user = await _uow.Repository<User>().Query(x=>x.Id == userId).Include(x=>x.Tenant).FirstOrDefaultAsync();
        if (user == null)
        {
            _logger.LogWarning("User not found for ID {UserId}", userId);
            throw new ArgumentException("Utente non trovato per eseguire il confronto delle predizioni.");
        }
        try
        {
            if (_trainingService == null)
            {
                _logger.LogWarning("TrainingService non disponibile per tenant {Tenant}", user.Tenant.Code);
                prediction.Explanation = "Servizio ML non configurato. Utilizzare GetPredictionAsync per predizioni basate su database.";
                return prediction;
            }

            var repo = _uow.Repository<ProductionData>();

            // Recupera gli ultimi dati per costruire le features
            var recentData = repo.Query(x =>
                x.Machine.ProductionLine.Department.TenantCode == user.Tenant.Code)
                .OrderByDescending(x => x.Timestamp)
                .Take(10)
                .ToList();

            if (!recentData.Any())
            {
                prediction.Explanation = "Dati insufficienti per generare una predizione ML.";
                return prediction;
            }

            var latest = recentData.First();
            var last3  = recentData.Take(3).ToList();

            var latestQty    = latest.GetMetric("quantity_produced");
            var latestCycle  = latest.GetMetric("cycle_time");
            var latestEnergy = latest.GetMetric("energy_consumption");
            var latestTemp   = latest.GetMetric("temperature");
            var latestScrap  = latest.GetMetric("scrap_quantity");

            var referenceTime = args.Horizon switch
            {
                "next_shift" => DateTime.UtcNow.AddHours(8),
                "tomorrow"   => DateTime.UtcNow.AddDays(1),
                "next_week"  => DateTime.UtcNow.AddDays(7),
                _            => DateTime.UtcNow
            };

            var features = new ProductionDataMLEnriched
            {
                CycleTime         = (float)latestCycle,
                EnergyConsumption = (float)latestEnergy,
                Temperature       = (float)latestTemp,
                ScrapQuantity     = (float)latestScrap,

                ScrapRate               = latestQty > 0 ? (float)latestScrap  / (float)latestQty  : 0f,
                EffectiveProductionRate = latestCycle > 0 ? (float)latestQty  / (float)latestCycle : 0f,
                EnergyPerUnit           = latestQty > 0 ? (float)latestEnergy / (float)latestQty  : 0f,

                HourOfDay = referenceTime.Hour,
                DayOfWeek = (int)referenceTime.DayOfWeek,
                IsWeekend = (referenceTime.DayOfWeek == System.DayOfWeek.Saturday ||
                             referenceTime.DayOfWeek == System.DayOfWeek.Sunday) ? 1 : 0,
                Shift = referenceTime.Hour switch
                {
                    >= 6  and < 14 => 1,
                    >= 14 and < 22 => 2,
                    _              => 3
                },

                AvgQuantityLast3    = (float)last3.Average(x => (double)x.GetMetric("quantity_produced")),
                AvgCycleTimeLast3   = (float)last3.Average(x => (double)x.GetMetric("cycle_time")),
                AvgTemperatureLast3 = (float)last3.Average(x => (double)x.GetMetric("temperature")),
                AvgEnergyLast3      = (float)last3.Average(x => (double)x.GetMetric("energy_consumption"))
            };

            // ── CHIAMATA AL MODELLO ML ────────────────────────────────────────
            // Il modello è addestrato solo su QuantityProduced come label.
            // Per target "scrap" ed "energy" il modello non ha un output diretto:
            // in quel caso restituiamo la media DB con una nota esplicita.
            if (args.Target != "quantity")
            {
                _logger.LogInformation(
                    "Target '{Target}' non supportato dal modello ML — fallback su media DB", args.Target);

                var avgScrap  = (double)recentData.Average(x => x.GetMetric("scrap_quantity"));
                var avgEnergy = (double)recentData.Average(x => x.GetMetric("energy_consumption"));

                prediction.PredictedValue = args.Target switch
                {
                    "scrap"  => avgScrap,
                    "energy" => avgEnergy,
                    _        => avgScrap
                };
                prediction.Confidence = 0.6;
                prediction.Explanation = args.Target switch
                {
                    "scrap"  => $"Il modello ML è addestrato sulla quantità prodotta. Per gli scarti uso la media degli ultimi {recentData.Count} cicli: {prediction.PredictedValue:F0} unità.",
                    "energy" => $"Il modello ML è addestrato sulla quantità prodotta. Per l'energia uso la media degli ultimi {recentData.Count} cicli: {prediction.PredictedValue:F1} kWh.",
                    _        => $"Predizione basata su media recente: {prediction.PredictedValue:F2}"
                };
                return prediction;
            }

            var mlPrediction = _trainingService.Predict(features, user.Tenant.Code);

            if (mlPrediction == null)
            {
                _logger.LogWarning("Modello ML non trovato su Blob Storage per tenant {Tenant} — training non ancora eseguito", user.Tenant.Code);

                // Fallback: usa la media mobile DB e avvisa l'utente
                var fallback = await GetPredictionAsync(userId, args, cancellationToken);
                fallback.Explanation =
                    $"Il modello ML non è ancora stato addestrato per questo tenant. " +
                    $"Come stima provvisoria uso la media degli ultimi cicli: {fallback.PredictedValue:F0} unità. " +
                    $"Per abilitare le previsioni ML eseguire TrainingService.TrainIncrementalAsync().";
                return fallback;
            }

            // ── POPOLA RISULTATO ──────────────────────────────────────────────
            prediction.PredictedValue = mlPrediction.Score;

            var checkpoint = await _trainingService.GetCheckpointAsync(user.Tenant.Code);
            if (checkpoint != null)
            {
                prediction.ModelRSquared = checkpoint.RSquared;
                prediction.ModelRMSE     = checkpoint.RMSE;
                prediction.Confidence    = Math.Max(0, Math.Min(1, checkpoint.RSquared));
            }

            prediction.Features = new Dictionary<string, double>
            {
                ["cycle_time"]               = features.CycleTime,
                ["energy_consumption"]        = features.EnergyConsumption,
                ["temperature"]              = features.Temperature,
                ["scrap_quantity"]           = features.ScrapQuantity,
                ["scrap_rate"]               = features.ScrapRate,
                ["effective_production_rate"] = features.EffectiveProductionRate,
                ["energy_per_unit"]          = features.EnergyPerUnit,
                ["hour_of_day"]              = features.HourOfDay,
                ["day_of_week"]              = features.DayOfWeek,
                ["shift"]                    = features.Shift,
                ["avg_quantity_last3"]       = features.AvgQuantityLast3,
                ["avg_cycle_time_last3"]     = features.AvgCycleTimeLast3,
                ["avg_temperature_last3"]    = features.AvgTemperatureLast3,
                ["avg_energy_last3"]         = features.AvgEnergyLast3
            };

            var horizonLabel = args.Horizon switch
            {
                "next_shift" => "turno successivo",
                "tomorrow"   => "domani",
                "next_week"  => "settimana prossima",
                _            => "prossimo ciclo"
            };

            prediction.Explanation =
                $"Predizione ML (FastTree) per {horizonLabel}: {prediction.PredictedValue:F0} unità. " +
                $"R²={prediction.ModelRSquared:F3}, RMSE=±{prediction.ModelRMSE:F1} pezzi, " +
                $"Confidenza={prediction.Confidence:P0}";

            _logger.LogInformation(
                "Predizione ML tenant {Tenant} — target={Target} horizon={Horizon} " +
                "referenceTime={RefTime:HH:mm} shift={Shift} → {Value:F0} pezzi (R²={R2:F3})",
                user.Tenant.Code, args.Target, args.Horizon,
                referenceTime, features.Shift,
                prediction.PredictedValue, prediction.ModelRSquared);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore nella predizione ML per tenant {Tenant}", user.Tenant.Code);
            prediction.Explanation = $"Errore nella predizione ML: {ex.Message}";
        }

        return prediction;
    }

    /// <summary>
    /// Esegue entrambe le predizioni (database e modello ML) per confronto.
    /// Utile per validare il modello ML contro l'approccio basato su medie.
    /// </summary>
    /// <param name="tenantCode">Codice del tenant</param>
    /// <param name="args">Argomenti della predizione</param>
    /// <param name="cancellationToken">Token di cancellazione</param>
    /// <returns>Risultato di confronto con entrambe le predizioni</returns>
    public async Task<PredictionComparisonResult> GetComparisonPredictionAsync(Guid userId, GetPredictionArgs args,CancellationToken cancellationToken = default)
    {
        _logger.LogInformation( "Esecuzione confronto predizioni per tenant {UserId}, target: {Target}",userId, args.Target);

       

        // Esegui entrambe le predizioni in parallelo
        var dbPredictionTask = GetPredictionAsync(userId, args, cancellationToken);
        var mlPredictionTask = GetPredictionFromMLModelAsync(userId, args, cancellationToken);

        await Task.WhenAll(dbPredictionTask, mlPredictionTask);

        var dbPrediction = await dbPredictionTask;
        var mlPrediction = await mlPredictionTask;

        // Calcola differenza percentuale
        double? percentageDifference = null;
        if (dbPrediction.PredictedValue > 0 && mlPrediction.PredictedValue > 0)
        {
            percentageDifference = Math.Abs(
                (mlPrediction.PredictedValue - dbPrediction.PredictedValue) / dbPrediction.PredictedValue * 100);
        }

        var result = new PredictionComparisonResult
        {
            DatabasePrediction = dbPrediction,
            MLModelPrediction = mlPrediction,
            PercentageDifference = percentageDifference,
            RecommendedPrediction = DetermineRecommendedPrediction(dbPrediction, mlPrediction),
            ComparisonSummary = GenerateComparisonSummary(dbPrediction, mlPrediction, percentageDifference)
        };

        _logger.LogInformation(
            "Confronto completato - DB: {DbValue:F0}, ML: {MlValue:F0}, Diff: {Diff:F1}%",
            dbPrediction.PredictedValue, mlPrediction.PredictedValue, percentageDifference ?? 0);

        return result;
    }

    /// <summary>
    /// Determina quale predizione è più affidabile
    /// </summary>
    private string DetermineRecommendedPrediction(PredictionData dbPrediction, PredictionData mlPrediction)
    {
        // Se il modello ML ha un buon R², preferiscilo
        if (mlPrediction.ModelRSquared.HasValue && mlPrediction.ModelRSquared.Value > 0.7)
        {
            return "ML";
        }

        // Se il modello ML non è disponibile o ha metriche scarse, usa il database
        if (!mlPrediction.ModelRSquared.HasValue || mlPrediction.ModelRSquared.Value < 0.5)
        {
            return "Database";
        }

        // Caso intermedio: suggerisci di usare entrambi
        return "Entrambi (verifica manuale consigliata)";
    }

    /// <summary>
    /// Genera un sommario del confronto tra le due predizioni
    /// </summary>
    private string GenerateComparisonSummary(
        PredictionData dbPrediction, 
        PredictionData mlPrediction, 
        double? percentageDifference)
    {
        var summary = new System.Text.StringBuilder();
        summary.AppendLine("## Confronto Predizioni");
        summary.AppendLine();
        summary.AppendLine($"**Predizione Database (Media Mobile):** {dbPrediction.PredictedValue:F0}");
        summary.AppendLine($"- Metodo: Media mobile ultimi 10 record con trend +2%");
        summary.AppendLine($"- Confidenza stimata: {dbPrediction.Confidence:P0}");
        summary.AppendLine();
        summary.AppendLine($"**Predizione Modello ML:** {mlPrediction.PredictedValue:F0}");
        
        if (mlPrediction.ModelRSquared.HasValue)
        {
            summary.AppendLine($"- R² del modello: {mlPrediction.ModelRSquared:F3}");
            summary.AppendLine($"- RMSE: {mlPrediction.ModelRMSE:F2}");
        }
        else
        {
            summary.AppendLine("- Metriche modello non disponibili");
        }
        summary.AppendLine();
        
        if (percentageDifference.HasValue)
        {
            summary.AppendLine($"**Differenza:** {percentageDifference:F1}%");
            
            if (percentageDifference < 5)
                summary.AppendLine("Le predizioni sono molto simili ✓");
            else if (percentageDifference < 15)
                summary.AppendLine("Differenza moderata - considerare entrambi i valori");
            else
                summary.AppendLine("⚠️ Differenza significativa - verificare i dati di input");
        }

        return summary.ToString();
    }

    /// <summary>
    /// Restituisce la predizione migliore in base alla qualità del modello ML.
    /// Implementa una strategia ibrida che seleziona automaticamente la fonte più affidabile.
    /// </summary>
    /// <param name="tenantCode">Codice del tenant</param>
    /// <param name="args">Argomenti della predizione</param>
    /// <param name="config">Configurazione opzionale per il fine tuning delle soglie</param>
    /// <param name="cancellationToken">Token di cancellazione</param>
    /// <returns>SmartPredictionResult con la predizione ottimale e metadata</returns>
    public async Task<SmartPredictionResult> GetSmartPredictionAsync(
        Guid userId,
        GetPredictionArgs args,
        SmartPredictionConfig? config = null,
        CancellationToken cancellationToken = default)
    {
        config ??= SmartPredictionConfig.Default;
        var user = await _uow.Repository<User>().Query(x=>x.Id == userId).Include(x=>x.Tenant).FirstOrDefaultAsync();
        if (user == null)
        {
            _logger.LogWarning("User not found for ID {UserId}", userId);
            throw new ArgumentException("Utente non trovato per eseguire la smart prediction.");
        }
        var tenantCode = user.Tenant.Code;
        _logger.LogInformation(
            "Smart prediction per tenant {Tenant}, target: {Target}, config: R²High={R2High}, R²Low={R2Low}",
            tenantCode, args.Target, config.R2ThresholdHigh, config.R2ThresholdLow);

        var result = new SmartPredictionResult
        {
            RequestedTarget = args.Target,
            Timestamp = DateTime.UtcNow,
            ConfigUsed = config
        };

        try
        {
            // Ottieni il confronto completo
            var comparison = await GetComparisonPredictionAsync(userId, args, cancellationToken);
            result.Comparison = comparison;

            var r2 = comparison.MLModelPrediction.ModelRSquared;
            var dbValue = comparison.DatabasePrediction.PredictedValue;
            var mlValue = comparison.MLModelPrediction.PredictedValue;

            // Logica di selezione basata su R²
            if (r2.HasValue && r2.Value >= config.R2ThresholdHigh)
            {
                // ML ha un ottimo R² -> usa ML
                result.SelectedSource = PredictionSource.MLModel;
                result.Prediction = comparison.MLModelPrediction;
                result.SelectionReason = $"Modello ML selezionato: R²={r2:F3} >= soglia alta ({config.R2ThresholdHigh:F2})";
                
                _logger.LogInformation("Smart prediction: ML selezionato (R²={R2:F3})", r2);
            }
            else if (r2.HasValue && r2.Value >= config.R2ThresholdLow)
            {
                // R² nella zona intermedia -> usa media pesata
                result.SelectedSource = PredictionSource.Hybrid;
                
                var weightedValue = (mlValue * config.MLWeightInHybrid) + 
                                   (dbValue * (1 - config.MLWeightInHybrid));
                
                // Calcola confidenza ibrida (ridotta per l'incertezza)
                var hybridConfidence = r2.Value * config.HybridConfidenceMultiplier;
                
                result.Prediction = new PredictionData
                {
                    PredictedValue = weightedValue,
                    Confidence = hybridConfidence,
                    ModelRSquared = r2,
                    ModelRMSE = comparison.MLModelPrediction.ModelRMSE,
                    Features = comparison.MLModelPrediction.Features,
                    Explanation = $"Predizione ibrida ({config.MLWeightInHybrid:P0} ML + {1 - config.MLWeightInHybrid:P0} DB): " +
                                 $"{weightedValue:F0} unità. R²={r2:F3}, Confidenza={hybridConfidence:P0}"
                };
                
                result.SelectionReason = $"Approccio ibrido: R²={r2:F3} nella zona intermedia " +
                                        $"({config.R2ThresholdLow:F2} <= R² < {config.R2ThresholdHigh:F2})";
                
                _logger.LogInformation(
                    "Smart prediction: Hybrid selezionato (R²={R2:F3}, weighted={Value:F0})", 
                    r2, weightedValue);
            }
            else
            {
                // R² basso o non disponibile -> usa Database
                result.SelectedSource = PredictionSource.Database;
                result.Prediction = comparison.DatabasePrediction;
                result.SelectionReason = r2.HasValue 
                    ? $"Database selezionato: R²={r2:F3} < soglia bassa ({config.R2ThresholdLow:F2})"
                    : "Database selezionato: modello ML non disponibile o senza metriche";
                
                _logger.LogInformation("Smart prediction: Database selezionato (R²={R2})", r2);
            }

            // Aggiungi metriche di qualità
            result.QualityMetrics = new PredictionQualityMetrics
            {
                R2Score = r2,
                RMSE = comparison.MLModelPrediction.ModelRMSE,
                DifferenceFromBaseline = comparison.PercentageDifference,
                DataPointsUsed = comparison.MLModelPrediction.Features?.Count ?? 0,
                IsMLModelAvailable = comparison.MLModelPrediction.PredictedValue > 0 && 
                                    !comparison.MLModelPrediction.Explanation?.Contains("non disponibile") == true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore in smart prediction per tenant {Tenant}", tenantCode);
            
            // Fallback a database in caso di errore
            result.SelectedSource = PredictionSource.Database;
            result.SelectionReason = $"Fallback a database per errore: {ex.Message}";
            
            try
            {
                result.Prediction = await GetPredictionAsync(userId, args, cancellationToken);
            }
            catch
            {
                result.Prediction = new PredictionData
                {
                    Explanation = $"Errore nella predizione: {ex.Message}"
                };
            }
        }

        return result;
    }

    /// <summary>
    /// Esegue un backtesting della strategia smart prediction sui dati storici.
    /// Utile per il fine tuning dei parametri.
    /// </summary>
    /// <param name="tenantCode">Codice del tenant</param>
    /// <param name="daysToTest">Numero di giorni da testare</param>
    /// <param name="config">Configurazione da testare</param>
    /// <param name="cancellationToken">Token di cancellazione</param>
    /// <returns>Risultati del backtesting</returns>
    public async Task<BacktestResult> RunBacktestAsync(
        string tenantCode,
        int daysToTest = 30,
        SmartPredictionConfig? config = null,
        CancellationToken cancellationToken = default)
    {
        config ??= SmartPredictionConfig.Default;
        
        _logger.LogInformation(
            "Avvio backtest per tenant {Tenant}, giorni: {Days}",
            tenantCode, daysToTest);

        var result = new BacktestResult
        {
            TenantCode = tenantCode,
            TestPeriodDays = daysToTest,
            ConfigTested = config,
            StartTime = DateTime.UtcNow
        };

        try
        {
            var repo = _uow.Repository<ProductionData>();
            var endDate = DateTime.UtcNow.AddDays(-1); // Escludi oggi
            var startDate = endDate.AddDays(-daysToTest);

            // Recupera dati storici
            var historicalData = repo.Query(x =>
                x.Machine.ProductionLine.Department.TenantCode == tenantCode &&
                x.Timestamp >= startDate &&
                x.Timestamp <= endDate)
                .OrderBy(x => x.Timestamp)
                .ToList();

            if (historicalData.Count < 10)
            {
                result.Success = false;
                result.ErrorMessage = $"Dati insufficienti per backtest: {historicalData.Count} record";
                return result;
            }

            // Raggruppa per giorno
            var dailyData = historicalData
                .GroupBy(x => x.Timestamp.Date)
                .OrderBy(g => g.Key)
                .ToList();

            var predictions = new List<BacktestPrediction>();
            var dbErrors = new List<double>();
            var mlErrors = new List<double>();
            var smartErrors = new List<double>();

            // Per ogni giorno, simula la predizione del giorno successivo
            for (int i = 5; i < dailyData.Count - 1; i++) // Inizia da 5 per avere storico
            {
                var currentDay = dailyData[i];
                var nextDay = dailyData[i + 1];
                var actualValue = (double)nextDay.Sum(x => x.GetMetric("quantity_produced"));

                var last5Days    = dailyData.Skip(i - 4).Take(5).SelectMany(g => g).ToList();
                var dbPrediction = (double)last5Days.Average(x => x.GetMetric("quantity_produced")) * 1.02;

                double mlPrediction = 0;
                double? r2 = null;

                if (_trainingService != null)
                {
                    var checkpoint = await _trainingService.GetCheckpointAsync(tenantCode);
                    r2 = checkpoint?.RSquared;

                    mlPrediction = (double)last5Days.Average(x => x.GetMetric("quantity_produced")) *
                                  (1 + (r2 ?? 0) * 0.05);
                }

                // Calcola smart prediction
                double smartPrediction;
                PredictionSource source;
                
                if (r2.HasValue && r2.Value >= config.R2ThresholdHigh)
                {
                    smartPrediction = mlPrediction;
                    source = PredictionSource.MLModel;
                }
                else if (r2.HasValue && r2.Value >= config.R2ThresholdLow)
                {
                    smartPrediction = (mlPrediction * config.MLWeightInHybrid) + 
                                     (dbPrediction * (1 - config.MLWeightInHybrid));
                    source = PredictionSource.Hybrid;
                }
                else
                {
                    smartPrediction = dbPrediction;
                    source = PredictionSource.Database;
                }

                // Calcola errori
                var dbError = Math.Abs(dbPrediction - actualValue) / actualValue * 100;
                var mlError = mlPrediction > 0 ? Math.Abs(mlPrediction - actualValue) / actualValue * 100 : 100;
                var smartError = Math.Abs(smartPrediction - actualValue) / actualValue * 100;

                dbErrors.Add(dbError);
                if (mlPrediction > 0) mlErrors.Add(mlError);
                smartErrors.Add(smartError);

                predictions.Add(new BacktestPrediction
                {
                    Date = nextDay.Key,
                    ActualValue = actualValue,
                    DatabasePrediction = dbPrediction,
                    MLPrediction = mlPrediction,
                    SmartPrediction = smartPrediction,
                    SelectedSource = source,
                    ErrorPercent = smartError
                });
            }

            // Calcola metriche aggregate
            result.Success = true;
            result.TotalPredictions = predictions.Count;
            result.Predictions = predictions;
            
            result.DatabaseMetrics = new BacktestMetrics
            {
                MeanAbsolutePercentageError = dbErrors.Average(),
                MedianError = dbErrors.OrderBy(x => x).ElementAt(dbErrors.Count / 2),
                MaxError = dbErrors.Max(),
                MinError = dbErrors.Min(),
                StandardDeviation = CalculateStdDev(dbErrors)
            };
            
            if (mlErrors.Any())
            {
                result.MLMetrics = new BacktestMetrics
                {
                    MeanAbsolutePercentageError = mlErrors.Average(),
                    MedianError = mlErrors.OrderBy(x => x).ElementAt(mlErrors.Count / 2),
                    MaxError = mlErrors.Max(),
                    MinError = mlErrors.Min(),
                    StandardDeviation = CalculateStdDev(mlErrors)
                };
            }
            
            result.SmartMetrics = new BacktestMetrics
            {
                MeanAbsolutePercentageError = smartErrors.Average(),
                MedianError = smartErrors.OrderBy(x => x).ElementAt(smartErrors.Count / 2),
                MaxError = smartErrors.Max(),
                MinError = smartErrors.Min(),
                StandardDeviation = CalculateStdDev(smartErrors)
            };

            // Conta selezioni per fonte
            result.SelectionBreakdown = predictions
                .GroupBy(p => p.SelectedSource)
                .ToDictionary(g => g.Key, g => g.Count());

            _logger.LogInformation(
                "Backtest completato: MAPE DB={DbMape:F2}%, ML={MlMape:F2}%, Smart={SmartMape:F2}%",
                result.DatabaseMetrics.MeanAbsolutePercentageError,
                result.MLMetrics?.MeanAbsolutePercentageError ?? 0,
                result.SmartMetrics.MeanAbsolutePercentageError);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore nel backtest per tenant {Tenant}", tenantCode);
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }
        finally
        {
            result.EndTime = DateTime.UtcNow;
        }

        return result;
    }

    /// <summary>
    /// Calcola la deviazione standard
    /// </summary>
    private double CalculateStdDev(List<double> values)
    {
        if (values.Count < 2) return 0;
        var avg = values.Average();
        var sumSquares = values.Sum(v => Math.Pow(v - avg, 2));
        return Math.Sqrt(sumSquares / (values.Count - 1));
    }

    #region Helper Methods

    private (DateTime start, DateTime end) GetDateRange(string period)
    {
        var now = DateTime.UtcNow;
        return period switch
        {
            "today" => (now.Date, now),
            "week" => (now.AddDays(-7), now),
            "month" => (now.AddMonths(-1), now),
            "quarter" => (now.AddMonths(-3), now),
            "year" => (now.AddYears(-1), now),
            _ => (now.AddDays(-7), now) // Default: ultima settimana
        };
    }

    private Dictionary<string, List<ProductionData>> GroupData(List<ProductionData> data, string groupBy)
    {
        return groupBy switch
        {
            "hour" => data.GroupBy(x => x.Timestamp.ToString("yyyy-MM-dd HH:00"))
                         .ToDictionary(g => g.Key, g => g.ToList()),
            "day" => data.GroupBy(x => x.Timestamp.ToString("yyyy-MM-dd"))
                        .ToDictionary(g => g.Key, g => g.ToList()),
            "week" => data.GroupBy(x => $"W{GetWeekOfYear(x.Timestamp):D2}")
                         .ToDictionary(g => g.Key, g => g.ToList()),
            "month" => data.GroupBy(x => x.Timestamp.ToString("yyyy-MM"))
                          .ToDictionary(g => g.Key, g => g.ToList()),
            "shift" => data.GroupBy(x => GetShift(x.Timestamp))
                          .ToDictionary(g => g.Key, g => g.ToList()),
            //"machine" => data.GroupBy(x => x.MachineId.ToString())
            //                .ToDictionary(g => g.Key, g => g.ToList()),
             "machine" => data.GroupBy(x => x.Machine.Code.ToString())
                            .ToDictionary(g => g.Key, g => g.ToList()),
            _ => data.GroupBy(x => x.Timestamp.ToString("yyyy-MM-dd"))
                    .ToDictionary(g => g.Key, g => g.ToList())
        };
    }

    private int GetWeekOfYear(DateTime date)
    {
        var cal = System.Globalization.CultureInfo.CurrentCulture.Calendar;
        return cal.GetWeekOfYear(date, System.Globalization.CalendarWeekRule.FirstDay, DayOfWeek.Monday);
    }

    private string GetShift(DateTime timestamp)
    {
        var hour = timestamp.Hour;
        return hour switch
        {
            >= 6 and < 14 => "Turno 1 (6-14)",
            >= 14 and < 22 => "Turno 2 (14-22)",
            _ => "Turno 3 (22-6)"
        };
    }

    private double CalculateEfficiency(IEnumerable<ProductionData> data)
    {
        var total = data.Sum(x => (double)x.GetMetric("quantity_produced"));
        var scrap = data.Sum(x => (double)x.GetMetric("scrap_quantity"));
        if (total == 0) return 0;
        return ((total - scrap) / total) * 100;
    }

    private ChartData CreateChart(
        string title,
        Dictionary<string, List<ProductionData>> groupedData,
        Func<IEnumerable<ProductionData>, double> valueSelector,
        string chartType,
        string borderColor,
        string backgroundColor)
    {
        var sortedKeys = groupedData.Keys.OrderBy(k => k).ToList();

        return new ChartData
        {
            Type = chartType,
            Title = title,
            Labels = sortedKeys,
            Datasets = new List<ChartDataset>
            {
                new ChartDataset
                {
                    Label = title,
                    Data = sortedKeys.Select(k => valueSelector(groupedData[k])).ToList(),
                    BorderColor = borderColor,
                    BackgroundColor = backgroundColor,
                    Fill = chartType == "area",
                    Tension = 0.4
                }
            },
            Options = new ChartOptions
            {
                ShowLegend = true,
                BeginAtZero = true,
                YAxisLabel = title,
                XAxisLabel = "Periodo"
            }
        };
    }

    #endregion
}

#region Args Classes

internal class GetProductionDataArgs
{
    [JsonPropertyName("metric")]
    public string Metric { get; set; } = "quantity";

    [JsonPropertyName("period")]
    public string Period { get; set; } = "week";

    [JsonPropertyName("chart_type")]
    public string? ChartType { get; set; }

    [JsonPropertyName("group_by")]
    public string? GroupBy { get; set; }
}

public class GetPredictionArgs
{
    [JsonPropertyName("target")]
    public string Target { get; set; } = "quantity";

    [JsonPropertyName("horizon")]
    public string? Horizon { get; set; }
}

#endregion
