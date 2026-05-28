using System;

namespace PlatformAI.Infrastructure.Application;

public class ProductionOrder : Entity
    {
        public string OrderNumber { get; set; } = string.Empty;
        public string ProductCode { get; set; } = string.Empty;
        public int PlannedQuantity { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public Guid ProductionLineId { get; set; }
        public ProductionLine ProductionLine { get; set; } = null!;
    }