using Microsoft.EntityFrameworkCore;
using TCM.Application.Abstractions;
using TCM.Infrastructure.Persistence;

namespace TCM.Infrastructure.Repositories;

/// <summary>
/// Generic EF Core repository. Specific repositories inherit from this and add the queries their
/// domain needs; everything shared lives here.
/// </summary>
public class Repository<T>(ApplicationDbContext context) : IRepository<T> where T : class
{
    protected ApplicationDbContext Context { get; } = context;
    protected DbSet<T> Set => Context.Set<T>();

    public virtual async Task<T?> GetByIdAsync(object id, CancellationToken ct = default) =>
        await Set.FindAsync([id], ct);

    /// <summary>Read-only, so it does not pay for change tracking.</summary>
    public virtual async Task<IReadOnlyList<T>> ListAsync(CancellationToken ct = default) =>
        await Set.AsNoTracking().ToListAsync(ct);

    public virtual async Task AddAsync(T entity, CancellationToken ct = default) =>
        await Set.AddAsync(entity, ct);

    public virtual void Update(T entity) => Set.Update(entity);

    public virtual void Remove(T entity) => Set.Remove(entity);

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => Context.SaveChangesAsync(ct);
}
