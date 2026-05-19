using System;

namespace PlatformAI.Infrastructure.Application;

public class CostCenter : Entity
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal HourlyCost { get; set; }
    }
