using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Daiv3.Persistence;

/// <summary>
/// Abstract base class for repository implementations.
/// Provides common database context access and logging functionality.
/// </summary>
/// <typeparam name="TEntity">The entity type managed by this repository</typeparam>
public abstract class RepositoryBase<TEntity> : IRepository<TEntity> where TEntity : class
{
    protected readonly IDatabaseContext DatabaseContext;
    protected readonly ILogger Logger;

    protected RepositoryBase(IDatabaseContext databaseContext, ILogger logger)
    {
        DatabaseContext = databaseContext ?? throw new ArgumentNullException(nameof(databaseContext));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public abstract Task<TEntity?> GetByIdAsync(string id, CancellationToken ct = default);

    /// <inheritdoc />
    public abstract Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken ct = default);

    /// <inheritdoc />
    public abstract Task<string> AddAsync(TEntity entity, CancellationToken ct = default);

    /// <inheritdoc />
    public abstract Task UpdateAsync(TEntity entity, CancellationToken ct = default);

    /// <inheritdoc />
    public abstract Task DeleteAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// Helper method to execute a scalar query.
    /// </summary>
    protected async Task<T?> ExecuteScalarAsync<T>(string sql, Action<SqliteParameterCollection>? configureParameters = null, CancellationToken ct = default)
    {
        await using var connection = await DatabaseContext.GetConnectionAsync(ct).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        configureParameters?.Invoke(command.Parameters);

        var result = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return result == null || result == DBNull.Value ? default : (T)result;
    }

    /// <summary>
    /// Helper method to execute a non-query command.
    /// </summary>
    protected async Task<int> ExecuteNonQueryAsync(string sql, Action<SqliteParameterCollection>? configureParameters = null, CancellationToken ct = default)
    {
        await using var connection = await DatabaseContext.GetConnectionAsync(ct).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        configureParameters?.Invoke(command.Parameters);

        return await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Helper method to execute a reader query and map results to entities.
    /// </summary>
    protected async Task<List<TEntity>> ExecuteReaderAsync(string sql, Func<SqliteDataReader, TEntity> mapper, Action<SqliteParameterCollection>? configureParameters = null, CancellationToken ct = default)
    {
        var results = new List<TEntity>();

        await using var connection = await DatabaseContext.GetConnectionAsync(ct).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        configureParameters?.Invoke(command.Parameters);

        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            results.Add(mapper(reader));
        }

        return results;
    }
}
