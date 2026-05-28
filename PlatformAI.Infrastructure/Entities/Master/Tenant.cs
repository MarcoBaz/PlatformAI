

using System.ComponentModel.DataAnnotations.Schema;

namespace PlatformAI.Infrastructure.Master;

[Table("Tenants")]
public class Tenant : Entity
{
    public string Code { get; set; }
    public string Name { get; set; }
    public string? Description { get; set; }
    public string Street { get; set; }
    public string? Number { get; set; }
    public string City { get; set; }
    public string Province { get; set; }
    public Guid? CountryId { get; set; }
    public Country? Country { get; set; }
    public List<Setting> Settings { get; set; }
    public List<User> Users { get; set; }

    public List<TenantCompany> TenantCompanies { get; set; }
}
