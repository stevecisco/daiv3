namespace Daiv3.Persistence;

/// <summary>
/// Generic repository interface for data access operations.
/// </summary>
/// <typeparam name="TEntity">The entity type managed by this repository</typeparam>
public interface IRepository<TEntity> where TEntity : class
{
    /// <summary>
    /// Gets an entity by its unique identifier.
    /// </summary>
    /// <param name="id">The entity identifier</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The entity if found, null otherwise</returns>
    Task<TEntity?> GetByIdAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// Gets all entities from the repository.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>A read-only list of all entities</returns>
    Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Adds a new entity to the repository.
    /// </summary>
    /// <param name="entity">The entity to add</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The identifier of the added entity</returns>
    Task<string> AddAsync(TEntity entity, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing entity in the repository.
    /// </summary>
    /// <param name="entity">The entity to update</param>
    /// <param name="ct">Cancellation token</param>
    Task UpdateAsync(TEntity entity, CancellationToken ct = default);

    /// <summary>
    /// Deletes an entity from the repository.
    /// </summary>
    /// <param name="id">The identifier of the entity to delete</param>
    /// <param name="ct">Cancellation token</param>
    Task DeleteAsync(string id, CancellationToken ct = default);
}
