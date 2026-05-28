


namespace PlatformAI.Infrastructure.Master;

public class UserRoleFunctionTuple : BaseEntity
{
    public Guid UserFunctionId { get; set; }
    public Guid UserRoleId { get; set; }
    public UserFunction UserFunction { get; set; } = null!;
    public UserRole UserRole { get; set; } = null!;
    public List<TenantCompany> TenantCompanies { get; } = [];
}
