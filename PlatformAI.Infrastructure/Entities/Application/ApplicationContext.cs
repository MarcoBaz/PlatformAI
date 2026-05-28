
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
    public DbSet<MetricType> MetricTypes => Set<MetricType>();

    public ApplicationContext() { }

    public ApplicationContext(DbContextOptions<ApplicationContext> options) : base(options) { }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Mappatura chiave primaria autogenerata per tutte le entità che ereditano da BaseEntity
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

        modelBuilder.Entity<ProductionLine>()
            .HasMany(p => p.Machines)
            .WithOne(m => m.ProductionLine)
            .HasForeignKey(m => m.ProductionLineId)
            .OnDelete(DeleteBehavior.Restrict);

        // ProductionData → MetricType (ogni record è la lettura di una metrica specifica)
        modelBuilder.Entity<ProductionData>()
            .HasOne(pd => pd.MetricType)
            .WithMany()
            .HasForeignKey(pd => pd.MetricTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
