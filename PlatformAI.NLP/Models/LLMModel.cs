using System;

namespace PlatformAI.NLP.Models;

// ============================================================================
// CLASSI DI SUPPORTO
// ============================================================================

/// <summary>
/// Provider LLM supportati
/// </summary>
public enum LLMProvider
{
    /// <summary>LLama locale tramite Ollama</summary>
    Ollama,
    /// <summary>OpenAI API diretta (GPT-4, GPT-3.5, etc)</summary>
    OpenAI,
    /// <summary>Azure OpenAI Service (consigliato per produzione)</summary>
    AzureOpenAI,
    /// <summary>Azure AI Foundry Agent Service (per agenti persistenti)</summary>
    AzureAIFoundryAgent
}

/// <summary>
/// Configurazione per il servizio LLM
/// </summary>
public class LLMConfig
{
    /// <summary>Provider da utilizzare (Ollama, OpenAI, AzureOpenAI, AzureAIFoundryAgent)</summary>
    public LLMProvider Provider { get; set; } = LLMProvider.Ollama;
    
    /// <summary>URL base del servizio (Ollama: http://localhost:11434)</summary>
    public string BaseUrl { get; set; } = "http://localhost:11434";
    
    /// <summary>Nome del modello (es: llama2, gpt-4o-mini)</summary>
    public string ModelName { get; set; } = "llama2";
    
    /// <summary>API Key (per OpenAI e Azure OpenAI)</summary>
    public string? ApiKey { get; set; }
    
    // ---- Campi specifici per Azure OpenAI ----
    
    /// <summary>Endpoint Azure (es: https://your-resource.openai.azure.com/)</summary>
    public string? Endpoint { get; set; }
    
    /// <summary>Nome del deployment Azure (es: gpt-4o-mini)</summary>
    public string? DeploymentName { get; set; }
    
    /// <summary>Versione API Azure (es: 2024-08-01-preview)</summary>
    public string ApiVersion { get; set; } = "2024-08-01-preview";
    
    // ---- Campi specifici per Azure AI Foundry Agent Service ----
    
    /// <summary>ID dell'agente esistente in Foundry (opzionale, se null ne viene creato uno nuovo)</summary>
    public string? AgentId { get; set; }
    
    /// <summary>Nome dell'agente (usato quando si crea un nuovo agente)</summary>
    public string AgentName { get; set; } = "PlatformAI-Assistant";
    
    /// <summary>Istruzioni per l'agente</summary>
    public string AgentInstructions { get; set; } = "Sei un assistente AI intelligente e utile. Rispondi in modo chiaro, conciso e professionale.";
}

// Response models per deserializzazione
internal class OllamaResponse
{
    public string Response { get; set; } = string.Empty;
}

internal class OpenAIResponse
{
    public List<OpenAIChoice>? Choices { get; set; }
}

internal class OpenAIChoice
{
    public OpenAIMessage? Message { get; set; }
}

internal class OpenAIMessage
{
    public string Content { get; set; } = string.Empty;
}

public class ChatMessage
{
    public string Role { get; set; } = "user"; // "system", "user", "assistant"
    public string Content { get; set; } = string.Empty;
}
