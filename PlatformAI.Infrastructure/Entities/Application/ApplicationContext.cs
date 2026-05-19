
using Microsoft.EntityFrameworkCore;
using PlatformAI.Infrastructure.Application;

namespace PlatformAI.Infrastructure;

public class ApplicationContext : DbContext, IAppDbContext
{
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Machine> Machines => Set<Machine>();
    public DbSet<CostCenter> CostCenters => Set<CostCenter>();
    public DbSet<MachineEvent> ProductionEvents => Set<MachineEvent>();
    public DbSet<ProductionData> ProductionData => Set<ProductionData>();
    public DbSet<ProductionLine> ProductionLines => Set<ProductionLine>();
    public DbSet<ProductionOrder> ProductionOrders => Set<ProductionOrder>();
    public DbSet<MachineEvent> MachineEvents => Set<MachineEvent>();
    public DbSet<Log> Logs => Set<Log>();
     public DbSet<Conversation> Conversations => Set<Conversation>();
    //public DbSet<Device> Devices { get; set; }

    public ApplicationContext()
    {

    }
    public ApplicationContext(DbContextOptions<ApplicationContext> options) : base(options) { }
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
        modelBuilder.Entity<Machine>()
               .HasOne(p => p.ProductionLine)
               .WithMany()
               .HasForeignKey(p => p.ProductionLineId)
               .OnDelete(DeleteBehavior.Restrict);


    }
}