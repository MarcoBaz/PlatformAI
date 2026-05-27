using System;

namespace PlatformAI.Infrastructure.Application;

/// <summary>
/// Valore di una specifica metrica registrato per un record di produzione.
/// Tabella di giunzione tra ProductionData e MetricType.
/// </summary>
public class ProductionDataMetric : Entity
{
    public Guid ProductionDataId { get; set; }
    public ProductionData ProductionData { get; set; } = null!;

    public Guid MetricTypeId { get; set; }
    public MetricType MetricType { get; set; } = null!;

    /// <summary>
    /// Valore misurato per questa metrica
    /// </summary>
    public decimal Value { get; set; }
}
