using System;

namespace PlatformAI.Infrastructure.Application;

 public class ProductionData : Entity
    {
        public Guid MachineId { get; set; }
        public Machine Machine { get; set; } = null!;

        public Guid ProductionOrderId { get; set; }
        public ProductionOrder ProductionOrder { get; set; } = null!;

        public DateTime Timestamp { get; set; }
        public int QuantityProduced { get; set; }
        public int ScrapQuantity { get; set; }
        public decimal CycleTime { get; set; }
        public decimal EnergyConsumption { get; set; }
        public decimal Temperature { get; set; }
    }
