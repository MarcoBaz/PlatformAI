

namespace PlatformAI.Infrastructure.Master;

public class UserRole : BaseEntity
{
    public string Code { get; set; }
    public string Description { get; set; }
    public List<UserRoleFunctionTuple> UserRoleFunctionTuples { get; } = [];
    public List<UserFunction> UserFunctions { get; set;} = [];
}
