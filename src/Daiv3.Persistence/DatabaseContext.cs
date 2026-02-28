using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Data.Common;

namespace Daiv3.Persistence;

/// <summary>
/// Database context implementation for SQLite.
/// Manages connections, transactions, and schema migrations.
/// </summary>
public sealed class DatabaseContext : IDatabaseContext
{
    private readonly ILogger<DatabaseContext> _logger;
    private readonly PersistenceOptions _options;
    private readonly string _connectionString;
    private bool _initialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public string DatabasePath { get; }

    public DatabaseContext(
        ILogger<DatabaseContext> logger,
        IOptions<PersistenceOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        
        DatabasePath = _options.GetExpandedDatabasePath();
        _connectionString = _options.BuildConnectionString();

        _logger.LogInformation("DatabaseContext initialized with path: {DatabasePath}", DatabasePath);
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_initialized)
        {
            _logger.LogDebug("Database already initialized");
            return;
        }

        await _initLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_initialized)
                return;

            _logger.LogInformation("Initializing database at {DatabasePath}", DatabasePath);

            // Ensure directory exists
            var directory = Path.GetDirectoryName(DatabasePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                _logger.LogInformation("Creating database directory: {Directory}", directory);
                Directory.CreateDirectory(directory);
            }

            // Open connection to create database file if it doesn't exist
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(ct).ConfigureAwait(false);

            // Configure SQLite connection
            await ConfigureConnectionAsync(connection, ct).ConfigureAwait(false);

            // Run migrations (don't call GetConnectionAsync here to avoid deadlock with init lock)
            await using var migrationConnection = new SqliteConnection(_connectionString);
            await migrationConnection.OpenAsync(ct).ConfigureAwait(false);
            await ConfigureConnectionAsync(migrationConnection, ct).ConfigureAwait(false);
            await MigrateToLatestInternalAsync(migrationConnection, ct).ConfigureAwait(false);

            _initialized = true;
            _logger.LogInformation("Database initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize database");
            throw;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task<SqliteConnection> GetConnectionAsync(CancellationToken ct = default)
    {
        if (!_initialized)
        {
            await InitializeAsync(ct).ConfigureAwait(false);
        }

        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);
        await ConfigureConnectionAsync(connection, ct).ConfigureAwait(false);

        return connection;
    }

    public async Task<DbTransaction> BeginTransactionAsync(CancellationToken ct = default)
    {
        var connection = await GetConnectionAsync(ct).ConfigureAwait(false);
        var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
        return new DatabaseTransaction(connection, transaction);
    }

    public async Task MigrateToLatestAsync(CancellationToken ct = default)
    {
        if (!_initialized)
        {
            await InitializeAsync(ct).ConfigureAwait(false);
        }

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);
        await ConfigureConnectionAsync(connection, ct).ConfigureAwait(false);
        await MigrateToLatestInternalAsync(connection, ct).ConfigureAwait(false);
    }

    private async Task MigrateToLatestInternalAsync(SqliteConnection connection, CancellationToken ct)
    {
        _logger.LogInformation("Starting database migration");
        
        _logger.LogDebug("Getting current schema version");
        var currentVersion = await GetSchemaVersionInternalAsync(connection, ct).ConfigureAwait(false);
        _logger.LogInformation("Current schema version: {Version}", currentVersion);

        var migrations = GetMigrations();
        var pendingMigrations = migrations.Where(m => m.Version > currentVersion).OrderBy(m => m.Version).ToList();

        if (pendingMigrations.Count == 0)
        {
            _logger.LogInformation("Database is up to date");
            return;
        }

        _logger.LogInformation("Applying {Count} pending migrations", pendingMigrations.Count);

        foreach (var migration in pendingMigrations)
        {
            _logger.LogInformation("Applying migration {Version}: {Description}", migration.Version, migration.Description);
            
            var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
            await using (transaction.ConfigureAwait(false))
            {
                try
                {
                    // Execute migration SQL - split into individual statements
                    _logger.LogDebug("Executing migration SQL (length: {Length} chars)", migration.Sql.Length);
                    
                    // Split SQL into individual statements (separated by semicolons)
                    // Keep comments as they're part of the statements
                    var statements = migration.Sql
                        .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => s.Trim())
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .ToList();

                    _logger.LogDebug("Executing {Count} SQL statements", statements.Count);
                    
                    foreach (var statement in statements)
                    {
                        await using var command = connection.CreateCommand();
                        command.Transaction = transaction;
                        command.CommandText = statement;
                        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                    }
                    
                    _logger.LogDebug("All migration SQL statements executed successfully");

                    // Update schema version
                    _logger.LogDebug("Recording migration version {Version}", migration.Version);
                    await RecordMigrationAsync(connection, transaction, migration.Version, migration.Description, ct).ConfigureAwait(false);

                    await transaction.CommitAsync(ct).ConfigureAwait(false);
                    _logger.LogInformation("Migration {Version} completed successfully", migration.Version);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Migration {Version} failed: {Message}", migration.Version, ex.Message);
                    await transaction.RollbackAsync(ct).ConfigureAwait(false);
                    throw;
                }
            }
        }

        _logger.LogInformation("All migrations completed successfully");
    }

    public async Task<int> GetSchemaVersionAsync(CancellationToken ct = default)
    {
        await using var connection = await GetConnectionAsync(ct).ConfigureAwait(false);
        return await GetSchemaVersionInternalAsync(connection, ct).ConfigureAwait(false);
    }

    private async Task<int> GetSchemaVersionInternalAsync(SqliteConnection connection, CancellationToken ct)
    {
        _logger.LogDebug("Querying schema_version table");
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COALESCE(MAX(version), 0) FROM schema_version";
            var result = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
            var version = result != null && result != DBNull.Value ? Convert.ToInt32(result) : 0;
            _logger.LogDebug("Schema version query returned: {Version}", version);
            return version;
        }
        catch (SqliteException ex)
        {
            // Table doesn't exist yet
            _logger.LogDebug("schema_version table does not exist yet: {Message}", ex.Message);
            return 0;
        }
    }

    private async Task RecordMigrationAsync(SqliteConnection connection, SqliteTransaction transaction, int version, string description, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"
            INSERT INTO schema_version (version, applied_at, description)
            VALUES ($version, $appliedAt, $description)";
        command.Parameters.AddWithValue("$version", version);
        command.Parameters.AddWithValue("$appliedAt", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        command.Parameters.AddWithValue("$description", description);
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private async Task ConfigureConnectionAsync(SqliteConnection connection, CancellationToken ct)
    {
        // Set busy timeout
        await using var busyTimeoutCmd = connection.CreateCommand();
        busyTimeoutCmd.CommandText = $"PRAGMA busy_timeout = {_options.BusyTimeout}";
        await busyTimeoutCmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        // Enable WAL mode if configured
        if (_options.EnableWAL)
        {
            await using var walCmd = connection.CreateCommand();
            walCmd.CommandText = "PRAGMA journal_mode = WAL";
            await walCmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        // Enable foreign keys
        await using var fkCmd = connection.CreateCommand();
        fkCmd.CommandText = "PRAGMA foreign_keys = ON";
        await fkCmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private List<Migration> GetMigrations()
    {
        return new List<Migration>
        {
            new Migration
            {
                Version = 1,
                Description = "Initial schema: documents, topic_index, chunk_index, projects, tasks, sessions, model_queue",
                Sql = SchemaScripts.Migration001_InitialSchema
            },
            new Migration
            {
                Version = 2,
                Description = "Add task scheduling timestamps: next_run_at, last_run_at",
                Sql = SchemaScripts.Migration002_TaskSchedulingTimestamps
            }
        };
    }

    public async ValueTask DisposeAsync()
    {
        _initLock.Dispose();
        await Task.CompletedTask;
    }

    private record Migration
    {
        public required int Version { get; init; }
        public required string Description { get; init; }
        public required string Sql { get; init; }
    }
}
