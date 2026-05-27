
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
    public DbSet<ProductionDataMetric> ProductionDataMetrics => Set<ProductionDataMetric>();

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

        // Machine → ProductionLine (Restrict per evitare cascade delete multipli)
        modelBuilder.Entity<Machine>()
            .HasOne(p => p.ProductionLine)
            .WithMany()
            .HasForeignKey(p => p.ProductionLineId)
            .OnDelete(DeleteBehavior.Restrict);

        // ProductionDataMetric → ProductionData
        modelBuilder.Entity<ProductionDataMetric>()
            .HasOne(m => m.ProductionData)
            .WithMany(pd => pd.Metrics)
            .HasForeignKey(m => m.ProductionDataId)
            .OnDelete(DeleteBehavior.Cascade);

        // ProductionDataMetric → MetricType
        modelBuilder.Entity<ProductionDataMetric>()
            .HasOne(m => m.MetricType)
            .WithMany(mt => mt.ProductionDataMetrics)
            .HasForeignKey(m => m.MetricTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
