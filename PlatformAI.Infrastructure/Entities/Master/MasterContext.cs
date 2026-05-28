using Microsoft.EntityFrameworkCore;
using PlatformAI.Infrastructure.Master;

namespace PlatformAI.Infrastructure;

public class MasterContext : DbContext, IAppDbContext
{
    public DbSet<Country> Countries { get; set; }
    public DbSet<Tenant> Tenants { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Setting> Settings { get; set; }
    public DbSet<UserSetting> UserSettings { get; set; }
    public DbSet<Server> Servers { get; set; }
      //public DbSet<Device> Devices { get; set; }

    public MasterContext()
    {

    }
     public MasterContext(DbContextOptions<MasterContext> options) : base(options) { }
    // The following configures EF to create a Sqlite database file in the
    // special "local" folder for your platform.
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // if (CommonConstants.DbMasterPath == null)
        // {
        //    Configuration.SetConfiguration(CommonConstants.GlobalConfigurationType); 
        // }//BTCMasterTest BTCMasterSandbox
        // //CommonConstants.DbMasterPath ="Server=tcp:b2aserver.database.windows.net,1433;Initial Catalog=BTCMasterTest;Persist Security Info=False;User ID=B2Administrator;Password=B24dm1n1strator;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;";
        // optionsBuilder.UseSqlServer(CommonConstants.DbMasterPath);
        //.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        /*mappatura della chiave primaria autogenerata */
          var baseType = typeof(BaseEntity);

          var entityTypes = modelBuilder.Model
              .GetEntityTypes()
              .Where(t => baseType.IsAssignableFrom(t.ClrType) && t.ClrType != baseType)
              .ToList();

          foreach (var entityType in entityTypes)
          {
              modelBuilder.Entity(entityType.ClrType)
                  .Property(nameof(BaseEntity.Id))
                  .HasDefaultValueSql("NEWSEQUENTIALID()");
          }
        /* fine mappatura */

        modelBuilder.Entity<UserRole>()
          .HasMany(e => e.UserFunctions)
          .WithMany(e => e.UserRoles)
          .UsingEntity<UserRoleFunctionTuple>(
              l => l.HasOne<UserFunction>(e => e.UserFunction).WithMany(e => e.UserRoleFunctionTuples).HasForeignKey(e => e.UserFunctionId),
              r => r.HasOne<UserRole>(e => e.UserRole).WithMany(e => e.UserRoleFunctionTuples).HasForeignKey(e => e.UserRoleId));


    }
}


