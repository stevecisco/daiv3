using Daiv3.Knowledge.DocProc;
using Xunit;

namespace Daiv3.Knowledge.Tests.DocProc;

/// <summary>
/// Unit tests for FileSystemWatcherOptions.
/// </summary>
public sealed class FileSystemWatcherOptionsTests
{
    [Fact]
    public void DefaultOptions_AreValid()
    {
        // Arrange
        var options = new FileSystemWatcherOptions();

        // Act & Assert - should not throw
        options.Validate();
    }

    [Fact]
    public void Validate_WithEmptyWatchPathsAndNoAutoStart_Succeeds()
    {
        // Arrange
        var options = new FileSystemWatcherOptions
        {
            WatchPaths = new Dictionary<string, bool>(),
            AutoStart = false
        };

        // Act & Assert - should not throw
        options.Validate();
    }

    [Fact]
    public void Validate_WithRelativePath_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = new FileSystemWatcherOptions
        {
            WatchPaths = new Dictionary<string, bool>
            {
                { "relative\\path", true }
            }
        };

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains("absolute path", exception.Message);
    }

    [Fact]
    public void Validate_WithEmptyPath_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = new FileSystemWatcherOptions
        {
            WatchPaths = new Dictionary<string, bool>
            {
                { string.Empty, true }
            }
        };

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains("cannot be empty", exception.Message);
    }

    [Fact]
    public void Validate_WithNegativeDebounceDelay_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = new FileSystemWatcherOptions
        {
            DebounceDelayMs = -100
        };

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains("non-negative", exception.Message);
    }

    [Fact]
    public void Validate_WithZeroMaxFileSize_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = new FileSystemWatcherOptions
        {
            MaxFileSizeBytes = 0
        };

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains("positive", exception.Message);
    }

    [Fact]
    public void Validate_WithNegativeMaxFileSize_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = new FileSystemWatcherOptions
        {
            MaxFileSizeBytes = -100
        };

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains("positive", exception.Message);
    }

    [Fact]
    public void DefaultIncludePatterns_ContainsCommonDocumentFormats()
    {
        // Arrange & Act
        var options = new FileSystemWatcherOptions();

        // Assert
        Assert.Contains("*.pdf", options.IncludePatterns);
        Assert.Contains("*.docx", options.IncludePatterns);
        Assert.Contains("*.md", options.IncludePatterns);
        Assert.Contains("*.txt", options.IncludePatterns);
    }

    [Fact]
    public void DefaultIncludePatterns_ContainsCommonCodeFormats()
    {
        // Arrange & Act
        var options = new FileSystemWatcherOptions();

        // Assert
        Assert.Contains("*.cs", options.IncludePatterns);
        Assert.Contains("*.js", options.IncludePatterns);
        Assert.Contains("*.py", options.IncludePatterns);
    }

    [Fact]
    public void DefaultExcludeDirectories_ContainsCommonBuildDirectories()
    {
        // Arrange & Act
        var options = new FileSystemWatcherOptions();

        // Assert
        Assert.Contains("node_modules", options.ExcludeDirectories);
        Assert.Contains(".git", options.ExcludeDirectories);
        Assert.Contains("bin", options.ExcludeDirectories);
        Assert.Contains("obj", options.ExcludeDirectories);
    }

    [Fact]
    public void DefaultExcludePatterns_ContainsTempFiles()
    {
        // Arrange & Act
        var options = new FileSystemWatcherOptions();

        // Assert
        Assert.Contains("*.tmp", options.ExcludePatterns);
        Assert.Contains("*.temp", options.ExcludePatterns);
        Assert.Contains("*.log", options.ExcludePatterns);
    }

    [Fact]
    public void DefaultDebounceDelay_Is500Ms()
    {
        // Arrange & Act
        var options = new FileSystemWatcherOptions();

        // Assert
        Assert.Equal(500, options.DebounceDelayMs);
    }

    [Fact]
    public void DefaultAutoStart_IsFalse()
    {
        // Arrange & Act
        var options = new FileSystemWatcherOptions();

        // Assert
        Assert.False(options.AutoStart);
    }

    [Fact]
    public void DefaultMaxFileSize_Is100MB()
    {
        // Arrange & Act
        var options = new FileSystemWatcherOptions();

        // Assert
        Assert.Equal(100 * 1024 * 1024, options.MaxFileSizeBytes);
    }

    [Fact]
    public void DefaultProcessExistingFiles_IsFalse()
    {
        // Arrange & Act
        var options = new FileSystemWatcherOptions();

        // Assert
        Assert.False(options.ProcessExistingFilesOnStart);
    }

    [Fact]
    public void DefaultVerboseLogging_IsFalse()
    {
        // Arrange & Act
        var options = new FileSystemWatcherOptions();

        // Assert
        Assert.False(options.EnableVerboseLogging);
    }
}
