namespace PlatformAI.NLP.Models;

/// <summary>
/// Risposta LLM con grafici opzionali
/// </summary>
public class LLMResponseWithCharts
{
    /// <summary>
    /// Risposta testuale dell'AI
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Lista di grafici generati (vuota se non richiesti)
    /// </summary>
    public List<ChartData> Charts { get; set; } = new();

    /// <summary>
    /// Indica se la risposta contiene grafici
    /// </summary>
    public bool HasCharts => Charts.Count > 0;

    /// <summary>
    /// Dati di predizione ML opzionali
    /// </summary>
    public PredictionData? Prediction { get; set; }
}

/// <summary>
/// Dati per un grafico (compatibile con Chart.js)
/// </summary>
public class ChartData
{
    /// <summary>
    /// Identificatore univoco del grafico
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Tipo di grafico: line, bar, pie, doughnut, radar, scatter
    /// </summary>
    public string Type { get; set; } = ChartType.Line;

    /// <summary>
    /// Titolo del grafico
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Sottotitolo/descrizione
    /// </summary>
    public string? Subtitle { get; set; }

    /// <summary>
    /// Labels per l'asse X (es: date, categorie)
    /// </summary>
    public List<string> Labels { get; set; } = new();

    /// <summary>
    /// Serie di dati (supporta più serie per grafico)
    /// </summary>
    public List<ChartDataset> Datasets { get; set; } = new();

    /// <summary>
    /// Opzioni aggiuntive per il grafico
    /// </summary>
    public ChartOptions? Options { get; set; }
}

/// <summary>
/// Dataset singolo per un grafico
/// </summary>
public class ChartDataset
{
    /// <summary>
    /// Nome della serie (mostrato in legenda)
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Valori numerici
    /// </summary>
    public List<double> Data { get; set; } = new();

    /// <summary>
    /// Colore di sfondo (es: "rgba(54, 162, 235, 0.5)")
    /// </summary>
    public string? BackgroundColor { get; set; }

    /// <summary>
    /// Colore del bordo
    /// </summary>
    public string? BorderColor { get; set; }

    /// <summary>
    /// Spessore del bordo
    /// </summary>
    public int BorderWidth { get; set; } = 2;

    /// <summary>
    /// Riempimento sotto la linea (per grafici a linea)
    /// </summary>
    public bool Fill { get; set; } = false;

    /// <summary>
    /// Tensione della curva (0 = linee rette, 0.4 = curve morbide)
    /// </summary>
    public double Tension { get; set; } = 0.4;
}

/// <summary>
/// Opzioni per la configurazione del grafico
/// </summary>
public class ChartOptions
{
    /// <summary>
    /// Mostra/nascondi legenda
    /// </summary>
    public bool ShowLegend { get; set; } = true;

    /// <summary>
    /// Posizione legenda: top, bottom, left, right
    /// </summary>
    public string LegendPosition { get; set; } = "top";

    /// <summary>
    /// Label asse X
    /// </summary>
    public string? XAxisLabel { get; set; }

    /// <summary>
    /// Label asse Y
    /// </summary>
    public string? YAxisLabel { get; set; }

    /// <summary>
    /// Inizia asse Y da zero
    /// </summary>
    public bool BeginAtZero { get; set; } = true;

    /// <summary>
    /// Grafico responsivo
    /// </summary>
    public bool Responsive { get; set; } = true;
}

/// <summary>
/// Risultato del confronto tra predizione database e modello ML
/// </summary>
public class PredictionComparisonResult
{
    /// <summary>
    /// Predizione basata su media mobile dal database
    /// </summary>
    public PredictionData DatabasePrediction { get; set; } = new();

    /// <summary>
    /// Predizione dal modello ML addestrato
    /// </summary>
    public PredictionData MLModelPrediction { get; set; } = new();

    /// <summary>
    /// Differenza percentuale tra le due predizioni
    /// </summary>
    public double? PercentageDifference { get; set; }

    /// <summary>
    /// Metodo di predizione consigliato ("ML", "Database", "Entrambi")
    /// </summary>
    public string RecommendedPrediction { get; set; } = string.Empty;

    /// <summary>
    /// Sommario testuale del confronto in formato Markdown
    /// </summary>
    public string ComparisonSummary { get; set; } = string.Empty;

    /// <summary>
    /// Indica se entrambe le predizioni sono disponibili
    /// </summary>
    public bool BothAvailable => 
        DatabasePrediction.PredictedValue > 0 && MLModelPrediction.PredictedValue > 0;
}

#region Smart Prediction Models

/// <summary>
/// Fonte della predizione selezionata
/// </summary>
public enum PredictionSource
{
    /// <summary>Predizione basata su media mobile dal database</summary>
    Database,
    /// <summary>Predizione dal modello ML addestrato</summary>
    MLModel,
    /// <summary>Predizione ibrida (media pesata DB + ML)</summary>
    Hybrid
}

/// <summary>
/// Configurazione per il fine tuning della smart prediction.
/// Permette di personalizzare le soglie di decisione.
/// </summary>
public class SmartPredictionConfig
{
    /// <summary>
    /// Soglia R² sopra la quale si usa solo il modello ML.
    /// Default: 0.75 (75% varianza spiegata)
    /// </summary>
    public double R2ThresholdHigh { get; set; } = 0.75;

    /// <summary>
    /// Soglia R² sotto la quale si usa solo il database.
    /// Default: 0.5 (50% varianza spiegata)
    /// </summary>
    public double R2ThresholdLow { get; set; } = 0.5;

    /// <summary>
    /// Peso del modello ML nella predizione ibrida (0-1).
    /// Default: 0.7 (70% ML, 30% Database)
    /// </summary>
    public double MLWeightInHybrid { get; set; } = 0.7;

    /// <summary>
    /// Moltiplicatore per la confidenza nella predizione ibrida.
    /// Default: 0.8 (riduce la confidenza del 20% per l'incertezza)
    /// </summary>
    public double HybridConfidenceMultiplier { get; set; } = 0.8;

    /// <summary>
    /// Configurazione di default
    /// </summary>
    public static SmartPredictionConfig Default => new();

    /// <summary>
    /// Configurazione conservativa (preferisce il database)
    /// </summary>
    public static SmartPredictionConfig Conservative => new()
    {
        R2ThresholdHigh = 0.85,
        R2ThresholdLow = 0.65,
        MLWeightInHybrid = 0.5,
        HybridConfidenceMultiplier = 0.7
    };

    /// <summary>
    /// Configurazione aggressiva (preferisce ML)
    /// </summary>
    public static SmartPredictionConfig Aggressive => new()
    {
        R2ThresholdHigh = 0.6,
        R2ThresholdLow = 0.4,
        MLWeightInHybrid = 0.8,
        HybridConfidenceMultiplier = 0.9
    };

    public override string ToString() =>
        $"R²High={R2ThresholdHigh:F2}, R²Low={R2ThresholdLow:F2}, MLWeight={MLWeightInHybrid:P0}";
}

/// <summary>
/// Risultato della smart prediction con metadata completi
/// </summary>
public class SmartPredictionResult
{
    /// <summary>Target richiesto (quantity, scrap, energy)</summary>
    public string RequestedTarget { get; set; } = string.Empty;

    /// <summary>Timestamp della predizione</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>Fonte selezionata per la predizione</summary>
    public PredictionSource SelectedSource { get; set; }

    /// <summary>Motivo della selezione</summary>
    public string SelectionReason { get; set; } = string.Empty;

    /// <summary>Predizione finale selezionata</summary>
    public PredictionData Prediction { get; set; } = new();

    /// <summary>Confronto completo tra le fonti (opzionale)</summary>
    public PredictionComparisonResult? Comparison { get; set; }

    /// <summary>Configurazione usata per la decisione</summary>
    public SmartPredictionConfig? ConfigUsed { get; set; }

    /// <summary>Metriche di qualità della predizione</summary>
    public PredictionQualityMetrics? QualityMetrics { get; set; }

    /// <summary>Valore predetto (shortcut)</summary>
    public double PredictedValue => Prediction.PredictedValue;

    /// <summary>Confidenza della predizione (shortcut)</summary>
    public double? Confidence => Prediction.Confidence;
}

/// <summary>
/// Metriche di qualità per la predizione
/// </summary>
public class PredictionQualityMetrics
{
    /// <summary>R² del modello ML (se disponibile)</summary>
    public double? R2Score { get; set; }

    /// <summary>RMSE del modello ML (se disponibile)</summary>
    public double? RMSE { get; set; }

    /// <summary>Differenza percentuale rispetto al baseline (database)</summary>
    public double? DifferenceFromBaseline { get; set; }

    /// <summary>Numero di data points usati</summary>
    public int DataPointsUsed { get; set; }

    /// <summary>Indica se il modello ML è disponibile e funzionante</summary>
    public bool IsMLModelAvailable { get; set; }
}

#endregion

#region Backtest Models

/// <summary>
/// Risultato del backtesting
/// </summary>
public class BacktestResult
{
    public string TenantCode { get; set; } = string.Empty;
    public int TestPeriodDays { get; set; }
    public SmartPredictionConfig? ConfigTested { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration => EndTime - StartTime;

    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }

    public int TotalPredictions { get; set; }
    public List<BacktestPrediction> Predictions { get; set; } = new();

    /// <summary>Metriche per predizioni database</summary>
    public BacktestMetrics? DatabaseMetrics { get; set; }

    /// <summary>Metriche per predizioni ML</summary>
    public BacktestMetrics? MLMetrics { get; set; }

    /// <summary>Metriche per smart prediction</summary>
    public BacktestMetrics? SmartMetrics { get; set; }

    /// <summary>Breakdown delle selezioni per fonte</summary>
    public Dictionary<PredictionSource, int> SelectionBreakdown { get; set; } = new();

    /// <summary>
    /// Genera un report testuale del backtest
    /// </summary>
    public string GenerateReport()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# Backtest Report");
        sb.AppendLine();
        sb.AppendLine($"**Tenant:** {TenantCode}");
        sb.AppendLine($"**Periodo:** {TestPeriodDays} giorni");
        sb.AppendLine($"**Predizioni totali:** {TotalPredictions}");
        sb.AppendLine($"**Durata test:** {Duration.TotalSeconds:F1}s");
        sb.AppendLine();

        if (!Success)
        {
            sb.AppendLine($"❌ **Errore:** {ErrorMessage}");
            return sb.ToString();
        }

        sb.AppendLine("## Metriche di Errore (MAPE - Mean Absolute Percentage Error)");
        sb.AppendLine();
        sb.AppendLine("| Metodo | MAPE | Mediana | Min | Max | StdDev |");
        sb.AppendLine("|--------|------|---------|-----|-----|--------|");
        
        if (DatabaseMetrics != null)
            sb.AppendLine($"| Database | {DatabaseMetrics.MeanAbsolutePercentageError:F2}% | {DatabaseMetrics.MedianError:F2}% | {DatabaseMetrics.MinError:F2}% | {DatabaseMetrics.MaxError:F2}% | {DatabaseMetrics.StandardDeviation:F2} |");
        
        if (MLMetrics != null)
            sb.AppendLine($"| ML Model | {MLMetrics.MeanAbsolutePercentageError:F2}% | {MLMetrics.MedianError:F2}% | {MLMetrics.MinError:F2}% | {MLMetrics.MaxError:F2}% | {MLMetrics.StandardDeviation:F2} |");
        
        if (SmartMetrics != null)
            sb.AppendLine($"| **Smart** | **{SmartMetrics.MeanAbsolutePercentageError:F2}%** | {SmartMetrics.MedianError:F2}% | {SmartMetrics.MinError:F2}% | {SmartMetrics.MaxError:F2}% | {SmartMetrics.StandardDeviation:F2} |");

        sb.AppendLine();
        sb.AppendLine("## Selezione Fonte");
        sb.AppendLine();
        foreach (var kvp in SelectionBreakdown)
        {
            var pct = (double)kvp.Value / TotalPredictions * 100;
            sb.AppendLine($"- **{kvp.Key}:** {kvp.Value} ({pct:F1}%)");
        }

        if (ConfigTested != null)
        {
            sb.AppendLine();
            sb.AppendLine("## Configurazione Testata");
            sb.AppendLine();
            sb.AppendLine($"- R² Threshold High: {ConfigTested.R2ThresholdHigh:F2}");
            sb.AppendLine($"- R² Threshold Low: {ConfigTested.R2ThresholdLow:F2}");
            sb.AppendLine($"- ML Weight in Hybrid: {ConfigTested.MLWeightInHybrid:P0}");
        }

        return sb.ToString();
    }
}

/// <summary>
/// Singola predizione nel backtest
/// </summary>
public class BacktestPrediction
{
    public DateTime Date { get; set; }
    public double ActualValue { get; set; }
    public double DatabasePrediction { get; set; }
    public double MLPrediction { get; set; }
    public double SmartPrediction { get; set; }
    public PredictionSource SelectedSource { get; set; }
    public double ErrorPercent { get; set; }
}

/// <summary>
/// Metriche aggregate del backtest
/// </summary>
public class BacktestMetrics
{
    /// <summary>Mean Absolute Percentage Error</summary>
    public double MeanAbsolutePercentageError { get; set; }
    
    /// <summary>Errore mediano</summary>
    public double MedianError { get; set; }
    
    /// <summary>Errore massimo</summary>
    public double MaxError { get; set; }
    
    /// <summary>Errore minimo</summary>
    public double MinError { get; set; }
    
    /// <summary>Deviazione standard</summary>
    public double StandardDeviation { get; set; }
}

#endregion

/// <summary>
/// Dati di predizione ML
/// </summary>
public class PredictionData
{
    /// <summary>
    /// Valore predetto
    /// </summary>
    public double PredictedValue { get; set; }

    /// <summary>
    /// Confidenza della predizione (0-1)
    /// </summary>
    public double? Confidence { get; set; }

    /// <summary>
    /// R² del modello usato
    /// </summary>
    public double? ModelRSquared { get; set; }

    /// <summary>
    /// RMSE del modello usato
    /// </summary>
    public double? ModelRMSE { get; set; }

    /// <summary>
    /// Features usate per la predizione
    /// </summary>
    public Dictionary<string, double>? Features { get; set; }

    /// <summary>
    /// Spiegazione AI della predizione
    /// </summary>
    public string? Explanation { get; set; }
}

/// <summary>
/// Tipi di grafici supportati
/// </summary>
public static class ChartType
{
    public const string Line = "line";
    public const string Bar = "bar";
    public const string Pie = "pie";
    public const string Doughnut = "doughnut";
    public const string Radar = "radar";
    public const string Scatter = "scatter";
    public const string Area = "area";
}

// /// <summary>
// /// Palette di colori per i grafici
// /// </summary>
public static class ChartColors
{
    public static readonly string Blue = "rgba(54, 162, 235, 1)";
    public static readonly string BlueFill = "rgba(54, 162, 235, 0.2)";
    
    public static readonly string Red = "rgba(255, 99, 132, 1)";
    public static readonly string RedFill = "rgba(255, 99, 132, 0.2)";
    
    public static readonly string Green = "rgba(75, 192, 192, 1)";
    public static readonly string GreenFill = "rgba(75, 192, 192, 0.2)";
    
    public static readonly string Yellow = "rgba(255, 206, 86, 1)";
    public static readonly string YellowFill = "rgba(255, 206, 86, 0.2)";
    
    public static readonly string Purple = "rgba(153, 102, 255, 1)";
    public static readonly string PurpleFill = "rgba(153, 102, 255, 0.2)";
    
    public static readonly string Orange = "rgba(255, 159, 64, 1)";
    public static readonly string OrangeFill = "rgba(255, 159, 64, 0.2)";

    public static readonly string[] Palette = new[]
    {
        Blue, Red, Green, Yellow, Purple, Orange
    };

    public static readonly string[] PaletteFill = new[]
    {
        BlueFill, RedFill, GreenFill, YellowFill, PurpleFill, OrangeFill
    };
}

#region Streaming Response Models

/// <summary>
/// Tipo di risposta streaming
/// </summary>
public enum StreamingResponseType
{
    /// <summary>Chunk di testo dalla risposta AI</summary>
    TextChunk,
    
    /// <summary>Dati di un grafico generato</summary>
    ChartData,
    
    /// <summary>Dati di una predizione ML</summary>
    PredictionData,
    
    /// <summary>Indicatore che i tools sono in elaborazione</summary>
    ProcessingTools,
    
    /// <summary>Risposta completata con successo</summary>
    Complete,
    
    /// <summary>Errore durante l'elaborazione</summary>
    Error
}

/// <summary>
/// Risposta streaming con supporto per grafici e predizioni.
/// Usato da GenerateResponseWithChartsStreamingAsync per restituire
/// i dati incrementalmente alla UI.
/// </summary>
public class StreamingChartResponse
{
    /// <summary>
    /// Tipo di questa risposta streaming
    /// </summary>
    public StreamingResponseType Type { get; set; }

    /// <summary>
    /// Contenuto testuale (per TextChunk, ProcessingTools, Error)
    /// </summary>
    public string? TextContent { get; set; }

    /// <summary>
    /// Dati del grafico (quando Type == ChartData)
    /// </summary>
    public ChartData? Chart { get; set; }

    /// <summary>
    /// Dati della predizione ML (quando Type == PredictionData)
    /// </summary>
    public PredictionData? Prediction { get; set; }

    /// <summary>
    /// Messaggio di errore (quando Type == Error)
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Numero totale di grafici generati (quando Type == Complete)
    /// </summary>
    public int TotalCharts { get; set; }

    /// <summary>
    /// Indica se è stata generata una predizione (quando Type == Complete)
    /// </summary>
    public bool HasPrediction { get; set; }

    /// <summary>
    /// Timestamp della risposta
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Helper per creare una risposta di tipo TextChunk
    /// </summary>
    public static StreamingChartResponse CreateTextChunk(string text) => new()
    {
        Type = StreamingResponseType.TextChunk,
        TextContent = text
    };

    /// <summary>
    /// Helper per creare una risposta di tipo ChartData
    /// </summary>
    public static StreamingChartResponse CreateChartResponse(ChartData chart) => new()
    {
        Type = StreamingResponseType.ChartData,
        Chart = chart
    };

    /// <summary>
    /// Helper per creare una risposta di tipo PredictionData
    /// </summary>
    public static StreamingChartResponse CreatePredictionResponse(PredictionData prediction) => new()
    {
        Type = StreamingResponseType.PredictionData,
        Prediction = prediction
    };

    /// <summary>
    /// Helper per creare una risposta di tipo Error
    /// </summary>
    public static StreamingChartResponse CreateError(string message) => new()
    {
        Type = StreamingResponseType.Error,
        ErrorMessage = message
    };

    /// <summary>
    /// Helper per creare una risposta di completamento
    /// </summary>
    public static StreamingChartResponse CreateComplete(int totalCharts = 0, bool hasPrediction = false) => new()
    {
        Type = StreamingResponseType.Complete,
        TotalCharts = totalCharts,
        HasPrediction = hasPrediction
    };
}

#endregion
