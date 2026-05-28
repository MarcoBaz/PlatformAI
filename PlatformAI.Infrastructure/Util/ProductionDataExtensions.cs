using PlatformAI.Infrastructure.Application;

namespace PlatformAI.Infrastructure;

/// <summary>
/// Extension methods per lavorare con collezioni di ProductionData nel nuovo modello
/// (un record per metrica per timestamp).
/// </summary>
public static class ProductionDataExtensions
{
    /// <summary>
    /// Restituisce il valore di una metrica specifica da una collezione di letture
    /// relative allo stesso snapshot (stessa macchina, stesso timestamp).
    /// </summary>
    public static decimal GetMetricValue(this IEnumerable<ProductionData> readings,
        string metricName, decimal defaultValue = 0)
        => readings.FirstOrDefault(r =>
                string.Equals(r.MetricType?.Name, metricName, StringComparison.OrdinalIgnoreCase))
            ?.Value ?? defaultValue;

    /// <summary>
    /// Raggruppa una lista piatta di ProductionData in snapshot ordinati per timestamp.
    /// Ogni snapshot è l'insieme di tutte le metriche per una coppia (MachineId, Timestamp).
    /// </summary>
    public static List<MachineSnapshot> ToSnapshots(this IEnumerable<ProductionData> data)
        => data
            .GroupBy(d => (d.MachineId, d.Timestamp))
            .Select(g => new MachineSnapshot(g.Key.MachineId, g.Key.Timestamp, g.ToList()))
            .OrderBy(s => s.Timestamp)
            .ToList();
}

/// <summary>
/// Rappresenta tutte le metriche misurate per una macchina in un dato istante.
/// </summary>
public record MachineSnapshot(Guid MachineId, DateTime Timestamp, IReadOnlyList<ProductionData> Readings)
{
    public decimal GetMetric(string metricName, decimal defaultValue = 0)
        => Readings.GetMetricValue(metricName, defaultValue);
}
