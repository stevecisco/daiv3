using Daiv3.Persistence;
using Daiv3.Persistence.Entities;
using Daiv3.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Daiv3.UnitTests.Persistence;

/// <summary>
/// Unit tests for WebFetchRepository.
/// Tests CRUD operations, filtering by URL, date range, content hash, and status.
/// Implements WFC-DATA-001: Metadata SHALL include source URL, fetch date, and content hash.
/// These tests use an in-memory SQLite database for isolation.
/// </summary>
public class WebFetchRepositoryTests : IAsyncLifetime
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IDatabaseContext _databaseContext;
    private readonly WebFetchRepository _repository;
    private readonly DocumentRepository _documentRepository;
    private readonly ILogger<WebFetchRepositoryTests> _logger;
    private readonly string _testDbPath;

    public WebFetchRepositoryTests()
    {
        // Use a temporary database file for each test
        _testDbPath = Path.Combine(Path.GetTempPath(), $"web-fetch-test-{Guid.NewGuid()}.db");
        
        var services = new ServiceCollection();
        services.AddLogging();
        
        services.Configure<PersistenceOptions>(options =>
        {
            options.DatabasePath = _testDbPath;
        });
        
        services.AddPersistence();

        _serviceProvider = services.BuildServiceProvider();
        _databaseContext = _serviceProvider.GetRequiredService<IDatabaseContext>();
        _repository = new WebFetchRepository(
            _databaseContext,
            _serviceProvider.GetRequiredService<ILogger<WebFetchRepository>>()
        );
        _documentRepository = new DocumentRepository(
            _databaseContext,
            _serviceProvider.GetRequiredService<ILogger<DocumentRepository>>()
        );
        _logger = _serviceProvider.GetRequiredService<ILogger<WebFetchRepositoryTests>>();
    }

    public async Task InitializeAsync()
    {
        await _databaseContext.InitializeAsync();
        _logger.LogInformation("Test database initialized");
    }

    public async Task DisposeAsync()
    {
        await _databaseContext.DisposeAsync();
        
        try
        {
            if (File.Exists(_testDbPath))
            {
                File.Delete(_testDbPath);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    private Daiv3.Persistence.Entities.WebFetch CreateTestWebFetch(
        string docId,
        string sourceUrl = "https://example.com/test",
        string contentHash = "hash123",
        string status = "active",
        string? title = "Test Page",
        string? description = "Test description")
    {
        return new Daiv3.Persistence.Entities.WebFetch
        {
            WebFetchId = Guid.NewGuid().ToString(),
            DocId = docId,
            SourceUrl = sourceUrl,
            ContentHash = contentHash,
            FetchDate = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Title = title,
            Description = description,
            Status = status,
            ErrorMessage = null,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
    }

    private Document CreateTestDocument(string docId, string sourcePath)
    {
        return new Document
        {
            DocId = docId,
            SourcePath = sourcePath,
            FileHash = "filehash123",
            Format = "web",
            SizeBytes = 5000,
            LastModified = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Status = "indexed",
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            MetadataJson = null
        };
    }

    [Fact]
    public async Task AddAsync_CreatesWebFetch()
    {
        // Arrange
        var docId = Guid.NewGuid().ToString();
        var document = CreateTestDocument(docId, "/test/path.md");
        await _documentRepository.AddAsync(document);

        var webFetch = CreateTestWebFetch(docId);

        // Act
        var result = await _repository.AddAsync(webFetch);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(webFetch.WebFetchId, result);
        
        var retrieved = await _repository.GetByIdAsync(webFetch.WebFetchId);
        Assert.NotNull(retrieved);
        Assert.Equal(webFetch.SourceUrl, retrieved.SourceUrl);
        Assert.Equal(webFetch.ContentHash, retrieved.ContentHash);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsWebFetch()
    {
        // Arrange
        var docId = Guid.NewGuid().ToString();
        var document = CreateTestDocument(docId, "/test/path.md");
        await _documentRepository.AddAsync(document);

        var webFetch = CreateTestWebFetch(docId);
        await _repository.AddAsync(webFetch);

        // Act
        var result = await _repository.GetByIdAsync(webFetch.WebFetchId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(webFetch.WebFetchId, result.WebFetchId);
        Assert.Equal(webFetch.SourceUrl, result.SourceUrl);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNullForMissing()
    {
        // Act
        var result = await _repository.GetByIdAsync("nonexistent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesWebFetch()
    {
        // Arrange
        var docId = Guid.NewGuid().ToString();
        var document = CreateTestDocument(docId, "/test/path.md");
        await _documentRepository.AddAsync(document);

        var webFetch = CreateTestWebFetch(docId);
        await _repository.AddAsync(webFetch);

        webFetch.Status = "stale";
        webFetch.Title = "Updated Title";
        webFetch.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Act
        await _repository.UpdateAsync(webFetch);

        // Assert
        var retrieved = await _repository.GetByIdAsync(webFetch.WebFetchId);
        Assert.NotNull(retrieved);
        Assert.Equal("stale", retrieved.Status);
        Assert.Equal("Updated Title", retrieved.Title);
    }

    [Fact]
    public async Task DeleteAsync_RemovesWebFetch()
    {
        // Arrange
        var docId = Guid.NewGuid().ToString();
        var document = CreateTestDocument(docId, "/test/path.md");
        await _documentRepository.AddAsync(document);

        var webFetch = CreateTestWebFetch(docId);
        await _repository.AddAsync(webFetch);

        // Act
        await _repository.DeleteAsync(webFetch.WebFetchId);

        // Assert
        var result = await _repository.GetByIdAsync(webFetch.WebFetchId);
        Assert.Null(result);
    }

    [Fact]
    public async Task GetBySourceUrlAsync_FindsWebFetch()
    {
        // Arrange
        var docId = Guid.NewGuid().ToString();
        var document = CreateTestDocument(docId, "/test/path.md");
        await _documentRepository.AddAsync(document);

        var sourceUrl = "https://example.com/article";
        var webFetch = CreateTestWebFetch(docId, sourceUrl);
        await _repository.AddAsync(webFetch);

        // Act
        var result = await _repository.GetBySourceUrlAsync(sourceUrl);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(sourceUrl, result.SourceUrl);
    }

    [Fact]
    public async Task GetBySourceUrlAsync_ReturnsNullForMissing()
    {
        // Act
        var result = await _repository.GetBySourceUrlAsync("https://notfound.com");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByDocIdAsync_ReturnsAllFetchesForDocument()
    {
        // Arrange
        var docId = Guid.NewGuid().ToString();
        var document = CreateTestDocument(docId, "/test/path.md");
        await _documentRepository.AddAsync(document);

        var fetch1 = CreateTestWebFetch(docId, "https://example.com/1");
        var fetch2 = CreateTestWebFetch(docId, "https://example.com/2");
        var fetch3 = CreateTestWebFetch(docId, "https://example.com/3");

        await _repository.AddAsync(fetch1);
        await _repository.AddAsync(fetch2);
        await _repository.AddAsync(fetch3);

        // Act
        var results = await _repository.GetByDocIdAsync(docId);

        // Assert
        Assert.Equal(3, results.Count);
        Assert.Contains(results, w => w.SourceUrl == "https://example.com/1");
        Assert.Contains(results, w => w.SourceUrl == "https://example.com/2");
        Assert.Contains(results, w => w.SourceUrl == "https://example.com/3");
    }

    [Fact]
    public async Task GetByStatusAsync_FiltersByStatus()
    {
        // Arrange
        var docId1 = Guid.NewGuid().ToString();
        var docId2 = Guid.NewGuid().ToString();
        
        var doc1 = CreateTestDocument(docId1, "/test/path1.md");
        var doc2 = CreateTestDocument(docId2, "/test/path2.md");
        
        await _documentRepository.AddAsync(doc1);
        await _documentRepository.AddAsync(doc2);

        var active = CreateTestWebFetch(docId1, "https://example.com/1", status: "active");
        var stale = CreateTestWebFetch(docId2, "https://example.com/2", status: "stale");

        await _repository.AddAsync(active);
        await _repository.AddAsync(stale);

        // Act
        var activeResults = await _repository.GetByStatusAsync("active");
        var staleResults = await _repository.GetByStatusAsync("stale");

        // Assert
        Assert.Single(activeResults);
        Assert.Equal("active", activeResults[0].Status);
        
        Assert.Single(staleResults);
        Assert.Equal("stale", staleResults[0].Status);
    }

    [Fact]
    public async Task GetFetchedBeforeDateAsync_ReturnsFetchesBeforeDate()
    {
        // Arrange
        var docId = Guid.NewGuid().ToString();
        var document = CreateTestDocument(docId, "/test/path.md");
        await _documentRepository.AddAsync(document);

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var oneHourAgo = now - 3600;
        var twoHoursAgo = now - 7200;

        var fetch1 = CreateTestWebFetch(docId, "https://example.com/1");
        fetch1.FetchDate = twoHoursAgo;
        
        var fetch2 = CreateTestWebFetch(docId, "https://example.com/2");
        fetch2.FetchDate = oneHourAgo;

        await _repository.AddAsync(fetch1);
        await _repository.AddAsync(fetch2);

        // Act
        var results = await _repository.GetFetchedBeforeDateAsync(oneHourAgo);

        // Assert
        Assert.Single(results);
        Assert.Equal(twoHoursAgo, results[0].FetchDate);
    }

    [Fact]
    public async Task GetFetchedAfterDateAsync_ReturnsFetchesAfterDate()
    {
        // Arrange
        var docId = Guid.NewGuid().ToString();
        var document = CreateTestDocument(docId, "/test/path.md");
        await _documentRepository.AddAsync(document);

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var oneHourAgo = now - 3600;
        var twoHoursAgo = now - 7200;

        var fetch1 = CreateTestWebFetch(docId, "https://example.com/1");
        fetch1.FetchDate = twoHoursAgo;
        
        var fetch2 = CreateTestWebFetch(docId, "https://example.com/2");
        fetch2.FetchDate = oneHourAgo;

        await _repository.AddAsync(fetch1);
        await _repository.AddAsync(fetch2);

        // Act
        var results = await _repository.GetFetchedAfterDateAsync(twoHoursAgo);

        // Assert
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task GetByContentHashAsync_FindsWebFetchesByHash()
    {
        // Arrange
        var docId1 = Guid.NewGuid().ToString();
        var docId2 = Guid.NewGuid().ToString();
        
        var doc1 = CreateTestDocument(docId1, "/test/path1.md");
        var doc2 = CreateTestDocument(docId2, "/test/path2.md");
        
        await _documentRepository.AddAsync(doc1);
        await _documentRepository.AddAsync(doc2);

        const string hash = "content_hash_123";
        var fetch1 = CreateTestWebFetch(docId1, "https://example.com/1", hash);
        var fetch2 = CreateTestWebFetch(docId2, "https://example.com/2", hash);

        await _repository.AddAsync(fetch1);
        await _repository.AddAsync(fetch2);

        // Act
        var results = await _repository.GetByContentHashAsync(hash);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.All(results, w => Assert.Equal(hash, w.ContentHash));
    }

    [Fact]
    public async Task GetMostRecentBySourceUrlAsync_ReturnsMostRecent()
    {
        // Arrange
        var docId = Guid.NewGuid().ToString();
        var document = CreateTestDocument(docId, "/test/path.md");
        await _documentRepository.AddAsync(document);

        var sourceUrl = "https://example.com/article";
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var fetchOld = CreateTestWebFetch(docId, sourceUrl, "hash1");
        fetchOld.FetchDate = now - 3600;
        
        var fetchNew = CreateTestWebFetch(docId, sourceUrl, "hash2");
        fetchNew.FetchDate = now;

        await _repository.AddAsync(fetchOld);
        await _repository.AddAsync(fetchNew);

        // Act
        var result = await _repository.GetMostRecentBySourceUrlAsync(sourceUrl);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(now, result.FetchDate);
        Assert.Equal("hash2", result.ContentHash);
    }

    [Fact]
    public async Task UpdateStatusAsync_UpdatesStatusAndError()
    {
        // Arrange
        var docId = Guid.NewGuid().ToString();
        var document = CreateTestDocument(docId, "/test/path.md");
        await _documentRepository.AddAsync(document);

        var webFetch = CreateTestWebFetch(docId);
        await _repository.AddAsync(webFetch);

        const string errorMsg = "Connection timeout";

        // Act
        await _repository.UpdateStatusAsync(webFetch.WebFetchId, "error", errorMsg);

        // Assert
        var updated = await _repository.GetByIdAsync(webFetch.WebFetchId);
        Assert.NotNull(updated);
        Assert.Equal("error", updated.Status);
        Assert.Equal(errorMsg, updated.ErrorMessage);
    }

    [Fact]
    public async Task UpdateContentAsync_UpdatesHashAndDate()
    {
        // Arrange
        var docId = Guid.NewGuid().ToString();
        var document = CreateTestDocument(docId, "/test/path.md");
        await _documentRepository.AddAsync(document);

        var webFetch = CreateTestWebFetch(docId);
        await _repository.AddAsync(webFetch);

        var newHash = "newhash456";
        var newFetchDate = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();

        // Act
        await _repository.UpdateContentAsync(webFetch.WebFetchId, newHash, newFetchDate);

        // Assert
        var updated = await _repository.GetByIdAsync(webFetch.WebFetchId);
        Assert.NotNull(updated);
        Assert.Equal(newHash, updated.ContentHash);
        Assert.Equal(newFetchDate, updated.FetchDate);
        Assert.Equal("active", updated.Status);
        Assert.Null(updated.ErrorMessage);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllWebFetches()
    {
        // Arrange
        var docId1 = Guid.NewGuid().ToString();
        var docId2 = Guid.NewGuid().ToString();
        
        var doc1 = CreateTestDocument(docId1, "/test/path1.md");
        var doc2 = CreateTestDocument(docId2, "/test/path2.md");
        
        await _documentRepository.AddAsync(doc1);
        await _documentRepository.AddAsync(doc2);

        var fetch1 = CreateTestWebFetch(docId1);
        var fetch2 = CreateTestWebFetch(docId2);

        await _repository.AddAsync(fetch1);
        await _repository.AddAsync(fetch2);

        // Act
        var results = await _repository.GetAllAsync();

        // Assert
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task NullableFields_ArePersisted()
    {
        // Arrange
        var docId = Guid.NewGuid().ToString();
        var document = CreateTestDocument(docId, "/test/path.md");
        await _documentRepository.AddAsync(document);

        var webFetch = CreateTestWebFetch(docId, title: null, description: null);

        // Act
        await _repository.AddAsync(webFetch);
        var retrieved = await _repository.GetByIdAsync(webFetch.WebFetchId);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Null(retrieved.Title);
        Assert.Null(retrieved.Description);
    }

    [Fact]
    public async Task MultipleDocuments_WithDifferentFetches_PreserveIsolation()
    {
        // Arrange
        var docId1 = Guid.NewGuid().ToString();
        var docId2 = Guid.NewGuid().ToString();
        
        var doc1 = CreateTestDocument(docId1, "/test/path1.md");
        var doc2 = CreateTestDocument(docId2, "/test/path2.md");
        
        await _documentRepository.AddAsync(doc1);
        await _documentRepository.AddAsync(doc2);

        var fetch1 = CreateTestWebFetch(docId1, "https://site1.com/page");
        var fetch2 = CreateTestWebFetch(docId2, "https://site2.com/page");

        await _repository.AddAsync(fetch1);
        await _repository.AddAsync(fetch2);

        // Act
        var results1 = await _repository.GetByDocIdAsync(docId1);
        var results2 = await _repository.GetByDocIdAsync(docId2);

        // Assert
        Assert.Single(results1);
        Assert.Equal("https://site1.com/page", results1[0].SourceUrl);
        
        Assert.Single(results2);
        Assert.Equal("https://site2.com/page", results2[0].SourceUrl);
    }
}
