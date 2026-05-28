using System;

namespace PlatformAI.Infrastructure.Application;

public class MetricType : BaseEntity
{
    /// <summary>
    /// Nome della metrica (es. "Temperature", "QuantityProduced", "CycleTime", "EnergyConsumption")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Descrizione opzionale della metrica
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Unità di misura opzionale (es. "°C", "units", "s", "kWh")
    /// </summary>
    public string? Unit { get; set; }

}
