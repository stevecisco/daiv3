using Daiv3.Persistence.Entities;
using Daiv3.Persistence.Repositories;
using Daiv3.WebFetch.Crawl;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Cryptography;
using System.Text;
using Xunit;
using WebFetchEntity = Daiv3.Persistence.Entities.WebFetch;

namespace Daiv3.WebFetch.Crawl.Tests;

/// <summary>
/// Unit tests for WebFetchMetadataService.
/// Tests metadata storage, content hashing, and error handling.
/// </summary>
public class WebFetchMetadataServiceTests
{
    private readonly Mock<IWebFetchRepository> _mockRepository = new();
    private readonly Mock<ILogger<WebFetchMetadataService>> _mockLogger = new();
    private readonly WebFetchMetadataService _service;

    public WebFetchMetadataServiceTests()
    {
        _service = new WebFetchMetadataService(_mockRepository.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task StoreMetadataAsync_WithValidInputs_StoresWebFetchAndReturnsMetadata()
    {
        // Arrange
        var sourceUrl = "https://example.com/article";
        var docId = "doc-123";
        var htmlContent = "<html><body>Hello World</body></html>";
        var title = "Example Article";
        var description = "A test article";

        WebFetchEntity capturedEntity = null!;
        _mockRepository
            .Setup(r => r.AddAsync(It.IsAny<WebFetchEntity>(), It.IsAny<CancellationToken>()))
            .Callback<WebFetchEntity, CancellationToken>((entity, _) => capturedEntity = entity)
            .ReturnsAsync("web-fetch-id");

        // Act
        var result = await _service.StoreMetadataAsync(sourceUrl, docId, htmlContent, title, description);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(sourceUrl, result.SourceUrl);
        Assert.Equal(docId, result.DocId);
        Assert.Equal(title, result.Title);
        Assert.Equal(description, result.Description);
        Assert.Equal("active", result.Status);
        Assert.NotEmpty(result.ContentHash);
        Assert.True(result.FetchDate > 0); // Unix timestamp should be positive

        // Verify repository was called
        _mockRepository.Verify(r => r.AddAsync(It.IsAny<WebFetchEntity>(), It.IsAny<CancellationToken>()), Times.Once);

        // Verify the captured entity had correct values
        Assert.NotNull(capturedEntity);
        Assert.Equal(sourceUrl, capturedEntity.SourceUrl);
        Assert.Equal(docId, capturedEntity.DocId);
        Assert.Equal("active", capturedEntity.Status);
    }

    [Fact]
    public async Task StoreMetadataAsync_WithNullSourceUrl_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.StoreMetadataAsync(null!, "doc-123", "<html></html>"));
    }

    [Fact]
    public async Task StoreMetadataAsync_WithEmptySourceUrl_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.StoreMetadataAsync("", "doc-123", "<html></html>"));
    }

    [Fact]
    public async Task StoreMetadataAsync_WithNullDocId_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.StoreMetadataAsync("https://example.com", null!, "<html></html>"));
    }

    [Fact]
    public async Task StoreMetadataAsync_WithNullHtmlContent_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.StoreMetadataAsync("https://example.com", "doc-123", null!));
    }

    [Fact]
    public async Task StoreMetadataAsync_WithEmptyHtmlContent_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.StoreMetadataAsync("https://example.com", "doc-123", ""));
    }

    [Fact]
    public async Task StoreMetadataAsync_WithOptionalFieldsAsNull_StoresMetadataSuccessfully()
    {
        // Arrange
        var sourceUrl = "https://example.com/page";
        var docId = "doc-456";
        var htmlContent = "<html></html>";

        _mockRepository
            .Setup(r => r.AddAsync(It.IsAny<WebFetchEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("web-fetch-id");

        // Act
        var result = await _service.StoreMetadataAsync(sourceUrl, docId, htmlContent, null, null);

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.Title);
        Assert.Null(result.Description);
        Assert.Equal(sourceUrl, result.SourceUrl);
        Assert.Equal(docId, result.DocId);
    }

    [Fact]
    public async Task StoreMetadataAsync_WithRepositoryException_ThrowsInvalidOperationException()
    {
        // Arrange
        _mockRepository
            .Setup(r => r.AddAsync(It.IsAny<WebFetchEntity>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new System.Data.DataException("Database error"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.StoreMetadataAsync("https://example.com", "doc-123", "<html></html>"));
    }

    [Fact]
    public async Task StoreMetadataAsync_WithCancellationToken_PropagatesCancellation()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _mockRepository
            .Setup(r => r.AddAsync(It.IsAny<WebFetchEntity>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _service.StoreMetadataAsync("https://example.com", "doc-123", "<html></html>", cancellationToken: cts.Token));
    }

    [Fact]
    public void CalculateContentHash_WithSimpleXml_CalculatesCorrectHash()
    {
        // Arrange
        var content = "<html></html>";

        // Calculate expected hash using SHA256
        using var sha256 = SHA256.Create();
        var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(content));
        var expectedHash = BitConverter.ToString(hashedBytes).Replace("-", "").ToLowerInvariant();

        // Act
        var result = _service.CalculateContentHash(content);

        // Assert
        Assert.Equal(expectedHash, result);
        Assert.NotEmpty(result);
        Assert.Equal(64, result.Length); // SHA256 produces 64 hex characters
    }

    [Fact]
    public void CalculateContentHash_WithDifferentContent_ProducesDifferentHash()
    {
        // Arrange
        var content1 = "<html><body>Content 1</body></html>";
        var content2 = "<html><body>Content 2</body></html>";

        // Act
        var hash1 = _service.CalculateContentHash(content1);
        var hash2 = _service.CalculateContentHash(content2);

        // Assert
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void CalculateContentHash_WithIdenticalContent_ProducesSameHash()
    {
        // Arrange
        var content = "<html><body>Identical Content</body></html>";

        // Act
        var hash1 = _service.CalculateContentHash(content);
        var hash2 = _service.CalculateContentHash(content);

        // Assert
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void CalculateContentHash_WithNullContent_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _service.CalculateContentHash(null!));
    }

    [Fact]
    public void CalculateContentHash_WithEmptyContent_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _service.CalculateContentHash(""));
    }

    [Fact]
    public void CalculateContentHash_WithWhitespaceOnlyContent_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _service.CalculateContentHash("   "));
    }

    [Fact]
    public void CalculateContentHash_WithLargeContent_CalculatesSuccessfully()
    {
        // Arrange
        var content = new string('x', 1_000_000); // 1 MB of content

        // Act
        var hash = _service.CalculateContentHash(content);

        // Assert
        Assert.NotEmpty(hash);
        Assert.Equal(64, hash.Length); // SHA256 produces exactly 64 hex characters
    }

    [Fact]
    public void CalculateContentHash_WithSpecialCharacters_CalculatesSuccessfully()
    {
        // Arrange
        var content = "<html>©™€¥¢£ 日本語 العربية</html>";

        // Act
        var hash = _service.CalculateContentHash(content);

        // Assert
        Assert.NotEmpty(hash);
        Assert.Equal(64, hash.Length);
    }

    [Fact]
    public async Task StoreMetadataAsync_GeneratesUniqueWebFetchIds()
    {
        // Arrange
        var sourceUrl = "https://example.com";
        var docId = "doc-123";
        var htmlContent = "<html></html>";
        var storedIds = new List<string>();

        _mockRepository
            .Setup(r => r.AddAsync(It.IsAny<WebFetchEntity>(), It.IsAny<CancellationToken>()))
            .Callback<WebFetchEntity, CancellationToken>((entity, _) => storedIds.Add(entity.WebFetchId))
            .ReturnsAsync("web-fetch-id");

        // Act
        await _service.StoreMetadataAsync(sourceUrl, docId, htmlContent);
        await _service.StoreMetadataAsync(sourceUrl, docId + "2", htmlContent);

        // Assert
        Assert.Equal(2, storedIds.Count);
        Assert.NotEqual(storedIds[0], storedIds[1]);
    }

    [Fact]
    public async Task StoreMetadataAsync_SetsCorrectTimestamps()
    {
        // Arrange
        var beforeCall = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var sourceUrl = "https://example.com";
        var docId = "doc-123";
        var htmlContent = "<html></html>";

        WebFetchEntity capturedEntity = null!;
        _mockRepository
            .Setup(r => r.AddAsync(It.IsAny<WebFetchEntity>(), It.IsAny<CancellationToken>()))
            .Callback<WebFetchEntity, CancellationToken>((entity, _) => capturedEntity = entity)
            .ReturnsAsync("web-fetch-id");

        // Act
        await _service.StoreMetadataAsync(sourceUrl, docId, htmlContent);
        var afterCall = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Assert
        Assert.NotNull(capturedEntity);
        Assert.True(capturedEntity.FetchDate >= beforeCall);
        Assert.True(capturedEntity.FetchDate <= afterCall);
        Assert.True(capturedEntity.CreatedAt >= beforeCall);
        Assert.True(capturedEntity.CreatedAt <= afterCall);
        Assert.Equal(capturedEntity.FetchDate, capturedEntity.CreatedAt);
    }
}
