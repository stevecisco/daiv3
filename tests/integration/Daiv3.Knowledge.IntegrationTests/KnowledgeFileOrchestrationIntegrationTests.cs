using Daiv3.Knowledge;
using Daiv3.Knowledge.DocProc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Daiv3.IntegrationTests.Knowledge;

/// <summary>
/// Integration tests for KnowledgeFileOrchestrationService with real FileSystemWatcherService.
/// Tests end-to-end file monitoring and document processing orchestration.
/// </summary>
public sealed class KnowledgeFileOrchestrationIntegrationTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly Mock<IKnowledgeDocumentProcessor> _mockDocumentProcessor;
    private readonly IFileSystemWatcher _fileSystemWatcher;
    private readonly KnowledgeFileOrchestrationService _orchestrationService;

    public KnowledgeFileOrchestrationIntegrationTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"daiv3_test_orchestration_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);

        _mockDocumentProcessor = new Mock<IKnowledgeDocumentProcessor>();

        var watcherOptions = Options.Create(new FileSystemWatcherOptions
        {
            WatchPaths = new Dictionary<string, bool> { { _testDirectory, false } },
            DebounceDelayMs = 50, // Shorter delay for faster tests
            IncludePatterns = new List<string> { "*.txt", "*.md" },
            ProcessExistingFilesOnStart = false
        });

        var watcherLogger = Mock.Of<ILogger<FileSystemWatcherService>>();
        _fileSystemWatcher = new FileSystemWatcherService(watcherLogger, watcherOptions);

        var orchestrationLogger = Mock.Of<ILogger<KnowledgeFileOrchestrationService>>();
        _orchestrationService = new KnowledgeFileOrchestrationService(
            _fileSystemWatcher,
            _mockDocumentProcessor.Object,
            orchestrationLogger);
    }

    [Fact]
    public async Task FileCreation_TriggersDocumentProcessing()
    {
        // Arrange
        _mockDocumentProcessor
            .Setup(x => x.ProcessDocumentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentProcessingResult
            {
                Success = true,
                DocumentId = "test-doc",
                ChunkCount = 1
            });

        await _orchestrationService.StartAsync();

        // Act
        var testFile = Path.Combine(_testDirectory, "test.txt");
        await File.WriteAllTextAsync(testFile, "Test content");

        // Wait for events to propagate
        await Task.Delay(200);

        // Assert
        _mockDocumentProcessor.Verify(
            x => x.ProcessDocumentAsync(testFile, It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);

        var stats = _orchestrationService.GetStatistics();
        Assert.True(stats.FilesProcessed >= 1, $"Expected at least 1 file processed, but got {stats.FilesProcessed}");
    }

    [Fact]
    public async Task FileModification_TriggersDocumentReprocessing()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "test.txt");
        await File.WriteAllTextAsync(testFile, "Initial content");

        _mockDocumentProcessor
            .Setup(x => x.ProcessDocumentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentProcessingResult
            {
                Success = true,
                DocumentId = "test-doc",
                ChunkCount = 1
            });

        await _orchestrationService.StartAsync();

        // Wait for initial state to settle
        await Task.Delay(100);

        var initialStats = _orchestrationService.GetStatistics();
        var initialCount = initialStats.FilesProcessed;

        // Act
        await File.WriteAllTextAsync(testFile, "Modified content");

        // Wait for events to propagate
        await Task.Delay(200);

        // Assert
        var finalStats = _orchestrationService.GetStatistics();
        Assert.True(finalStats.FilesProcessed > initialCount,
            $"Expected files processed to increase from {initialCount}, but got {finalStats.FilesProcessed}");
    }

    [Fact]
    public async Task FileDeletion_TriggersDocumentRemoval()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "test.txt");
        await File.WriteAllTextAsync(testFile, "Test content");

        _mockDocumentProcessor
            .Setup(x => x.RemoveDocumentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _orchestrationService.StartAsync();

        // Wait for initial state to settle
        await Task.Delay(100);

        // Act
        File.Delete(testFile);

        // Wait for events to propagate (deletion events are not debounced)
        await Task.Delay(200);

        // Assert
        _mockDocumentProcessor.Verify(
            x => x.RemoveDocumentAsync(testFile, It.IsAny<CancellationToken>()),
            Times.Once);

        var stats = _orchestrationService.GetStatistics();
        Assert.Equal(1, stats.FilesDeleted);
    }

    [Fact]
    public async Task FileRename_TriggersDeleteAndProcess()
    {
        // Arrange
        var originalFile = Path.Combine(_testDirectory, "original.txt");
        var renamedFile = Path.Combine(_testDirectory, "renamed.txt");

        await File.WriteAllTextAsync(originalFile, "Test content");

        _mockDocumentProcessor
            .Setup(x => x.RemoveDocumentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockDocumentProcessor
            .Setup(x => x.ProcessDocumentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentProcessingResult
            {
                Success = true,
                DocumentId = "test-doc",
                ChunkCount = 1
            });

        await _orchestrationService.StartAsync();

        // Wait for initial state to settle
        await Task.Delay(100);

        // Act
        File.Move(originalFile, renamedFile);

        // Wait for events to propagate
        await Task.Delay(300);

        // Assert
        _mockDocumentProcessor.Verify(
            x => x.RemoveDocumentAsync(originalFile, It.IsAny<CancellationToken>()),
            Times.Once);

        _mockDocumentProcessor.Verify(
            x => x.ProcessDocumentAsync(renamedFile, It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);

        var stats = _orchestrationService.GetStatistics();
        Assert.Equal(1, stats.FilesDeleted);
        Assert.True(stats.FilesProcessed >= 1);
    }

    [Fact]
    public async Task MultipleFiles_ProcessedIndependently()
    {
        // Arrange
        _mockDocumentProcessor
            .Setup(x => x.ProcessDocumentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentProcessingResult
            {
                Success = true,
                DocumentId = "test-doc",
                ChunkCount = 1
            });

        await _orchestrationService.StartAsync();

        // Act
        var file1 = Path.Combine(_testDirectory, "file1.txt");
        var file2 = Path.Combine(_testDirectory, "file2.txt");
        var file3 = Path.Combine(_testDirectory, "file3.txt");

        await File.WriteAllTextAsync(file1, "Content 1");
        await File.WriteAllTextAsync(file2, "Content 2");
        await File.WriteAllTextAsync(file3, "Content 3");

        // Wait for all events to propagate
        await Task.Delay(300);

        // Assert
        _mockDocumentProcessor.Verify(
            x => x.ProcessDocumentAsync(file1, It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
        _mockDocumentProcessor.Verify(
            x => x.ProcessDocumentAsync(file2, It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
        _mockDocumentProcessor.Verify(
            x => x.ProcessDocumentAsync(file3, It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);

        var stats = _orchestrationService.GetStatistics();
        Assert.True(stats.FilesProcessed >= 3, $"Expected at least 3 files processed, but got {stats.FilesProcessed}");
    }

    [Fact]
    public async Task FilteredFileExtensions_OnlyProcessMatching()
    {
        // Arrange
        _mockDocumentProcessor
            .Setup(x => x.ProcessDocumentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentProcessingResult
            {
                Success = true,
                DocumentId = "test-doc",
                ChunkCount = 1
            });

        await _orchestrationService.StartAsync();

        // Act
        var txtFile = Path.Combine(_testDirectory, "test.txt");
        var mdFile = Path.Combine(_testDirectory, "test.md");
        var pdfFile = Path.Combine(_testDirectory, "test.pdf"); // Not in include patterns

        await File.WriteAllTextAsync(txtFile, "Text content");
        await File.WriteAllTextAsync(mdFile, "Markdown content");
        await File.WriteAllTextAsync(pdfFile, "PDF content");

        // Wait for events to propagate
        await Task.Delay(300);

        // Assert
        _mockDocumentProcessor.Verify(
            x => x.ProcessDocumentAsync(txtFile, It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
        _mockDocumentProcessor.Verify(
            x => x.ProcessDocumentAsync(mdFile, It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
        _mockDocumentProcessor.Verify(
            x => x.ProcessDocumentAsync(pdfFile, It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessingError_TrackedInStatistics()
    {
        // Arrange
        _mockDocumentProcessor
            .Setup(x => x.ProcessDocumentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentProcessingResult
            {
                Success = false,
                ErrorMessage = "Test error"
            });

        await _orchestrationService.StartAsync();

        // Act
        var testFile = Path.Combine(_testDirectory, "test.txt");
        await File.WriteAllTextAsync(testFile, "Test content");

        // Wait for events to propagate
        await Task.Delay(200);

        // Assert
        var stats = _orchestrationService.GetStatistics();
        Assert.Equal(0, stats.FilesProcessed);
        Assert.True(stats.ProcessingErrors >= 1, $"Expected at least 1 error,  but got {stats.ProcessingErrors}");
    }

    [Fact]
    public async Task StopService_StopsProcessingEvents()
    {
        // Arrange
        _mockDocumentProcessor
            .Setup(x => x.ProcessDocumentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentProcessingResult
            {
                Success = true,
                DocumentId = "test-doc",
                ChunkCount = 1
            });

        await _orchestrationService.StartAsync();

        // Act
        await _orchestrationService.StopAsync();

        var testFile = Path.Combine(_testDirectory, "test.txt");
        await File.WriteAllTextAsync(testFile, "Test content");

        // Wait to see if any events are processed (they shouldn't be)
        await Task.Delay(200);

        // Assert
        _mockDocumentProcessor.Verify(
            x => x.ProcessDocumentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    public void Dispose()
    {
        _orchestrationService.DisposeAsync().AsTask().Wait();
        _fileSystemWatcher.DisposeAsync().AsTask().Wait();

        try
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}
