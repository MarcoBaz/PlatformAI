
using System.ComponentModel.DataAnnotations.Schema;

namespace PlatformAI.Infrastructure.Master;

[Table("UserSetting")]
public class UserSetting : BaseEntity
{
    public string Code { get; set; } //univoco
    public string? Description { get; set; }
    public string Value { get; set; }
    public string PagePath { get; set; } = String.Empty;
     public bool Default { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; }
}
