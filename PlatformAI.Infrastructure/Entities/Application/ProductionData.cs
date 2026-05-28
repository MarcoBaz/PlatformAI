using System;

namespace PlatformAI.Infrastructure.Application;

public class ProductionData : Entity
{
    public Guid MachineId { get; set; }
    public Machine Machine { get; set; } = null!;

    public Guid ProductionOrderId { get; set; }
    public ProductionOrder ProductionOrder { get; set; } = null!;

    public DateTime Timestamp { get; set; }

    public Guid MetricTypeId { get; set; }
    public MetricType MetricType { get; set; } = null!;

    /// <summary>
    /// Valore misurato per questa metrica
    /// </summary>
    public decimal Value { get; set; }


}
