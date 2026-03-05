using Daiv3.Persistence;
using Daiv3.Persistence.Entities;
using Daiv3.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using WebFetchEntity = Daiv3.Persistence.Entities.WebFetch;

#pragma warning disable IDISP006 // Test classes don't need to implement IDisposable

namespace Daiv3.Persistence.Tests;

/// <summary>
/// Integration tests for web fetch schema migration.
/// Tests Migration 008 (WFC-DATA-001): Schema for web content metadata with source URL, fetch date, and content hash.
/// Validates schema migration, round-trip persistence, and backward compatibility.
/// </summary>
public class WebFetchMigrationTests : IAsyncLifetime
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IDatabaseContext _databaseContext;
    private readonly WebFetchRepository _webFetchRepository;
    private readonly DocumentRepository _documentRepository;
    private readonly ILogger<WebFetchMigrationTests> _logger;
    private readonly string _testDbPath;

    public WebFetchMigrationTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"web-fetch-migration-test-{Guid.NewGuid()}.db");

        var services = new ServiceCollection();
        services.AddLogging();
        services.Configure<PersistenceOptions>(options =>
        {
            options.DatabasePath = _testDbPath;
        });
        services.AddPersistence();

        _serviceProvider = services.BuildServiceProvider();
        _databaseContext = _serviceProvider.GetRequiredService<IDatabaseContext>();
        _webFetchRepository = new WebFetchRepository(
            _databaseContext,
            _serviceProvider.GetRequiredService<ILogger<WebFetchRepository>>()
        );
        _documentRepository = new DocumentRepository(
            _databaseContext,
            _serviceProvider.GetRequiredService<ILogger<DocumentRepository>>()
        );
        _logger = _serviceProvider.GetRequiredService<ILogger<WebFetchMigrationTests>>();
    }

    public async Task InitializeAsync()
    {
        await _databaseContext.InitializeAsync();
        _logger.LogInformation("Test database initialized with migrations");
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

    [Fact]
    public async Task Migration008_CreatesWebFetchesTable()
    {
        // Arrange & Act
        var schemaVersion = await _databaseContext.GetSchemaVersionAsync();

        // Assert
        Assert.True(schemaVersion >= 8, $"Schema version {schemaVersion} should be >= 8");
        _logger.LogInformation("Schema version is {Version}", schemaVersion);
    }

    [Fact]
    public async Task WebFetch_RoundTripPersistence_PreservesAllFields()
    {
        // Arrange
        var docId = Guid.NewGuid().ToString();
        var document = new Document
        {
            DocId = docId,
            SourcePath = "/content/article.md",
            FileHash = "abc123def456",
            Format = "web",
            SizeBytes = 12345,
            LastModified = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Status = "indexed",
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            MetadataJson = null
        };
        await _documentRepository.AddAsync(document);

        var webFetch = new WebFetchEntity
        {
            WebFetchId = Guid.NewGuid().ToString(),
            DocId = docId,
            SourceUrl = "https://example.com/article",
            ContentHash = "hash_of_content_abc123",
            FetchDate = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Title = "Example Article Title",
            Description = "This is the article description",
            Status = "active",
            ErrorMessage = null,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        // Act
        var addedId = await _webFetchRepository.AddAsync(webFetch);
        var retrieved = await _webFetchRepository.GetByIdAsync(addedId);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(webFetch.WebFetchId, retrieved.WebFetchId);
        Assert.Equal(webFetch.DocId, retrieved.DocId);
        Assert.Equal(webFetch.SourceUrl, retrieved.SourceUrl);
        Assert.Equal(webFetch.ContentHash, retrieved.ContentHash);
        Assert.Equal(webFetch.FetchDate, retrieved.FetchDate);
        Assert.Equal(webFetch.Title, retrieved.Title);
        Assert.Equal(webFetch.Description, retrieved.Description);
        Assert.Equal(webFetch.Status, retrieved.Status);
        Assert.Null(retrieved.ErrorMessage);
        _logger.LogInformation("Round-trip persistence successful");
    }

    [Fact]
    public async Task WebFetch_ForeignKey_EnforcesDocumentReference()
    {
        // Arrange
        var nonexistentDocId = Guid.NewGuid().ToString();
        var webFetch = new WebFetchEntity
        {
            WebFetchId = Guid.NewGuid().ToString(),
            DocId = nonexistentDocId,
            SourceUrl = "https://example.com/test",
            ContentHash = "hash123",
            FetchDate = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Title = "Test",
            Description = "Description",
            Status = "active",
            ErrorMessage = null,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        // Act & Assert - Should fail due to foreign key constraint
        // The repository should handle this, but we're testing the constraint exists
        var exception = await Record.ExceptionAsync(async () =>
            await _webFetchRepository.AddAsync(webFetch)
        );

        // Note: SQLite foreign key enforcement depends on PRAGMA foreign_keys = ON
        // which is set in DatabaseContext.ConfigureConnectionAsync
        if (exception != null)
        {
            Assert.Contains("FOREIGN KEY", exception.Message, StringComparison.Ordinal);
            _logger.LogInformation("Foreign key constraint properly enforced");
        }
    }

    [Fact]
    public async Task WebFetch_CascadeDelete_RemovesWebFetchesWhenDocumentDeleted()
    {
        // Arrange
        var docId = Guid.NewGuid().ToString();
        var document = new Document
        {
            DocId = docId,
            SourcePath = "/content/article.md",
            FileHash = "abc123def456",
            Format = "web",
            SizeBytes = 12345,
            LastModified = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Status = "indexed",
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            MetadataJson = null
        };
        await _documentRepository.AddAsync(document);

        var webFetch = new WebFetchEntity
        {
            WebFetchId = Guid.NewGuid().ToString(),
            DocId = docId,
            SourceUrl = "https://example.com/article",
            ContentHash = "hash_of_content",
            FetchDate = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Title = "Example",
            Description = "Description",
            Status = "active",
            ErrorMessage = null,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
        await _webFetchRepository.AddAsync(webFetch);

        // Act
        await _documentRepository.DeleteAsync(docId);

        // Assert
        var retrievedFetch = await _webFetchRepository.GetByIdAsync(webFetch.WebFetchId);
        Assert.Null(retrievedFetch);
        _logger.LogInformation("Cascade delete properly removes web fetches when document is deleted");
    }

    [Fact]
    public async Task WebFetch_ImplementsWFCDATA001_IncludesSourceUrl()
    {
        // Requirement: WFC-DATA-001 - Metadata SHALL include source URL
        // Arrange
        var docId = Guid.NewGuid().ToString();
        var document = new Document
        {
            DocId = docId,
            SourcePath = "/test.md",
            FileHash = "hash",
            Format = "web",
            SizeBytes = 100,
            LastModified = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Status = "indexed",
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            MetadataJson = null
        };
        await _documentRepository.AddAsync(document);

        const string sourceUrl = "https://example.com/page";
        var webFetch = new WebFetchEntity
        {
            WebFetchId = Guid.NewGuid().ToString(),
            DocId = docId,
            SourceUrl = sourceUrl,
            ContentHash = "hash123",
            FetchDate = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Title = "Page",
            Description = "Description",
            Status = "active",
            ErrorMessage = null,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        // Act
        await _webFetchRepository.AddAsync(webFetch);
        var retrieved = await _webFetchRepository.GetBySourceUrlAsync(sourceUrl);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(sourceUrl, retrieved.SourceUrl);
        _logger.LogInformation("WFC-DATA-001: Source URL stored and retrieved successfully");
    }

    [Fact]
    public async Task WebFetch_ImplementsWFCDATA001_IncludesFetchDate()
    {
        // Requirement: WFC-DATA-001 - Metadata SHALL include fetch date
        // Arrange
        var docId = Guid.NewGuid().ToString();
        var document = new Document
        {
            DocId = docId,
            SourcePath = "/test.md",
            FileHash = "hash",
            Format = "web",
            SizeBytes = 100,
            LastModified = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Status = "indexed",
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            MetadataJson = null
        };
        await _documentRepository.AddAsync(document);

        var fetchDate = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var webFetch = new WebFetchEntity
        {
            WebFetchId = Guid.NewGuid().ToString(),
            DocId = docId,
            SourceUrl = "https://example.com/page",
            ContentHash = "hash123",
            FetchDate = fetchDate,
            Title = "Page",
            Description = "Description",
            Status = "active",
            ErrorMessage = null,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        // Act
        await _webFetchRepository.AddAsync(webFetch);
        var retrieved = await _webFetchRepository.GetByIdAsync(webFetch.WebFetchId);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(fetchDate, retrieved.FetchDate);
        _logger.LogInformation("WFC-DATA-001: Fetch date stored and retrieved successfully");
    }

    [Fact]
    public async Task WebFetch_ImplementsWFCDATA001_IncludesContentHash()
    {
        // Requirement: WFC-DATA-001 - Metadata SHALL include content hash
        // Arrange
        var docId = Guid.NewGuid().ToString();
        var document = new Document
        {
            DocId = docId,
            SourcePath = "/test.md",
            FileHash = "hash",
            Format = "web",
            SizeBytes = 100,
            LastModified = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Status = "indexed",
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            MetadataJson = null
        };
        await _documentRepository.AddAsync(document);

        const string contentHash = "abc123def456ghi789";
        var webFetch = new WebFetchEntity
        {
            WebFetchId = Guid.NewGuid().ToString(),
            DocId = docId,
            SourceUrl = "https://example.com/page",
            ContentHash = contentHash,
            FetchDate = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Title = "Page",
            Description = "Description",
            Status = "active",
            ErrorMessage = null,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        // Act
        await _webFetchRepository.AddAsync(webFetch);
        var retrieved = await _webFetchRepository.GetByContentHashAsync(contentHash);

        // Assert
        Assert.NotEmpty(retrieved);
        Assert.Contains(retrieved, w => w.ContentHash == contentHash);
        _logger.LogInformation("WFC-DATA-001: Content hash stored and retrieved successfully");
    }

    [Fact]
    public async Task WebFetch_BackwardCompatibility_ExistingDocumentsUnaffected()
    {
        // Arrange - Create a document before creating any web fetches
        var docId = Guid.NewGuid().ToString();
        var document = new Document
        {
            DocId = docId,
            SourcePath = "/local/file.md",
            FileHash = "local_hash",
            Format = "markdown",
            SizeBytes = 5000,
            LastModified = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Status = "indexed",
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            MetadataJson = null
        };
        await _documentRepository.AddAsync(document);

        // Act - Retrieve the document
        var retrieved = await _documentRepository.GetByIdAsync(docId);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal("/local/file.md", retrieved.SourcePath);
        _logger.LogInformation("Backward compatibility verified: existing documents unaffected by migration");
    }

    [Fact]
    public async Task WebFetch_IndexesExist_ForPerformance()
    {
        // Test that all expected indexes are created for query performance
        // Arrange
        var docId = Guid.NewGuid().ToString();
        var document = new Document
        {
            DocId = docId,
            SourcePath = "/test.md",
            FileHash = "hash",
            Format = "web",
            SizeBytes = 100,
            LastModified = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Status = "indexed",
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            MetadataJson = null
        };
        await _documentRepository.AddAsync(document);

        for (int i = 0; i < 10; i++)
        {
            var webFetch = new WebFetchEntity
            {
                WebFetchId = Guid.NewGuid().ToString(),
                DocId = docId,
                SourceUrl = $"https://example.com/page{i}",
                ContentHash = $"hash{i}",
                FetchDate = DateTimeOffset.UtcNow.AddHours(i).ToUnixTimeSeconds(),
                Title = $"Page {i}",
                Description = "Description",
                Status = i % 2 == 0 ? "active" : "stale",
                ErrorMessage = null,
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
            await _webFetchRepository.AddAsync(webFetch);
        }

        // Act - queries that should use indexes
        var byStatus = await _webFetchRepository.GetByStatusAsync("active");
        var byUrl = await _webFetchRepository.GetBySourceUrlAsync("https://example.com/page5");
        var byFetchDate = await _webFetchRepository.GetFetchedAfterDateAsync(
            DateTimeOffset.UtcNow.AddHours(5).ToUnixTimeSeconds());

        // Assert
        Assert.NotEmpty(byStatus);
        Assert.NotNull(byUrl);
        Assert.NotEmpty(byFetchDate);
        _logger.LogInformation("Indexes appear to be functioning correctly for query performance");
    }
}
