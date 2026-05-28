

namespace PlatformAI.Infrastructure.Master;

public class UserFunction : BaseEntity
{
    public string Code { get; set; }
    public string Description { get; set; }
    public List<UserRoleFunctionTuple> UserRoleFunctionTuples { get; } = [];
    public List<UserRole> UserRoles { get; } = [];
}
