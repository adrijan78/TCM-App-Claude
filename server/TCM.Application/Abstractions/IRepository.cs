namespace TCM.Application.Abstractions;

/// <summary>
/// CRUD that every entity needs, with no business rules (SPEC section 3.1). Anything that needs
/// filtering, includes or aggregation belongs on a specific repository interface instead — this
/// one deliberately does not expose <c>IQueryable</c>, so EF Core cannot leak into the services.
/// </summary>
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(object id, CancellationToken ct = default);

    Task<IReadOnlyList<T>> ListAsync(CancellationToken ct = default);

    Task AddAsync(T entity, CancellationToken ct = default);

    void Update(T entity);

    void Remove(T entity);

    /// <summary>Commits the current unit of work. Returns the number of rows affected.</summary>
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
