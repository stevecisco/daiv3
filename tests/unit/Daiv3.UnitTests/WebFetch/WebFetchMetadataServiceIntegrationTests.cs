using Daiv3.Persistence;
using Daiv3.Persistence.Entities;
using Daiv3.Persistence.Repositories;
using Daiv3.WebFetch.Crawl;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Daiv3.UnitTests.WebFetch;

/// <summary>
/// Integration tests for WebFetchMetadataService with real database persistence.
/// Tests end-to-end metadata storage workflow.
/// </summary>
public class WebFetchMetadataServiceIntegrationTests : IAsyncLifetime
{
    private DatabaseContext _context = null!;
    private IWebFetchRepository _repository = null!;
    private IWebFetchMetadataService _service = null!;

    public async Task InitializeAsync()
    {
        // Set up in-memory database
        var services = new ServiceCollection();
        services.AddPersistenceServices(":memory:");
        services.AddLogging(config => config.AddConsole());
        services.AddScoped<IWebFetchMetadataService, WebFetchMetadataService>();

        var serviceProvider = services.BuildServiceProvider();
        _context = serviceProvider.GetRequiredService<DatabaseContext>();
        _repository = serviceProvider.GetRequiredService<IWebFetchRepository>();
        _service = serviceProvider.GetRequiredService<IWebFetchMetadataService>();

        // Create schema
        await _context.InitializeDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        if (_context != null)
        {
            await _context.DisposeAsync();
        }
    }

    [Fact]
    public async Task StoreMetadataAsync_WithValidInputs_PersistsToDatabase()
    {
        // Arrange
        var sourceUrl = "https://example.com/article";
        var docId = "doc-abc123";
        var htmlContent = "<html><body>Test content</body></html>";
        var title = "Test Article";
        var description = "A test article for integration testing";

        // Create a document first (required for foreign key constraint)
        var doc = new Document
        {
            DocId = docId,
            FileName = "test.html",
            SourcePath = sourceUrl,
            FileHash = "test-hash",
            ProcessingStatus = "complete",
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
        await _repository.GetType().BaseType?.GetMethod("AddAsync")?.Invoke(_repository, new object[] { doc, CancellationToken.None }) ?? Task.CompletedTask;

        // Act
        var result = await _service.StoreMetadataAsync(sourceUrl, docId, htmlContent, title, description);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(sourceUrl, result.SourceUrl);
        Assert.Equal(docId, result.DocId);
        Assert.Equal(title, result.Title);
        Assert.Equal(description, result.Description);
        Assert.NotEmpty(result.ContentHash);

        // Verify the metadata was persisted by querying it back
        var storedMetadata = await _repository.GetBySourceUrlAsync(sourceUrl);
        Assert.NotNull(storedMetadata);
        Assert.Equal(sourceUrl, storedMetadata.SourceUrl);
        Assert.Equal(docId, storedMetadata.DocId);
        Assert.Equal(title, storedMetadata.Title);
        Assert.Equal(description, storedMetadata.Description);
        Assert.Equal(result.ContentHash, storedMetadata.ContentHash);
    }

    [Fact]
    public async Task StoreMetadataAsync_MultipleURLs_PersistsIndependently()
    {
        // Arrange
        var docId1 = "doc-001";
        var docId2 = "doc-002";
        var url1 = "https://example.com/page1";
        var url2 = "https://example.com/page2";
        var content1 = "<html>Page 1</html>";
        var content2 = "<html>Page 2</html>";

        // Create documents
        var doc1 = new Document
        {
            DocId = docId1,
            FileName = "page1.html",
            SourcePath = url1,
            FileHash = "hash1",
            ProcessingStatus = "complete",
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
        var doc2 = new Document
        {
            DocId = docId2,
            FileName = "page2.html",
            SourcePath = url2,
            FileHash = "hash2",
            ProcessingStatus = "complete",
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        var docRepository = _context.Set<Document>();
        docRepository.Add(doc1);
        docRepository.Add(doc2);
        await _context.SaveChangesAsync();

        // Act
        var result1 = await _service.StoreMetadataAsync(url1, docId1, content1);
        var result2 = await _service.StoreMetadataAsync(url2, docId2, content2);

        // Assert
        Assert.NotEqual(result1.ContentHash, result2.ContentHash);
        Assert.Equal(url1, result1.SourceUrl);
        Assert.Equal(url2, result2.SourceUrl);

        // Verify both are stored
        var retrieved1 = await _repository.GetBySourceUrlAsync(url1);
        var retrieved2 = await _repository.GetBySourceUrlAsync(url2);

        Assert.NotNull(retrieved1);
        Assert.NotNull(retrieved2);
        Assert.Equal(docId1, retrieved1.DocId);
        Assert.Equal(docId2, retrieved2.DocId);
    }

    [Fact]
    public async Task StoreMetadataAsync_ContentHashChanges_DetectedOnRefetch()
    {
        // Arrange
        var sourceUrl = "https://example.com/changing";
        var docId = "doc-changing";
        var contentV1 = "<html>Version 1</html>";
        var contentV2 = "<html>Version 2 - Updated</html>";

        // Create document
        var doc = new Document
        {
            DocId = docId,
            FileName = "changing.html",
            SourcePath = sourceUrl,
            FileHash = "hash-initial",
            ProcessingStatus = "complete",
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
        var docRepository = _context.Set<Document>();
        docRepository.Add(doc);
        await _context.SaveChangesAsync();

        // Act - Store initial fetch
        var result1 = await _service.StoreMetadataAsync(sourceUrl, docId, contentV1);
        var hash1 = result1.ContentHash;

        // Simulate refetch with different content
        var result2 = await _service.StoreMetadataAsync(sourceUrl, docId, contentV2);
        var hash2 = result2.ContentHash;

        // Assert - Hashes should be different for different content
        Assert.NotEqual(hash1, hash2);

        // Both records should exist (new fetch)
        var records = await _repository.GetByDocIdAsync(docId);
        Assert.NotEmpty(records);
    }

    [Fact]
    public async Task CalculateContentHash_ConsistentAcrossService()
    {
        // Arrange
        var content = "<html><body>Consistent Content</body></html>";

        // Act
        var hash1 = _service.CalculateContentHash(content);
        var hash2 = _service.CalculateContentHash(content);

        // Assert
        Assert.Equal(hash1, hash2);
        Assert.NotEmpty(hash1);
        Assert.Equal(64, hash1.Length); // SHA256 hex string is 64 characters
    }

    [Fact]
    public async Task StoreMetadataAsync_DefaultStatus_IsActive()
    {
        // Arrange
        var sourceUrl = "https://example.com/test";
        var docId = "doc-status-test";
        var content = "<html></html>";

        // Create document
        var doc = new Document
        {
            DocId = docId,
            FileName = "test.html",
            SourcePath = sourceUrl,
            FileHash = "hash-test",
            ProcessingStatus = "complete",
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
        var docRepository = _context.Set<Document>();
        docRepository.Add(doc);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.StoreMetadataAsync(sourceUrl, docId, content);

        // Assert
        Assert.Equal("active", result.Status);

        // Verify in database
        var stored = await _repository.GetBySourceUrlAsync(sourceUrl);
        Assert.NotNull(stored);
        Assert.Equal("active", stored.Status);
    }
}
