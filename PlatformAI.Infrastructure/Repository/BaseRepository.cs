using System;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PlatformAI.Infrastructure.Master;

namespace PlatformAI.Infrastructure;

public class BaseRepository<T> : IBaseRepository<T> where T : BaseEntity
{
    private readonly IDbContextResolver _resolver;
    DbSet<T> _dbSet => _resolver.Resolve<T>().Set<T>();
    IAppDbContext _context => _resolver.Resolve<T>();
    private readonly ICurrentUserService _currentUserService;
    private readonly User? _currentUser;

    public BaseRepository(IDbContextResolver resolver, ICurrentUserService currentUserService)
    {
        _resolver = resolver;
        _currentUserService = currentUserService;
        _currentUser = _currentUserService.GetCurrentUser();
    }


    public async Task<T?> GetByIdAsync(Guid id) => await _dbSet.FindAsync(id);

    public async Task<IReadOnlyList<T>> ListAsync(System.Linq.Expressions.Expression<Func<T, bool>>? predicate = null)
    {
        if (predicate != null)
            return await _dbSet.Where(predicate).ToListAsync();
        return await _dbSet.ToListAsync();
    }

    // I metodi Add, Update e Remove sono sincroni perché modificano solo il Change Tracker in memoria.
    // Il salvataggio asincrono avverrà con CommitAsync() dell'UnitOfWork.
    public Task<T> AddAsync(T entity)
    {

        _dbSet.Add(entity);
        return Task.FromResult(entity);
    }
    public Task<T> UpdateAsync(T entity)
    {
        _dbSet.Update(entity);
        return Task.FromResult(entity);
    }

    public Task DeleteAsync(T entity)
    {
        _dbSet.Remove(entity);
        return Task.CompletedTask;
    }

    public IQueryable<T> Query(Expression<Func<T, bool>> predicate)
    {
        return _dbSet.Where(predicate).AsQueryable();
    }


}