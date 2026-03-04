using Daiv3.Knowledge;
using Daiv3.Knowledge.DocProc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Daiv3.UnitTests.Knowledge;

/// <summary>
/// Unit tests for KnowledgeFileOrchestrationService.
/// Tests file change event handling and document processing orchestration.
/// </summary>
public sealed class KnowledgeFileOrchestrationServiceTests
{
    private readonly Mock<IFileSystemWatcher> _mockFileSystemWatcher;
    private readonly Mock<IKnowledgeDocumentProcessor> _mockDocumentProcessor;
    private readonly Mock<ILogger<KnowledgeFileOrchestrationService>> _mockLogger;
    private readonly KnowledgeFileOrchestrationService _service;

    public KnowledgeFileOrchestrationServiceTests()
    {
        _mockFileSystemWatcher = new Mock<IFileSystemWatcher>();
        _mockDocumentProcessor = new Mock<IKnowledgeDocumentProcessor>();
        _mockLogger = new Mock<ILogger<KnowledgeFileOrchestrationService>>();

        _mockFileSystemWatcher.Setup(x => x.IsRunning).Returns(false);

        _service = new KnowledgeFileOrchestrationService(
            _mockFileSystemWatcher.Object,
            _mockDocumentProcessor.Object,
            _mockLogger.Object);
    }

    [Fact]
    public void Constructor_WithNullFileSystemWatcher_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new KnowledgeFileOrchestrationService(
                null!,
                _mockDocumentProcessor.Object,
                _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullDocumentProcessor_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new KnowledgeFileOrchestrationService(
                _mockFileSystemWatcher.Object,
                null!,
                _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new KnowledgeFileOrchestrationService(
                _mockFileSystemWatcher.Object,
                _mockDocumentProcessor.Object,
                null!));
    }

    [Fact]
    public void IsRunning_InitiallyFalse()
    {
        // Assert
        Assert.False(_service.IsRunning);
    }

    [Fact]
    public async Task StartAsync_StartsService()
    {
        // Act
        await _service.StartAsync();

        // Assert
        Assert.True(_service.IsRunning);
        _mockFileSystemWatcher.Verify(x => x.StartAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StartAsync_WhenAlreadyRunning_DoesNotStartAgain()
    {
        // Arrange
        await _service.StartAsync();

        // Act
        await _service.StartAsync();

        // Assert
        Assert.True(_service.IsRunning);
        _mockFileSystemWatcher.Verify(x => x.StartAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StartAsync_WhenFileSystemWatcherIsRunning_DoesNotStartItAgain()
    {
        // Arrange
        _mockFileSystemWatcher.Setup(x => x.IsRunning).Returns(true);

        // Act
        await _service.StartAsync();

        // Assert
        Assert.True(_service.IsRunning);
        _mockFileSystemWatcher.Verify(x => x.StartAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StopAsync_StopsService()
    {
        // Arrange
        await _service.StartAsync();

        // Act
        await _service.StopAsync();

        // Assert
        Assert.False(_service.IsRunning);
    }

    [Fact]
    public async Task StopAsync_WhenNotRunning_DoesNothing()
    {
        // Act
        await _service.StopAsync();

        // Assert
        Assert.False(_service.IsRunning);
    }

    [Fact]
    public async Task OnFileCreated_ProcessesDocument()
    {
        // Arrange
        var filePath = @"C:\test\document.txt";
        _mockDocumentProcessor
            .Setup(x => x.ProcessDocumentAsync(filePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentProcessingResult
            {
                Success = true,
                DocumentId = "doc1",
                ChunkCount = 3
            });

        await _service.StartAsync();

        // Act
        RaiseFileChangedEvent(filePath, FileChangeType.Created);

        // Wait for async event handler
        await Task.Delay(100);

        // Assert
        _mockDocumentProcessor.Verify(
            x => x.ProcessDocumentAsync(filePath, It.IsAny<CancellationToken>()),
            Times.Once);

        var stats = _service.GetStatistics();
        Assert.Equal(1, stats.FilesProcessed);
        Assert.Equal(0, stats.FilesDeleted);
    }

    [Fact]
    public async Task OnFileModified_ProcessesDocument()
    {
        // Arrange
        var filePath = @"C:\test\document.txt";
        _mockDocumentProcessor
            .Setup(x => x.ProcessDocumentAsync(filePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentProcessingResult
            {
                Success = true,
                DocumentId = "doc2",
                ChunkCount = 5
            });

        await _service.StartAsync();

        // Act
        RaiseFileChangedEvent(filePath, FileChangeType.Modified);

        // Wait for async event handler
        await Task.Delay(100);

        // Assert
        _mockDocumentProcessor.Verify(
            x => x.ProcessDocumentAsync(filePath, It.IsAny<CancellationToken>()),
            Times.Once);

        var stats = _service.GetStatistics();
        Assert.Equal(1, stats.FilesProcessed);
    }

    [Fact]
    public async Task OnFileDeleted_DeletesDocument()
    {
        // Arrange
        var filePath = @"C:\test\document.txt";
        _mockDocumentProcessor
            .Setup(x => x.RemoveDocumentAsync(filePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _service.StartAsync();

        // Act
        RaiseFileChangedEvent(filePath, FileChangeType.Deleted);

        // Wait for async event handler
        await Task.Delay(100);

        // Assert
        _mockDocumentProcessor.Verify(
            x => x.RemoveDocumentAsync(filePath, It.IsAny<CancellationToken>()),
            Times.Once);

        var stats = _service.GetStatistics();
        Assert.Equal(0, stats.FilesProcessed);
        Assert.Equal(1, stats.FilesDeleted);
    }

    [Fact]
    public async Task OnFileRenamed_DeletesOldAndProcessesNew()
    {
        // Arrange
        var oldPath = @"C:\test\old.txt";
        var newPath = @"C:\test\new.txt";

        _mockDocumentProcessor
            .Setup(x => x.RemoveDocumentAsync(oldPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockDocumentProcessor
            .Setup(x => x.ProcessDocumentAsync(newPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentProcessingResult
            {
                Success = true,
                DocumentId = "doc3",
                ChunkCount = 3
            });

        await _service.StartAsync();

        // Act
        RaiseFileChangedEvent(newPath, FileChangeType.Renamed, oldPath);

        // Wait for async event handler
        await Task.Delay(100);

        // Assert
        _mockDocumentProcessor.Verify(
            x => x.RemoveDocumentAsync(oldPath, It.IsAny<CancellationToken>()),
            Times.Once);
        _mockDocumentProcessor.Verify(
            x => x.ProcessDocumentAsync(newPath, It.IsAny<CancellationToken>()),
            Times.Once);

        var stats = _service.GetStatistics();
        Assert.Equal(1, stats.FilesProcessed);
        Assert.Equal(1, stats.FilesDeleted);
    }

    [Fact]
    public async Task ProcessingError_IncrementsErrorCounter()
    {
        // Arrange
        var filePath = @"C:\test\document.txt";
        _mockDocumentProcessor
            .Setup(x => x.ProcessDocumentAsync(filePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentProcessingResult
            {
                Success = false,
                DocumentId = "doc4",
                ErrorMessage = "Test error"
            });

        await _service.StartAsync();

        // Act
        RaiseFileChangedEvent(filePath, FileChangeType.Created);

        // Wait for async event handler
        await Task.Delay(100);

        // Assert
        var stats = _service.GetStatistics();
        Assert.Equal(0, stats.FilesProcessed);
        Assert.Equal(1, stats.ProcessingErrors);
    }

    [Fact]
    public async Task ProcessingException_IncrementsErrorCounter()
    {
        // Arrange
        var filePath = @"C:\test\document.txt";
        _mockDocumentProcessor
            .Setup(x => x.ProcessDocumentAsync(filePath, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Test exception"));

        await _service.StartAsync();

        // Act
        RaiseFileChangedEvent(filePath, FileChangeType.Created);

        // Wait for async event handler
        await Task.Delay(100);

        // Assert
        var stats = _service.GetStatistics();
        Assert.Equal(0, stats.FilesProcessed);
        Assert.Equal(1, stats.ProcessingErrors);
    }

    [Fact]
    public async Task DeletionError_IncrementsErrorCounter()
    {
        // Arrange
        var filePath = @"C:\test\document.txt";
        _mockDocumentProcessor
            .Setup(x => x.RemoveDocumentAsync(filePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false); // Document not found

        await _service.StartAsync();

        // Act
        RaiseFileChangedEvent(filePath, FileChangeType.Deleted);

        // Wait for async event handler
        await Task.Delay(100);

        // Assert
        var stats = _service.GetStatistics();
        Assert.Equal(0, stats.FilesDeleted);
        Assert.Equal(1, stats.DeletionErrors);
    }

    [Fact]
    public async Task DeletionException_IncrementsErrorCounter()
    {
        // Arrange
        var filePath = @"C:\test\document.txt";
        _mockDocumentProcessor
            .Setup(x => x.RemoveDocumentAsync(filePath, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Test exception"));

        await _service.StartAsync();

        // Act
        RaiseFileChangedEvent(filePath, FileChangeType.Deleted);

        // Wait for async event handler
        await Task.Delay(100);

        // Assert
        var stats = _service.GetStatistics();
        Assert.Equal(0, stats.FilesDeleted);
        Assert.Equal(1, stats.DeletionErrors);
    }

    [Fact]
    public async Task GetStatistics_ReturnsAccurateStatistics()
    {
        // Arrange
        var filePath1 = @"C:\test\doc1.txt";
        var filePath2 = @"C:\test\doc2.txt";
        var filePath3 = @"C:\test\doc3.txt";

        _mockDocumentProcessor
            .Setup(x => x.ProcessDocumentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentProcessingResult { Success = true, DocumentId = "doc", ChunkCount = 1 });

        _mockDocumentProcessor
            .Setup(x => x.RemoveDocumentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _service.StartAsync();

        // Act
        RaiseFileChangedEvent(filePath1, FileChangeType.Created);
        RaiseFileChangedEvent(filePath2, FileChangeType.Modified);
        RaiseFileChangedEvent(filePath3, FileChangeType.Deleted);

        // Wait for all async handlers
        await Task.Delay(200);

        // Assert
        var stats = _service.GetStatistics();
        Assert.Equal(2, stats.FilesProcessed);
        Assert.Equal(1, stats.FilesDeleted);
        Assert.Equal(0, stats.ProcessingErrors);
        Assert.Equal(0, stats.DeletionErrors);
    }

    [Fact]
    public async Task DisposeAsync_StopsService()
    {
        // Arrange
        await _service.StartAsync();

        // Act
        await _service.DisposeAsync();

        // Assert
        Assert.False(_service.IsRunning);
    }

    [Fact]
    public async Task DisposeAsync_MultipleCalls_DoesNotThrow()
    {
        // Act 1
        await _service.DisposeAsync();

        // Act 2
        await _service.DisposeAsync();

        // Assert - no exception thrown
        Assert.False(_service.IsRunning);
    }

    [Fact]
    public async Task StartAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        await _service.DisposeAsync();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(() => _service.StartAsync());
    }

    private void RaiseFileChangedEvent(string filePath, FileChangeType changeType, string? oldFilePath = null)
    {
        var args = new FileSystemChangeEventArgs
        {
            FilePath = filePath,
            ChangeType = changeType,
            OldFilePath = oldFilePath
        };

        _mockFileSystemWatcher.Raise(x => x.FileChanged += null, _mockFileSystemWatcher.Object, args);
    }
}
