using System;
using Microsoft.EntityFrameworkCore.Storage;

namespace PlatformAI.Infrastructure;

public interface IUnitOfWork : IDisposable
{
    IRepository<TEntity> Repository<TEntity>() where TEntity : Entity;
    Task<int> SaveChangesAsync();
    Task BeginTransactionAsync();
    Task CommitTransactionAsync();
    Task RollbackTransactionAsync();
    Task CommitAsync();
}
