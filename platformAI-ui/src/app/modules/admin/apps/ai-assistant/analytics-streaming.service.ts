import { HttpClient } from '@angular/common/http';
import { inject, Injectable, NgZone } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';
import { AuthService } from 'app/core/auth/auth.service';
import { environment } from 'environments/environment';
import {
    AnalyticsStreamRequest,
    AnalyticsRequest,
    AnalyticsResponse,
    AnalyticsStreamEvent,
    AnalyticsEventType,
    AnalyticsStreamState,
    AnalyticsMessage,
    ChartData,
    PredictionData,
    PredictionRequest,
    SmartPredictionResult,
    PredictionComparisonResult,
    StartEventData,
    TextEventData,
    CompleteEventData,
    ErrorEventData,
    ProcessingEventData,
} from './analytics-streaming.types';

/**
 * Servizio per lo streaming analytics con supporto per grafici e predizioni ML.
 * Utilizza Server-Sent Events (SSE) per ricevere risposte in tempo reale.
 * 
 * @example
 * ```typescript
 * // Nel componente
 * this.analyticsService.streamAnalytics('Mostrami i trend di produzione', 'TENANT001');
 * 
 * // Sottoscrivi allo stato
 * this.analyticsService.streamState$.subscribe(state => {
 *   this.textContent = state.textContent;
 *   this.charts = state.charts;
 *   this.prediction = state.prediction;
 * });
 * ```
 */
@Injectable({
    providedIn: 'root',
})
export class AnalyticsStreamingService {
    private readonly _apiUrl = `${environment.apiUrl}/analytics`;

    // State management
    private _streamState = new BehaviorSubject<AnalyticsStreamState>({
        isStreaming: false,
        isProcessingTools: false,
        textContent: '',
        charts: [],
        prediction: undefined,
        error: undefined,
    });

    private _messages = new BehaviorSubject<AnalyticsMessage[]>([]);
    private _currentMessageId = new BehaviorSubject<string | null>(null);

    // AbortController per cancellare lo streaming
    private _abortController: AbortController | null = null;

    // Injected services
    private _httpClient = inject(HttpClient);
    private _authService = inject(AuthService);
    private _ngZone = inject(NgZone);

    // =========================================================================
    // Accessors
    // =========================================================================

    /** Observable dello stato corrente dello streaming */
    get streamState$(): Observable<AnalyticsStreamState> {
        return this._streamState.asObservable();
    }

    /** Observable dei messaggi analytics */
    get messages$(): Observable<AnalyticsMessage[]> {
        return this._messages.asObservable();
    }

    /** Observable dell'ID del messaggio corrente */
    get currentMessageId$(): Observable<string | null> {
        return this._currentMessageId.asObservable();
    }

    /** Stato corrente (snapshot) */
    get currentState(): AnalyticsStreamState {
        return this._streamState.getValue();
    }

    /** Verifica se lo streaming è attivo */
    get isStreaming(): boolean {
        return this._streamState.getValue().isStreaming;
    }

    // =========================================================================
    // Public Methods - Streaming
    // =========================================================================

    /**
     * Avvia lo streaming analytics con supporto per grafici e predizioni.
     * I risultati vengono emessi progressivamente tramite streamState$.
     * 
     * @param message - Messaggio dell'utente
     * @param tenantCode - Codice del tenant per filtrare i dati
     * @param conversationId - ID della conversazione (opzionale)
     */
    async streamAnalytics(
        message: string,
        tenantCode: string,
        conversationId?: string
    ): Promise<void> {
        // Reset dello stato
        this._resetState();
        this._updateState({ isStreaming: true });

        const request: AnalyticsStreamRequest = {
            userId: this._authService.user?.id,
            conversationId: conversationId,
            message: message,
            tenantCode: tenantCode,
        };

        // Crea AbortController per permettere la cancellazione
        this._abortController = new AbortController();

        try {
            const response = await fetch(`${this._apiUrl}/stream`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': `Bearer ${this._authService.accessToken}`,
                },
                body: JSON.stringify(request),
                signal: this._abortController.signal,
            });

            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`);
            }

            const reader = response.body?.getReader();
            const decoder = new TextDecoder();
            let buffer = '';

            if (!reader) {
                throw new Error('No reader available');
            }

            // Legge lo stream SSE
            while (true) {
                const { done, value } = await reader.read();

                if (done) break;

                buffer += decoder.decode(value, { stream: true });

                // Parsing eventi SSE
                const { parsed, remaining } = this._parseSSEEvents(buffer);
                buffer = remaining;

                // Gestisce ogni evento
                for (const event of parsed) {
                    this._handleStreamEvent(event);
                }
            }
        } catch (error: any) {
            if (error.name === 'AbortError') {
                console.log('[AnalyticsStreaming] Stream cancelled by user');
            } else {
                console.error('[AnalyticsStreaming] Error:', error);
                this._updateState({
                    error: error.message || 'Errore durante lo streaming',
                });
            }
        } finally {
            this._updateState({ isStreaming: false, isProcessingTools: false });
            this._abortController = null;
        }
    }

    /**
     * Ferma lo streaming in corso
     */
    stopStreaming(): void {
        if (this._abortController) {
            this._abortController.abort();
            this._abortController = null;
        }
    }

    /**
     * Resetta lo stato del servizio
     */
    resetState(): void {
        this._resetState();
    }

    // =========================================================================
    // Public Methods - Non-Streaming API Calls
    // =========================================================================

    /**
     * Esegue un'analisi completa (non streaming)
     */
    analyze(request: AnalyticsRequest): Observable<AnalyticsResponse> {
        return this._httpClient.post<AnalyticsResponse>(
            `${this._apiUrl}/analyze`,
            request
        );
    }

    /**
     * Ottiene una smart prediction (sceglie automaticamente la fonte migliore)
     */
    getSmartPrediction(request: PredictionRequest): Observable<SmartPredictionResult> {
        return this._httpClient.post<SmartPredictionResult>(
            `${this._apiUrl}/predict`,
            request
        );
    }

    /**
     * Confronta le predizioni (database vs ML)
     */
    comparePredictions(request: PredictionRequest): Observable<PredictionComparisonResult> {
        return this._httpClient.post<PredictionComparisonResult>(
            `${this._apiUrl}/predict/compare`,
            request
        );
    }

    // =========================================================================
    // Private Methods - SSE Parsing
    // =========================================================================

    /**
     * Parsing degli eventi SSE dal buffer
     */
    private _parseSSEEvents(buffer: string): {
        parsed: AnalyticsStreamEvent[];
        remaining: string;
    } {
        const events: AnalyticsStreamEvent[] = [];
        const lines = buffer.split('\n');
        let remaining = '';
        let currentEvent: Partial<AnalyticsStreamEvent> = {};

        for (let i = 0; i < lines.length; i++) {
            const line = lines[i];

            if (line.startsWith('event: ')) {
                currentEvent.type = line.substring(7) as AnalyticsEventType;
            } else if (line.startsWith('data: ')) {
                try {
                    currentEvent.data = JSON.parse(line.substring(6));
                } catch {
                    currentEvent.data = line.substring(6);
                }
            } else if (line === '' && currentEvent.type) {
                events.push(currentEvent as AnalyticsStreamEvent);
                currentEvent = {};
            }
        }

        // Mantieni eventi incompleti nel buffer
        if (currentEvent.type || currentEvent.data) {
            remaining = lines.slice(-2).join('\n');
        }

        return { parsed: events, remaining };
    }

    // =========================================================================
    // Private Methods - Event Handling
    // =========================================================================

    /**
     * Gestisce un singolo evento SSE
     */
    private _handleStreamEvent(event: AnalyticsStreamEvent): void {
        // Esegui nel NgZone per triggere change detection
        this._ngZone.run(() => {
            switch (event.type) {
                case 'start':
                    this._handleStartEvent(event.data as StartEventData);
                    break;

                case 'text':
                    this._handleTextEvent(event.data as TextEventData);
                    break;

                case 'chart':
                    this._handleChartEvent(event.data as ChartData);
                    break;

                case 'prediction':
                    this._handlePredictionEvent(event.data as PredictionData);
                    break;

                case 'processing':
                    this._handleProcessingEvent(event.data as ProcessingEventData);
                    break;

                case 'complete':
                    this._handleCompleteEvent(event.data as CompleteEventData);
                    break;

                case 'error':
                    this._handleErrorEvent(event.data as ErrorEventData);
                    break;

                case 'cancelled':
                    console.log('[AnalyticsStreaming] Stream cancelled');
                    break;

                default:
                    console.warn('[AnalyticsStreaming] Unknown event type:', event.type);
            }
        });
    }

    private _handleStartEvent(data: StartEventData): void {
        console.log('[AnalyticsStreaming] Stream started:', data);
        this._currentMessageId.next(data.messageId);
    }

    private _handleTextEvent(data: TextEventData): void {
        const currentState = this._streamState.getValue();
        this._updateState({
            textContent: currentState.textContent + (data.content || ''),
            isProcessingTools: false,
        });
    }

    private _handleChartEvent(chartData: ChartData): void {
        console.log('[AnalyticsStreaming] Chart received:', chartData.title);
        const currentState = this._streamState.getValue();
        this._updateState({
            charts: [...currentState.charts, chartData],
        });
    }

    private _handlePredictionEvent(prediction: PredictionData): void {
        console.log('[AnalyticsStreaming] Prediction received:', prediction);
        this._updateState({
            prediction: prediction,
        });
    }

    private _handleProcessingEvent(data: ProcessingEventData): void {
        console.log('[AnalyticsStreaming] Processing:', data.message);
        this._updateState({
            isProcessingTools: true,
        });
    }

    private _handleCompleteEvent(data: CompleteEventData): void {
        console.log('[AnalyticsStreaming] Stream completed:', {
            chartsCount: data.chartsCount,
            hasPrediction: data.hasPrediction,
        });
        this._updateState({
            isStreaming: false,
            isProcessingTools: false,
        });

        // Salva il messaggio finale
        this._saveMessage(data);
    }

    private _handleErrorEvent(data: ErrorEventData): void {
        console.error('[AnalyticsStreaming] Error:', data.message);
        this._updateState({
            error: data.message,
            isStreaming: false,
            isProcessingTools: false,
        });
    }

    // =========================================================================
    // Private Methods - State Management
    // =========================================================================

    private _updateState(partialState: Partial<AnalyticsStreamState>): void {
        const currentState = this._streamState.getValue();
        this._streamState.next({
            ...currentState,
            ...partialState,
        });
    }

    private _resetState(): void {
        this._streamState.next({
            isStreaming: false,
            isProcessingTools: false,
            textContent: '',
            charts: [],
            prediction: undefined,
            error: undefined,
        });
        this._currentMessageId.next(null);
    }

    private _saveMessage(completeData: CompleteEventData): void {
        const currentState = this._streamState.getValue();
        const messages = this._messages.getValue();

        const newMessage: AnalyticsMessage = {
            id: completeData.messageId,
            conversationId: '', // Può essere passato dal completeData se necessario
            content: currentState.textContent,
            isAnswer: true,
            isLoading: false,
            charts: currentState.charts,
            prediction: currentState.prediction,
            timestamp: new Date(),
        };

        this._messages.next([...messages, newMessage]);
    }
}
