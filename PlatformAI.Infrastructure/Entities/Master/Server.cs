using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;


namespace PlatformAI.Infrastructure.Master;


public class Server : BaseEntity
{
    public string Code { get; set; }
    public string Description { get; set; }
    public string? IPAddress { get; set; }
    public int Port { get; set; }
    public string? StringConnectionDB { get; set; }
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; }
}
