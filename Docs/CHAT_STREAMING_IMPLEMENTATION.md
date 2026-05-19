# 🚀 Implementazione Chat Streaming (Tipo ChatGPT)

**Data**: Dicembre 2024 (Aggiornato: Dicembre 2024)  
**Obiettivo**: Implementare risposte AI in streaming dove il testo appare carattere per carattere come ChatGPT

---

## 📋 Riepilogo Implementazione

### Architettura

```
┌─────────────┐    POST /stream    ┌──────────────┐    IAsyncEnumerable    ┌───────────────┐
│   Angular   │ ──────────────────►│  Controller  │ ◄────────────────────►│ LLMStreaming  │
│   Frontend  │ ◄────SSE Events────│     SSE      │                       │    Service    │
└─────────────┘                    └──────────────┘                       └───────────────┘
                                         │
                                         ▼
                              ┌─────────────────────┐
                              │  Azure OpenAI       │
                              │  Azure AI Foundry   │
                              │  OpenAI / Ollama    │
                              └─────────────────────┘
```

### Tecnologie Utilizzate
- **Backend**: ASP.NET Core con Server-Sent Events (SSE)
- **Frontend**: Angular con Fetch API per streaming
- **LLM**: Azure AI Foundry Agent Service, Azure OpenAI Service, OpenAI diretto, Ollama locale
- **SDK**: Azure.AI.OpenAI 2.1.0, Azure.AI.Agents.Persistent 1.1.0

---

## 🆕 Aggiornamento Dicembre 2024: Azure AI Foundry Agent Service

### Nuovo Provider: AzureAIFoundryAgent

Aggiunto supporto per **Azure AI Foundry Agent Service** che permette di utilizzare agenti persistenti con funzionalità avanzate come:
- Thread di conversazione gestiti dal servizio
- Agenti riutilizzabili tra sessioni
- Supporto per tool come Code Interpreter, File Search, Azure AI Search
- Integrazione con Azure Functions

### Architettura con Foundry Agent

```
┌─────────────┐     ┌──────────────┐     ┌─────────────────────┐
│   Angular   │────►│  Controller  │────►│ LLMStreamingService │
│   Frontend  │◄────│     SSE      │◄────│                     │
└─────────────┘     └──────────────┘     └──────────┬──────────┘
                                                     │
                                                     ▼
                                         ┌───────────────────────┐
                                         │  PersistentAgentsClient│
                                         └───────────┬───────────┘
                                                     │
                              ┌──────────────────────┼──────────────────────┐
                              │                      │                      │
                              ▼                      ▼                      ▼
                    ┌─────────────┐       ┌─────────────┐       ┌─────────────┐
                    │   Agent     │       │   Thread    │       │    Run      │
                    │ (Persistent)│       │ (Conversation)│     │ (Streaming) │
                    └─────────────┘       └─────────────┘       └─────────────┘
```

---

## ☁️ Configurazione

### appsettings.json - Azure AI Foundry Agent (Nuovo - Consigliato)

```json
{
  "LLMSettings": {
    "Provider": "AzureAIFoundryAgent",
    "Endpoint": "https://auranlp.services.ai.azure.com/api/projects/AuraNLP-project",
    "ApiKey": "YOUR_API_KEY",
    "DeploymentName": "gpt-4o-mini",
    "ApiVersion": "2024-10-01-preview",
    "AgentId": null,
    "AgentName": "PlatformAI-Assistant",
    "AgentInstructions": "Sei un assistente AI intelligente e utile. Rispondi in modo chiaro, conciso e professionale."
  }
}
```

### appsettings.json - Azure OpenAI (Chat Completion diretto)

```json
{
  "LLMSettings": {
    "Provider": "AzureOpenAI",
    "Endpoint": "https://auranlp.services.ai.azure.com/api/projects/AuraNLP-project",
    "ApiKey": "YOUR_API_KEY",
    "DeploymentName": "gpt-4o-mini",
    "ApiVersion": "2024-10-01-preview"
  }
}
```

### ⚠️ IMPORTANTE: Autenticazione per AzureAIFoundryAgent

Il provider `AzureAIFoundryAgent` **NON supporta l'autenticazione con API Key**. Utilizza esclusivamente `DefaultAzureCredential` (Azure AD).

**Per sviluppo locale:**
```bash
az login
```

**Per produzione (Azure):**
- Configurare **Managed Identity** sul servizio (App Service, Container App, etc.)
- Assegnare il ruolo **Azure AI User** all'identità sul progetto Foundry

---

## 📦 Pacchetti NuGet Richiesti

```xml
<ItemGroup>
  <!-- LLM Integration -->
  <PackageReference Include="Azure.AI.OpenAI" Version="2.1.0" />
  <PackageReference Include="Azure.AI.Agents.Persistent" Version="1.1.0" />
  <PackageReference Include="Azure.Identity" Version="1.13.1" />
  <PackageReference Include="Microsoft.SemanticKernel" Version="1.33.2" />
  <PackageReference Include="Microsoft.SemanticKernel.Connectors.OpenAI" Version="1.33.2" />
</ItemGroup>
```

---

## 📁 File Creati/Modificati

### 1. LLMStreamingService.cs
**Path**: `/PlatformAI.NLP/Services/LLMStreamingService.cs`

Servizio che gestisce lo streaming delle risposte LLM:
- Supporto per **Azure AI Foundry Agent**, **Azure OpenAI**, **OpenAI** e **Ollama**
- Metodo `GenerateStreamingResponseAsync()` che restituisce `IAsyncEnumerable<string>`
- Gestione della cronologia conversazione per contesto

```csharp
public async IAsyncEnumerable<string> GenerateStreamingResponseAsync(
    string userMessage, 
    List<ChatMessage>? conversationHistory = null,
    CancellationToken cancellationToken = default)
```

**Metodi di streaming per provider:**

| Provider | Metodo | SDK/API |
|----------|--------|---------|
| AzureAIFoundryAgent | `StreamFromAzureAIFoundryAgentAsync` | Azure.AI.Agents.Persistent |
| AzureOpenAI | `StreamFromAzureOpenAIAsync` | Azure.AI.OpenAI 2.x |
| OpenAI | `StreamFromOpenAIAsync` | HTTP/SSE |
| Ollama | `StreamFromOllamaAsync` | HTTP/SSE |

### 2. LLMService.cs
**Path**: `/PlatformAI.NLP/Services/LLMService.cs`

Enum e configurazione:

```csharp
public enum LLMProvider
{
    Ollama,
    OpenAI,
    AzureOpenAI,
    AzureAIFoundryAgent  // NUOVO
}

public class LLMConfig
{
    public LLMProvider Provider { get; set; }
    public string BaseUrl { get; set; }
    public string ModelName { get; set; }
    public string? ApiKey { get; set; }
    public string? Endpoint { get; set; }
    public string? DeploymentName { get; set; }
    public string ApiVersion { get; set; }
    
    // Azure AI Foundry Agent settings
    public string? AgentId { get; set; }
    public string AgentName { get; set; }
    public string AgentInstructions { get; set; }
}
```

### 3. Program.cs
**Path**: `/PlatformAI/Program.cs`

Parsing configurazione aggiornato:

```csharp
var llmConfig = new LLMConfig
{
    Provider = Enum.TryParse<LLMProvider>(llmSection["Provider"], ignoreCase: true, out var provider) 
        ? provider : LLMProvider.Ollama,
    BaseUrl = llmSection["BaseUrl"] ?? "http://localhost:11434",
    ModelName = llmSection["ModelName"] ?? "llama2",
    ApiKey = llmSection["ApiKey"],
    Endpoint = llmSection["Endpoint"],
    DeploymentName = llmSection["DeploymentName"],
    ApiVersion = llmSection["ApiVersion"] ?? "2024-08-01-preview",
    // Azure AI Foundry Agent settings
    AgentId = llmSection["AgentId"],
    AgentName = llmSection["AgentName"] ?? "PlatformAI-Assistant",
    AgentInstructions = llmSection["AgentInstructions"] ?? "Sei un assistente AI intelligente e utile."
};
```

---

## 🔄 Provider Supportati

### 1. Azure AI Foundry Agent (NUOVO - Consigliato per scenari avanzati)

```json
{
  "Provider": "AzureAIFoundryAgent",
  "Endpoint": "https://YOUR-RESOURCE.services.ai.azure.com/api/projects/YOUR-PROJECT",
  "DeploymentName": "gpt-4o-mini",
  "AgentId": null,
  "AgentName": "MyAssistant",
  "AgentInstructions": "You are a helpful assistant."
}
```

**Vantaggi:**
- Agenti persistenti riutilizzabili
- Thread di conversazione gestiti dal servizio
- Supporto tool avanzati (Code Interpreter, File Search, Azure AI Search)
- Integrazione con Azure Functions

**Requisiti:**
- Autenticazione Azure AD (az login o Managed Identity)
- Ruolo "Azure AI User" sul progetto

### 2. Azure OpenAI (Consigliato per Chat Completion semplice)

```json
{
  "Provider": "AzureOpenAI",
  "Endpoint": "https://YOUR-RESOURCE.openai.azure.com/",
  "ApiKey": "your-azure-key",
  "DeploymentName": "gpt-4o-mini",
  "ApiVersion": "2024-10-01-preview"
}
```

### 3. OpenAI Diretto

```json
{
  "Provider": "OpenAI",
  "ModelName": "gpt-4o-mini",
  "ApiKey": "sk-..."
}
```

### 4. Ollama Locale

```json
{
  "Provider": "Ollama",
  "BaseUrl": "http://localhost:11434",
  "ModelName": "llama2"
}
```

---

## 🔧 Dettagli Implementazione SDK

### Azure.AI.OpenAI SDK 2.x (StreamFromAzureOpenAIAsync)

```csharp
// 1. Creo il client
var azureClient = new AzureOpenAIClient(
    new Uri(_config.Endpoint),
    new AzureKeyCredential(_config.ApiKey)
);

// 2. Ottengo ChatClient
var chatClient = azureClient.GetChatClient(_config.DeploymentName);

// 3. Costruisco messaggi
var messages = new List<OpenAI.Chat.ChatMessage>
{
    new SystemChatMessage("System prompt..."),
    new UserChatMessage(userMessage)
};

// 4. Configuro opzioni
var options = new ChatCompletionOptions
{
    Temperature = 0.7f,
    MaxOutputTokenCount = 2000
};

// 5. Streaming
await foreach (StreamingChatCompletionUpdate update in 
    chatClient.CompleteChatStreamingAsync(messages, options))
{
    if (update.ContentUpdate.Count > 0)
    {
        yield return update.ContentUpdate[0].Text;
    }
}
```

### Azure.AI.Agents.Persistent (StreamFromAzureAIFoundryAgentAsync)

```csharp
// 1. Creo client (SOLO DefaultAzureCredential!)
var agentsClient = new PersistentAgentsClient(
    _config.Endpoint!,
    new DefaultAzureCredential()
);

// 2. Recupero o creo agente
PersistentAgent agent = await agentsClient.Administration.CreateAgentAsync(
    model: _config.DeploymentName!,
    name: _config.AgentName,
    instructions: _config.AgentInstructions
);

// 3. Creo thread
PersistentAgentThread thread = await agentsClient.Threads.CreateThreadAsync();

// 4. Aggiungo messaggio
await agentsClient.Messages.CreateMessageAsync(
    thread.Id,
    MessageRole.User,
    userMessage
);

// 5. Run con streaming
await foreach (StreamingUpdate update in 
    agentsClient.Runs.CreateRunStreamingAsync(thread.Id, agent.Id))
{
    if (update is MessageContentUpdate contentUpdate)
    {
        yield return contentUpdate.Text;
    }
}
```

---

## 🔄 Flusso di Esecuzione

### Per AzureAIFoundryAgent:

1. **Utente** scrive messaggio
2. **Frontend** chiama endpoint streaming
3. **Backend** crea `PersistentAgentsClient` con `DefaultAzureCredential`
4. **Backend** recupera/crea Agent
5. **Backend** crea Thread
6. **Backend** aggiunge messaggio utente al Thread
7. **Backend** esegue Run con `CreateRunStreamingAsync`
8. **Foundry** restituisce `StreamingUpdate` con token
9. **Backend** invia token come SSE events
10. **Frontend** aggiorna UI in tempo reale

### Per AzureOpenAI:

1. **Utente** scrive messaggio
2. **Frontend** chiama endpoint streaming
3. **Backend** crea `AzureOpenAIClient` con `AzureKeyCredential`
4. **Backend** chiama `CompleteChatStreamingAsync`
5. **Azure OpenAI** restituisce `StreamingChatCompletionUpdate`
6. **Backend** invia token come SSE events
7. **Frontend** aggiorna UI in tempo reale

---

## 🆚 Confronto Provider

| Aspetto | AzureAIFoundryAgent | AzureOpenAI | OpenAI | Ollama |
|---------|---------------------|-------------|--------|--------|
| **Autenticazione** | Azure AD only | API Key / Azure AD | API Key | Nessuna |
| **Thread gestiti** | ✅ Sì | ❌ No | ❌ No | ❌ No |
| **Agenti persistenti** | ✅ Sì | ❌ No | ❌ No | ❌ No |
| **Tool support** | ✅ Avanzato | ❌ Base | ❌ Base | ❌ No |
| **Complessità** | Alta | Media | Media | Bassa |
| **Costo** | Pay-per-token | Pay-per-token | Pay-per-token | Solo HW |
| **Privacy** | ✅ Azure region | ✅ Azure region | ⚠️ OpenAI servers | ✅ Locale |

---

## 💰 Stima Costi

### Azure OpenAI / Foundry

| Modello | Input (1M token) | Output (1M token) |
|---------|------------------|-------------------|
| **gpt-4o-mini** | $0.15 | $0.60 |
| **gpt-4o** | $2.50 | $10.00 |

**Per uso tipico (1000 query/giorno):** ~$60-90/mese con gpt-4o-mini

---

## 🧪 Testing

### Avvio Backend

```bash
cd PlatformAI
dotnet run
```

**Log atteso per AzureAIFoundryAgent:**
```
[LLM Config] Provider: AzureAIFoundryAgent
[LLM Config] Endpoint: https://auranlp.services.ai.azure.com/api/projects/AuraNLP-project
[LLM Config] Deployment: gpt-4o-mini
[LLM Config] AgentId: (will create new)
[LLM Config] AgentName: PlatformAI-Assistant
```

**Log atteso per AzureOpenAI:**
```
[LLM Config] Provider: AzureOpenAI
[LLM Config] Endpoint: https://auranlp.services.ai.azure.com/api/projects/AuraNLP-project
[LLM Config] Deployment: gpt-4o-mini
```

### Avvio Frontend

```bash
cd platformAI-ui
ng serve
```

---

## 🐛 Troubleshooting

### Errore 401 Unauthorized (AzureAIFoundryAgent)
**Causa**: DefaultAzureCredential non configurato
**Soluzione**: 
- Locale: eseguire `az login`
- Azure: configurare Managed Identity e assegnare ruolo "Azure AI User"

### Errore 404 da Azure OpenAI
**Causa**: Nome deployment errato
**Soluzione**: Verifica il nome esatto su Azure Portal > Azure OpenAI > Deployments

### Errore "AzureKeyCredential not supported"
**Causa**: PersistentAgentsClient non supporta API Key
**Soluzione**: Usa `DefaultAzureCredential` invece di `AzureKeyCredential`

### CORS Error nel Frontend
**Causa**: Backend non raggiungibile
**Soluzione**: Verifica che il backend sia in esecuzione e CORS configurato

---

## 📚 Riferimenti

- [Azure AI Foundry Agent Service](https://learn.microsoft.com/en-us/azure/ai-foundry/agents/)
- [Azure.AI.Agents.Persistent NuGet](https://www.nuget.org/packages/Azure.AI.Agents.Persistent)
- [Azure.AI.OpenAI SDK 2.x](https://learn.microsoft.com/en-us/dotnet/api/overview/azure/ai.openai-readme)
- [Azure OpenAI Service Documentation](https://learn.microsoft.com/en-us/azure/ai-services/openai/)
- [Azure OpenAI Streaming](https://learn.microsoft.com/en-us/azure/ai-services/openai/how-to/streaming)
- [DefaultAzureCredential](https://learn.microsoft.com/en-us/dotnet/api/azure.identity.defaultazurecredential)
- [Server-Sent Events MDN](https://developer.mozilla.org/en-US/docs/Web/API/Server-sent_events)
