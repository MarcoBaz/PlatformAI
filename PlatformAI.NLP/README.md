# 🤖 PlatformAI - Integrazione LLM per Query con Linguaggio Naturale

## 📋 Panoramica

Sistema completo per interrogare dati di produzione industriale usando **linguaggio naturale** e generare automaticamente grafici interattivi.

### Architettura

```
┌─────────────────┐
│   Angular UI    │  "Mostrami la produzione degli ultimi 7 giorni"
└────────┬────────┘
         │ HTTP POST
┌────────▼────────┐
│  ASP.NET API    │  NLPQueryController
└────────┬────────┘
         │
┌────────▼────────────────┐
│  NLPQueryOrchestrator   │  Orchestrazione
└─┬──────┬────────┬───────┘
  │      │        │
  │      │        │
  ▼      ▼        ▼
┌─────┐┌───┐┌──────┐
│ LLM ││DB ││  ML  │
└─────┘└───┘└──────┘
```

## 🚀 Setup Backend

### 1. Installare Ollama (LLM Locale)

**MacOS/Linux:**
```bash
curl https://ollama.ai/install.sh | sh
```

**Windows:**
Scarica da https://ollama.ai/download

**Avvia Ollama:**
```bash
ollama serve
```

**Scarica modello LLama2:**
```bash
ollama pull llama2
# Oppure modelli più grandi:
# ollama pull llama2:13b
# ollama pull llama2:70b
```

**Verifica installazione:**
```bash
curl http://localhost:11434/api/generate -d '{
  "model": "llama2",
  "prompt": "Hello!",
  "stream": false
}'
```

### 2. Configurare il Backend

**Program.cs:**
```csharp
using PlatformAI.NLP.Services;
using PlatformAI.Infrastructure;
using PlatformAI.ML.Services;

var builder = WebApplication.CreateBuilder(args);

// Aggiungi HttpClient per LLM
builder.Services.AddHttpClient<LLMService>();

// Configura LLM (Ollama locale)
builder.Services.AddSingleton(new LLMConfig
{
    Provider = LLMProvider.Ollama,
    BaseUrl = "http://localhost:11434",
    ModelName = "llama2"
});

// Oppure OpenAI:
// builder.Services.AddSingleton(new LLMConfig
// {
//     Provider = LLMProvider.OpenAI,
//     ModelName = "gpt-4",
//     ApiKey = "your-api-key"
// });

// Servizi NLP
builder.Services.AddScoped<LLMService>();
builder.Services.AddScoped<NLPQueryOrchestrator>();

// Servizi esistenti
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<TrainingService>();
builder.Services.AddScoped<DataLoader>();

// CORS per Angular
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddControllers();

var app = builder.Build();

app.UseCors("AllowAngular");
app.MapControllers();
app.Run();
```

### 3. Test API

**Esempi di richieste:**

```bash
# 1. Health check
curl http://localhost:5000/api/nlpquery/health

# 2. Esempi di query disponibili
curl http://localhost:5000/api/nlpquery/examples

# 3. Query in linguaggio naturale
curl -X POST http://localhost:5000/api/nlpquery/ask \
  -H "Content-Type: application/json" \
  -d '{
    "query": "Mostrami la produzione degli ultimi 7 giorni",
    "tenantCode": "TENANT-001"
  }'
```

**Risposta esempio:**
```json
{
  "success": true,
  "analysis": {
    "interpretedQuery": "Mostra andamento produzione ultimi 7 giorni",
    "confidence": 0.95
  },
  "chartType": "line",
  "chartConfig": {
    "title": "Produzione Ultimi 7 Giorni",
    "xAxisLabel": "Data",
    "yAxisLabel": "Quantità Prodotta",
    "series": [
      {
        "name": "Produzione",
        "type": "line",
        "dataKey": "QuantityProduced",
        "color": "#3b82f6"
      }
    ]
  },
  "data": [
    {
      "timestamp": "2025-01-17T00:00:00Z",
      "date": "2025-01-17",
      "QuantityProduced": 150
    },
    ...
  ],
  "insights": [
    "La produzione è aumentata del 12% rispetto alla settimana precedente",
    "Il picco produttivo si verifica alle ore 10:00",
    "Temperature stabili intorno ai 75°C"
  ]
}
```

## 🎨 Setup Frontend Angular

### 1. Installare Dipendenze

```bash
cd platformAI-ui
npm install recharts @types/recharts
# O se preferisci Chart.js:
# npm install chart.js ng2-charts
```

### 2. Creare Service Angular

**services/nlp-query.service.ts:**
```typescript
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface NLQuery {
  query: string;
  tenantCode: string;
  contextDate?: Date;
}

export interface NLQueryResponse {
  success: boolean;
  errorMessage?: string;
  chartType: string;
  chartConfig: ChartConfig;
  data: any[];
  insights: string[];
}

export interface ChartConfig {
  title: string;
  xAxisLabel: string;
  yAxisLabel: string;
  series: ChartSeries[];
}

export interface ChartSeries {
  name: string;
  type: string;
  dataKey: string;
  color: string;
}

@Injectable({
  providedIn: 'root'
})
export class NLPQueryService {
  private apiUrl = 'http://localhost:5000/api/nlpquery';

  constructor(private http: HttpClient) {}

  askQuestion(query: NLQuery): Observable<NLQueryResponse> {
    return this.http.post<NLQueryResponse>(`${this.apiUrl}/ask`, query);
  }

  getExamples(): Observable<any[]> {
    return this.http.get<any[]>(`${this.apiUrl}/examples`);
  }
}
```

### 3. Componente UI

**components/nlp-dashboard/nlp-dashboard.component.ts:**
```typescript
import { Component, OnInit } from '@angular/core';
import { NLPQueryService, NLQueryResponse } from '../../services/nlp-query.service';

@Component({
  selector: 'app-nlp-dashboard',
  templateUrl: './nlp-dashboard.component.html',
  styleUrls: ['./nlp-dashboard.component.scss']
})
export class NLPDashboardComponent implements OnInit {
  query: string = '';
  loading: boolean = false;
  response: NLQueryResponse | null = null;
  examples: any[] = [];
  error: string | null = null;

  constructor(private nlpService: NLPQueryService) {}

  ngOnInit() {
    this.loadExamples();
  }

  loadExamples() {
    this.nlpService.getExamples().subscribe(
      examples => this.examples = examples,
      error => console.error('Error loading examples:', error)
    );
  }

  submitQuery() {
    if (!this.query.trim()) return;

    this.loading = true;
    this.error = null;

    this.nlpService.askQuestion({
      query: this.query,
      tenantCode: 'TENANT-001' // TODO: Get from auth service
    }).subscribe({
      next: (response) => {
        this.loading = false;
        this.response = response;
      },
      error: (err) => {
        this.loading = false;
        this.error = err.error?.error || 'An error occurred';
        console.error('Query error:', err);
      }
    });
  }

  useExample(exampleQuery: string) {
    this.query = exampleQuery;
    this.submitQuery();
  }
}
```

**nlp-dashboard.component.html:**
```html
<div class="nlp-dashboard">
  <!-- Query Input -->
  <div class="query-section">
    <h2>🤖 Interroga i tuoi dati</h2>
    
    <div class="search-box">
      <input
        type="text"
        [(ngModel)]="query"
        (keyup.enter)="submitQuery()"
        placeholder="es: Mostrami la produzione degli ultimi 7 giorni"
        class="query-input"
      />
      <button 
        (click)="submitQuery()" 
        [disabled]="loading || !query.trim()"
        class="search-btn">
        {{ loading ? 'Elaborazione...' : 'Cerca' }}
      </button>
    </div>

    <!-- Examples -->
    <div class="examples">
      <p>Prova questi esempi:</p>
      <div class="example-chips">
        <button 
          *ngFor="let ex of examples"
          (click)="useExample(ex.query)"
          class="example-chip">
          {{ ex.query }}
        </button>
      </div>
    </div>
  </div>

  <!-- Loading -->
  <div *ngIf="loading" class="loading">
    <div class="spinner"></div>
    <p>Analizzo la tua richiesta...</p>
  </div>

  <!-- Error -->
  <div *ngIf="error" class="error-box">
    ⚠️ {{ error }}
  </div>

  <!-- Results -->
  <div *ngIf="response && !loading" class="results">
    
    <!-- Chart -->
    <div class="chart-container">
      <h3>{{ response.chartConfig.title }}</h3>
      
      <!-- Recharts Line Chart -->
      <app-dynamic-chart
        [data]="response.data"
        [chartType]="response.chartType"
        [config]="response.chartConfig">
      </app-dynamic-chart>
    </div>

    <!-- Insights -->
    <div class="insights-panel" *ngIf="response.insights.length > 0">
      <h4>💡 Insights</h4>
      <ul>
        <li *ngFor="let insight of response.insights">
          {{ insight }}
        </li>
      </ul>
    </div>

    <!-- Data Table -->
    <div class="data-table">
      <h4>📊 Dati</h4>
      <button (click)="downloadData()">Download CSV</button>
      <table>
        <thead>
          <tr>
            <th *ngFor="let key of getDataKeys()">{{ key }}</th>
          </tr>
        </thead>
        <tbody>
          <tr *ngFor="let row of response.data">
            <td *ngFor="let key of getDataKeys()">{{ row[key] }}</td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</div>
```

### 4. Componente Grafico Dinamico

**components/dynamic-chart/dynamic-chart.component.ts:**
```typescript
import { Component, Input, OnChanges } from '@angular/core';

@Component({
  selector: 'app-dynamic-chart',
  template: `
    <div [ngSwitch]="chartType">
      <!-- Line Chart -->
      <div *ngSwitchCase="'line'">
        <recharts-line-chart
          [data]="data"
          [width]="800"
          [height]="400">
          <recharts-cartesian-grid strokeDasharray="3 3"/>
          <recharts-x-axis dataKey="date"/>
          <recharts-y-axis [label]="config.yAxisLabel"/>
          <recharts-tooltip/>
          <recharts-legend/>
          <recharts-line
            *ngFor="let series of config.series"
            [type]="'monotone'"
            [dataKey]="series.dataKey"
            [stroke]="series.color"
            [name]="series.name"/>
        </recharts-line-chart>
      </div>

      <!-- Bar Chart -->
      <div *ngSwitchCase="'bar'">
        <recharts-bar-chart
          [data]="data"
          [width]="800"
          [height]="400">
          <recharts-cartesian-grid strokeDasharray="3 3"/>
          <recharts-x-axis dataKey="date"/>
          <recharts-y-axis/>
          <recharts-tooltip/>
          <recharts-legend/>
          <recharts-bar
            *ngFor="let series of config.series"
            [dataKey]="series.dataKey"
            [fill]="series.color"
            [name]="series.name"/>
        </recharts-bar-chart>
      </div>

      <!-- Fallback -->
      <div *ngSwitchDefault>
        <p>Chart type "{{ chartType }}" not implemented yet</p>
        <pre>{{ data | json }}</pre>
      </div>
    </div>
  `
})
export class DynamicChartComponent implements OnChanges {
  @Input() data: any[] = [];
  @Input() chartType: string = 'line';
  @Input() config: any = {};

  ngOnChanges() {
    console.log('Chart updated:', this.chartType, this.data.length, 'points');
  }
}
```

### 5. Styling (SCSS)

```scss
.nlp-dashboard {
  padding: 2rem;
  max-width: 1200px;
  margin: 0 auto;

  .query-section {
    background: white;
    padding: 2rem;
    border-radius: 12px;
    box-shadow: 0 2px 8px rgba(0,0,0,0.1);
    margin-bottom: 2rem;

    h2 {
      margin-bottom: 1.5rem;
      color: #1f2937;
    }

    .search-box {
      display: flex;
      gap: 1rem;
      margin-bottom: 1.5rem;

      .query-input {
        flex: 1;
        padding: 1rem;
        font-size: 1rem;
        border: 2px solid #e5e7eb;
        border-radius: 8px;
        transition: border-color 0.2s;

        &:focus {
          outline: none;
          border-color: #3b82f6;
        }
      }

      .search-btn {
        padding: 1rem 2rem;
        background: #3b82f6;
        color: white;
        border: none;
        border-radius: 8px;
        font-weight: 600;
        cursor: pointer;
        transition: background 0.2s;

        &:hover:not(:disabled) {
          background: #2563eb;
        }

        &:disabled {
          opacity: 0.5;
          cursor: not-allowed;
        }
      }
    }

    .examples {
      .example-chips {
        display: flex;
        flex-wrap: wrap;
        gap: 0.5rem;
        margin-top: 0.5rem;

        .example-chip {
          padding: 0.5rem 1rem;
          background: #f3f4f6;
          border: 1px solid #e5e7eb;
          border-radius: 20px;
          font-size: 0.875rem;
          cursor: pointer;
          transition: all 0.2s;

          &:hover {
            background: #e5e7eb;
            transform: translateY(-2px);
          }
        }
      }
    }
  }

  .chart-container {
    background: white;
    padding: 2rem;
    border-radius: 12px;
    box-shadow: 0 2px 8px rgba(0,0,0,0.1);
    margin-bottom: 2rem;
  }

  .insights-panel {
    background: #fef3c7;
    padding: 1.5rem;
    border-radius: 12px;
    margin-bottom: 2rem;

    h4 {
      margin-bottom: 1rem;
      color: #92400e;
    }

    ul {
      list-style: none;
      padding: 0;

      li {
        padding: 0.5rem 0;
        color: #78350f;
        
        &:before {
          content: "✓ ";
          color: #f59e0b;
          font-weight: bold;
        }
      }
    }
  }
}
```

## 🎯 Esempi di Query Supportate

```
✅ "Mostrami la produzione degli ultimi 7 giorni"
✅ "Qual è il tasso di scarto per turno?"
✅ "Confronta energia consumata vs produzione"
✅ "Trend temperature ultima settimana"
✅ "Previsione produzione prossimi 3 giorni"
✅ "Quali sono le ore di picco produzione?"
✅ "Performance attuale vs target del mese"
✅ "Distribuzione produzione per giorno della settimana"
```

## 🔧 Configurazione Avanzata

### Usare OpenAI invece di Ollama

```csharp
builder.Services.AddSingleton(new LLMConfig
{
    Provider = LLMProvider.OpenAI,
    ModelName = "gpt-4",
    ApiKey = builder.Configuration["OpenAI:ApiKey"]
});
```

### Ottimizzare Performance

1. **Cache delle risposte LLM** (query simili)
2. **Aggregazione dati lato DB** (meno dati da trasferire)
3. **WebSockets** per streaming delle risposte

## 📊 Roadmap Features

- [ ] Multi-lingua (inglese, italiano, etc)
- [ ] Export grafici come PNG
- [ ] Query salvate / preferiti
- [ ] Suggerimenti intelligenti mentre si digita
- [ ] Confronto tra periodi temporali
- [ ] Alerts automatici basati su anomalie rilevate dall'AI

## 🐛 Troubleshooting

**Problema: Ollama non risponde**
```bash
# Verifica che Ollama sia in esecuzione
ps aux | grep ollama

# Riavvia
ollama serve
```

**Problema: Timeout LLM**
- Usa modelli più piccoli (llama2 invece di llama2:70b)
- Aumenta timeout HTTP Client nel backend

**Problema: Grafici non visualizzati**
- Verifica console browser per errori
- Controlla che i dataKey nel chartConfig matchino i nomi delle colonne nei dati

