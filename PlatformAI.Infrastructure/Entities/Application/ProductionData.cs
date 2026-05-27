using System;

namespace PlatformAI.Infrastructure.Application;

public class ProductionData : Entity
{
    public Guid MachineId { get; set; }
    public Machine Machine { get; set; } = null!;

    public Guid ProductionOrderId { get; set; }
    public ProductionOrder ProductionOrder { get; set; } = null!;

    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Valori delle metriche associate a questo record di produzione.
    /// Ogni metrica è definita in MetricType (es. Temperature, QuantityProduced, ...).
    /// </summary>
    public ICollection<ProductionDataMetric> Metrics { get; set; } = new List<ProductionDataMetric>();

    /// <summary>
    /// Legge il valore di una metrica per nome. Restituisce defaultValue se non presente.
    /// Richiede che Metrics e MetricType siano caricati (Include).
    /// </summary>
    public decimal GetMetric(string metricTypeName, decimal defaultValue = 0)
        => Metrics.FirstOrDefault(m => m.MetricType?.Name == metricTypeName)?.Value ?? defaultValue;
}
