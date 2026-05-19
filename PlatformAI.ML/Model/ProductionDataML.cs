using System;
using Microsoft.ML.Data;

namespace PlatformAI.ML;

public class ProductionDataML
{
    public float QuantityProduced { get; set; }
    public float ScrapQuantity { get; set; }
    public float CycleTime { get; set; }
    public float EnergyConsumption { get; set; }
    public float Temperature { get; set; }
}

public class ProductionPrediction
{
    [ColumnName("Score")]
    public float Score { get; set; }
}

/// <summary>
/// Risultato del training con metriche
/// </summary>
public class TrainingResult
{
    public double RSquared { get; set; }
    public double RMSE { get; set; }
    public double MAE { get; set; }
    public string ModelPath { get; set; } = string.Empty;
    public int DataCount { get; set; }
    public int TrainCount { get; set; }
    public int TestCount { get; set; }

    public bool IsGoodModel => RSquared > 0.5 && RMSE < 100; // Soglie configurabili

    public override string ToString()
    {
        return $"R²: {RSquared:F4}, RMSE: {RMSE:F2}, MAE: {MAE:F2}, Data: {DataCount} (Train: {TrainCount}, Test: {TestCount})";
    }
}
public class ProductionDataMLEnriched
{
    // ============================================================================
    // LABEL (Target) - Quello che il modello deve predire
    // ============================================================================
    
    /// <summary>
    /// Quantità prodotta - TARGET del modello
    /// </summary>
    public float QuantityProduced { get; set; }

    // ============================================================================
    // FEATURES BASE - Dati grezzi dalla produzione
    // ============================================================================
    
    /// <summary>
    /// Tempo di ciclo in minuti
    /// </summary>
    public float CycleTime { get; set; }

    /// <summary>
    /// Consumo energetico in kWh
    /// </summary>
    public float EnergyConsumption { get; set; }

    /// <summary>
    /// Temperatura in °C
    /// </summary>
    public float Temperature { get; set; }

    /// <summary>
    /// Quantità scartata (pezzi difettosi)
    /// </summary>
    public float ScrapQuantity { get; set; }

    // ============================================================================
    // FEATURES DERIVATE - Calcolate da dati base
    // ============================================================================
    
    /// <summary>
    /// Tasso di scarto = ScrapQuantity / QuantityProduced
    /// Range: 0.0 - 1.0 (es: 0.05 = 5% di scarto)
    /// </summary>
    public float ScrapRate { get; set; }

    /// <summary>
    /// Tasso di produzione effettivo = QuantityProduced / CycleTime
    /// Unità: pezzi/minuto
    /// </summary>
    public float EffectiveProductionRate { get; set; }

    /// <summary>
    /// Energia per unità = EnergyConsumption / QuantityProduced
    /// Unità: kWh/pezzo
    /// </summary>
    public float EnergyPerUnit { get; set; }

    // ============================================================================
    // FEATURES TEMPORALI - Per catturare pattern di tempo
    // ============================================================================
    
    /// <summary>
    /// Ora del giorno (0-23)
    /// Cattura pattern di turni di lavoro
    /// </summary>
    public float HourOfDay { get; set; }

    /// <summary>
    /// Giorno della settimana (0=Domenica, 1=Lunedì, ..., 6=Sabato)
    /// Cattura differenze di produzione per giorno
    /// </summary>
    public float DayOfWeek { get; set; }

    /// <summary>
    /// Indicatore weekend (0=Feriale, 1=Weekend)
    /// Produzione solitamente diversa nel weekend
    /// </summary>
    public float IsWeekend { get; set; }

    /// <summary>
    /// Turno di lavoro (1=Mattina 6-14, 2=Pomeriggio 14-22, 3=Notte 22-6)
    /// Cattura differenze tra turni
    /// </summary>
    public float Shift { get; set; }

    // ============================================================================
    // FEATURES ROLLING WINDOW - Trend recenti (ultimi 3 record)
    // ============================================================================
    
    /// <summary>
    /// Media quantità prodotta negli ultimi 3 cicli
    /// Cattura trend di produzione recente
    /// </summary>
    public float AvgQuantityLast3 { get; set; }

    /// <summary>
    /// Media tempo di ciclo negli ultimi 3 cicli
    /// Cattura rallentamenti o accelerazioni
    /// </summary>
    public float AvgCycleTimeLast3 { get; set; }

    /// <summary>
    /// Media temperatura negli ultimi 3 cicli
    /// Cattura riscaldamento o raffreddamento
    /// </summary>
    public float AvgTemperatureLast3 { get; set; }

    /// <summary>
    /// Media consumo energetico negli ultimi 3 cicli
    /// Cattura variazioni di consumo
    /// </summary>
    public float AvgEnergyLast3 { get; set; }

    // ============================================================================
    // METODI HELPER
    // ============================================================================

    /// <summary>
    /// Crea un esempio di features per testing
    /// </summary>
    public static ProductionDataMLEnriched CreateExample()
    {
        return new ProductionDataMLEnriched
        {
            // Features base
            CycleTime = 1.5f,              // 1.5 minuti per ciclo
            EnergyConsumption = 12.5f,     // 12.5 kWh
            Temperature = 45.0f,           // 45°C
            ScrapQuantity = 5,             // 5 pezzi scartati

            // Features derivate
            ScrapRate = 0.05f,             // 5% di scarto
            EffectiveProductionRate = 66.7f, // ~67 pezzi/minuto
            EnergyPerUnit = 0.125f,        // 0.125 kWh per pezzo

            // Features temporali
            HourOfDay = 10,                // 10:00 AM
            DayOfWeek = 2,                 // Martedì
            IsWeekend = 0,                 // Giorno feriale
            Shift = 1,                     // Turno mattina

            // Rolling window (se non disponibili, usa 0)
            AvgQuantityLast3 = 95.0f,
            AvgCycleTimeLast3 = 1.6f,
            AvgTemperatureLast3 = 44.0f,
            AvgEnergyLast3 = 12.0f,

            // Target (solo per training, non per predizione)
            QuantityProduced = 100
        };
    }

    /// <summary>
    /// Valida che tutte le features siano valide
    /// </summary>
    public bool IsValid(out string errorMessage)
    {
        if (CycleTime <= 0)
        {
            errorMessage = "CycleTime deve essere > 0";
            return false;
        }

        if (EnergyConsumption < 0)
        {
            errorMessage = "EnergyConsumption non può essere negativo";
            return false;
        }

        if (Temperature < -50 || Temperature > 200)
        {
            errorMessage = "Temperature fuori range (-50°C a 200°C)";
            return false;
        }

        if (ScrapQuantity < 0)
        {
            errorMessage = "ScrapQuantity non può essere negativo";
            return false;
        }

        if (HourOfDay < 0 || HourOfDay > 23)
        {
            errorMessage = "HourOfDay deve essere tra 0 e 23";
            return false;
        }

        if (DayOfWeek < 0 || DayOfWeek > 6)
        {
            errorMessage = "DayOfWeek deve essere tra 0 e 6";
            return false;
        }

        if (Shift < 1 || Shift > 3)
        {
            errorMessage = "Shift deve essere 1, 2 o 3";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    /// <summary>
    /// Rappresentazione testuale per debug
    /// </summary>
    public override string ToString()
    {
        return $"Cycle: {CycleTime:F2}min, Temp: {Temperature:F1}°C, " +
               $"Energy: {EnergyConsumption:F2}kWh, Scrap: {ScrapQuantity}, " +
               $"Hour: {HourOfDay}, Shift: {Shift}";
    }
}

