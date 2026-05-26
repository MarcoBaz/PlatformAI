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
    /// Metriche flessibili inviate dalla macchina (es. "quantity_produced", "temperature", "flow_rate", ...).
    /// Ogni costruttore può definire le proprie chiavi; i valori sono sempre decimali.
    /// Serializzato come JSON su DB tramite value converter EF Core.
    /// </summary>
    public Dictionary<string, decimal> Metrics { get; set; } = new();

    /// <summary>
    /// Legge una metrica per chiave. Restituisce defaultValue se la chiave non è presente.
    /// </summary>
    public decimal GetMetric(string key, decimal defaultValue = 0)
        => Metrics.TryGetValue(key, out var v) ? v : defaultValue;
}
