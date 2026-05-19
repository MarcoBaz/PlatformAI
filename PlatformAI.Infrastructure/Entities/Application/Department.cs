using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace PlatformAI.Infrastructure.Application;

public class Department : Entity
{
     public string Code { get; set; } = string.Empty;
     public string Name { get; set; } = string.Empty;
     public string TenantCode { get; set; } = string.Empty;

     public string? Description { get; set; }
     public bool IsActive { get; set; } = true;

     // Relazioni
     public ICollection<ProductionLine> ProductionLines { get; set; } = new List<ProductionLine>();
}
