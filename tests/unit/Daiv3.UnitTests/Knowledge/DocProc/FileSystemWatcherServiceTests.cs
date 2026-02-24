using Daiv3.Knowledge.DocProc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Daiv3.UnitTests.Knowledge.DocProc;

/// <summary>
/// Unit tests for FileSystemWatcherService.
/// </summary>
public sealed class FileSystemWatcherServiceTests : IDisposable
{
    private readonly Mock<ILogger<FileSystemWatcherService>> _loggerMock;
    private readonly string _testDirectory;

    public FileSystemWatcherServiceTests()
    {
        _loggerMock = new Mock<ILogger<FileSystemWatcherService>>();
        _testDirectory = Path.Combine(Path.GetTempPath(), $"Daiv3Test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            try
            {
                Directory.Delete(_testDirectory, true);
            }
            catch
            {
                // Best effort cleanup
            }
        }
    }

    [Fact]
    public void Constructor_WithValidOptions_Succeeds()
    {
        // Arrange
        var options = Options.Create(new FileSystemWatcherOptions());

        // Act
        var service = new FileSystemWatcherService(_loggerMock.Object, options);

        // Assert
        Assert.NotNull(service);
        Assert.False(service.IsRunning);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var options = Options.Create(new FileSystemWatcherOptions());

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new FileSystemWatcherService(null!, options));
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new FileSystemWatcherService(_loggerMock.Object, null!));
    }

    [Fact]
    public void AddWatchPath_WithEmptyPath_ThrowsArgumentException()
    {
        // Arrange
        var options = Options.Create(new FileSystemWatcherOptions());
        var service = new FileSystemWatcherService(_loggerMock.Object, options);

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            service.AddWatchPath(string.Empty));
    }

    [Fact]
    public void AddWatchPath_WithRelativePath_ThrowsArgumentException()
    {
        // Arrange
        var options = Options.Create(new FileSystemWatcherOptions());
        var service = new FileSystemWatcherService(_loggerMock.Object, options);

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            service.AddWatchPath("relative\\path"));
    }

    [Fact]
    public void AddWatchPath_WithValidPath_AddsToWatchedPaths()
    {
        // Arrange
        var options = Options.Create(new FileSystemWatcherOptions());
        var service = new FileSystemWatcherService(_loggerMock.Object, options);

        // Act
        service.AddWatchPath(_testDirectory);

        // Assert
        var watchedPaths = service.GetWatchedPaths();
        Assert.Contains(_testDirectory, watchedPaths);
    }

    [Fact]
    public void RemoveWatchPath_RemovesPath()
    {
        // Arrange
        var options = Options.Create(new FileSystemWatcherOptions());
        var service = new FileSystemWatcherService(_loggerMock.Object, options);
        service.AddWatchPath(_testDirectory);

        // Act
        service.RemoveWatchPath(_testDirectory);

        // Assert
        var watchedPaths = service.GetWatchedPaths();
        Assert.DoesNotContain(_testDirectory, watchedPaths);
    }

    [Fact]
    public void GetWatchedPaths_ReturnsEmptyList_WhenNoPaths()
    {
        // Arrange
        var options = Options.Create(new FileSystemWatcherOptions());
        var service = new FileSystemWatcherService(_loggerMock.Object, options);

        // Act
        var watchedPaths = service.GetWatchedPaths();

        // Assert
        Assert.Empty(watchedPaths);
    }

    [Fact]
    public async Task StartAsync_WithNoPaths_Succeeds()
    {
        // Arrange
        var options = Options.Create(new FileSystemWatcherOptions());
        var service = new FileSystemWatcherService(_loggerMock.Object, options);

        // Act
        await service.StartAsync();

        // Assert
        Assert.True(service.IsRunning);
    }

    [Fact]
    public async Task StartAsync_WhenAlreadyRunning_DoesNotThrow()
    {
        // Arrange
        var options = Options.Create(new FileSystemWatcherOptions());
        var service = new FileSystemWatcherService(_loggerMock.Object, options);
        await service.StartAsync();

        // Act & Assert (should not throw)
        await service.StartAsync();
        Assert.True(service.IsRunning);
    }

    [Fact]
    public async Task StopAsync_StopsService()
    {
        // Arrange
        var options = Options.Create(new FileSystemWatcherOptions());
        var service = new FileSystemWatcherService(_loggerMock.Object, options);
        await service.StartAsync();

        // Act
        await service.StopAsync();

        // Assert
        Assert.False(service.IsRunning);
    }

    [Fact]
    public async Task StopAsync_WhenNotRunning_DoesNotThrow()
    {
        // Arrange
        var options = Options.Create(new FileSystemWatcherOptions());
        var service = new FileSystemWatcherService(_loggerMock.Object, options);

        // Act & Assert (should not throw)
        await service.StopAsync();
    }

    [Fact]
    public async Task FileCreation_RaisesFileChangedEvent()
    {
        // Arrange
        var options = Options.Create(new FileSystemWatcherOptions
        {
            DebounceDelayMs = 100,
            WatchPaths = new Dictionary<string, bool> { { _testDirectory, false } }
        });

        var service = new FileSystemWatcherService(_loggerMock.Object, options);
        var eventRaised = false;
        var eventArgs = (FileSystemChangeEventArgs?)null;

        service.FileChanged += (sender, args) =>
        {
            eventRaised = true;
            eventArgs = args;
        };

        await service.StartAsync();

        // Act
        var testFilePath = Path.Combine(_testDirectory, "test.txt");
        File.WriteAllText(testFilePath, "test content");

        // Wait for debounce + some buffer (debounce is 100ms)
        await Task.Delay(300);

        // Assert
        Assert.True(eventRaised);
        Assert.NotNull(eventArgs);
        Assert.Equal(testFilePath, eventArgs.FilePath);
        // File.WriteAllText on a new file may trigger Created or Modified events depending on OS/timing
        Assert.True(eventArgs.ChangeType == FileChangeType.Created || eventArgs.ChangeType == FileChangeType.Modified,
            $"Expected Created or Modified, but got {eventArgs.ChangeType}");

        await service.DisposeAsync();
    }

    [Fact]
    public async Task FileModification_RaisesFileChangedEvent()
    {
        // Arrange
        var testFilePath = Path.Combine(_testDirectory, "test.txt");
        File.WriteAllText(testFilePath, "initial content");

        var options = Options.Create(new FileSystemWatcherOptions
        {
            DebounceDelayMs = 100,
            WatchPaths = new Dictionary<string, bool> { { _testDirectory, false } }
        });

        var service = new FileSystemWatcherService(_loggerMock.Object, options);
        var modificationDetected = false;

        service.FileChanged += (sender, args) =>
        {
            if (args.ChangeType == FileChangeType.Modified)
            {
                modificationDetected = true;
            }
        };

        await service.StartAsync();
        await Task.Delay(100); // Let watcher initialize

        // Act
        File.WriteAllText(testFilePath, "modified content");
        await Task.Delay(300); // Wait for debounce

        // Assert
        Assert.True(modificationDetected);

        await service.DisposeAsync();
    }

    [Fact]
    public async Task FileDeletion_RaisesFileChangedEvent()
    {
        // Arrange
        var testFilePath = Path.Combine(_testDirectory, "test.txt");
        File.WriteAllText(testFilePath, "test content");

        var options = Options.Create(new FileSystemWatcherOptions
        {
            DebounceDelayMs = 100,
            WatchPaths = new Dictionary<string, bool> { { _testDirectory, false } }
        });

        var service = new FileSystemWatcherService(_loggerMock.Object, options);
        var deletionDetected = false;

        service.FileChanged += (sender, args) =>
        {
            if (args.ChangeType == FileChangeType.Deleted)
            {
                deletionDetected = true;
            }
        };

        await service.StartAsync();
        await Task.Delay(100); // Let watcher initialize

        // Act
        File.Delete(testFilePath);
        await Task.Delay(200); // Deletion events are not debounced

        // Assert
        Assert.True(deletionDetected);

        await service.DisposeAsync();
    }

    [Fact]
    public async Task ExcludePattern_FiltersFiles()
    {
        // Arrange
        var options = Options.Create(new FileSystemWatcherOptions
        {
            DebounceDelayMs = 100,
            WatchPaths = new Dictionary<string, bool> { { _testDirectory, false } },
            ExcludePatterns = new List<string> { "*.tmp" }
        });

        var service = new FileSystemWatcherService(_loggerMock.Object, options);
        var eventRaised = false;

        service.FileChanged += (sender, args) =>
        {
            eventRaised = true;
        };

        await service.StartAsync();

        // Act
        var testFilePath = Path.Combine(_testDirectory, "test.tmp");
        File.WriteAllText(testFilePath, "temp content");
        await Task.Delay(300);

        // Assert - event should NOT be raised for excluded file
        Assert.False(eventRaised);

        await service.DisposeAsync();
    }

    [Fact]
    public async Task IncludePattern_FiltersFiles()
    {
        // Arrange
        var options = Options.Create(new FileSystemWatcherOptions
        {
            DebounceDelayMs = 100,
            WatchPaths = new Dictionary<string, bool> { { _testDirectory, false } },
            IncludePatterns = new List<string> { "*.txt" } // Only .txt files
        });

        var service = new FileSystemWatcherService(_loggerMock.Object, options);
        var eventCount = 0;

        service.FileChanged += (sender, args) =>
        {
            eventCount++;
        };

        await service.StartAsync();

        // Act
        File.WriteAllText(Path.Combine(_testDirectory, "test.txt"), "content"); // Should be detected
        File.WriteAllText(Path.Combine(_testDirectory, "test.pdf"), "content"); // Should be ignored
        await Task.Delay(500);

        // Assert - only .txt file should trigger event
        Assert.Equal(1, eventCount);

        await service.DisposeAsync();
    }

    [Fact]
    public async Task Debouncing_CombinesRapidChanges()
    {
        // Arrange
        var options = Options.Create(new FileSystemWatcherOptions
        {
            DebounceDelayMs = 300, // Longer debounce for this test
            WatchPaths = new Dictionary<string, bool> { { _testDirectory, false } }
        });

        var service = new FileSystemWatcherService(_loggerMock.Object, options);
        var eventCount = 0;

        service.FileChanged += (sender, args) =>
        {
            if (args.ChangeType == FileChangeType.Modified)
            {
                eventCount++;
            }
        };

        // Create file first
        var testFilePath = Path.Combine(_testDirectory, "test.txt");
        File.WriteAllText(testFilePath, "initial");

        await service.StartAsync();
        await Task.Delay(100);

        // Act - Make rapid changes
        File.WriteAllText(testFilePath, "change1");
        await Task.Delay(50);
        File.WriteAllText(testFilePath, "change2");
        await Task.Delay(50);
        File.WriteAllText(testFilePath, "change3");

        // Wait for debounce to complete
        await Task.Delay(600);

        // Assert - should only get one event due to debouncing
        Assert.Equal(1, eventCount);

        await service.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_StopsService()
    {
        // Arrange
        var options = Options.Create(new FileSystemWatcherOptions
        {
            WatchPaths = new Dictionary<string, bool> { { _testDirectory, false } }
        });

        var service = new FileSystemWatcherService(_loggerMock.Object, options);
        await service.StartAsync();

        // Act
        await service.DisposeAsync();

        // Assert
        Assert.False(service.IsRunning);
        Assert.Empty(service.GetWatchedPaths());
    }

    [Fact]
    public async Task AddWatchPath_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var options = Options.Create(new FileSystemWatcherOptions());
        var service = new FileSystemWatcherService(_loggerMock.Object, options);
        await service.DisposeAsync();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() =>
            service.AddWatchPath(_testDirectory));
    }
}
