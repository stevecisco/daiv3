using Daiv3.Knowledge;
using Daiv3.Orchestration;
using Daiv3.Orchestration.Interfaces;
using Daiv3.WebFetch.Crawl;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Daiv3.UnitTests.Orchestration;

/// <summary>
/// Unit tests for WebContentIngestionService.
/// </summary>
public class WebContentIngestionServiceTests : IAsyncLifetime
{
    private readonly Mock<ILogger<WebContentIngestionService>> _loggerMock;
    private readonly Mock<IMarkdownContentStore> _contentStoreMock;
    private readonly Mock<IKnowledgeDocumentProcessor> _documentProcessorMock;
    private readonly Mock<IOptions<WebContentIngestionOptions>> _optionsMock;
    private WebContentIngestionService _service;
    private string _tempDir = string.Empty;

    public WebContentIngestionServiceTests()
    {
        _loggerMock = new Mock<ILogger<WebContentIngestionService>>();
        _contentStoreMock = new Mock<IMarkdownContentStore>();
        _documentProcessorMock = new Mock<IKnowledgeDocumentProcessor>();
        _optionsMock = new Mock<IOptions<WebContentIngestionOptions>>();

        var options = new WebContentIngestionOptions();
        _optionsMock.Setup(o => o.Value).Returns(options);

        _service = new WebContentIngestionService(
            _loggerMock.Object,
            _contentStoreMock.Object,
            _documentProcessorMock.Object,
            _optionsMock.Object);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _service.StopMonitoringAsync();
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [Fact]
    public async Task IngestContentAsync_WithValidFile_ProcessesSuccessfully()
    {
        // Arrange
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        var testFile = Path.Combine(_tempDir, "test.md");
        await File.WriteAllTextAsync(testFile, "# Test Content\n\nTest markdown content.");

        var expectedResult = new DocumentProcessingResult
        {
            DocumentId = testFile,
            Success = true,
            ChunkCount = 2,
            TotalTokens = 150,
            SummaryTokens = 20,
            ProcessingTimeMs = 100
        };

        _documentProcessorMock
            .Setup(x => x.ProcessDocumentAsync(testFile, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _service.IngestContentAsync(testFile, "https://example.com/page");

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.ChunkCount);
        Assert.Equal(150, result.TotalTokens);
        Assert.Equal("https://example.com/page", result.SourceUrl);
        _documentProcessorMock.Verify(x => x.ProcessDocumentAsync(testFile, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IngestContentAsync_WithNonExistentFile_ReturnsFailure()
    {
        // Arrange
        var nonExistentFile = "/nonexistent/file.md";

        // Act
        var result = await _service.IngestContentAsync(nonExistentFile);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("not found", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task IngestContentAsync_WithNullFilePath_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.IngestContentAsync(null!));
    }

    [Fact]
    public async Task IngestContentAsync_WithEmptyFilePath_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.IngestContentAsync(string.Empty));
    }

    [Fact]
    public async Task IngestContentAsync_WithAlreadyIngestedFile_SkipsReprocessing()
    {
        // Arrange
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        var testFile = Path.Combine(_tempDir, "test.md");
        await File.WriteAllTextAsync(testFile, "# Test");

        var processingResult = new DocumentProcessingResult
        {
            DocumentId = testFile,
            Success = true,
            ChunkCount = 1,
            TotalTokens = 50,
            ProcessingTimeMs = 100
        };

        _documentProcessorMock
            .Setup(x => x.ProcessDocumentAsync(testFile, It.IsAny<CancellationToken>()))
            .ReturnsAsync(processingResult);

        var options = new WebContentIngestionOptions { SkipAlreadyIngestedFiles = true };
        _optionsMock.Setup(o => o.Value).Returns(options);
        _service = new WebContentIngestionService(_loggerMock.Object, _contentStoreMock.Object, _documentProcessorMock.Object, _optionsMock.Object);

        // First ingestion
        var result1 = await _service.IngestContentAsync(testFile);
        Assert.True(result1.Success);

        // Second ingestion (should skip)
        var result2 = await _service.IngestContentAsync(testFile);

        // Assert
        var stats = _service.GetStatistics();
        Assert.Equal(2, stats.TotalFilesDetected); // Both attempts counted
        Assert.Equal(1, stats.FilesIngested);  // Only first one ingested
        Assert.Equal(1, stats.FilesSkipped);   // Second one skipped
        _documentProcessorMock.Verify(x => x.ProcessDocumentAsync(testFile, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IngestContentAsync_WhenProcessorFails_ReturnsFailureWithError()
    {
        // Arrange
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        var testFile = Path.Combine(_tempDir, "test.md");
        await File.WriteAllTextAsync(testFile, "# Test");

        var failedResult = new DocumentProcessingResult
        {
            DocumentId = testFile,
            Success = false,
            ErrorMessage = "Processing failed"
        };

        _documentProcessorMock
            .Setup(x => x.ProcessDocumentAsync(testFile, It.IsAny<CancellationToken>()))
            .ReturnsAsync(failedResult);

        var options = new WebContentIngestionOptions();
        _optionsMock.Setup(o => o.Value).Returns(options);
        _service = new WebContentIngestionService(_loggerMock.Object, _contentStoreMock.Object, _documentProcessorMock.Object, _optionsMock.Object);

        // Act
        var result = await _service.IngestContentAsync(testFile);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        var stats = _service.GetStatistics();
        Assert.Equal(1, stats.FilesWithErrors);
    }

    [Fact]
    public async Task IngestContentAsync_WhenExceptionOccurs_CatchesAndLogsError()
    {
        // Arrange
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        var testFile = Path.Combine(_tempDir, "test.md");
        await File.WriteAllTextAsync(testFile, "# Test");

        _documentProcessorMock
            .Setup(x => x.ProcessDocumentAsync(testFile, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Test exception"));

        var options = new WebContentIngestionOptions();
        _optionsMock.Setup(o => o.Value).Returns(options);
        _service = new WebContentIngestionService(_loggerMock.Object, _contentStoreMock.Object, _documentProcessorMock.Object, _optionsMock.Object);

        // Act
        var result = await _service.IngestContentAsync(testFile);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Test exception", result.ErrorMessage ?? string.Empty);
    }

    [Fact]
    public async Task IngestPendingContentAsync_WithMultipleFiles_ProcessesAllFiles()
    {
        // Arrange
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);

        var file1 = Path.Combine(_tempDir, "test1.md");
        var file2 = Path.Combine(_tempDir, "test2.md");
        await File.WriteAllTextAsync(file1, "# Test 1");
        await File.WriteAllTextAsync(file2, "# Test 2");

        _contentStoreMock.Setup(x => x.GetStorageDirectory()).Returns(_tempDir);

        var processingResult = new DocumentProcessingResult
        {
            DocumentId = "test",
            Success = true,
            ChunkCount = 1,
            TotalTokens = 50,
            ProcessingTimeMs = 100
        };

        _documentProcessorMock
            .Setup(x => x.ProcessDocumentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(processingResult);

        var options = new WebContentIngestionOptions();
        _optionsMock.Setup(o => o.Value).Returns(options);
        _service = new WebContentIngestionService(_loggerMock.Object, _contentStoreMock.Object, _documentProcessorMock.Object, _optionsMock.Object);

        var progressReports = new List<(int, int, string)>();

        // Act
        var results = await _service.IngestPendingContentAsync(new Progress<(int, int, string)>(p => progressReports.Add(p)));

        // Assert
        Assert.NotEmpty(results);
        Assert.True(results.Count >= 2);
        Assert.All(results, r => Assert.True(r.Success));
        var stats = _service.GetStatistics();
        Assert.True(stats.FilesIngested > 0);
    }

    [Fact]
    public async Task IngestPendingContentAsync_WithNonExistentDirectory_ReturnsEmpty()
    {
        // Arrange
        _contentStoreMock.Setup(x => x.GetStorageDirectory()).Returns("/nonexistent/path");

        var options = new WebContentIngestionOptions();
        _optionsMock.Setup(o => o.Value).Returns(options);
        _service = new WebContentIngestionService(_loggerMock.Object, _contentStoreMock.Object, _documentProcessorMock.Object, _optionsMock.Object);

        // Act
        var results = await _service.IngestPendingContentAsync();

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public async Task IngestPendingContentAsync_WithDisabledService_ReturnsEmpty()
    {
        // Arrange
        var options = new WebContentIngestionOptions { Enabled = false };
        _optionsMock.Setup(o => o.Value).Returns(options);
        _service = new WebContentIngestionService(_loggerMock.Object, _contentStoreMock.Object, _documentProcessorMock.Object, _optionsMock.Object);

        // Act
        var results = await _service.IngestPendingContentAsync();

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public async Task StartMonitoringAsync_WithDisabledAutoMonitoring_DoesNotStartWatcher()
    {
        // Arrange
        var options = new WebContentIngestionOptions { EnableAutoMonitoring = false };
        _optionsMock.Setup(o => o.Value).Returns(options);
        _service = new WebContentIngestionService(_loggerMock.Object, _contentStoreMock.Object, _documentProcessorMock.Object, _optionsMock.Object);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(100); // Cancel quickly

        // Act
        await _service.StartMonitoringAsync(cts.Token);

        // Assert
        var stats = _service.GetStatistics();
        Assert.False(stats.IsMonitoring);
    }

    [Fact]
    public async Task StartMonitoringAsync_WithDisabledService_DoesNotStartWatcher()
    {
        // Arrange
        var options = new WebContentIngestionOptions { Enabled = false };
        _optionsMock.Setup(o => o.Value).Returns(options);
        _service = new WebContentIngestionService(_loggerMock.Object, _contentStoreMock.Object, _documentProcessorMock.Object, _optionsMock.Object);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(100);

        // Act
        await _service.StartMonitoringAsync(cts.Token);

        // Assert
        var stats = _service.GetStatistics();
        Assert.False(stats.IsMonitoring);
    }

    [Fact]
    public async Task StopMonitoringAsync_WhenNotMonitoring_DoesNotThrow()
    {
        // Act & Assert - should not throw
        await _service.StopMonitoringAsync();
    }

    [Fact]
    public void GetStatistics_ReturnsAccurateMetrics()
    {
        // Arrange & Act
        var stats = _service.GetStatistics();

        // Assert
        Assert.NotNull(stats);
        Assert.Equal(0, stats.TotalFilesDetected);
        Assert.Equal(0, stats.FilesIngested);
        Assert.Equal(0, stats.FilesSkipped);
        Assert.Equal(0, stats.FilesWithErrors);
        Assert.Equal(0, stats.TotalChunksCreated);
        Assert.Equal(0, stats.TotalTokensProcessed);
        Assert.Equal(0, stats.TotalIngestionTimeMs);
        Assert.False(stats.IsMonitoring);
    }

    [Fact]
    public async Task IngestContentAsync_TracksCumulativeMetrics()
    {
        // Arrange
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);

        var file1 = Path.Combine(_tempDir, "test1.md");
        var file2 = Path.Combine(_tempDir, "test2.md");
        await File.WriteAllTextAsync(file1, "# Test 1");
        await File.WriteAllTextAsync(file2, "# Test 2");

        var processingResult1 = new DocumentProcessingResult
        {
            DocumentId = file1,
            Success = true,
            ChunkCount = 2,
            TotalTokens = 100,
            ProcessingTimeMs = 100
        };

        var processingResult2 = new DocumentProcessingResult
        {
            DocumentId = file2,
            Success = true,
            ChunkCount = 3,
            TotalTokens = 150,
            ProcessingTimeMs = 150
        };

        _documentProcessorMock
            .SetupSequence(x => x.ProcessDocumentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(processingResult1)
            .ReturnsAsync(processingResult2);

        var options = new WebContentIngestionOptions();
        _optionsMock.Setup(o => o.Value).Returns(options);
        _service = new WebContentIngestionService(_loggerMock.Object, _contentStoreMock.Object, _documentProcessorMock.Object, _optionsMock.Object);

        // Act
        await _service.IngestContentAsync(file1);
        await _service.IngestContentAsync(file2);

        // Assert
        var stats = _service.GetStatistics();
        Assert.Equal(2, stats.TotalFilesDetected);
        Assert.Equal(2, stats.FilesIngested);
        Assert.Equal(5, stats.TotalChunksCreated); // 2 + 3
        Assert.Equal(250, stats.TotalTokensProcessed); // 100 + 150
        Assert.True(stats.TotalIngestionTimeMs >= 0);
    }

    [Fact]
    public async Task IngestContentAsync_ExtracsSourceUrlFromMetadata_WhenNotProvided()
    {
        // Arrange
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);

        var testFile = Path.Combine(_tempDir, "test.md");
        var metadataFile = Path.Combine(_tempDir, "test.metadata.json");

        await File.WriteAllTextAsync(testFile, "# Test");
        await File.WriteAllTextAsync(metadataFile, """{"sourceUrl":"https://example.com/article"}""");

        var processingResult = new DocumentProcessingResult
        {
            DocumentId = testFile,
            Success = true,
            ChunkCount = 1,
            TotalTokens = 50,
            ProcessingTimeMs = 100
        };

        _documentProcessorMock
            .Setup(x => x.ProcessDocumentAsync(testFile, It.IsAny<CancellationToken>()))
            .ReturnsAsync(processingResult);

        var options = new WebContentIngestionOptions();
        _optionsMock.Setup(o => o.Value).Returns(options);
        _service = new WebContentIngestionService(_loggerMock.Object, _contentStoreMock.Object, _documentProcessorMock.Object, _optionsMock.Object);

        // Act
        var result = await _service.IngestContentAsync(testFile);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("https://example.com/article", result.SourceUrl);
    }
}
