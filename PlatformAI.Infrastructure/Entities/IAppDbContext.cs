using System;
using Microsoft.EntityFrameworkCore;

namespace PlatformAI.Infrastructure;

public interface IAppDbContext
{
    DbSet<TEntity> Set<TEntity>() where TEntity : class;
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
