using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PlatformAI.Infrastructure.Master;

[Table("Settings")]
public class Setting : BaseEntity
{
    public string Value { get; set; }
    public Guid SettingKeyId { get; set; }
    public SettingKey SettingKey { get; set; }
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } =null!;
}
