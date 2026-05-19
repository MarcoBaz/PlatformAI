using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace PlatformAI.Infrastructure.Application;

public class Machine :Entity
{
        public Guid ProductionLineId { get; set; }
        public ProductionLine ProductionLine { get; set; } = null!;

        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Status { get; set; } = "Idle";

        public ICollection<ProductionData> ProductionData { get; set; } = new List<ProductionData>();
        public ICollection<MachineEvent> MachineEvents { get; set; } = new List<MachineEvent>();
}
