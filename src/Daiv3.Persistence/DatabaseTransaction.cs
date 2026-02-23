using Microsoft.Data.Sqlite;
using System.Data;
using System.Data.Common;

namespace Daiv3.Persistence;

/// <summary>
/// Wraps a SqliteTransaction and its associated connection to ensure proper disposal.
/// This class ensures that when the transaction is disposed, the underlying connection is also disposed.
/// </summary>
public sealed class DatabaseTransaction : DbTransaction
{
    private readonly SqliteConnection _connection;
    private readonly SqliteTransaction _transaction;
    private bool _disposed;

    internal DatabaseTransaction(SqliteConnection connection, SqliteTransaction transaction)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
    }

    /// <summary>
    /// Gets the underlying SqliteTransaction for scenarios requiring SqliteTransaction-specific functionality.
    /// </summary>
    public SqliteTransaction InnerTransaction => _transaction;

    public override IsolationLevel IsolationLevel => _transaction.IsolationLevel;

    protected override DbConnection? DbConnection => _connection;

    public override void Commit()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _transaction.Commit();
    }

    public override void Rollback()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _transaction.Rollback();
    }

    public override Task CommitAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transaction.CommitAsync(cancellationToken);
    }

    public override Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transaction.RollbackAsync(cancellationToken);
    }

    protected override void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            // Dispose transaction first, then connection
            _transaction.Dispose();
            _connection.Dispose();
        }

        _disposed = true;
        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        // Dispose transaction first, then connection
        await _transaction.DisposeAsync().ConfigureAwait(false);
        await _connection.DisposeAsync().ConfigureAwait(false);

        _disposed = true;

        // Suppress finalization
        GC.SuppressFinalize(this);
    }
}
