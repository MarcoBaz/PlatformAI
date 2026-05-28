import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subject, takeUntil } from 'rxjs';
import { AnalyticsStreamingService } from './analytics-streaming.service';
import {
    AnalyticsStreamState,
    ChartData,
    PredictionData,
} from './analytics-streaming.types';

/**
 * Componente esempio per lo streaming analytics con grafici.
 * Dimostra come usare AnalyticsStreamingService per ottenere
 * risposte AI in tempo reale con grafici e predizioni.
 * 
 * @example
 * ```html
 * <app-analytics-chat [tenantCode]="'TENANT001'"></app-analytics-chat>
 * ```
 */
@Component({
    selector: 'app-analytics-chat',
    standalone: true,
    imports: [CommonModule, FormsModule],
    template: `
        <div class="analytics-chat-container">
            <!-- Header -->
            <div class="chat-header">
                <h2>Analytics AI Assistant</h2>
                <span class="tenant-badge">{{ tenantCode }}</span>
            </div>

            <!-- Messages Area -->
            <div class="messages-area" #messagesContainer>
                <!-- Messaggio utente corrente -->
                @if (currentUserMessage) {
                    <div class="message user-message">
                        <div class="message-content">{{ currentUserMessage }}</div>
                    </div>
                }

                <!-- Risposta AI in streaming -->
                @if (state.isStreaming || state.textContent) {
                    <div class="message ai-message">
                        <!-- Testo della risposta -->
                        <div class="message-content">
                            {{ state.textContent }}
                            @if (state.isStreaming && !state.isProcessingTools) {
                                <span class="cursor-blink">|</span>
                            }
                        </div>

                        <!-- Indicatore elaborazione -->
                        @if (state.isProcessingTools) {
                            <div class="processing-indicator">
                                <span class="spinner"></span>
                                Elaborazione dati in corso...
                            </div>
                        }

                        <!-- Grafici -->
                        @if (state.charts.length > 0) {
                            <div class="charts-container">
                                <h4>📊 Grafici Generati</h4>
                                @for (chart of state.charts; track chart.id) {
                                    <div class="chart-card">
                                        <h5>{{ chart.title }}</h5>
                                        @if (chart.subtitle) {
                                            <p class="chart-subtitle">{{ chart.subtitle }}</p>
                                        }
                                        <!-- Qui puoi integrare Chart.js o ngx-charts -->
                                        <div class="chart-placeholder" [attr.data-type]="chart.type">
                                            <pre>{{ chart | json }}</pre>
                                        </div>
                                    </div>
                                }
                            </div>
                        }

                        <!-- Predizione -->
                        @if (state.prediction) {
                            <div class="prediction-card">
                                <h4>🔮 Predizione ML</h4>
                                <div class="prediction-value">
                                    {{ state.prediction.predictedValue | number:'1.0-2' }}
                                </div>
                                @if (state.prediction.confidence) {
                                    <div class="prediction-confidence">
                                        Confidenza: {{ state.prediction.confidence | percent:'1.0-0' }}
                                    </div>
                                }
                                @if (state.prediction.explanation) {
                                    <p class="prediction-explanation">{{ state.prediction.explanation }}</p>
                                }
                            </div>
                        }
                    </div>
                }

                <!-- Errore -->
                @if (state.error) {
                    <div class="message error-message">
                        <span class="error-icon">⚠️</span>
                        {{ state.error }}
                    </div>
                }
            </div>

            <!-- Input Area -->
            <div class="input-area">
                <input
                    type="text"
                    [(ngModel)]="userInput"
                    (keyup.enter)="sendMessage()"
                    [disabled]="state.isStreaming"
                    placeholder="Chiedi analisi, grafici o predizioni..."
                    class="message-input"
                />
                
                @if (state.isStreaming) {
                    <button (click)="stopStreaming()" class="stop-btn">
                        ⏹️ Stop
                    </button>
                } @else {
                    <button 
                        (click)="sendMessage()" 
                        [disabled]="!userInput.trim()"
                        class="send-btn"
                    >
                        📤 Invia
                    </button>
                }
            </div>

            <!-- Quick Actions -->
            <div class="quick-actions">
                <button (click)="quickAction('Mostrami i trend di produzione dell\\'ultima settimana')">
                    📈 Trend Produzione
                </button>
                <button (click)="quickAction('Genera un grafico dei consumi energetici')">
                    ⚡ Consumi Energia
                </button>
                <button (click)="quickAction('Fammi una predizione sulla produzione di domani')">
                    🔮 Predizione
                </button>
                <button (click)="quickAction('Analizza gli scarti del mese')">
                    📊 Analisi Scarti
                </button>
            </div>
        </div>
    `,
    styles: [`
        .analytics-chat-container {
            display: flex;
            flex-direction: column;
            height: 100%;
            max-height: 800px;
            border: 1px solid #e0e0e0;
            border-radius: 12px;
            overflow: hidden;
            background: #fff;
        }

        .chat-header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            padding: 16px 20px;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
        }

        .chat-header h2 {
            margin: 0;
            font-size: 18px;
        }

        .tenant-badge {
            background: rgba(255,255,255,0.2);
            padding: 4px 12px;
            border-radius: 20px;
            font-size: 12px;
        }

        .messages-area {
            flex: 1;
            overflow-y: auto;
            padding: 20px;
            background: #f5f7fa;
        }

        .message {
            margin-bottom: 16px;
            max-width: 85%;
        }

        .user-message {
            margin-left: auto;
        }

        .user-message .message-content {
            background: #667eea;
            color: white;
            padding: 12px 16px;
            border-radius: 18px 18px 4px 18px;
        }

        .ai-message .message-content {
            background: white;
            padding: 12px 16px;
            border-radius: 18px 18px 18px 4px;
            box-shadow: 0 1px 3px rgba(0,0,0,0.1);
            white-space: pre-wrap;
        }

        .cursor-blink {
            animation: blink 1s infinite;
        }

        @keyframes blink {
            0%, 50% { opacity: 1; }
            51%, 100% { opacity: 0; }
        }

        .processing-indicator {
            display: flex;
            align-items: center;
            gap: 8px;
            margin-top: 12px;
            padding: 8px 12px;
            background: #e8f4fd;
            border-radius: 8px;
            color: #1976d2;
            font-size: 14px;
        }

        .spinner {
            width: 16px;
            height: 16px;
            border: 2px solid #1976d2;
            border-top-color: transparent;
            border-radius: 50%;
            animation: spin 1s linear infinite;
        }

        @keyframes spin {
            to { transform: rotate(360deg); }
        }

        .charts-container {
            margin-top: 16px;
        }

        .charts-container h4 {
            margin: 0 0 12px 0;
            color: #333;
        }

        .chart-card {
            background: #f8f9fa;
            border: 1px solid #e0e0e0;
            border-radius: 8px;
            padding: 12px;
            margin-bottom: 12px;
        }

        .chart-card h5 {
            margin: 0 0 8px 0;
            color: #444;
        }

        .chart-subtitle {
            margin: 0 0 8px 0;
            color: #666;
            font-size: 13px;
        }

        .chart-placeholder {
            background: #fff;
            border: 1px dashed #ccc;
            border-radius: 4px;
            padding: 12px;
            font-size: 11px;
            overflow: auto;
            max-height: 200px;
        }

        .prediction-card {
            margin-top: 16px;
            background: linear-gradient(135deg, #f093fb 0%, #f5576c 100%);
            color: white;
            padding: 16px;
            border-radius: 12px;
        }

        .prediction-card h4 {
            margin: 0 0 8px 0;
        }

        .prediction-value {
            font-size: 32px;
            font-weight: bold;
        }

        .prediction-confidence {
            font-size: 14px;
            opacity: 0.9;
        }

        .prediction-explanation {
            margin-top: 8px;
            font-size: 13px;
            opacity: 0.9;
        }

        .error-message {
            background: #ffebee;
            color: #c62828;
            padding: 12px 16px;
            border-radius: 8px;
            display: flex;
            align-items: center;
            gap: 8px;
        }

        .input-area {
            display: flex;
            gap: 8px;
            padding: 16px;
            background: white;
            border-top: 1px solid #e0e0e0;
        }

        .message-input {
            flex: 1;
            padding: 12px 16px;
            border: 1px solid #e0e0e0;
            border-radius: 24px;
            font-size: 14px;
            outline: none;
            transition: border-color 0.2s;
        }

        .message-input:focus {
            border-color: #667eea;
        }

        .message-input:disabled {
            background: #f5f5f5;
        }

        .send-btn, .stop-btn {
            padding: 12px 20px;
            border: none;
            border-radius: 24px;
            cursor: pointer;
            font-size: 14px;
            transition: all 0.2s;
        }

        .send-btn {
            background: #667eea;
            color: white;
        }

        .send-btn:hover:not(:disabled) {
            background: #5a6fd6;
        }

        .send-btn:disabled {
            background: #ccc;
            cursor: not-allowed;
        }

        .stop-btn {
            background: #ff5252;
            color: white;
        }

        .stop-btn:hover {
            background: #ff1744;
        }

        .quick-actions {
            display: flex;
            gap: 8px;
            padding: 12px 16px;
            background: #f8f9fa;
            border-top: 1px solid #e0e0e0;
            overflow-x: auto;
        }

        .quick-actions button {
            flex-shrink: 0;
            padding: 8px 16px;
            border: 1px solid #e0e0e0;
            border-radius: 20px;
            background: white;
            font-size: 12px;
            cursor: pointer;
            transition: all 0.2s;
        }

        .quick-actions button:hover {
            background: #667eea;
            color: white;
            border-color: #667eea;
        }
    `],
})
export class AnalyticsChatComponent implements OnInit, OnDestroy {
    /** Codice del tenant per filtrare i dati */
    tenantCode = 'DEFAULT'; // Può essere passato come @Input()

    /** Input utente */
    userInput = '';

    /** Messaggio utente corrente (per visualizzazione) */
    currentUserMessage = '';

    /** Stato corrente dello streaming */
    state: AnalyticsStreamState = {
        isStreaming: false,
        isProcessingTools: false,
        textContent: '',
        charts: [],
        prediction: undefined,
        error: undefined,
    };

    private _destroy$ = new Subject<void>();

    constructor(private _analyticsService: AnalyticsStreamingService) {}

    ngOnInit(): void {
        // Sottoscrivi allo stato dello streaming
        this._analyticsService.streamState$
            .pipe(takeUntil(this._destroy$))
            .subscribe((state) => {
                this.state = state;
            });
    }

    ngOnDestroy(): void {
        this._destroy$.next();
        this._destroy$.complete();
        this._analyticsService.stopStreaming();
    }

    /**
     * Invia il messaggio e avvia lo streaming
     */
    async sendMessage(): Promise<void> {
        const message = this.userInput.trim();
        if (!message || this.state.isStreaming) return;

        this.currentUserMessage = message;
        this.userInput = '';

        await this._analyticsService.streamAnalytics(message, this.tenantCode);
    }

    /**
     * Ferma lo streaming in corso
     */
    stopStreaming(): void {
        this._analyticsService.stopStreaming();
    }

    /**
     * Esegue una quick action predefinita
     */
    quickAction(message: string): void {
        this.userInput = message;
        this.sendMessage();
    }
}
