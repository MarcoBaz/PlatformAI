using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace PlatformAI.Infrastructure;

public class UnitOfWork : IUnitOfWork
{
    private readonly IDbContextResolver _resolver;
    private readonly ICurrentUserService _currentUserService;
    private readonly Dictionary<Type, object> _repositories = new();

    //private DbContext _context => _resolver.Resolve<Entity>() as DbContext;
    private DbContext _context
    {
        get
        {
            //mi recupero antrambi i contesti per vedere dove ci sono delle modifiche
            var master = _resolver.Resolve<PlatformAI.Infrastructure.Master.User>() as DbContext;
            var app = _resolver.Resolve<Entity>() as DbContext;

            // Restituisce quello che ha modifiche pendenti nel ChangeTracker
            if (master?.ChangeTracker.HasChanges() == true)
                return master;

            return app;
        }
    }
    private IDbContextTransaction? _transaction;

    public UnitOfWork(IDbContextResolver resolver, ICurrentUserService currentUserService)
    {
        _resolver = resolver;
        _currentUserService = currentUserService;
    }

    public IRepository<TEntity> Repository<TEntity>() where TEntity : Entity
    {
        if (_repositories.TryGetValue(typeof(TEntity), out var repo))
            return (IRepository<TEntity>)repo;

        var repository = new Repository<TEntity>(_resolver, _currentUserService);
        _repositories[typeof(TEntity)] = repository;
        return (IRepository<TEntity>)repository;
    }
    public IBaseRepository<TEntity> BaseRepository<TEntity>() where TEntity : BaseEntity
    {
        if (_repositories.TryGetValue(typeof(TEntity), out var repo))
            return (IBaseRepository<TEntity>)repo;

        var repository = new BaseRepository<TEntity>(_resolver, _currentUserService);
        _repositories[typeof(TEntity)] = repository;
        return (IBaseRepository<TEntity>)repository;
    }

    public async Task CommitAsync()
    {
        // 🔹 Qui puoi decidere se commitare tutti i contesti
        await _context.SaveChangesAsync();
    }

    public async Task BeginTransactionAsync()
    {
        _transaction = await _context.Database.BeginTransactionAsync();
    }

    public async Task CommitTransactionAsync()
    {
        if (_transaction != null)
        {
            await _context.SaveChangesAsync();
            await _transaction.CommitAsync();
        }
    }

    public async Task RollbackTransactionAsync()
    {
        if (_transaction != null)
            await _transaction.RollbackAsync();
    }

    public Task<int> SaveChangesAsync() => _context.SaveChangesAsync();

    public void Dispose()
    {
        _transaction?.Dispose();
        _context.Dispose();
    }

}
