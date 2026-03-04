using Daiv3.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Daiv3.IntegrationTests.Persistence;

/// <summary>
/// Integration tests for DatabaseContext with actual SQLite database.
/// Tests full database initialization, migrations, transactions, and data operations.
/// </summary>
[Collection("Database")]
public class DatabaseContextIntegrationTests : IAsyncLifetime
{
    private readonly string _testDbPath;
    private readonly ILogger<DatabaseContext> _logger;
    private DatabaseContext? _context;

    public DatabaseContextIntegrationTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"daiv3_integration_test_{Guid.NewGuid():N}.db");
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        _logger = loggerFactory.CreateLogger<DatabaseContext>();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (_context != null)
        {
            await _context.DisposeAsync();
            _context = null;
        }

        // Clear connection pools before attempting file deletion
        SqliteConnection.ClearAllPools();
        
        // Give time for finalizers to complete
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        // Wait a bit for any lingering file handles to release
        await Task.Delay(200);

        // Clean up test database with retry logic
        if (File.Exists(_testDbPath))
        {
            var retries = 10;
            while (retries > 0)
            {
                try
                {
                    File.Delete(_testDbPath);
                    break;
                }
                catch (IOException) when (retries > 1)
                {
                    retries--;
                    await Task.Delay(100);
                }
            }
        }
    }

    [Fact]
    public async Task InitializeAsync_CreatesDatabase()
    {
        // Arrange
        _context = CreateContext();

        // Act
        await _context.InitializeAsync();

        // Assert
        Assert.True(File.Exists(_testDbPath), "Database file should exist");
    }

    [Fact]
    public async Task InitializeAsync_CreatesDirectoryIfNotExists()
    {
        // Arrange
        var nestedPath = Path.Combine(Path.GetTempPath(), $"daiv3_test_{Guid.NewGuid():N}", "subdir", "test.db");
        var nestedContext = CreateContext(nestedPath);

        try
        {
            // Act
            await nestedContext.InitializeAsync();

            // Assert
            Assert.True(File.Exists(nestedPath), "Database file should exist in nested directory");
            Assert.True(Directory.Exists(Path.GetDirectoryName(nestedPath)!), "Directory should be created");
        }
        finally
        {
            await nestedContext.DisposeAsync();
            if (File.Exists(nestedPath))
            {
                SqliteConnection.ClearAllPools();
                GC.Collect();
                GC.WaitForPendingFinalizers();
                await Task.Delay(100);
                File.Delete(nestedPath);
                Directory.Delete(Path.GetDirectoryName(nestedPath)!, true);
            }
        }
    }

    [Fact]
    public async Task InitializeAsync_CanBeCalledMultipleTimes()
    {
        // Arrange
        _context = CreateContext();

        // Act
        await _context.InitializeAsync();
        await _context.InitializeAsync(); // Should not throw
        await _context.InitializeAsync(); // Should not throw

        // Assert
        Assert.True(File.Exists(_testDbPath));
    }

    [Fact]
    public async Task MigrateToLatest_CreatesAllTables()
    {
        // Arrange
        _context = CreateContext();
        await _context.InitializeAsync();

        // Act
        await _context.MigrateToLatestAsync();

        // Assert
        var tables = await GetTableNamesAsync();
        Assert.Contains("schema_version", tables);
        Assert.Contains("documents", tables);
        Assert.Contains("topic_index", tables);
        Assert.Contains("chunk_index", tables);
        Assert.Contains("projects", tables);
        Assert.Contains("tasks", tables);
        Assert.Contains("sessions", tables);
        Assert.Contains("model_queue", tables);
    }

    [Fact]
    public async Task MigrateToLatest_SetsSchemaVersion()
    {
        // Arrange
        _context = CreateContext();
        await _context.InitializeAsync();

        // Act
        await _context.MigrateToLatestAsync();
        var version = await _context.GetSchemaVersionAsync();

        // Assert
           Assert.Equal(8, version);
    }

    [Fact]
    public async Task MigrateToLatest_IsIdempotent()
    {
        // Arrange
        _context = CreateContext();
        await _context.InitializeAsync();

        // Act
        await _context.MigrateToLatestAsync();
        var version1 = await _context.GetSchemaVersionAsync();
        
        await _context.MigrateToLatestAsync(); // Run again
        var version2 = await _context.GetSchemaVersionAsync();

        // Assert
        Assert.Equal(version1, version2);
    }

    [Fact]
    public async Task GetConnection_ReturnsOpenConnection()
    {
        // Arrange
        _context = CreateContext();
        await _context.InitializeAsync();

        // Act
        await using var connection = await _context.GetConnectionAsync();

        // Assert
        Assert.NotNull(connection);
        Assert.Equal(System.Data.ConnectionState.Open, connection.State);
    }

    [Fact]
    public async Task GetConnection_EnablesForeignKeys()
    {
        // Arrange
        _context = CreateContext();
        await _context.InitializeAsync();
        await _context.MigrateToLatestAsync();

        // Act
        await using var connection = await _context.GetConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys";
        var result = await command.ExecuteScalarAsync();

        // Assert
        Assert.Equal(1L, result); // Foreign keys should be enabled (1 = ON)
    }

    [Fact]
    public async Task BeginTransaction_ReturnsTransaction()
    {
        // Arrange
        _context = CreateContext();
        await _context.InitializeAsync();
        await _context.MigrateToLatestAsync();

        // Act
        await using var transaction = await _context.BeginTransactionAsync();

        // Assert
        Assert.NotNull(transaction);
        Assert.NotNull(transaction.Connection);
    }

    [Fact]
    public async Task Transaction_CanCommit()
    {
        // Arrange
        _context = CreateContext();
        await _context.InitializeAsync();
        await _context.MigrateToLatestAsync();

        var projectId = Guid.NewGuid().ToString();
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Act
        await using (var transaction = await _context.BeginTransactionAsync())
        {
            var dbTran = (DatabaseTransaction)transaction;
            await using var command = dbTran.Connection!.CreateCommand();
            command.Transaction = dbTran.InnerTransaction;
            command.CommandText = @"
                INSERT INTO projects (project_id, name, root_paths, created_at, updated_at, status)
                VALUES ($id, $name, $paths, $created, $updated, $status)";
            command.Parameters.Add(new SqliteParameter("$id", projectId));
            command.Parameters.Add(new SqliteParameter("$name", "Test Project"));
            command.Parameters.Add(new SqliteParameter("$paths", "/test/path"));
            command.Parameters.Add(new SqliteParameter("$created", timestamp));
            command.Parameters.Add(new SqliteParameter("$updated", timestamp));
            command.Parameters.Add(new SqliteParameter("$status", "active"));
            await command.ExecuteNonQueryAsync();
            await transaction.CommitAsync();
        }

        // Assert
        var count = await GetProjectCountAsync();
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Transaction_CanRollback()
    {
        // Arrange
        _context = CreateContext();
        await _context.InitializeAsync();
        await _context.MigrateToLatestAsync();

        var projectId = Guid.NewGuid().ToString();
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Act
        await using (var transaction = await _context.BeginTransactionAsync())
        {
            var dbTran = (DatabaseTransaction)transaction;
            await using var command = dbTran.Connection!.CreateCommand();
            command.Transaction = dbTran.InnerTransaction;
            command.CommandText = @"
                INSERT INTO projects (project_id, name, root_paths, created_at, updated_at, status)
                VALUES ($id, $name, $paths, $created, $updated, $status)";
            command.Parameters.Add(new SqliteParameter("$id", projectId));
            command.Parameters.Add(new SqliteParameter("$name", "Test Project"));
            command.Parameters.Add(new SqliteParameter("$paths", "/test/path"));
            command.Parameters.Add(new SqliteParameter("$created", timestamp));
            command.Parameters.Add(new SqliteParameter("$updated", timestamp));
            command.Parameters.Add(new SqliteParameter("$status", "active"));
            await command.ExecuteNonQueryAsync();
            await transaction.RollbackAsync();
        }

        // Assert
        var count = await GetProjectCountAsync();
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task ForeignKey_CascadeDelete_WorksCorrectly()
    {
        // Arrange
        _context = CreateContext();
        await _context.InitializeAsync();
        await _context.MigrateToLatestAsync();

        var docId = Guid.NewGuid().ToString();
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Insert document
        await InsertDocumentAsync(docId, timestamp);

        // Insert topic index referencing document
        await InsertTopicIndexAsync(docId, timestamp);

        // Verify both exist
        var docCount = await GetDocumentCountAsync();
        var topicCount = await GetTopicCountAsync();
        Assert.Equal(1, docCount);
        Assert.Equal(1, topicCount);

        // Act - Delete document (should cascade to topic_index)
        await DeleteDocumentAsync(docId);

        // Assert
        docCount = await GetDocumentCountAsync();
        topicCount = await GetTopicCountAsync();
        Assert.Equal(0, docCount);
        Assert.Equal(0, topicCount); // Should be deleted due to CASCADE
    }

    [Fact]
    public async Task BlobStorage_CanStoreAndRetrieve()
    {
        // Arrange
        _context = CreateContext();
        await _context.InitializeAsync();
        await _context.MigrateToLatestAsync();

        var docId = Guid.NewGuid().ToString();
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        
        // Create a test embedding (768-dimensional vector)
        var embedding = new byte[768 * sizeof(float)];
        new Random().NextBytes(embedding);

        // Insert document
        await InsertDocumentAsync(docId, timestamp);

        // Act - Insert topic index with embedding blob
        await using (var connection = await _context.GetConnectionAsync())
        {
            await using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO topic_index (doc_id, summary_text, embedding_blob, embedding_dimensions, source_path, file_hash, ingested_at)
                VALUES ($docId, $summary, $blob, $dims, $path, $hash, $ingested)";
            command.Parameters.AddWithValue("$docId", docId);
            command.Parameters.AddWithValue("$summary", "Test summary");
            command.Parameters.AddWithValue("$blob", embedding);
            command.Parameters.AddWithValue("$dims", 768);
            command.Parameters.AddWithValue("$path", "/test/path.txt");
            command.Parameters.AddWithValue("$hash", "test-hash");
            command.Parameters.AddWithValue("$ingested", timestamp);
            await command.ExecuteNonQueryAsync();
        }

        // Assert - Retrieve and verify
        await using (var connection = await _context.GetConnectionAsync())
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT embedding_blob FROM topic_index WHERE doc_id = $docId";
            command.Parameters.AddWithValue("$docId", docId);
            var retrievedBlob = (byte[])(await command.ExecuteScalarAsync())!;
            
            Assert.NotNull(retrievedBlob);
            Assert.Equal(embedding.Length, retrievedBlob.Length);
            Assert.Equal(embedding, retrievedBlob);
        }
    }

    [Fact]
    public async Task CheckConstraint_EnforcesValidStatus()
    {
        // Arrange
        _context = CreateContext();
        await _context.InitializeAsync();
        await _context.MigrateToLatestAsync();

        var projectId = Guid.NewGuid().ToString();
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Act & Assert
        await using var connection = await _context.GetConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO projects (project_id, name, root_paths, created_at, updated_at, status)
            VALUES ($id, $name, $paths, $created, $updated, $status)";
        command.Parameters.AddWithValue("$id", projectId);
        command.Parameters.AddWithValue("$name", "Test Project");
        command.Parameters.AddWithValue("$paths", "/test/path");
        command.Parameters.AddWithValue("$created", timestamp);
        command.Parameters.AddWithValue("$updated", timestamp);
        command.Parameters.AddWithValue("$status", "invalid_status"); // Invalid status

        await Assert.ThrowsAsync<SqliteException>(async () => await command.ExecuteNonQueryAsync());
    }

    [Fact]
    public async Task UniqueConstraint_EnforcesUniqueness()
    {
        // Arrange
        _context = CreateContext();
        await _context.InitializeAsync();
        await _context.MigrateToLatestAsync();

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var sourcePath = "/test/unique/path.txt";

        // Insert first document
        await InsertDocumentAsync(Guid.NewGuid().ToString(), timestamp, sourcePath);

        // Act & Assert - Try to insert duplicate source_path
        await Assert.ThrowsAsync<SqliteException>(async () =>
            await InsertDocumentAsync(Guid.NewGuid().ToString(), timestamp, sourcePath));
    }

    [Fact]
    public async Task ConcurrentConnections_CanAccessDatabase()
    {
        // Arrange
        _context = CreateContext();
        await _context.InitializeAsync();
        await _context.MigrateToLatestAsync();

        // Act - Open multiple connections concurrently
        var tasks = Enumerable.Range(0, 5).Select(async i =>
        {
            await using var connection = await _context.GetConnectionAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM projects";
            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        });

        var results = await Task.WhenAll(tasks);

        // Assert - All queries should succeed
        Assert.All(results, result => Assert.Equal(0, result));
    }

    #region Helper Methods

    private DatabaseContext CreateContext(string? dbPath = null)
    {
        var options = Options.Create(new PersistenceOptions
        {
            DatabasePath = dbPath ?? _testDbPath,
            EnableWAL = true,
            BusyTimeout = 5000,
            MaxPoolSize = 10
        });
        return new DatabaseContext(_logger, options);
    }

    private async Task<List<string>> GetTableNamesAsync()
    {
        await using var connection = await _context!.GetConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name";
        
        var tables = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tables.Add(reader.GetString(0));
        }
        return tables;
    }

    private async Task<int> GetProjectCountAsync()
    {
        await using var connection = await _context!.GetConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM projects";
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private async Task<int> GetDocumentCountAsync()
    {
        await using var connection = await _context!.GetConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM documents";
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private async Task<int> GetTopicCountAsync()
    {
        await using var connection = await _context!.GetConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM topic_index";
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private async Task InsertDocumentAsync(string docId, long timestamp, string? sourcePath = null)
    {
        await using var connection = await _context!.GetConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO documents (doc_id, source_path, file_hash, format, size_bytes, last_modified, status, created_at)
            VALUES ($id, $path, $hash, $format, $size, $modified, $status, $created)";
        command.Parameters.AddWithValue("$id", docId);
        command.Parameters.AddWithValue("$path", sourcePath ?? $"/test/path/{docId}.txt");
        command.Parameters.AddWithValue("$hash", $"hash-{docId}");
        command.Parameters.AddWithValue("$format", "text/plain");
        command.Parameters.AddWithValue("$size", 1024);
        command.Parameters.AddWithValue("$modified", timestamp);
        command.Parameters.AddWithValue("$status", "indexed");
        command.Parameters.AddWithValue("$created", timestamp);
        await command.ExecuteNonQueryAsync();
    }

    private async Task InsertTopicIndexAsync(string docId, long timestamp)
    {
        var embedding = new byte[768 * sizeof(float)];
        await using var connection = await _context!.GetConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO topic_index (doc_id, summary_text, embedding_blob, embedding_dimensions, source_path, file_hash, ingested_at)
            VALUES ($docId, $summary, $blob, $dims, $path, $hash, $ingested)";
        command.Parameters.AddWithValue("$docId", docId);
        command.Parameters.AddWithValue("$summary", "Test summary");
        command.Parameters.AddWithValue("$blob", embedding);
        command.Parameters.AddWithValue("$dims", 768);
        command.Parameters.AddWithValue("$path", "/test/path.txt");
        command.Parameters.AddWithValue("$hash", "test-hash");
        command.Parameters.AddWithValue("$ingested", timestamp);
        await command.ExecuteNonQueryAsync();
    }

    private async Task DeleteDocumentAsync(string docId)
    {
        await using var connection = await _context!.GetConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM documents WHERE doc_id = $id";
        command.Parameters.AddWithValue("$id", docId);
        await command.ExecuteNonQueryAsync();
    }

    #endregion
}

/// <summary>
/// Collection definition to ensure database tests don't run in parallel.
/// </summary>
[CollectionDefinition("Database")]
public class DatabaseCollection : ICollectionFixture<DatabaseTestFixture>
{
}

/// <summary>
/// Fixture for database tests.
/// </summary>
public class DatabaseTestFixture : IDisposable
{
    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }
}
