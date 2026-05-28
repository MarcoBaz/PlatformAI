
using System.ComponentModel.DataAnnotations.Schema;

namespace PlatformAI.Infrastructure.Master;

[Table("Users")]
public class User : Entity
{
    public string Name { get; set; }
    public string Surname { get; set; }
    public string Login { get; set; }
    public string Password { get; set; }
    public string? Email { get; set; }
    public string? MobilePhone { get; set; }
    public string? BearerToken { get; set; }
    public bool Enabled { get; set; }
    public Guid RoleId { get; set; }
    public UserRole Role { get; set; }
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; }
    public string LanguageCode { get; set; }
    public List<UserSetting> UserSettings { get; } = [];
}
