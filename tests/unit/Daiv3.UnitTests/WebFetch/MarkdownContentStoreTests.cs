using System.Text.Json;
using Daiv3.WebFetch.Crawl;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Daiv3.UnitTests.WebFetch;

public class MarkdownContentStoreTests : IDisposable
{
    private readonly string _tempDir;
    private IMarkdownContentStore? _storeField;
    
    private IMarkdownContentStore _store => _storeField ??= CreateStore();

    public MarkdownContentStoreTests()
    {
        // Use unique directory per test instance to isolate tests
        _tempDir = Path.Combine(Path.GetTempPath(), $"wfc-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
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

    private IMarkdownContentStore CreateStore(MarkdownContentStoreOptions? configureOptions = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMarkdownContentStore(opts =>
        {
            opts.StorageDirectory = _tempDir;
            opts.CreateDirectoryIfNotExists = true;
            if (configureOptions != null)
            {
                opts.StorageDirectory = configureOptions.StorageDirectory ?? opts.StorageDirectory;
                opts.MaxContentSizeBytes = configureOptions.MaxContentSizeBytes;
                opts.OrganizeByDomain = configureOptions.OrganizeByDomain;
                opts.StoreSidecarMetadata = configureOptions.StoreSidecarMetadata;
                opts.IncludeFrontMatter = configureOptions.IncludeFrontMatter;
            }
        });
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IMarkdownContentStore>();
    }

    #region Store Tests

    [Fact]
    public async Task StoreAsync_WithValidInput_StoresContentSuccessfully()
    {
        // Arrange
        var url = "https://example.com/article";
        var content = "# Test Article\n\nThis is test content.";

        // Act
        var result = await _store.StoreAsync(url, content);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsNew);
        Assert.NotNull(result.Metadata);
        Assert.Contains("example.com", result.Metadata.FilePath);
        Assert.True(File.Exists(result.Metadata.FilePath));
    }

    [Fact]
    public async Task StoreAsync_WithTitle_IncludesTitleInMetadata()
    {
        // Arrange
        var url = "https://example.com/article";
        var content = "# Test Article\n\nContent here.";
        var title = "My Test Article";

        // Act
        var result = await _store.StoreAsync(url, content, title: title);

        // Assert
        Assert.NotNull(result.Metadata);
        Assert.Equal(title, result.Metadata.Title);
    }

    [Fact]
    public async Task StoreAsync_WithDescription_IncludesDescriptionInMetadata()
    {
        // Arrange
        var url = "https://example.com/article";
        var content = "# Test\n\nContent";
        var description = "A test article description";

        // Act
        var result = await _store.StoreAsync(url, content, description: description);

        // Assert
        Assert.NotNull(result.Metadata);
        Assert.Equal(description, result.Metadata.Description);
    }

    [Fact]
    public async Task StoreAsync_WithTags_IncludesTagsInMetadata()
    {
        // Arrange
        var url = "https://example.com/article";
        var content = "# Test\n\nContent";
        var tags = new[] { "tech", "article", "test" };

        // Act
        var result = await _store.StoreAsync(url, content, tags: tags);

        // Assert
        Assert.NotNull(result.Metadata);
        Assert.Equal(tags, result.Metadata.Tags);
    }

    [Fact]
    public async Task StoreAsync_WithNullUrl_ThrowsArgumentNullException()
    {
        // Arrange
        var content = "# Test\n\nContent";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _store.StoreAsync(null!, content));
    }

    [Fact]
    public async Task StoreAsync_WithEmptyContent_ThrowsArgumentNullException()
    {
        // Arrange
        var url = "https://example.com/test";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _store.StoreAsync(url, null!));
    }

    [Fact]
    public async Task StoreAsync_WithOversizeContent_ThrowsInvalidOperationException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMarkdownContentStore(options =>
        {
            options.StorageDirectory = _tempDir;
            options.MaxContentSizeBytes = 100; // Very small limit
        });
        var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<IMarkdownContentStore>();

        var url = "https://example.com/test";
        var content = new string('x', 1000); // Much larger than limit

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.StoreAsync(url, content));
    }

    [Fact]
    public async Task StoreAsync_WithDuplicateContent_MarksAsUpdate()
    {
        // Arrange
        var url = "https://example.com/article";
        var content = "# Test\n\nContent";

        // Store first time
        var result1 = await _store.StoreAsync(url, content);
        Assert.True(result1.IsNew);

        // Store same content
        var result2 = await _store.StoreAsync(url, content);

        // Assert
        Assert.False(result2.IsNew);
    }

    [Fact]
    public async Task StoreAsync_WithUpdatedContent_MarksAsUpdate()
    {
        // Arrange
        var url = "https://example.com/article";
        var content1 = "# Test\n\nContent 1";
        var content2 = "# Test\n\nContent 2 - Updated";

        // Store first time
        var result1 = await _store.StoreAsync(url, content1);
        Assert.True(result1.IsNew);

        // Store updated content
        var result2 = await _store.StoreAsync(url, content2);

        // Assert
        Assert.False(result2.IsNew);
        Assert.NotEqual(result1.Metadata.ContentHash, result2.Metadata.ContentHash);
    }

    [Fact]
    public async Task StoreAsync_WithFrontMatter_IncludesFrontMatterInFile()
    {
        // Arrange
        var url = "https://example.com/article";
        var content = "# Test\n\nContent";

        // Act
        var result = await _store.StoreAsync(url, content, title: "Test Article");

        // Assert
        var fileContent = File.ReadAllText(result.Metadata.FilePath);
        Assert.StartsWith("---", fileContent);
        Assert.Contains("title: Test Article", fileContent);
        Assert.Contains("source_url: https://example.com/article", fileContent);
    }

    [Fact]
    public async Task StoreAsync_WithOrganizeByDomain_CreatesSubdirectories()
    {
        // Arrange
        var tempDir2 = Path.Combine(Path.GetTempPath(), $"wfc-test-org-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir2);
        try
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddMarkdownContentStore(options =>
            {
                options.StorageDirectory = tempDir2;
                options.OrganizeByDomain = true;
            });
            var provider = services.BuildServiceProvider();
            var store = provider.GetRequiredService<IMarkdownContentStore>();

            var url = "https://example.com/article";
            var content = "# Test\n\nContent";

            // Act
            var result = await store.StoreAsync(url, content);

            // Assert
            var filePath = result.Metadata.FilePath;
            Assert.Contains("example.com", filePath);
        }
        finally
        {
            Directory.Delete(tempDir2, recursive: true);
        }
    }

    [Fact]
    public async Task StoreAsync_WithSidecarMetadata_CreatesMetadataFile()
    {
        // Arrange
        var url = "https://example.com/article";
        var content = "# Test\n\nContent";

        // Act
        var result = await _store.StoreAsync(url, content, title: "Test");

        // Assert
        var metadataFile = Path.Combine(
            Path.GetDirectoryName(result.Metadata.FilePath)!,
            $"{result.Metadata.ContentId}.metadata.json");
        Assert.True(File.Exists(metadataFile));

        var json = File.ReadAllText(metadataFile);
        var metadata = JsonSerializer.Deserialize<StoredContentMetadata>(
            json, 
            new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Assert.NotNull(metadata);
        Assert.Equal("Test", metadata.Title);
    }

    #endregion

    #region Retrieve Tests

    [Fact]
    public async Task RetrieveAsync_WithValidId_ReturnsContent()
    {
        // Arrange
        var url = "https://example.com/article";
        var content = "# Test\n\nContent";
        var stored = await _store.StoreAsync(url, content);

        // Act
        var retrieved = await _store.RetrieveAsync(stored.Metadata.ContentId);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(stored.Metadata.ContentId, retrieved.ContentId);
        Assert.Contains("# Test", retrieved.MarkdownContent);
    }

    [Fact]
    public async Task RetrieveAsync_WithNonExistentId_ReturnsNull()
    {
        // Arrange
        // No setup needed

        // Act
        var retrieved = await _store.RetrieveAsync("nonexistent-id");

        // Assert
        Assert.Null(retrieved);
    }

    [Fact]
    public async Task RetrieveAsync_WithoutFrontMatter_StripsFrontMatterFromContent()
    {
        // Arrange
        var url = "https://example.com/article";
        var content = "# Test\n\nContent";
        var stored = await _store.StoreAsync(url, content, title: "Test");

        // Act
        var retrieved = await _store.RetrieveAsync(stored.Metadata.ContentId);

        // Assert
        Assert.NotNull(retrieved);
        Assert.DoesNotContain("---", retrieved.MarkdownContent);
        Assert.DoesNotContain("source_url:", retrieved.MarkdownContent);
    }

    [Fact]
    public async Task RetrieveAsync_WithMetadataOption_IncludesMetadata()
    {
        // Arrange
        var url = "https://example.com/article";
        var content = "# Test\n\nContent";
        var stored = await _store.StoreAsync(url, content, title: "Test");

        // Act
        var retrieved = await _store.RetrieveAsync(
            stored.Metadata.ContentId,
            new RetrieveContentOptions { IncludeMetadata = true });

        // Assert
        Assert.NotNull(retrieved);
        Assert.NotNull(retrieved.Metadata);
        Assert.Equal(url, retrieved.Metadata.SourceUrl);
        Assert.Equal("Test", retrieved.Metadata.Title);
    }

    [Fact]
    public async Task RetrieveAsync_WithoutContentOption_ExcludesContent()
    {
        // Arrange
        var url = "https://example.com/article";
        var content = "# Test\n\nContent";
        var stored = await _store.StoreAsync(url, content);

        // Act
        var retrieved = await _store.RetrieveAsync(
            stored.Metadata.ContentId,
            new RetrieveContentOptions { IncludeContent = false, IncludeMetadata = true });

        // Assert
        Assert.NotNull(retrieved);
        Assert.Null(retrieved.MarkdownContent);
        Assert.NotNull(retrieved.Metadata);
    }

    #endregion

    #region List Tests

    [Fact]
    public async Task ListAllAsync_WithNoContent_ReturnsEmptyList()
    {
        // Arrange
        // No setup needed

        // Act
        var items = await _store.ListAllAsync();

        // Assert
        Assert.Empty(items);
    }

    [Fact]
    public async Task ListAllAsync_WithMultipleItems_ReturnsAllItems()
    {
        // Arrange
        var urls = new[] { "https://a.com/1", "https://b.com/2", "https://c.com/3" };

        foreach (var url in urls)
        {
            await _store.StoreAsync(url, $"# Test\n\nContent from {url}");
        }

        // Act
        var items = await _store.ListAllAsync();

        // Assert
        Assert.Equal(3, items.Count());
    }

    #endregion

    #region Delete Tests

    [Fact]
    public async Task DeleteAsync_WithValidId_DeletesContent()
    {
        // Arrange
        var url = "https://example.com/article";
        var content = "# Test\n\nContent";
        var stored = await _store.StoreAsync(url, content);

        // Act
        var deleted = await _store.DeleteAsync(stored.Metadata.ContentId);

        // Assert
        Assert.True(deleted);
        Assert.False(File.Exists(stored.Metadata.FilePath));
    }

    [Fact]
    public async Task DeleteAsync_WithNonExistentId_ReturnsFalse()
    {
        // Arrange
        // No setup needed

        // Act
        var deleted = await _store.DeleteAsync("nonexistent-id");

        // Assert
        Assert.False(deleted);
    }

    [Fact]
    public async Task DeleteAsync_WithMetadataFile_DeletesBothFiles()
    {
        // Arrange
        var url = "https://example.com/article";
        var content = "# Test\n\nContent";
        var stored = await _store.StoreAsync(url, content);

        var metadataFile = Path.Combine(
            Path.GetDirectoryName(stored.Metadata.FilePath)!,
            $"{stored.Metadata.ContentId}.metadata.json");

        Assert.True(File.Exists(metadataFile));

        // Act
        var deleted = await _store.DeleteAsync(stored.Metadata.ContentId);

        // Assert
        Assert.True(deleted);
        Assert.False(File.Exists(stored.Metadata.FilePath));
        Assert.False(File.Exists(metadataFile));
    }

    #endregion

    #region Exists Tests

    [Fact]
    public async Task ExistsAsync_WithValidId_ReturnsTrue()
    {
        // Arrange
        var url = "https://example.com/article";
        var content = "# Test\n\nContent";
        var stored = await _store.StoreAsync(url, content);

        // Act
        var exists = await _store.ExistsAsync(stored.Metadata.ContentId);

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public async Task ExistsAsync_WithNonExistentId_ReturnsFalse()
    {
        // Arrange
        // No setup needed

        // Act
        var exists = await _store.ExistsAsync("nonexistent-id");

        // Assert
        Assert.False(exists);
    }

    #endregion

    #region GetStorageDirectory Tests

    [Fact]
    public void GetStorageDirectory_ReturnsConfiguredDirectory()
    {
        // Arrange
        // No setup needed

        // Act
        var dir = _store.GetStorageDirectory();

        // Assert
        Assert.Equal(_tempDir, dir);
    }

    #endregion

    #region Options Validation Tests

    [Fact]
    public void Options_WithNullStorageDirectory_ThrowsValidationError()
    {
        // Arrange
        var options = new MarkdownContentStoreOptions { StorageDirectory = null! };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => options.Validate());
    }

    [Fact]
    public void Options_WithInvalidMaxSize_ThrowsValidationError()
    {
        // Arrange
        var options = new MarkdownContentStoreOptions { MaxContentSizeBytes = -1 };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => options.Validate());
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task StoreAsync_WithSpecialCharactersInTitle_StoresSuccessfully()
    {
        // Arrange
        var url = "https://example.com/article";
        var content = "# Test\n\nContent";
        var title = "Test \"Article\" with: Special: Characters!";

        // Act
        var result = await _store.StoreAsync(url, content, title: title);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(title, result.Metadata.Title);
    }

    [Fact]
    public async Task StoreAsync_WithLongContent_StoresSuccessfully()
    {
        // Arrange
        var url = "https://example.com/article";
        var content = new string('x', 1_000_000); // 1MB of content

        // Act
        var result = await _store.StoreAsync(url, content);

        // Assert
        Assert.NotNull(result);
        Assert.True(File.Exists(result.Metadata.FilePath));
    }

    [Fact]
    public async Task StoreAsync_WithMultipleUrlsFromSameDomain_StoresInSameDirectory()
    {
        // Arrange
        var tempDir2 = Path.Combine(Path.GetTempPath(), $"wfc-test-multi-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir2);
        try
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddMarkdownContentStore(options =>
            {
                options.StorageDirectory = tempDir2;
                options.OrganizeByDomain = true;
            });
            var provider = services.BuildServiceProvider();
            var store = provider.GetRequiredService<IMarkdownContentStore>();

            var urls = new[] 
            { 
                "https://example.com/article1",
                "https://example.com/article2",
                "https://www.example.com/article3"
            };

            // Act
            var results = new List<StoreContentResult>();
            foreach (var url in urls)
            {
                var result = await store.StoreAsync(url, $"# Test\n\nContent from {url}");
                results.Add(result);
            }

            // Assert
            // First two should be in example.com directory
            var dir1 = Path.GetDirectoryName(results[0].Metadata.FilePath)!;
            var dir2 = Path.GetDirectoryName(results[1].Metadata.FilePath)!;
            Assert.Equal(dir1, dir2);
        }
        finally
        {
            Directory.Delete(tempDir2, recursive: true);
        }
    }

    #endregion
}
