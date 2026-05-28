/**
 * Analytics Streaming Types
 * Tipi per lo streaming AI con supporto per grafici e predizioni ML
 */

// ============================================================================
// Request Types
// ============================================================================

/**
 * Request per lo streaming analytics
 */
export interface AnalyticsStreamRequest {
    userId?: string;
    conversationId?: string;
    message: string;
    tenantCode: string;
}

/**
 * Request per analisi completa (non streaming)
 */
export interface AnalyticsRequest {
    message: string;
    tenantCode: string;
    conversationHistory?: ChatMessage[];
}

/**
 * Request per predizioni
 */
export interface PredictionRequest {
    tenantCode: string;
    target: 'quantity' | 'scrap' | 'energy';
    horizon?: 'next_shift' | 'tomorrow' | 'next_week';
    config?: SmartPredictionConfig;
}

/**
 * Messaggio di chat per la cronologia
 */
export interface ChatMessage {
    role: 'user' | 'assistant' | 'system';
    content: string;
}

// ============================================================================
// Response Types
// ============================================================================

/**
 * Response per analisi completa
 */
export interface AnalyticsResponse {
    content: string;
    charts: ChartData[];
    prediction?: PredictionData;
    hasCharts: boolean;
}

// ============================================================================
// Streaming Event Types
// ============================================================================

/**
 * Tipi di eventi SSE per lo streaming analytics
 */
export type AnalyticsEventType = 
    | 'start'       // Inizio streaming
    | 'text'        // Chunk di testo
    | 'chart'       // Dati grafico
    | 'prediction'  // Dati predizione
    | 'processing'  // Elaborazione tools
    | 'complete'    // Completamento
    | 'error'       // Errore
    | 'cancelled';  // Annullato

/**
 * Evento SSE generico per analytics
 */
export interface AnalyticsStreamEvent {
    type: AnalyticsEventType;
    data: any;
}

/**
 * Dati evento 'start'
 */
export interface StartEventData {
    conversationId: string;
    messageId: string;
    tenantCode: string;
}

/**
 * Dati evento 'text'
 */
export interface TextEventData {
    content: string;
}

/**
 * Dati evento 'processing'
 */
export interface ProcessingEventData {
    message: string;
}

/**
 * Dati evento 'complete'
 */
export interface CompleteEventData {
    messageId: string;
    content: string;
    chartsCount: number;
    hasPrediction: boolean;
    chartIds: string[];
}

/**
 * Dati evento 'error'
 */
export interface ErrorEventData {
    message: string;
}

// ============================================================================
// Chart Types (compatibile con Chart.js)
// ============================================================================

/**
 * Tipo di grafico supportato
 */
export type ChartType = 'line' | 'bar' | 'pie' | 'doughnut' | 'radar' | 'scatter' | 'area';

/**
 * Dati per un grafico
 */
export interface ChartData {
    id: string;
    type: ChartType;
    title: string;
    subtitle?: string;
    labels: string[];
    datasets: ChartDataset[];
    options?: ChartOptions;
}

/**
 * Dataset singolo per un grafico
 */
export interface ChartDataset {
    label: string;
    data: number[];
    backgroundColor?: string;
    borderColor?: string;
    borderWidth?: number;
    fill?: boolean;
    tension?: number;
}

/**
 * Opzioni per la configurazione del grafico
 */
export interface ChartOptions {
    showLegend?: boolean;
    legendPosition?: 'top' | 'bottom' | 'left' | 'right';
    xAxisLabel?: string;
    yAxisLabel?: string;
    beginAtZero?: boolean;
    responsive?: boolean;
}

// ============================================================================
// Prediction Types
// ============================================================================

/**
 * Dati di predizione ML
 */
export interface PredictionData {
    predictedValue: number;
    confidence?: number;
    modelRSquared?: number;
    modelRMSE?: number;
    features?: Record<string, number>;
    explanation?: string;
}

/**
 * Configurazione per smart prediction
 */
export interface SmartPredictionConfig {
    r2ThresholdHigh: number;
    r2ThresholdLow: number;
    mlWeightInHybrid: number;
    hybridConfidenceMultiplier: number;
}

/**
 * Fonte della predizione
 */
export type PredictionSource = 'Database' | 'MLModel' | 'Hybrid';

/**
 * Risultato della smart prediction
 */
export interface SmartPredictionResult {
    requestedTarget: string;
    timestamp: Date;
    selectedSource: PredictionSource;
    selectionReason: string;
    prediction: PredictionData;
    comparison?: PredictionComparisonResult;
    configUsed?: SmartPredictionConfig;
    qualityMetrics?: PredictionQualityMetrics;
    predictedValue: number;
    confidence?: number;
}

/**
 * Risultato del confronto predizioni
 */
export interface PredictionComparisonResult {
    databasePrediction: PredictionData;
    mlModelPrediction: PredictionData;
    percentageDifference?: number;
    recommendedPrediction: string;
    comparisonSummary: string;
    bothAvailable: boolean;
}

/**
 * Metriche di qualità della predizione
 */
export interface PredictionQualityMetrics {
    r2Score?: number;
    rmse?: number;
    differenceFromBaseline?: number;
    dataPointsUsed: number;
    isMLModelAvailable: boolean;
}

// ============================================================================
// UI State Types
// ============================================================================

/**
 * Stato dell'analytics streaming nella UI
 */
export interface AnalyticsStreamState {
    isStreaming: boolean;
    isProcessingTools: boolean;
    textContent: string;
    charts: ChartData[];
    prediction?: PredictionData;
    error?: string;
}

/**
 * Messaggio analytics con grafici
 */
export interface AnalyticsMessage {
    id: string;
    conversationId: string;
    content: string;
    isAnswer: boolean;
    isLoading: boolean;
    charts?: ChartData[];
    prediction?: PredictionData;
    timestamp?: Date;
}
