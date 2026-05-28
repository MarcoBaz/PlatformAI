/**
 * AI Assistant Types
 */

import { ChartData, PredictionData } from './analytics-streaming.types';

export interface ConversationVM {
    id: string;
    title: string;
    userId: string;
    messages: MessageVM[];
    lastModifiedDate?: Date;
}

export interface MessageVM {
    id: string;
    conversationId: string;
    content: string;
    isAnswer: boolean;
    isLoading: boolean;
    lastModifiedDate?: Date;
    charts?: ChartData[];
    chartsJson?: string;
    prediction?: PredictionData;
}

export interface ConversationRequest {
    userId: string;
    message: MessageVM;
}

/**
 * Request per lo streaming SSE
 */
export interface StreamingRequest {
    userId: string;
    conversationId: string;
    message: string;
}

/**
 * Evento SSE dal backend
 */
export interface StreamingEvent {
    type: 'start' | 'chunk' | 'complete' | 'error' | 'cancelled';
    data: any;
}
