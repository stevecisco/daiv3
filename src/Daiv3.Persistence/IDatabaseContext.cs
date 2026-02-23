using Microsoft.Data.Sqlite;
using System.Data.Common;

namespace Daiv3.Persistence;

/// <summary>
/// Database context interface for SQLite database operations.
/// Provides connection management, transactions, and schema migration support.
/// </summary>
public interface IDatabaseContext : IAsyncDisposable
{
    /// <summary>
    /// Initializes the database, creating it if it doesn't exist.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    Task InitializeAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets a database connection from the pool.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>An open SQLite connection</returns>
    Task<SqliteConnection> GetConnectionAsync(CancellationToken ct = default);

    /// <summary>
    /// Begins a new database transaction.
    /// The returned transaction manages both the transaction and its connection.
    /// Disposing the transaction will automatically dispose the connection.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>A database transaction that manages its connection lifecycle</returns>
    Task<DbTransaction> BeginTransactionAsync(CancellationToken ct = default);

    /// <summary>
    /// Migrates the database schema to the latest version.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    Task MigrateToLatestAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets the current database schema version.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Current schema version, or 0 if not initialized</returns>
    Task<int> GetSchemaVersionAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets the database file path.
    /// </summary>
    string DatabasePath { get; }
}
