using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PlatformAI.NLP.Models;
using Azure.AI.OpenAI;
using Azure;
using OpenAI.Chat;
using ChatMessage = PlatformAI.NLP.Models.ChatMessage;

namespace PlatformAI.NLP.Services;

/// <summary>
/// Servizio per streaming LLM - genera risposte token per token come ChatGPT
/// Supporta: Azure OpenAI, OpenAI, Ollama
/// </summary>
public class LLMStreamingService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<LLMStreamingService> _logger;
    private readonly LLMConfig _config;

    /// <summary>
    /// Costruttore per Dependency Injection con IOptions (usato in produzione)
    /// </summary>
    public LLMStreamingService(HttpClient httpClient, ILogger<LLMStreamingService> logger, IOptions<LLMConfig> options)
        : this(httpClient, logger, options.Value)
    {
    }

    /// <summary>
    /// Costruttore diretto con LLMConfig (usato per test e mock)
    /// </summary>
    public LLMStreamingService(HttpClient httpClient, ILogger<LLMStreamingService> logger, LLMConfig config)
    {
        _httpClient = httpClient;
        _logger = logger;
        _config = config;
    }

    /// <summary>
    /// Genera una risposta completa (non streaming) - attende l'intera risposta prima di restituirla
    /// Supporta: Azure OpenAI, OpenAI, Ollama
    /// </summary>
    public virtual async Task<string> GenerateCompleteResponseAsync(string userMessage, List<ChatMessage>? conversationHistory = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting complete response for: {Message} using provider: {Provider}", userMessage.Substring(0, Math.Min(50, userMessage.Length)), _config.Provider);

        var resConf = _config.Provider switch
        {
            LLMProvider.AzureOpenAI => await SendToAzureOpenAIOnceAsync(userMessage, conversationHistory, cancellationToken),
            LLMProvider.OpenAI => await SendToOpenAIOnceAsync(userMessage, conversationHistory, cancellationToken),
            LLMProvider.Ollama => await SendToOllamaOnceAsync(userMessage, conversationHistory, cancellationToken),
            _ => throw new NotSupportedException($"Provider {_config.Provider} not supported for complete response")
        };
        return resConf;
    }

    /// <summary>
    /// Genera una risposta in streaming - restituisce i token man mano che vengono generati
    /// </summary>
    public virtual async IAsyncEnumerable<string> GenerateStreamingResponseAsync(string userMessage, List<ChatMessage>? conversationHistory = null,[EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting streaming response for: {Message} using provider: {Provider}",
            userMessage.Substring(0, Math.Min(50, userMessage.Length)), _config.Provider);

        if (_config.Provider == LLMProvider.Ollama)
        {
            await foreach (var chunk in StreamFromOllamaAsync(userMessage, conversationHistory, cancellationToken))
            {
                yield return chunk;
            }
        }
        else if (_config.Provider == LLMProvider.OpenAI)
        {
            await foreach (var chunk in StreamFromOpenAIAsync(userMessage, conversationHistory, cancellationToken))
            {
                yield return chunk;
            }
        }
        else if (_config.Provider == LLMProvider.AzureOpenAI)
        {
            //SendToAzureOpenAIOnceAsync  - StreamFromAzureOpenAIAsync
            await foreach (var chunk in StreamFromAzureOpenAIAsync(userMessage, conversationHistory, cancellationToken))
            {
                yield return chunk;
            }
        }
        else if (_config.Provider == LLMProvider.AzureAIFoundryAgent)
        {
            await foreach (var chunk in StreamFromAzureAIFoundryAgentAsync(userMessage, conversationHistory, cancellationToken))
            {
                yield return chunk;
            }
        }
        else
        {
            throw new NotSupportedException($"Provider {_config.Provider} not supported for streaming");
        }
    }
public async Task<string> SendToAzureOpenAIOnceAsync(string userMessage,List<ChatMessage>? history,CancellationToken cancellationToken)
{
    try
    {
        _logger.LogInformation("Azure OpenAI Request - Endpoint: {Endpoint}, Deployment: {Deployment}, ApiKey length: {ApiKeyLen}",
            _config.Endpoint, _config.DeploymentName, _config.ApiKey?.Length ?? 0);

        if (string.IsNullOrEmpty(_config.Endpoint))
        {
            _logger.LogError("Azure OpenAI Endpoint is null or empty");
            throw new InvalidOperationException("Azure OpenAI Endpoint is not configured");
        }

        if (string.IsNullOrEmpty(_config.ApiKey))
        {
            _logger.LogError("Azure OpenAI ApiKey is null or empty");
            throw new InvalidOperationException("Azure OpenAI ApiKey is not configured");
        }

        if (string.IsNullOrEmpty(_config.DeploymentName))
        {
            _logger.LogError("Azure OpenAI DeploymentName is null or empty");
            throw new InvalidOperationException("Azure OpenAI DeploymentName is not configured");
        }

        // var azureClient = new AzureOpenAIClient(
        //     new Uri(_config.Endpoint),
        //     new AzureKeyCredential(_config.ApiKey)
        // );
        AzureOpenAIClient azureClient = new(new Uri(_config.Endpoint),new AzureKeyCredential(_config.ApiKey));


        _logger.LogInformation("AzureOpenAIClient created, getting chat client for deployment: {Deployment}", _config.DeploymentName);

        var chatClient = azureClient.GetChatClient(_config.DeploymentName);

        var messages = new List<OpenAI.Chat.ChatMessage>
        {
            new SystemChatMessage("Sei un assistente AI intelligente e utile. Rispondi in modo chiaro e conciso.")
        };

        if (history != null && history.Count > 0)
        {
            _logger.LogInformation("Adding {Count} history messages", history.Count);
            foreach (var h in history.TakeLast(10))
            {
                switch (h.Role.ToLower())
                {
                    case "user":
                        messages.Add(new UserChatMessage(h.Content));
                        break;
                    case "assistant":
                        messages.Add(new AssistantChatMessage(h.Content));
                        break;
                    case "system":
                        messages.Add(new SystemChatMessage(h.Content));
                        break;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(userMessage))
        {
            messages.Add(new UserChatMessage(userMessage));
            _logger.LogInformation("User message added: {Message}", userMessage.Substring(0, Math.Min(100, userMessage.Length)));
        }
        else
        {
            _logger.LogWarning("Il messaggio utente è vuoto.");
            return string.Empty;
        }
// max_tokens=4096,
//     temperature=1.0,
//     top_p=1.0,
//     model=deployment
        var options = new ChatCompletionOptions
        {
            Temperature = 0.7f,
            MaxOutputTokenCount = 2000,
            TopP = 1.0f,
        };

        _logger.LogInformation("Calling Azure OpenAI CompleteChatAsync with {Count} messages...", messages.Count);

        var response = await chatClient.CompleteChatAsync(messages, options, cancellationToken);
        
        var chatCompletion = response.Value;
        
        if (chatCompletion == null)
        {
            _logger.LogError("ChatCompletion response is null");
            return string.Empty;
        }

        if (chatCompletion.Content == null || chatCompletion.Content.Count == 0)
        {
            _logger.LogError("ChatCompletion.Content is null or empty");
            return string.Empty;
        }

        string result = chatCompletion.Content.Last().Text ?? "";
        _logger.LogInformation("Azure OpenAI response received: {Length} chars", result.Length);
        
        return result;
    }
    catch (Azure.RequestFailedException ex)
    {
        _logger.LogError(ex, "Azure RequestFailedException - Status: {Status}, ErrorCode: {Code}, Message: {Message}", 
            ex.Status, ex.ErrorCode, ex.Message);
        throw;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error in SendToAzureOpenAIOnceAsync: {Type} - {Message}", ex.GetType().Name, ex.Message);
        throw;
    }
}

    /// <summary>
    /// Invia richiesta a OpenAI e attende la risposta completa (non streaming)
    /// </summary>
    private async Task<string> SendToOpenAIOnceAsync(string userMessage, List<ChatMessage>? history, CancellationToken cancellationToken)
    {
        _logger.LogInformation("OpenAI Request (non-streaming) - Model: {Model}", _config.ModelName);

        var messages = BuildMessagesList(userMessage, history);

        var requestBody = new
        {
            model = _config.ModelName,
            messages = messages.Select(m => new { role = m.Role, content = m.Content }),
            stream = false,
            temperature = 0.7,
            max_tokens = 2000
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions")
        {
            Content = content
        };
        request.Headers.Add("Authorization", $"Bearer {_config.ApiKey}");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var responseDoc = JsonDocument.Parse(responseJson);

        if (responseDoc.RootElement.TryGetProperty("choices", out var choices) &&
            choices.GetArrayLength() > 0)
        {
            var choice = choices[0];
            if (choice.TryGetProperty("message", out var message) &&
                message.TryGetProperty("content", out var contentElement))
            {
                return contentElement.GetString() ?? string.Empty;
            }
        }

        _logger.LogWarning("OpenAI response did not contain expected content");
        return string.Empty;
    }

    /// <summary>
    /// Invia richiesta a Ollama e attende la risposta completa (non streaming)
    /// </summary>
    private async Task<string> SendToOllamaOnceAsync(
        string userMessage,
        List<ChatMessage>? history,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Ollama Request (non-streaming) - Model: {Model}, BaseUrl: {BaseUrl}",
            _config.ModelName, _config.BaseUrl);

        var messages = BuildMessagesList(userMessage, history);

        var requestBody = new
        {
            model = _config.ModelName,
            messages = messages.Select(m => new { role = m.Role, content = m.Content }),
            stream = false,
            options = new
            {
                temperature = 0.7,
                top_p = 0.9
            }
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_config.BaseUrl}/api/chat")
        {
            Content = content
        };

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var responseDoc = JsonDocument.Parse(responseJson);

        if (responseDoc.RootElement.TryGetProperty("message", out var message) &&
            message.TryGetProperty("content", out var contentElement))
        {
            return contentElement.GetString() ?? string.Empty;
        }

        _logger.LogWarning("Ollama response did not contain expected content");
        return string.Empty;
    }

    /// <summary>
    /// Streaming da Azure OpenAI (SDK 2.x)
    /// Configurazione da appsettings.json:
    /// - Endpoint: https://auranlp.services.ai.azure.com/api/projects/AuraNLP-project
    /// - DeploymentName: gpt4o-mini
    /// - ApiVersion: 2024-10-01-preview
    /// </summary>
    private async IAsyncEnumerable<string> StreamFromAzureOpenAIAsync(
    string userMessage,
    List<ChatMessage>? history,
    [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        _logger.LogInformation("Azure OpenAI Streaming - Endpoint: {Endpoint}, Deployment: {Deployment}",
            _config.Endpoint, _config.DeploymentName);

        // 1. Creo il client Azure OpenAI (SDK 2.x)  
        var azureClient = new AzureOpenAIClient(
            new Uri(_config.Endpoint),
            new AzureKeyCredential(_config.ApiKey)
        );

        // 2. Ottengo il ChatClient per il deployment specifico  
        var chatClient = azureClient.GetChatClient(_config.DeploymentName);

        // 3. Costruisco la lista messaggi nel formato SDK 2.x  
        var messages = new List<OpenAI.Chat.ChatMessage>
        {
            new SystemChatMessage("Sei un assistente AI intelligente e utile. " +
                                "Rispondi in modo chiaro, conciso e professionale.")
        };

        // Aggiungo la cronologia (ultimi 10 messaggi)  
        if (history != null && history.Count > 0)
        {
            foreach (var h in history.TakeLast(10))
            {
                switch (h.Role.ToLower())
                {
                    case "user":
                        messages.Add(new UserChatMessage(h.Content));
                        break;
                    case "assistant":
                        messages.Add(new AssistantChatMessage(h.Content));
                        break;
                    case "system":
                        messages.Add(new SystemChatMessage(h.Content));
                        break;
                }
            }
        }

        // Aggiungo il messaggio utente corrente  
        if (!string.IsNullOrWhiteSpace(userMessage))
        {
            messages.Add(new UserChatMessage(userMessage));
        }
        else
        {
            _logger.LogWarning("Il messaggio utente è vuoto.");
            yield break;
        }

        // 4. Configuro le opzioni  
        var options = new ChatCompletionOptions
        {
            Temperature = 0.7f,
            MaxOutputTokenCount = 2000
        };

        // 5. Streaming della risposta - AsyncCollectionResult<StreamingChatCompletionUpdate>  
        var completionUpdates = chatClient.CompleteChatStreamingAsync(messages, options);

        await foreach (StreamingChatCompletionUpdate completionUpdate in completionUpdates)
        {
            // Verifica cancellazione  
            if (cancellationToken.IsCancellationRequested)
                yield break;

            // ContentUpdate contiene i token man mano che arrivano  
            if (completionUpdate.ContentUpdate.Count > 0)
            {
                var text = completionUpdate.ContentUpdate[0].Text;
                if (!string.IsNullOrEmpty(text))
                {
                    _logger.LogInformation("Risposta dell'agente AI: {Response}", text);
                    yield return text;
                }
            }
        }
    }
    /// <summary>
    /// Streaming da Azure AI Foundry Agent Service
    /// Usa Azure.AI.Agents.Persistent SDK per comunicare con agenti persistenti
    /// Configurazione da appsettings.json:
    /// - Endpoint: https://auranlp.services.ai.azure.com/api/projects/AuraNLP-project
    /// - DeploymentName: gpt4o-mini (modello usato dall'agente)
    /// - AgentId: ID dell'agente esistente (opzionale)
    /// </summary>
    /// 
    /// @Marco: read this -  https://ai.azure.com/doc/azure/ai-foundry/how-to/develop/sdk-overview?tid=448c3a64-ab1a-44aa-823c-e64150c513fe
    private async IAsyncEnumerable<string> StreamFromAzureAIFoundryAgentAsync(
    string userMessage,
    List<ChatMessage>? history,
    [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        yield return "Azure AI Foundry Agent streaming not yet implemented.";
    }
    private async IAsyncEnumerable<string> StreamFromAzureOpenAIAsyncOld(
        string userMessage,
        List<ChatMessage>? history,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var messages = BuildMessagesList(userMessage, history);

        var requestBody = new
        {
            messages = messages.Select(m => new { role = m.Role, content = m.Content }),
            stream = true,
            temperature = 0.7,
            max_tokens = 2000
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Azure OpenAI URL format:
        // https://{resource}.openai.azure.com/openai/deployments/{deployment}/chat/completions?api-version={version}
        var endpoint = _config.Endpoint.TrimEnd('/');
        var url = $"{endpoint}/openai/deployments/chat/completions?api-version={_config.ApiVersion}";

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = content
        };

        // Azure usa "api-key" header invece di "Authorization: Bearer"
        request.Headers.Add("api-key", _config.ApiKey);

        _logger.LogDebug("Calling Azure OpenAI: {Url}", url);

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Azure OpenAI error: {Status} - {Content}", response.StatusCode, errorContent);
            throw new HttpRequestException($"Azure OpenAI returned {response.StatusCode}: {errorContent}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);

            if (string.IsNullOrEmpty(line) || !line.StartsWith("data: "))
                continue;

            var data = line.Substring(6); // Rimuovi "data: "

            if (data == "[DONE]")
                break;

            var chunk = ParseOpenAIStreamChunk(data);
            if (!string.IsNullOrEmpty(chunk))
            {
                yield return chunk;
            }
        }
    }

    /// <summary>
    /// Streaming da Ollama (LLama locale)
    /// </summary>
    private async IAsyncEnumerable<string> StreamFromOllamaAsync(
        string userMessage,
        List<ChatMessage>? history,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var messages = BuildMessagesList(userMessage, history);

        var requestBody = new
        {
            model = _config.ModelName,
            messages = messages.Select(m => new { role = m.Role, content = m.Content }),
            stream = true,
            options = new
            {
                temperature = 0.7,
                top_p = 0.9
            }
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_config.BaseUrl}/api/chat")
        {
            Content = content
        };

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrEmpty(line)) continue;

            var chunk = ParseOllamaStreamChunk(line);
            if (!string.IsNullOrEmpty(chunk))
            {
                yield return chunk;
            }
        }
    }

    /// <summary>
    /// Streaming da OpenAI (GPT-4, GPT-3.5, etc.)
    /// </summary>
    private async IAsyncEnumerable<string> StreamFromOpenAIAsync(
        string userMessage,
        List<ChatMessage>? history,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var messages = BuildMessagesList(userMessage, history);

        var requestBody = new
        {
            model = _config.ModelName,
            messages = messages.Select(m => new { role = m.Role, content = m.Content }),
            stream = true,
            temperature = 0.7,
            max_tokens = 2000
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions")
        {
            Content = content
        };
        request.Headers.Add("Authorization", $"Bearer {_config.ApiKey}");

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);

            if (string.IsNullOrEmpty(line) || !line.StartsWith("data: "))
                continue;

            var data = line.Substring(6); // Rimuovi "data: "

            if (data == "[DONE]")
                break;

            var chunk = ParseOpenAIStreamChunk(data);
            if (!string.IsNullOrEmpty(chunk))
            {
                yield return chunk;
            }
        }
    }

    /// <summary>
    /// Costruisce la lista di messaggi per la chat
    /// </summary>
    private List<ChatMessage> BuildMessagesList(string userMessage, List<ChatMessage>? history)
    {
        var messages = new List<ChatMessage>
        {
            new ChatMessage
            {
                Role = "system",
                Content = @"Sei un assistente AI intelligente e utile. 
                           Rispondi in modo chiaro, conciso e professionale.
                           Se non sai qualcosa, ammettilo onestamente.
                           Usa il markdown per formattare le risposte quando appropriato."
            }
        };

        // Aggiungi la cronologia della conversazione
        if (history != null && history.Count > 0)
        {
            // Limita la cronologia agli ultimi 10 messaggi per evitare token overflow
            var recentHistory = history.TakeLast(10).ToList();
            messages.AddRange(recentHistory);
        }

        // Aggiungi il messaggio corrente
        messages.Add(new ChatMessage { Role = "user", Content = userMessage });

        return messages;
    }

    /// <summary>
    /// Parsing del chunk da Ollama streaming
    /// </summary>
    private string? ParseOllamaStreamChunk(string line)
    {
        try
        {
            var json = JsonDocument.Parse(line);
            if (json.RootElement.TryGetProperty("message", out var message) &&
                message.TryGetProperty("content", out var content))
            {
                return content.GetString();
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning("Failed to parse Ollama chunk: {Line}, Error: {Error}", line, ex.Message);
        }
        return null;
    }

    /// <summary>
    /// Parsing del chunk da OpenAI/Azure OpenAI streaming (stesso formato)
    /// </summary>
    private string? ParseOpenAIStreamChunk(string data)
    {
        try
        {
            var json = JsonDocument.Parse(data);
            if (json.RootElement.TryGetProperty("choices", out var choices) &&
                choices.GetArrayLength() > 0)
            {
                var choice = choices[0];
                if (choice.TryGetProperty("delta", out var delta) &&
                    delta.TryGetProperty("content", out var content))
                {
                    return content.GetString();
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning("Failed to parse OpenAI chunk: {Data}, Error: {Error}", data, ex.Message);
        }
        return null;
    }
}

/// <summary>
/// Modello per un messaggio nella chat
/// </summary>

