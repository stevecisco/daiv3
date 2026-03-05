using Daiv3.Knowledge;
using Daiv3.Persistence;
using Daiv3.Persistence.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Daiv3.UnitTests.Knowledge;

/// <summary>
/// Unit tests for IndexingStatusService.
/// Tests querying indexing statistics and file status information.
/// Implements CT-REQ-005: Dashboard SHALL display indexing progress, file browser, per-file status.
/// </summary>
public sealed class IndexingStatusServiceTests : IAsyncLifetime
{
    private readonly string _testDbPath;
    private IDatabaseContext _databaseContext = null!;
    private DocumentRepository _documentRepository = null!;
    private Mock<IKnowledgeFileOrchestrationService> _mockOrchestrationService = null!;
    private Mock<ILogger<IndexingStatusService>> _mockLogger = null!;
    private IndexingStatusService _service = null!;

    public IndexingStatusServiceTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"indexing-status-test-{Guid.NewGuid()}.db");
    }

    public async Task InitializeAsync()
    {
        // Set up database context (InitializeAsync applies all schema migrations)
        var persistenceOptions = Options.Create(new PersistenceOptions { DatabasePath = _testDbPath });
        _databaseContext = new DatabaseContext(
            Mock.Of<ILogger<DatabaseContext>>(),
            persistenceOptions);
        await _databaseContext.InitializeAsync();

        // Set up repositories and mocks
        _documentRepository = new DocumentRepository(
            _databaseContext,
            Mock.Of<ILogger<DocumentRepository>>());

        _mockOrchestrationService = new Mock<IKnowledgeFileOrchestrationService>();
        _mockOrchestrationService.Setup(x => x.IsRunning).Returns(true);
        _mockOrchestrationService.Setup(x => x.GetStatistics()).Returns(
            new KnowledgeFileOrchestrationStatistics
            {
                FilesProcessed = 10,
                FilesDeleted = 2,
                ProcessingErrors = 1,
                DeletionErrors = 0
            });

        _mockLogger = new Mock<ILogger<IndexingStatusService>>();

        _service = new IndexingStatusService(
            _databaseContext,
            _documentRepository,
            _mockLogger.Object,
            _mockOrchestrationService.Object);
    }

    public async Task DisposeAsync()
    {
        // Dispose database context first to release all handles
        await _databaseContext.DisposeAsync();
        
        // Retry file deletion with backoff (SQLite handle release can be async on Windows)
        await DeleteFileWithRetryAsync(_testDbPath, maxAttempts: 3, delayMs: 50);
        await DeleteFileWithRetryAsync(_testDbPath + "-wal", maxAttempts: 3, delayMs: 50);
        await DeleteFileWithRetryAsync(_testDbPath + "-shm", maxAttempts: 3, delayMs: 50);
    }

    private static async Task DeleteFileWithRetryAsync(string filePath, int maxAttempts, int delayMs)
    {
        if (!File.Exists(filePath))
            return;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                File.Delete(filePath);
                return; // Success
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                await Task.Delay(delayMs * attempt); // Exponential backoff
            }
            catch (IOException)
            {
                // Final attempt failed - ignore to avoid failing test cleanup
                return;
            }
        }
    }

    [Fact]
    public void Constructor_WithNullDatabaseContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new IndexingStatusService(
                null!,
                _documentRepository,
                _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullDocumentRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new IndexingStatusService(
                _databaseContext,
                null!,
                _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new IndexingStatusService(
                _databaseContext,
                _documentRepository,
                null!));
    }

    [Fact]
    public async Task GetIndexingStatisticsAsync_WithNoDocuments_ReturnsZeroStats()
    {
        // Act
        var stats = await _service.GetIndexingStatisticsAsync();

        // Assert
        Assert.NotNull(stats);
        Assert.Equal(0, stats.TotalIndexed);
        Assert.Equal(0, stats.TotalErrors);
        Assert.Equal(0, stats.TotalNotIndexed);
        Assert.True(stats.IsWatcherActive);
        Assert.NotNull(stats.OrchestrationStats);
    }

    [Fact]
    public async Task GetIndexingStatisticsAsync_WithIndexedDocuments_ReturnsCorrectCounts()
    {
        // Arrange
        await AddTestDocument("doc1", "indexed", "/path/to/file1.txt");
        await AddTestDocument("doc2", "indexed", "/path/to/file2.txt");
        await AddTestDocument("doc3", "error", "/path/to/file3.txt");
        await AddTestDocument("doc4", "pending", "/path/to/file4.txt");

        // Act
        var stats = await _service.GetIndexingStatisticsAsync();

        // Assert
        Assert.NotNull(stats);
        Assert.Equal(4, stats.TotalIndexed); // Total count includes all documents
        Assert.Equal(1, stats.TotalErrors);
        Assert.Equal(1, stats.TotalNotIndexed); // Pending count
    }

    [Fact]
    public async Task GetAllFilesAsync_WithNoDocuments_ReturnsEmptyList()
    {
        // Act
        var files = await _service.GetAllFilesAsync();

        // Assert
        Assert.NotNull(files);
        Assert.Empty(files);
    }

    [Fact]
    public async Task GetAllFilesAsync_WithDocuments_ReturnsAllFiles()
    {
        // Arrange
        await AddTestDocument("doc1", "indexed", "/path/to/file1.txt");
        await AddTestDocument("doc2", "indexed", "/path/to/file2.txt");

        // Act
        var files = await _service.GetAllFilesAsync();

        // Assert
        Assert.NotNull(files);
        Assert.Equal(2, files.Count);
        Assert.Contains(files, f => f.FilePath == "/path/to/file1.txt");
        Assert.Contains(files, f => f.FilePath == "/path/to/file2.txt");
    }

    [Fact]
    public async Task GetFilesByStatusAsync_FiltersByStatus()
    {
        // Arrange
        await AddTestDocument("doc1", "indexed", "/path/to/file1.txt");
        await AddTestDocument("doc2", "error", "/path/to/file2.txt");
        await AddTestDocument("doc3", "indexed", "/path/to/file3.txt");

        // Act
        var indexedFiles = await _service.GetFilesByStatusAsync(FileIndexingStatus.Indexed);
        var errorFiles = await _service.GetFilesByStatusAsync(FileIndexingStatus.Error);

        // Assert
        Assert.Equal(2, indexedFiles.Count);
        Assert.Single(errorFiles);
        Assert.All(indexedFiles, f => Assert.Equal(FileIndexingStatus.Indexed, f.Status));
        Assert.All(errorFiles, f => Assert.Equal(FileIndexingStatus.Error, f.Status));
    }

    [Fact]
    public async Task GetFilesByFormatAsync_FiltersByFormat()
    {
        // Arrange
        await AddTestDocument("doc1", "indexed", "/path/to/file1.pdf", "pdf");
        await AddTestDocument("doc2", "indexed", "/path/to/file2.txt", "txt");
        await AddTestDocument("doc3", "indexed", "/path/to/file3.pdf", "pdf");

        // Act
        var pdfFiles = await _service.GetFilesByFormatAsync("pdf");
        var txtFiles = await _service.GetFilesByFormatAsync("txt");

        // Assert
        Assert.Equal(2, pdfFiles.Count);
        Assert.Single(txtFiles);
        Assert.All(pdfFiles, f => Assert.Equal("pdf", f.Format));
        Assert.All(txtFiles, f => Assert.Equal("txt", f.Format));
    }

    [Fact]
       public async Task GetFilesByFormatAsync_WithNullFormat_ThrowsArgumentNullException()
    {
        // Act & Assert
           await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _service.GetFilesByFormatAsync(null!));
    }

    [Fact]
    public async Task SearchFilesAsync_FindsMatchingFiles()
    {
        // Arrange
        await AddTestDocument("doc1", "indexed", "/path/to/document1.pdf");
        await AddTestDocument("doc2", "indexed", "/path/to/document2.pdf");
        await AddTestDocument("doc3", "indexed", "/other/file.txt");

        // Act
        var results = await _service.SearchFilesAsync("document");

        // Assert
        Assert.Equal(2, results.Count);
        Assert.All(results, f => Assert.Contains("document", f.FilePath,
            StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
       public async Task SearchFilesAsync_WithNullSearchTerm_ThrowsArgumentNullException()
    {
        // Act & Assert
           await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _service.SearchFilesAsync(null!));
    }

    [Fact]
    public async Task GetFileDetailsAsync_ReturnsFileInfo()
    {
        // Arrange
        var filePath = "/path/to/file1.txt";
        await AddTestDocument("doc1", "indexed", filePath);

        // Act
        var file = await _service.GetFileDetailsAsync(filePath);

        // Assert
        Assert.NotNull(file);
        Assert.Equal(filePath, file.FilePath);
        Assert.Equal("doc1", file.DocId);
        Assert.Equal(FileIndexingStatus.Indexed, file.Status);
    }

    [Fact]
    public async Task GetFileDetailsAsync_WithNonexistentFile_ReturnsNull()
    {
        // Act
        var file = await _service.GetFileDetailsAsync("/nonexistent/file.txt");

        // Assert
        Assert.Null(file);
    }

    [Fact]
       public async Task GetFileDetailsAsync_WithNullFilePath_ThrowsArgumentNullException()
    {
        // Act & Assert
           await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _service.GetFileDetailsAsync(null!));
    }

    [Fact]
    public async Task GetAllFilesAsync_IncludesEmbeddingInfo_WhenTopicIndexExists()
    {
        // Arrange
        await AddTestDocument("doc1", "indexed", "/path/to/file1.txt");
        await AddTopicIndex("doc1", "This is a test summary", 384);

        // Act
        var files = await _service.GetAllFilesAsync();

        // Assert
        Assert.Single(files);
        Assert.Equal(384, files[0].EmbeddingDimension);
        Assert.NotNull(files[0].TopicSummary);
        Assert.StartsWith("This is a test", files[0].TopicSummary);
    }

    [Fact]
    public async Task GetAllFilesAsync_IncludesChunkCount_WhenChunksExist()
    {
        // Arrange
        await AddTestDocument("doc1", "indexed", "/path/to/file1.txt");
        await AddChunkIndex("chunk1", "doc1", 0, "Chunk 1", 768);
        await AddChunkIndex("chunk2", "doc1", 1, "Chunk 2", 768);

        // Act
        var files = await _service.GetAllFilesAsync();

        // Assert
        Assert.Single(files);
        Assert.Equal(2, files[0].ChunkCount);
    }

    private async Task AddTestDocument(
        string docId,
        string status,
        string sourcePath,
        string format = "txt")
    {
        using var connection = await _databaseContext.GetConnectionAsync();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO documents (doc_id, source_path, file_hash, format, size_bytes, last_modified, status, created_at)
            VALUES ($docId, $sourcePath, $fileHash, $format, $sizeBytes, $lastModified, $status, $createdAt)";
        
        command.Parameters.Add(new SqliteParameter("$docId", docId));
        command.Parameters.Add(new SqliteParameter("$sourcePath", sourcePath));
        command.Parameters.Add(new SqliteParameter("$fileHash", $"hash-{docId}"));
        command.Parameters.Add(new SqliteParameter("$format", format));
        command.Parameters.Add(new SqliteParameter("$sizeBytes", 1024));
        command.Parameters.Add(new SqliteParameter("$lastModified", DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
        command.Parameters.Add(new SqliteParameter("$status", status));
        command.Parameters.Add(new SqliteParameter("$createdAt", DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
        
        await command.ExecuteNonQueryAsync();
    }

    private async Task AddTopicIndex(string docId, string summary, int dimensions)
    {
        using var connection = await _databaseContext.GetConnectionAsync();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO topic_index (doc_id, summary_text, embedding_blob, embedding_dimensions, source_path, file_hash, ingested_at)
            VALUES ($docId, $summary, $embeddingBlob, $dimensions, $sourcePath, $fileHash, $ingestedAt)";
        
        command.Parameters.Add(new SqliteParameter("$docId", docId));
        command.Parameters.Add(new SqliteParameter("$summary", summary));
        command.Parameters.Add(new SqliteParameter("$embeddingBlob", new byte[dimensions * 4]));
        command.Parameters.Add(new SqliteParameter("$dimensions", dimensions));
        command.Parameters.Add(new SqliteParameter("$sourcePath", "/test/path"));
        command.Parameters.Add(new SqliteParameter("$fileHash", "test-hash"));
        command.Parameters.Add(new SqliteParameter("$ingestedAt", DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
        
        await command.ExecuteNonQueryAsync();
    }

    private async Task AddChunkIndex(string chunkId, string docId, int chunkOrder, string chunkText, int dimensions)
    {
        using var connection = await _databaseContext.GetConnectionAsync();
        using var command = connection.CreateCommand();
        command.CommandText = @"
               INSERT INTO chunk_index (chunk_id, doc_id, chunk_order, chunk_text, embedding_blob, embedding_dimensions, created_at)
               VALUES ($chunkId, $docId, $chunkOrder, $chunkText, $embeddingBlob, $dimensions, $createdAt)";
        
        command.Parameters.Add(new SqliteParameter("$chunkId", chunkId));
        command.Parameters.Add(new SqliteParameter("$docId", docId));
        command.Parameters.Add(new SqliteParameter("$chunkOrder", chunkOrder));
        command.Parameters.Add(new SqliteParameter("$chunkText", chunkText));
        command.Parameters.Add(new SqliteParameter("$embeddingBlob", new byte[dimensions * 4]));
        command.Parameters.Add(new SqliteParameter("$dimensions", dimensions));
           command.Parameters.Add(new SqliteParameter("$createdAt", DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
        
        await command.ExecuteNonQueryAsync();
    }
}
