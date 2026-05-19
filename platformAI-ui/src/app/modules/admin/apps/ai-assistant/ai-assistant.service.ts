import { HttpClient } from '@angular/common/http';
import { inject, Injectable, NgZone } from '@angular/core';
import { BehaviorSubject, Observable, map, of, tap } from 'rxjs';
import {
    ConversationRequest,
    ConversationVM,
    MessageVM,
    StreamingRequest,
} from './ai-assistant.types';
import { ChartData, PredictionData } from './analytics-streaming.types';
import { AuthService } from 'app/core/auth/auth.service';
import { environment } from 'environments/environment';

@Injectable({
    providedIn: 'root',
})
export class AiAssistantService {
    private readonly _apiUrl = environment.apiUrl;

    // BehaviorSubjects
    private _conversations: BehaviorSubject<ConversationVM[]> = new BehaviorSubject<ConversationVM[]>([]);
    public _currentConversation: BehaviorSubject<ConversationVM | null> = new BehaviorSubject<ConversationVM | null>(null);
    private _isLoading: BehaviorSubject<boolean> = new BehaviorSubject<boolean>(false);
    private _isStreaming: BehaviorSubject<boolean> = new BehaviorSubject<boolean>(false);

    // Controller per abort dello streaming
    private _abortController: AbortController | null = null;

    constructor(private _authService: AuthService, private _ngZone: NgZone) {}
    private _httpClient = inject(HttpClient);

    // -----------------------------------------------------------------------------------------------------
    // @ Accessors
    // -----------------------------------------------------------------------------------------------------

    get conversations$(): Observable<ConversationVM[]> {
        var user = this._authService.user;
        return this._httpClient.get(`${this._apiUrl}/conversation/getall/${user.id}`).pipe(
            map((response: any) => {
                // Deserializza chartsJson → charts per ogni messaggio
                const conversations: ConversationVM[] = response.map((conv: ConversationVM) => ({
                    ...conv,
                    messages: conv.messages.map((msg: MessageVM) => {
                        let charts = undefined;
                        if (msg.chartsJson) {
                            try {
                                const parsed = JSON.parse(msg.chartsJson);
                                // Normalizza PascalCase → camelCase per retrocompatibilità
                                charts = parsed.map((c: any) => ({
                                    id: c.id ?? c.Id,
                                    type: c.type ?? c.Type,
                                    title: c.title ?? c.Title,
                                    subtitle: c.subtitle ?? c.Subtitle,
                                    labels: c.labels ?? c.Labels ?? [],
                                    datasets: (c.datasets ?? c.Datasets ?? []).map((d: any) => ({
                                        label: d.label ?? d.Label,
                                        data: d.data ?? d.Data ?? [],
                                        backgroundColor: d.backgroundColor ?? d.BackgroundColor,
                                        borderColor: d.borderColor ?? d.BorderColor,
                                        borderWidth: d.borderWidth ?? d.BorderWidth,
                                        fill: d.fill ?? d.Fill,
                                        tension: d.tension ?? d.Tension,
                                    })),
                                    options: c.options ?? c.Options,
                                }));
                            } catch (e) {
                                console.error('[Charts parse error]', e);
                            }
                        }
                        return { ...msg, charts };
                    })
                }));
                this._conversations.next(conversations);
                return conversations;
            })
        );
    }

    get currentConversation$(): Observable<ConversationVM | null> {
        return this._currentConversation.asObservable();
    }

    get isLoading$(): Observable<boolean> {
        return this._isLoading.asObservable();
    }

    get isStreaming$(): Observable<boolean> {
        return this._isStreaming.asObservable();
    }

    // -----------------------------------------------------------------------------------------------------
    // @ Public methods
    // -----------------------------------------------------------------------------------------------------

    createNewConversation(): ConversationVM {
        const newConversation: ConversationVM = {
            id: this._generateId(),
            title: 'Nuova conversazione',
            userId: this._authService.user.id,
            messages: []
        };

        const conversations = this._conversations.getValue();
        this._conversations.next([newConversation, ...conversations]);
        this._currentConversation.next(newConversation);
        return newConversation;
    }

    selectConversation(conversationId: string): void {
        const conversations = this._conversations.getValue();
        const conversation = conversations.find((c) => c.id === conversationId);
        if (conversation) {
            this._currentConversation.next(conversation);
        }
    }

    deleteConversation(conversationId: string): void {
        const conversations = this._conversations.getValue();
        const filteredConversations = conversations.filter((c) => c.id !== conversationId);
        this._conversations.next(filteredConversations);

        const current = this._currentConversation.getValue();
        if (current?.id === conversationId) {
            this._currentConversation.next(
                filteredConversations.length > 0 ? filteredConversations[0] : null
            );
        }
    }

    /**
     * Invia messaggio con streaming della risposta (come ChatGPT)
     */
    async sendMessageWithStreaming(message: string): Promise<void> {
        let currentConversation = this._currentConversation.getValue();

        if (!currentConversation) {
            currentConversation = this.createNewConversation();
        }

        // 1. Aggiungi il messaggio dell'utente
        const userMessage: MessageVM = {
            id: this._generateId(),
            isAnswer: false,
            isLoading: false,
            content: message,
            conversationId: currentConversation.id,
        };

        currentConversation.messages = [...currentConversation.messages, userMessage];

        // 2. Aggiungi il placeholder per la risposta AI (con loading)
        const aiMessageId = this._generateId();
        const aiMessage: MessageVM = {
            id: aiMessageId,
            isAnswer: true,
            isLoading: true,
            content: '',
            conversationId: currentConversation.id,
        };

        currentConversation.messages = [...currentConversation.messages, aiMessage];

        // Aggiorna il titolo se è il primo messaggio
        if (currentConversation.messages.length === 2) {
            currentConversation.title = message.length > 30 
                ? message.substring(0, 30) + '...' 
                : message;
        }

        this._updateConversation(currentConversation);
        this._isLoading.next(true);
        this._isStreaming.next(true);

        // 3. Prepara la richiesta di streaming
        const streamRequest: StreamingRequest = {
            userId: this._authService.user.id,
            conversationId: currentConversation.id,
            message: message
        };

        // 4. Crea l'AbortController per poter cancellare lo streaming
        this._abortController = new AbortController();

        try {
            // 5. Fetch con streaming
            const response = await fetch(`${this._apiUrl}/analytics/stream`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': `Bearer ${this._authService.accessToken}`
                },
                body: JSON.stringify(streamRequest),
                signal: this._abortController.signal
            });

            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`);
            }

            // 6. Leggi lo stream SSE
            const reader = response.body?.getReader();
            const decoder = new TextDecoder();
            let buffer = '';

            if (!reader) {
                throw new Error('No reader available');
            }

            while (true) {
                const { done, value } = await reader.read();
                
                if (done) break;

                buffer += decoder.decode(value, { stream: true });

                // Processa gli eventi SSE nel buffer
                const events = this._parseSSEEvents(buffer);
                buffer = events.remaining;

                for (const event of events.parsed) {
                    this._handleSSEEvent(event, aiMessageId, currentConversation);
                }
            }

        } catch (error: any) {
            if (error.name === 'AbortError') {
                console.log('Streaming cancelled by user');
            } else {
                console.error('Streaming error:', error);
                // Aggiorna il messaggio con l'errore
                this._updateAIMessage(aiMessageId, currentConversation, 
                    'Mi dispiace, si è verificato un errore. Riprova più tardi.', false);
            }
        } finally {
            this._isLoading.next(false);
            this._isStreaming.next(false);
            this._abortController = null;
        }
    }

    /**
     * Ferma lo streaming in corso
     */
    stopStreaming(): void {
        if (this._abortController) {
            this._abortController.abort();
        }
    }

    /**
     * Invia messaggio (metodo legacy senza streaming)
     */
    sendMessage(message: string): Observable<ConversationVM> {
        // Usa il nuovo metodo con streaming
        this.sendMessageWithStreaming(message);
        return this._currentConversation.asObservable() as Observable<ConversationVM>;
    }

    clearCurrentConversation(): void {
        const conversation = this._currentConversation.getValue();
        if (conversation) {
            conversation.messages = [];
        }
    }

    // -----------------------------------------------------------------------------------------------------
    // @ Private methods
    // -----------------------------------------------------------------------------------------------------

    /**
     * Parsing degli eventi SSE dal buffer
     */
    private _parseSSEEvents(buffer: string): { parsed: SSEEvent[], remaining: string } {
        const events: SSEEvent[] = [];
        const lines = buffer.split('\n');
        let remaining = '';
        let currentEvent: Partial<SSEEvent> = {};

        for (let i = 0; i < lines.length; i++) {
            const line = lines[i];

            if (line.startsWith('event: ')) {
                currentEvent.type = line.substring(7);
            } else if (line.startsWith('data: ')) {
                try {
                    currentEvent.data = JSON.parse(line.substring(6));
                } catch {
                    currentEvent.data = line.substring(6);
                }
            } else if (line === '' && currentEvent.type) {
                events.push(currentEvent as SSEEvent);
                currentEvent = {};
            }
        }

        // Se c'è un evento incompleto, mantienilo nel buffer
        if (currentEvent.type || currentEvent.data) {
            remaining = lines.slice(-2).join('\n');
        }

        return { parsed: events, remaining };
    }

    /**
     * Gestisce un singolo evento SSE.
     * Il backend invia: start | text | chart | prediction | processing | complete | error | cancelled
     */
    private _handleSSEEvent(event: SSEEvent, aiMessageId: string, conversation: ConversationVM): void {
        this._ngZone.run(() => {
            switch (event.type) {
                case 'start':
                    console.log('Streaming started:', event.data);
                    break;

                // Il backend manda "text" (non "chunk") per i chunk di testo
                case 'text': {
                    const aiMsg = conversation.messages.find(m => m.id === aiMessageId);
                    if (aiMsg) {
                        aiMsg.content += event.data?.content || '';
                        aiMsg.isLoading = false;
                        this._updateConversation({ ...conversation });
                    }
                    break;
                }

                // Grafici generati dal tool get_production_data
                case 'chart': {
                    const aiMsg = conversation.messages.find(m => m.id === aiMessageId);
                    if (aiMsg && event.data) {
                        aiMsg.charts = [...(aiMsg.charts || []), event.data as ChartData];
                        this._updateConversation({ ...conversation });
                    }
                    break;
                }

                // Predizione ML generata dal tool get_prediction
                case 'prediction': {
                    const aiMsg = conversation.messages.find(m => m.id === aiMessageId);
                    if (aiMsg && event.data) {
                        aiMsg.prediction = event.data as PredictionData;
                        this._updateConversation({ ...conversation });
                    }
                    break;
                }

                case 'processing':
                    console.log('Processing tools:', event.data?.message);
                    break;

                case 'complete': {
                    // Il backend include il testo completo in event.data.content:
                    // lo usiamo solo come fallback se il testo non è arrivato via "text"
                    console.log('Streaming completed:', event.data);
                    const aiMsg = conversation.messages.find(m => m.id === aiMessageId);
                    if (aiMsg) {
                        if (!aiMsg.content && event.data?.content) {
                            aiMsg.content = event.data.content;
                        }
                        aiMsg.isLoading = false;
                        this._updateConversation({ ...conversation });
                    }
                    break;
                }

                case 'error':
                    console.error('Stream error:', event.data);
                    this._updateAIMessage(aiMessageId, conversation,
                        'Errore durante la generazione della risposta.', false);
                    break;

                case 'cancelled':
                    console.log('Streaming cancelled');
                    break;
            }
        });
    }

    /**
     * Aggiorna un messaggio AI specifico
     */
    private _updateAIMessage(
        messageId: string, 
        conversation: ConversationVM, 
        content: string, 
        isLoading: boolean
    ): void {
        const message = conversation.messages.find(m => m.id === messageId);
        if (message) {
            message.content = content;
            message.isLoading = isLoading;
            this._updateConversation({ ...conversation });
        }
    }

    private _updateConversation(conversation: ConversationVM): void {
        const conversations = this._conversations.getValue();
        const index = conversations.findIndex((c) => c.id === conversation.id);

        if (index !== -1) {
            conversations[index] = { ...conversation };
        } else {
            conversations.unshift(conversation);
        }

        this._conversations.next([...conversations]);
        this._currentConversation.next({ ...conversation });
    }

    private _generateId(): string {
        return crypto.randomUUID();
    }
}

/**
 * Interfaccia per evento SSE
 */
interface SSEEvent {
    type: string;
    data: any;
}
