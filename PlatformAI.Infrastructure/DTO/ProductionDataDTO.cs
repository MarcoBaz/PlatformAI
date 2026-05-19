namespace PlatformAI.Infrastructure.DTO;

public class ProductionDataDTO 
    {
        public ProductionDataDTO()
        {
        
        }
        public string Id {get;set;}
        public string MachineId { get; set; }
        public string ProductionOrderId { get; set; }
        public DateTime Timestamp { get; set; }
        public float QuantityProduced { get; set; }
        public float ScrapQuantity { get; set; }
        public float CycleTime { get; set; }
        public float EnergyConsumption { get; set; }
        public float Temperature { get; set; }
    }