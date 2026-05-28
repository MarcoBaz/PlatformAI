
namespace PlatformAI.Infrastructure.Master;

// [Table("SettingKeys")]
public class SettingKey : BaseEntity
{
    public string Key { get; set; }
    public List<Setting> Settings { get; set; }
}
