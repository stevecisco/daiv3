using Daiv3.WebFetch.Crawl;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Daiv3.UnitTests.WebFetch;

public class MarkdownContentStoreIntegrationTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ServiceProvider _serviceProvider;

    public MarkdownContentStoreIntegrationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"wfc-integration-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Information));
        services.AddMarkdownContentStore(opts =>
        {
            opts.StorageDirectory = _tempDir;
            opts.CreateDirectoryIfNotExists = true;
            opts.OrganizeByDomain = true;
            opts.StoreSidecarMetadata = true;
            opts.IncludeFrontMatter = true;
        });

        _serviceProvider = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
        if (Directory.Exists(_tempDir))
        {
            try
            {
                Directory.Delete(_tempDir, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [Fact]
    public async Task StoreAndRetrieve_WithFullRoundTrip_PreservesAllData()
    {
        // Arrange
        var store = _serviceProvider.GetRequiredService<IMarkdownContentStore>();
        var url = "https://example.com/article/test";
        var content = "# Test Article\n\nThis is a test article with content.";
        var title = "Test Article Title";
        var description = "A test article for integration testing";
        var tags = new[] { "test", "integration", "article" };

        // Act - Store
        var storeResult = await store.StoreAsync(url, content, title, description, tags);

        // Assert - Store operation
        Assert.NotNull(storeResult);
        Assert.True(storeResult.IsNew);
        Assert.NotNull(storeResult.Metadata);
        Assert.Equal(title, storeResult.Metadata.Title);
        Assert.Equal(description, storeResult.Metadata.Description);
        Assert.Equal(tags, storeResult.Metadata.Tags);

        // Act - Retrieve
        var retrieved = await store.RetrieveAsync(storeResult.Metadata.ContentId);

        // Assert - Retrieve operation
        Assert.NotNull(retrieved);
        Assert.Equal(storeResult.Metadata.ContentId, retrieved.ContentId);
        Assert.Contains("Test Article", retrieved.MarkdownContent);
        Assert.NotNull(retrieved.Metadata);
        Assert.Equal(url, retrieved.Metadata.SourceUrl);
    }

    [Fact]
    public async Task StoreAndList_WithMultipleItems_ReturnsAllInListingWithoutErrors()
    {
        // Arrange
        var store = _serviceProvider.GetRequiredService<IMarkdownContentStore>();
        var items = new[]
        {
            ("https://example.com/article1", "# Article 1", "Article 1"),
            ("https://example.com/article2", "# Article 2", "Article 2"),
            ("https://other.com/article3", "# Article 3", "Article 3"),
        };

        // Act - Store all items
        var storedIds = new List<string>();
        foreach (var (url, content, title) in items)
        {
            var result = await store.StoreAsync(url, content, title: title);
            storedIds.Add(result.Metadata.ContentId);
        }

        // Act - List all
        var listedItems = await store.ListAllAsync();
        var listedIds = listedItems.Select(m => m.ContentId).ToList();

        // Assert
        Assert.Equal(3, listedItems.Count());
        foreach (var id in storedIds)
        {
            Assert.Contains(id, listedIds);
        }
    }

    [Fact]
    public async Task UpdateContent_WithChangedContent_ReflectsInRetrievedContent()
    {
        // Arrange
        var store = _serviceProvider.GetRequiredService<IMarkdownContentStore>();
        var url = "https://example.com/updatable";
        var originalContent = "# Original Content";
        var updatedContent = "# Updated Content\n\nWith more details";

        // Act - Store original
        var original = await store.StoreAsync(url, originalContent);
        Assert.True(original.IsNew);

        // Act - Update
        var updated = await store.StoreAsync(url, updatedContent);
        Assert.False(updated.IsNew);
        Assert.NotEqual(original.Metadata.ContentHash, updated.Metadata.ContentHash);

        // Act - Retrieve
        var retrieved = await store.RetrieveAsync(updated.Metadata.ContentId);

        // Assert
        Assert.Contains("more details", retrieved!.MarkdownContent);
    }

    [Fact]
    public async Task DeleteAndExists_WithDeletedContent_ReturnsFalseOnExists()
    {
        // Arrange
        var store = _serviceProvider.GetRequiredService<IMarkdownContentStore>();
        var url = "https://example.com/deletable";
        var content = "# Content to Delete";

        // Act - Store
        var stored = await store.StoreAsync(url, content);
        var contentId = stored.Metadata.ContentId;

        // Assert - Exists before delete
        var existsBefore = await store.ExistsAsync(contentId);
        Assert.True(existsBefore);

        // Act - Delete
        var deleted = await store.DeleteAsync(contentId);

        // Assert - Delete operation
        Assert.True(deleted);

        // Assert - Exists after delete
        var existsAfter = await store.ExistsAsync(contentId);
        Assert.False(existsAfter);

        // Assert - Cannot retrieve after delete
        var retrieved = await store.RetrieveAsync(contentId);
        Assert.Null(retrieved);
    }

    [Fact]
    public async Task FileSystem_WithOrganizeByDomain_CreatesCorrectDirectoryStructure()
    {
        // Arrange
        var store = _serviceProvider.GetRequiredService<IMarkdownContentStore>();
        var urlsExampleCom = new[]
        {
            "https://example.com/article1",
            "https://example.com/article2"
        };
        var urlsOtherCom = new[]
        {
            "https://other.com/page1",
            "https://other.com/page2"
        };

        // Act - Store from different domains
        foreach (var url in urlsExampleCom.Concat(urlsOtherCom))
        {
            await store.StoreAsync(url, $"# Content from {url}");
        }

        // Assert - Verify directory structure
        var storageDir = store.GetStorageDirectory();
        Assert.True(Directory.Exists(storageDir));

        var exampleDir = Path.Combine(storageDir, "example.com");
        var otherDir = Path.Combine(storageDir, "other.com");

        Assert.True(Directory.Exists(exampleDir), "example.com directory should exist");
        Assert.True(Directory.Exists(otherDir), "other.com directory should exist");

        // Verify files are in correct directories
        var exampleFiles = Directory.GetFiles(exampleDir, "*.md");
        var otherFiles = Directory.GetFiles(otherDir, "*.md");

        Assert.Equal(2, exampleFiles.Length);
        Assert.Equal(2, otherFiles.Length);
    }

    [Fact]
    public async Task MetadataFile_WithSidecarOption_CreatesAndUpdatesProperlyOnRevisions()
    {
        // Arrange
        var store = _serviceProvider.GetRequiredService<IMarkdownContentStore>();
        var url = "https://example.com/metadata-test";
        var content1 = "# Original";
        var content2 = "# Updated";

        // Act - Store initial content
        var result1 = await store.StoreAsync(url, content1, title: "Version 1");
        var metadataPath = Path.Combine(
            Path.GetDirectoryName(result1.Metadata.FilePath)!,
            $"{result1.Metadata.ContentId}.metadata.json");

        // Assert - Metadata file exists
        Assert.True(File.Exists(metadataPath));

        // Act - Update content
        var result2 = await store.StoreAsync(url, content2, title: "Version 2");

        // Assert - Metadata file still exists with updated content
        Assert.True(File.Exists(metadataPath));
        var metadata = await store.RetrieveAsync(result2.Metadata.ContentId, new RetrieveContentOptions { IncludeMetadata = true });
        Assert.NotNull(metadata?.Metadata);
        Assert.Equal("Version 2", metadata.Metadata.Title);
    }

    [Fact]
    public async Task FrontMatter_WithIncludeFrontMatterOption_IncludesAndRemovableOnRetrieval()
    {
        // Arrange
        var store = _serviceProvider.GetRequiredService<IMarkdownContentStore>();
        var url = "https://example.com/frontmatter-test";
        var content = "# Content Title\n\nBody content here.";
        var title = "Test Title";

        // Act - Store with front matter
        var stored = await store.StoreAsync(url, content, title: title);

        // Assert - File contains front matter
        var fileContent = File.ReadAllText(stored.Metadata.FilePath);
        Assert.StartsWith("---", fileContent);
        Assert.Contains("title: Test Title", fileContent);
        Assert.Contains("source_url:", fileContent);

        // Act - Retrieve
        var retrieved = await store.RetrieveAsync(stored.Metadata.ContentId);

        // Assert - Retrieved content has front matter stripped
        Assert.DoesNotContain("---", retrieved!.MarkdownContent!);
        Assert.DoesNotContain("title:", retrieved.MarkdownContent!);
        Assert.Contains("# Content Title", retrieved.MarkdownContent!);
        Assert.Contains("Body content here.", retrieved.MarkdownContent!);
    }

    [Fact]
    public async Task ConcurrentOperations_WithMultipleStores_WorkCorrectly()
    {
        // Arrange
        var store = _serviceProvider.GetRequiredService<IMarkdownContentStore>();
        var urls = Enumerable.Range(1, 10).Select(i => $"https://example.com/article{i}").ToList();

        // Act - Store concurrently
        var tasks = urls.Select(url =>
            store.StoreAsync(url, $"# Article\nContent from {url}", title: $"Article {url.Split('/').Last()}"));

        var results = await Task.WhenAll(tasks);

        // Assert - All stored successfully
        Assert.Equal(10, results.Count(r => r.IsNew));

        // Act - List all
        var listed = await store.ListAllAsync();

        // Assert - All items present
        Assert.Equal(10, listed.Count());
    }
}
