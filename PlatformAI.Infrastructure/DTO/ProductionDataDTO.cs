namespace PlatformAI.Infrastructure.DTO;

public class ProductionDataDTO
{
    public string Id { get; set; } = string.Empty;
    public string MachineId { get; set; } = string.Empty;
    public string ProductionOrderId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Metriche inviate dalla macchina (chiave/valore liberi, es. "quantity_produced", "temperature").
    /// </summary>
    public Dictionary<string, decimal> Metrics { get; set; } = new();
}