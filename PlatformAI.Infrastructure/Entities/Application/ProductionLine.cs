using System;

namespace PlatformAI.Infrastructure.Application;

 public class ProductionLine : Entity
 {
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid DepartmentId { get; set; }
    public Department Department { get; set; } = null!;
    public ICollection<Machine> Machines { get; set; } = new List<Machine>();
}
