using Microsoft.ML;

namespace PlatformAI.ML;

// ============================================================================
// CLASSI DI SUPPORTO
// ============================================================================



/// <summary>
/// DTO per dati arricchiti con feature engineering.
/// </summary>


public class IncrementalTrainingConfig
{
    public int MinDataPoints { get; set; } = 50;
    public bool IncludeHistoricalContext { get; set; } = true;
    public int HistoricalContextDays { get; set; } = 30;
    public int MaxHistoricalRecords { get; set; } = 500;
    public double HistoricalSamplingRatio { get; set; } = 1.0; // 0..1

    // ML params
    public int NumberOfTrees { get; set; } = 100;
    public int NumberOfLeaves { get; set; } = 40;
    public int MinimumExampleCountPerLeaf { get; set; } = 5;
    public double LearningRate { get; set; } = 0.1;
    public double TestFraction { get; set; } = 0.2; // il modello viene addestrato sull'80% dei dati e testato sul restante 20%
    public TrainerType Trainer { get; set; } = TrainerType.FastTree;

    // Model retention
    public int MaxModelVersionsToKeep { get; set; } = 5;
}
public enum TrainerType
{
    FastTree,
    Sdca
}

public class TrainingCheckpoint
{
    public string TenantCode { get; set; } = string.Empty;
    public DateTime LastProcessedDate { get; set; }
    public DateTime LastTrainingDate { get; set; }
    public string ModelVersion { get; set; } = string.Empty;
    public double RSquared { get; set; }
    public double RMSE { get; set; }
    public int RecordsProcessed { get; set; }

    public override string ToString() => $"Checkpoint: {LastProcessedDate:yyyy-MM-dd HH:mm:ss}, Model: {ModelVersion}, R²: {RSquared:F4}";
}

public class IncrementalTrainingResult
{
    public string TenantCode { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int NewRecordsCount { get; set; }
    public int TotalDataUsed { get; set; }
    public string? ModelVersion { get; set; }
    public double RSquared { get; set; }
    public double RMSE { get; set; }
    public double MAE { get; set; }
    public DateTime? PreviousCheckpoint { get; set; }
    public DateTime? NewCheckpoint { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration => EndTime - StartTime;
    public override string ToString() => Success ? $"✅ R²: {RSquared:F4}, RMSE: {RMSE:F2}, Nuovi: {NewRecordsCount}, Totale: {TotalDataUsed}, Tempo: {Duration.TotalSeconds:F1}s" : $"❌ {Message}";
}

public class TrainingResultInternal
{
    public ITransformer Model { get; set; } = null!;
    public string ModelVersion { get; set; } = string.Empty;
    public double RSquared { get; set; }
    public double RMSE { get; set; }
    public double MAE { get; set; }
    public int DataCount { get; set; }
}

public class ProductionDataEnriched
{
    // Metriche base — float per coerenza con l'intera pipeline ML.NET
    public float QuantityProduced { get; set; }
    public float ScrapQuantity { get; set; }
    public float CycleTime { get; set; }
    public float EnergyConsumption { get; set; }
    public float Temperature { get; set; }

    // Feature derivate
    public float ScrapRate { get; set; }
    public float EffectiveProductionRate { get; set; }
    public float EnergyPerUnit { get; set; }

    // Feature temporali
    public int HourOfDay { get; set; }
    public int DayOfWeek { get; set; }
    public int IsWeekend { get; set; }
    public int Shift { get; set; }

    // Rolling windows
    public float AvgQuantityLast3 { get; set; }
    public float AvgCycleTimeLast3 { get; set; }
    public float AvgTemperatureLast3 { get; set; }
    public float AvgEnergyLast3 { get; set; }
}


