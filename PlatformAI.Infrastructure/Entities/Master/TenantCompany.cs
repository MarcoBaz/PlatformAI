

namespace PlatformAI.Infrastructure.Master;

public class TenantCompany : Entity
{
    public string Code { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? CodeWS { get; set; }
     public string? ExternalCode { get; set; } //codice esterno vedi apot
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; }
    public List<UserRoleFunctionTuple> UserRoleFunctionTuples { get; } = [];
}
