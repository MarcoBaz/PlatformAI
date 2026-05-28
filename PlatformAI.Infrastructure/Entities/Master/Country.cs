using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PlatformAI.Infrastructure;

namespace PlatformAI.Infrastructure.Master;

[Table("Countries")]
public class Country:BaseEntity
{
   public string Code { get; set; }
    public string Description { get; set; }
}