using System;

namespace PlatformAI.Infrastructure;

public interface IDbContextResolver
{
    IAppDbContext Resolve<TEntity>() where TEntity : class;
}

public class DbContextResolver : IDbContextResolver
{
    private readonly MasterContext _master;
    private readonly ApplicationContext _app;

    // Costruttore per DI - riceve i context già configurati
    public DbContextResolver(MasterContext master, ApplicationContext app)
    {
        _master = master;
        _app = app;
    }

    public IAppDbContext Resolve<TEntity>() where TEntity : class
    {
        // In base al tipo dell'entità, scegli il contesto giusto
        if (typeof(TEntity).Namespace?.Contains("Master") == true)
            return _master;

        return _app; // default
    }
}
