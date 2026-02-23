using Daiv3.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace Daiv3.IntegrationTests.Persistence;

/// <summary>
/// Performance tests for DatabaseContext with realistic data volumes.
/// Tests insertion, retrieval, and query performance with large datasets.
/// </summary>
[Collection("Database")]
public class DatabaseContextPerformanceTests : IAsyncLifetime
{
    private readonly string _testDbPath;
    private readonly ILogger<DatabaseContext> _logger;
    private readonly ITestOutputHelper _output;
    private DatabaseContext? _context;

    public DatabaseContextPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
        _testDbPath = Path.Combine(Path.GetTempPath(), $"daiv3_perf_test_{Guid.NewGuid():N}.db");
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        _logger = loggerFactory.CreateLogger<DatabaseContext>();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (_context != null)
        {
            await _context.DisposeAsync();
        }

        // Clean up test database
        if (File.Exists(_testDbPath))
        {
            var retries = 5;
            while (retries > 0)
            {
                try
                {
                    SqliteConnection.ClearAllPools();
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    File.Delete(_testDbPath);
                    break;
                }
                catch
                {
                    retries--;
                    if (retries == 0) throw;
                    await Task.Delay(100);
                }
            }
        }
    }

    [Fact]
    public async Task Insert1000Documents_CompletesWithinReasonableTime()
    {
        // Arrange
        _context = CreateContext();
        await _context.InitializeAsync();
        await _context.MigrateToLatestAsync();

        var documentCount = 1000;
        var sw = Stopwatch.StartNew();

        // Act
        for (int i = 0; i < documentCount; i++)
        {
            await InsertDocumentAsync(
                Guid.NewGuid().ToString(),
                $"/test/doc_{i}.txt",
                DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        }

        sw.Stop();

        // Assert
        var count = await GetDocumentCountAsync();
        Assert.Equal(documentCount, count);
        
        _output.WriteLine($"Inserted {documentCount} documents in {sw.ElapsedMilliseconds}ms");
        _output.WriteLine($"Average: {sw.ElapsedMilliseconds / (double)documentCount:F2}ms per document");
        
        // Should complete in under 10 seconds for 1000 documents
        Assert.True(sw.ElapsedMilliseconds < 10000, 
            $"Document insertion took too long: {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task Insert1000Documents_WithTransaction_IsFaster()
    {
        // Arrange
        _context = CreateContext();
        await _context.InitializeAsync();
        await _context.MigrateToLatestAsync();

        var documentCount = 1000;
        var sw = Stopwatch.StartNew();

        // Act
        await using (var transaction = await _context.BeginTransactionAsync())
        {
            for (int i = 0; i < documentCount; i++)
            {
                await InsertDocumentAsync(
                    transaction,
                    Guid.NewGuid().ToString(),
                    $"/test/doc_{i}.txt",
                    DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            }
            await transaction.CommitAsync();
        }

        sw.Stop();

        // Assert
        var count = await GetDocumentCountAsync();
        Assert.Equal(documentCount, count);
        
        _output.WriteLine($"Inserted {documentCount} documents (transactional) in {sw.ElapsedMilliseconds}ms");
        _output.WriteLine($"Average: {sw.ElapsedMilliseconds / (double)documentCount:F2}ms per document");
        
        // Transactional insert should be much faster - under 2 seconds
        Assert.True(sw.ElapsedMilliseconds < 2000, 
            $"Transactional insertion took too long: {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task InsertLargeEmbeddings_HandlesMemoryEfficiently()
    {
        // Arrange
        _context = CreateContext();
        await _context.InitializeAsync();
        await _context.MigrateToLatestAsync();

        var documentCount = 1000;
        var embeddingDimensions = 768;
        var embeddingSize = embeddingDimensions * sizeof(float);
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var sw = Stopwatch.StartNew();

        // Act
        await using (var transaction = await _context.BeginTransactionAsync())
        {
            for (int i = 0; i < documentCount; i++)
            {
                var docId = Guid.NewGuid().ToString();
                
                // Insert document
                await InsertDocumentAsync(transaction, docId, $"/test/doc_{i}.txt", timestamp);
                
                // Insert topic index with embedding
                var embedding = CreateRandomEmbedding(embeddingDimensions);
                await InsertTopicIndexAsync(transaction, docId, embedding, timestamp);
            }
            await transaction.CommitAsync();
        }

        sw.Stop();

        // Assert
        var count = await GetTopicCountAsync();
        Assert.Equal(documentCount, count);
        
        var totalSize = documentCount * embeddingSize;
        _output.WriteLine($"Inserted {documentCount} embeddings ({embeddingDimensions}-dim) in {sw.ElapsedMilliseconds}ms");
        _output.WriteLine($"Total blob storage: {totalSize / 1024.0 / 1024.0:F2} MB");
        _output.WriteLine($"Average: {sw.ElapsedMilliseconds / (double)documentCount:F2}ms per embedding");
        
        // Should handle 1000 embeddings in under 3 seconds
        Assert.True(sw.ElapsedMilliseconds < 3000, 
            $"Embedding insertion took too long: {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task QueryWithIndex_PerformsEfficiently()
    {
        // Arrange
        _context = CreateContext();
        await _context.InitializeAsync();
        await _context.MigrateToLatestAsync();

        // Insert 1000 documents
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await using (var transaction = await _context.BeginTransactionAsync())
        {
            for (int i = 0; i < 1000; i++)
            {
                await InsertDocumentAsync(
                    transaction,
                    Guid.NewGuid().ToString(),
                    $"/test/doc_{i}.txt",
                    timestamp);
            }
            await transaction.CommitAsync();
        }

        // Act - Query using indexed column
        var sw = Stopwatch.StartNew();
        var results = await QueryDocumentsByStatusAsync("indexed");
        sw.Stop();

        // Assert
        Assert.Equal(1000, results.Count);
        _output.WriteLine($"Queried {results.Count} documents by status in {sw.ElapsedMilliseconds}ms");
        
        // Index-based query should be very fast
        Assert.True(sw.ElapsedMilliseconds < 100, 
            $"Indexed query took too long: {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task ConcurrentReads_ScaleWithConnectionPool()
    {
        // Arrange
        _context = CreateContext();
        await _context.InitializeAsync();
        await _context.MigrateToLatestAsync();

        // Insert some test data
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await using (var transaction = await _context.BeginTransactionAsync())
        {
            for (int i = 0; i < 100; i++)
            {
                await InsertDocumentAsync(
                    transaction,
                    Guid.NewGuid().ToString(),
                    $"/test/doc_{i}.txt",
                    timestamp);
            }
            await transaction.CommitAsync();
        }

        // Act - Run 20 concurrent read queries
        var sw = Stopwatch.StartNew();
        var tasks = Enumerable.Range(0, 20).Select(async i =>
        {
            await using var connection = await _context.GetConnectionAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM documents WHERE status = $status";
            command.Parameters.AddWithValue("$status", "indexed");
            return Convert.ToInt32(await command.ExecuteScalarAsync());
        });

        var results = await Task.WhenAll(tasks);
        sw.Stop();

        // Assert
        Assert.All(results, result => Assert.Equal(100, result));
        _output.WriteLine($"Executed 20 concurrent queries in {sw.ElapsedMilliseconds}ms");
        _output.WriteLine($"Average: {sw.ElapsedMilliseconds / 20.0:F2}ms per query");
        
        // All queries should complete quickly
        Assert.True(sw.ElapsedMilliseconds < 1000, 
            $"Concurrent queries took too long: {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task FullTableScan_CompletesWithin_ReasonableTime()
    {
        // Arrange
        _context = CreateContext();
        await _context.InitializeAsync();
        await _context.MigrateToLatestAsync();

        var documentCount = 1000;
        var embeddingDimensions = 768;
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Insert documents with embeddings
        await using (var transaction = await _context.BeginTransactionAsync())
        {
            for (int i = 0; i < documentCount; i++)
            {
                var docId = Guid.NewGuid().ToString();
                await InsertDocumentAsync(transaction, docId, $"/test/doc_{i}.txt", timestamp);
                var embedding = CreateRandomEmbedding(embeddingDimensions);
                await InsertTopicIndexAsync(transaction, docId, embedding, timestamp);
            }
            await transaction.CommitAsync();
        }

        // Act - Full table scan
        var sw = Stopwatch.StartNew();
        var embeddings = await GetAllEmbeddingsAsync();
        sw.Stop();

        // Assert
        Assert.Equal(documentCount, embeddings.Count);
        _output.WriteLine($"Full table scan of {documentCount} embeddings in {sw.ElapsedMilliseconds}ms");
        
        // Full scan should complete in reasonable time
        Assert.True(sw.ElapsedMilliseconds < 500, 
            $"Full table scan took too long: {sw.ElapsedMilliseconds}ms");
    }

    #region Helper Methods

    private DatabaseContext CreateContext()
    {
        var options = Options.Create(new PersistenceOptions
        {
            DatabasePath = _testDbPath,
            EnableWAL = true,
            BusyTimeout = 5000,
            MaxPoolSize = 10
        });
        return new DatabaseContext(_logger, options);
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

    private async Task InsertDocumentAsync(string docId, string sourcePath, long timestamp)
    {
        await using var connection = await _context!.GetConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO documents (doc_id, source_path, file_hash, format, size_bytes, last_modified, status, created_at)
            VALUES ($id, $path, $hash, $format, $size, $modified, $status, $created)";
        command.Parameters.AddWithValue("$id", docId);
        command.Parameters.AddWithValue("$path", sourcePath);
        command.Parameters.AddWithValue("$hash", $"hash-{docId}");
        command.Parameters.AddWithValue("$format", "text/plain");
        command.Parameters.AddWithValue("$size", 1024);
        command.Parameters.AddWithValue("$modified", timestamp);
        command.Parameters.AddWithValue("$status", "indexed");
        command.Parameters.AddWithValue("$created", timestamp);
        await command.ExecuteNonQueryAsync();
    }

    private async Task InsertDocumentAsync(SqliteTransaction transaction, string docId, string sourcePath, long timestamp)
    {
        await using var command = transaction.Connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"
            INSERT INTO documents (doc_id, source_path, file_hash, format, size_bytes, last_modified, status, created_at)
            VALUES ($id, $path, $hash, $format, $size, $modified, $status, $created)";
        command.Parameters.AddWithValue("$id", docId);
        command.Parameters.AddWithValue("$path", sourcePath);
        command.Parameters.AddWithValue("$hash", $"hash-{docId}");
        command.Parameters.AddWithValue("$format", "text/plain");
        command.Parameters.AddWithValue("$size", 1024);
        command.Parameters.AddWithValue("$modified", timestamp);
        command.Parameters.AddWithValue("$status", "indexed");
        command.Parameters.AddWithValue("$created", timestamp);
        await command.ExecuteNonQueryAsync();
    }

    private async Task InsertTopicIndexAsync(SqliteTransaction transaction, string docId, byte[] embedding, long timestamp)
    {
        await using var command = transaction.Connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"
            INSERT INTO topic_index (doc_id, summary_text, embedding_blob, embedding_dimensions, source_path, file_hash, ingested_at)
            VALUES ($docId, $summary, $blob, $dims, $path, $hash, $ingested)";
        command.Parameters.AddWithValue("$docId", docId);
        command.Parameters.AddWithValue("$summary", "Test summary");
        command.Parameters.AddWithValue("$blob", embedding);
        command.Parameters.AddWithValue("$dims", embedding.Length / sizeof(float));
        command.Parameters.AddWithValue("$path", "/test/path.txt");
        command.Parameters.AddWithValue("$hash", $"hash-{docId}");
        command.Parameters.AddWithValue("$ingested", timestamp);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<List<string>> QueryDocumentsByStatusAsync(string status)
    {
        await using var connection = await _context!.GetConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT doc_id FROM documents WHERE status = $status";
        command.Parameters.AddWithValue("$status", status);
        
        var results = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(reader.GetString(0));
        }
        return results;
    }

    private async Task<List<byte[]>> GetAllEmbeddingsAsync()
    {
        await using var connection = await _context!.GetConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT embedding_blob FROM topic_index";
        
        var results = new List<byte[]>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var blob = new byte[reader.GetBytes(0, 0, null, 0, 0)];
            reader.GetBytes(0, 0, blob, 0, blob.Length);
            results.Add(blob);
        }
        return results;
    }

    private static byte[] CreateRandomEmbedding(int dimensions)
    {
        var embedding = new byte[dimensions * sizeof(float)];
        new Random().NextBytes(embedding);
        return embedding;
    }

    #endregion
}
