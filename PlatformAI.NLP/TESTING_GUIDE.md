# 🧪 Guida Completa Testing NLP Services

## 📋 Panoramica Test

Abbiamo creato **3 suite di test** con **21 test totali**:

1. **LLMServiceTests** (10 test) - Unit test con mock HTTP
2. **NLPQueryOrchestratorTests** (4 test) - Integration test con DB
3. **OllamaIntegrationTests** (7 test) - Test con Ollama reale

---

## 🚀 Setup Ollama su Mac

### Step 1: Installazione

```bash
# Metodo 1: Homebrew (CONSIGLIATO)
brew install ollama

# Metodo 2: Installer ufficiale
# Scarica da: https://ollama.ai/download
curl -fsSL https://ollama.ai/install.sh | sh

# Verifica installazione
which ollama
ollama --version
```

### Step 2: Avvio Servizio

```bash
# Terminale 1: Avvia Ollama (lascialo in esecuzione)
ollama serve

# Output atteso:
# time=2025-01-24T14:30:00.000Z level=INFO msg="Listening on 127.0.0.1:11434"
# time=2025-01-24T14:30:00.000Z level=INFO msg="Ollama is running"
```

### Step 3: Download Modelli

```bash
# In un NUOVO terminale (lascia ollama serve in esecuzione)

# Scarica LLama2 (4GB - richiede 5-10 minuti)
ollama pull llama2

# Output atteso:
# pulling manifest
# pulling 8934d96d3f08... 100% ████████████████ 3.8 GB
# pulling 8c17c2ebb0ea... 100% ████████████████ 7.0 KB
# pulling 7c23fb36d801... 100% ████████████████ 4.8 KB
# pulling 2e0493f67d0c... 100% ████████████████   59 B
# pulling fa304d675061... 100% ████████████████   91 B
# pulling 42ba7f8a01dd... 100% ████████████████  557 B
# verifying sha256 digest
# writing manifest
# success

# Verifica modelli installati
ollama list

# Output atteso:
# NAME            ID              SIZE    MODIFIED
# llama2:latest   78e26419b446    3.8 GB  2 minutes ago
```

### Step 4: Test Ollama

```bash
# Test 1: Chat interattiva
ollama run llama2
# >>> Ciao! Come stai?
# >>> (Ollama risponderà in italiano)
# >>> /bye (per uscire)

# Test 2: API REST
curl http://localhost:11434/api/tags

# Output atteso (JSON):
# {"models":[{"name":"llama2:latest","modified_at":"2025-01-24T14:35:00Z",...}]}

# Test 3: Generazione semplice
curl http://localhost:11434/api/generate -d '{
  "model": "llama2",
  "prompt": "Dimmi ciao in italiano",
  "stream": false
}'

# Test 4: Test con nostro prompt (simula quello che fa il codice)
curl http://localhost:11434/api/generate -d '{
  "model": "llama2",
  "prompt": "Analizza questa query: Mostrami la produzione degli ultimi 7 giorni. Restituisci JSON con startDate e endDate.",
  "stream": false,
  "options": {
    "temperature": 0.1
  }
}' | jq '.response'
```

### Troubleshooting Ollama

```bash
# Problema: Porta 11434 già in uso
lsof -i :11434
kill -9 <PID>

# Problema: Ollama non risponde
ps aux | grep ollama
# Se non c'è processo, riavvia
ollama serve

# Problema: Modello non trovato
ollama list  # Verifica che llama2 sia nell'elenco
ollama pull llama2  # Riscarica se necessario

# Problema: Ollama crash o lento
# Verifica RAM disponibile (serve almeno 8GB)
top -l 1 | grep PhysMem

# Reinstallazione completa
brew uninstall ollama
rm -rf ~/.ollama
brew install ollama
ollama pull llama2
```

---

## 🧪 Esecuzione Unit Test

### Prerequisiti

```bash
# 1. Naviga nella directory test
cd /Users/marcobazzoli/tmp/b2a/Claude/PlatformAI/PlatformAi.Test

# 2. Verifica che i file di test esistano
ls -la *NLP*.cs

# Output atteso:
# NLPServicesTests.cs
# OllamaIntegrationTests.cs

# 3. Restore dipendenze
dotnet restore
```

### Test Categoria 1: Unit Test (Mock) - VELOCI

Questi test **NON richiedono Ollama** in esecuzione, usano mock HTTP.

```bash
# Esegui SOLO unit test (no integration)
dotnet test --filter "FullyQualifiedName~LLMServiceTests"

# Output atteso (~2 secondi):
# Starting test execution, please wait...
# 
# Passed! - Failed:     0, Passed:    10, Skipped:     0, Total:    10
# 
# Test Run Successful.
# Total tests: 10
#      Passed: 10
#  Total time: 1.8 Seconds
```

**Test eseguiti:**
```
✅ AnalyzeQueryAsync_WithSimpleQuery_ReturnsValidAnalysis
✅ AnalyzeQueryAsync_WithComparisonQuery_ReturnsScatterChart  
✅ AnalyzeQueryAsync_WithAggregationQuery_ReturnsGroupBy
✅ AnalyzeQueryAsync_WithPredictionRequest_SetsIncludePredictions
✅ AnalyzeQueryAsync_WithInvalidLLMResponse_ReturnsFallback
✅ GenerateInsightsAsync_WithProductionData_ReturnsInsights
✅ GenerateInsightsAsync_OnError_ReturnsDefaultMessage
✅ LLMConfig_DefaultsToOllama
✅ LLMConfig_CanConfigureOpenAI
```

### Test Categoria 2: Orchestrator Test - MEDI

```bash
# Esegui test orchestratore (usa DB reale ma mock LLM)
dotnet test --filter "FullyQualifiedName~NLPQueryOrchestratorTests"

# Output atteso (~5 secondi):
# Passed! - Failed:     0, Passed:     4, Skipped:     0, Total:     4
```

**Test eseguiti:**
```
✅ ProcessQueryAsync_WithValidQuery_ReturnsCompleteResponse
✅ ProcessQueryAsync_WithPredictions_IncludesMLPredictions
✅ ProcessQueryAsync_WithGrouping_AggregatesData
✅ ProcessQueryAsync_OnError_ReturnsErrorResponse
```

### Test Categoria 3: Integration Ollama - LENTI

**⚠️ IMPORTANTE: Richiede Ollama in esecuzione!**

```bash
# STEP 1: In un terminale, avvia Ollama
ollama serve

# STEP 2: In un altro terminale, esegui test
dotnet test --filter "Category=RequiresOllama" --logger "console;verbosity=detailed"

# Output atteso (1-3 minuti):
# Starting test execution, please wait...
# 
# 🤖 Invio query a Ollama: 'Mostrami la produzione degli ultimi 7 giorni'
# ⏳ Attendere risposta (può richiedere 10-30 secondi)...
# 
# ✅ RISULTATO OLLAMA (elaborato in 15.3s):
# ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
# 📊 Interpretazione: Mostra andamento produzione ultimi 7 giorni
# 📈 Tipo Grafico: Line
# 🎯 Confidence: 0.95
# 📅 Periodo: 2025-01-17 → 2025-01-24
# 📊 Metriche: QuantityProduced
# ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
# 
# Passed! - Failed:     0, Passed:     6, Skipped:     0, Total:     6
# Total time: 125.4 Seconds (2 minuti)
```

**Test eseguiti:**
```
✅ RealOllama_SimpleProductionQuery_Works (15-30s)
✅ RealOllama_ComparisonQuery_ReturnsScatter (15-30s)
✅ RealOllama_AggregationQuery_ReturnsGroupBy (15-30s)
✅ RealOllama_PredictionQuery_SetsIncludePredictions (15-30s)
✅ RealOllama_ItalianQueries_WorkCorrectly (4 query = 60-120s)
✅ RealOllama_GenerateInsights_Works (15-30s)
```

**Nota:** Il test `RealOllama_MultipleQueries_Performance` è marcato `[Explicit]` e non viene eseguito automaticamente.

### Esegui Test Espliciti (Performance)

```bash
# Test di performance (5 query = 2-5 minuti)
dotnet test --filter "FullyQualifiedName~RealOllama_MultipleQueries_Performance"

# Output atteso:
# ⏱️  'Produzione ultimi 7 giorni': 18.2s
# ⏱️  'Temperatura media per giorno': 16.5s
# ⏱️  'Scarto per turno': 17.1s
# ⏱️  'Energia vs produzione': 19.3s
# ⏱️  'Previsione domani': 15.8s
# 
# 📊 Performance Summary:
#    Avg: 17.4s
#    Max: 19.3s
#    Min: 15.8s
```

### Esegui TUTTI i Test

```bash
# Tutti i test (richiede Ollama attivo, altrimenti skip integration)
dotnet test --logger "console;verbosity=detailed"

# Se Ollama NON è attivo, vedrai:
# Skipped RealOllama_SimpleProductionQuery_Works
#   ⚠️  Ollama non disponibile su http://localhost:11434.
#       Avvialo con: ollama serve
# 
# Total tests: 21
#      Passed: 14
#    Skipped: 7 (integration tests)
#  Total time: 8.2 Seconds

# Se Ollama È attivo:
# Total tests: 21
#      Passed: 21
#    Skipped: 0
#  Total time: 145.6 Seconds (~2.5 minuti)
```

---

## 📊 Dettaglio Test Coverage

### LLMServiceTests (Unit)

| Test | Descrizione | Tempo |
|------|-------------|-------|
| `AnalyzeQueryAsync_WithSimpleQuery_ReturnsValidAnalysis` | Verifica parsing query base | <1s |
| `AnalyzeQueryAsync_WithComparisonQuery_ReturnsScatterChart` | Verifica query confronto | <1s |
| `AnalyzeQueryAsync_WithAggregationQuery_ReturnsGroupBy` | Verifica query con groupBy | <1s |
| `AnalyzeQueryAsync_WithPredictionRequest_SetsIncludePredictions` | Verifica flag predizioni | <1s |
| `AnalyzeQueryAsync_WithInvalidLLMResponse_ReturnsFallback` | Verifica fallback su errore | <1s |
| `GenerateInsightsAsync_WithProductionData_ReturnsInsights` | Verifica generazione insights | <1s |
| `GenerateInsightsAsync_OnError_ReturnsDefaultMessage` | Verifica gestione errori insights | <1s |
| `LLMConfig_DefaultsToOllama` | Verifica configurazione default | <1s |
| `LLMConfig_CanConfigureOpenAI` | Verifica configurazione OpenAI | <1s |

### NLPQueryOrchestratorTests (Integration)

| Test | Descrizione | Tempo |
|------|-------------|-------|
| `ProcessQueryAsync_WithValidQuery_ReturnsCompleteResponse` | Workflow completo query→dati→chart | 2-3s |
| `ProcessQueryAsync_WithPredictions_IncludesMLPredictions` | Verifica integrazione ML predictions | 2-3s |
| `ProcessQueryAsync_WithGrouping_AggregatesData` | Verifica aggregazioni (day/week/month) | 2-3s |
| `ProcessQueryAsync_OnError_ReturnsErrorResponse` | Verifica gestione errori completa | 1s |

### OllamaIntegrationTests (Integration con Ollama reale)

| Test | Descrizione | Tempo |
|------|-------------|-------|
| `RealOllama_SimpleProductionQuery_Works` | Query produzione ultimi 7 giorni | 15-30s |
| `RealOllama_ComparisonQuery_ReturnsScatter` | Query confronto energia vs produzione | 15-30s |
| `RealOllama_AggregationQuery_ReturnsGroupBy` | Query con aggregazione per turno | 15-30s |
| `RealOllama_PredictionQuery_SetsIncludePredictions` | Query con richiesta predizioni | 15-30s |
| `RealOllama_ItalianQueries_WorkCorrectly` | Test 4 query in italiano | 60-120s |
| `RealOllama_GenerateInsights_Works` | Generazione insights su dati reali | 15-30s |
| `RealOllama_MultipleQueries_Performance` (Explicit) | Benchmark 5 query diverse | 2-5min |

---

## 🎯 Quick Commands

```bash
# Test veloci (solo mock)
dotnet test --filter "Category!=Integration&Category!=RequiresOllama"

# Test con DB ma senza Ollama
dotnet test --filter "Category!=RequiresOllama"

# Solo test Ollama (richiede ollama serve)
dotnet test --filter "Category=RequiresOllama"

# Tutti i test con output dettagliato
dotnet test --logger "console;verbosity=detailed"

# Test singolo specifico
dotnet test --filter "FullyQualifiedName~RealOllama_SimpleProductionQuery_Works"

# Test con coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

---

## 📈 Interpretazione Risultati

### ✅ Successo
```
Passed! - Failed:     0, Passed:    21, Skipped:     0, Total:    21
```
Tutti i test passano, sistema funzionante!

### ⚠️ Skip Parziale
```
Passed! - Failed:     0, Passed:    14, Skipped:     7, Total:    21
  ⚠️  Ollama non disponibile su http://localhost:11434
```
Test integration skippati perché Ollama non è attivo. **Non è un errore**, solo i test unit sono passati.

### ❌ Fallimento
```
Failed! - Failed:     3, Passed:    18, Skipped:     0, Total:    21
```
Alcuni test falliti. Controlla output per dettagli.

---

## 🐛 Troubleshooting Test

### Problema: "Ollama non disponibile"

```bash
# Verifica che Ollama sia in esecuzione
lsof -i :11434

# Se non c'è output, avvialo
ollama serve

# Riprova test
dotnet test --filter "Category=RequiresOllama"
```

### Problema: Test timeout

```bash
# Se vedi "Test execution timed out after 60000 milliseconds"
# Aumenta timeout in OllamaIntegrationTests.cs:

var httpClient = new HttpClient 
{ 
    Timeout = TimeSpan.FromMinutes(5) // Aumenta da 1 a 5 minuti
};
```

### Problema: Modello non trovato

```bash
# Errore: "model 'llama2' not found"
ollama list  # Verifica modelli installati
ollama pull llama2  # Riscarica
```

### Problema: Risposta LLM non valida

Se Ollama risponde ma il JSON non viene parsato:
- LLama2 potrebbe dare risposte non strutturate
- Il test `AnalyzeQueryAsync_WithInvalidLLMResponse_ReturnsFallback` verifica che ci sia fallback
- In produzione, considera GPT-4 per JSON più affidabili

---

## 📝 Prossimi Passi

1. ✅ Installa Ollama: `brew install ollama`
2. ✅ Scarica modello: `ollama pull llama2`
3. ✅ Avvia servizio: `ollama serve`
4. ✅ Esegui test unit: `dotnet test --filter "Category!=RequiresOllama"`
5. ✅ Esegui test integration: `dotnet test --filter "Category=RequiresOllama"`
6. 🔄 Integra nel CI/CD (solo unit test, skip integration)

---

## 🎓 Best Practices

✅ **In development:** Usa Ollama locale (gratis)
✅ **In CI/CD:** Esegui solo test mock (veloci)
✅ **Prima del deploy:** Esegui tutti i test inclusi integration
✅ **In production:** Considera OpenAI (più veloce e affidabile)

