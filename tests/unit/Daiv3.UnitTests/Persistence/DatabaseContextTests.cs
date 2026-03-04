using Daiv3.Persistence;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Daiv3.UnitTests.Persistence;

/// <summary>
/// Unit tests for DatabaseContext initialization and configuration.
/// Tests basic functionality without full integration.
/// </summary>
public class DatabaseContextTests
{
    private readonly Mock<ILogger<DatabaseContext>> _mockLogger;
    private readonly string _testDbPath;

    public DatabaseContextTests()
    {
        _mockLogger = new Mock<ILogger<DatabaseContext>>();
        _testDbPath = Path.Combine(Path.GetTempPath(), $"daiv3_test_{Guid.NewGuid():N}.db");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var options = CreateTestOptions();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new DatabaseContext(null!, options));
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() => new DatabaseContext(_mockLogger.Object, null!));
    }

    [Fact]
    public async Task Constructor_WithValidParameters_Succeeds()
    {
        // Arrange
        var options = CreateTestOptions();

        // Act
        await using var context = new DatabaseContext(_mockLogger.Object, options);

        // Assert
        Assert.NotNull(context);
        Assert.NotNull(context.DatabasePath);
    }

    [Fact]
    public async Task DatabasePath_ReturnsExpandedPath()
    {
        // Arrange
        var options = CreateTestOptions();

        // Act
        await using var context = new DatabaseContext(_mockLogger.Object, options);

        // Assert
        Assert.Equal(_testDbPath, context.DatabasePath);
        Assert.DoesNotContain("%", context.DatabasePath);
    }

    [Fact]
    public async Task Constructor_LogsInitialization()
    {
        // Arrange
        var options = CreateTestOptions();

        // Act
        await using var context = new DatabaseContext(_mockLogger.Object, options);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("DatabaseContext initialized")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task DisposeAsync_CompletesSuccessfully()
    {
        // Arrange
        var options = CreateTestOptions();
        var context = new DatabaseContext(_mockLogger.Object, options);

        // Act
        await context.DisposeAsync();

        // Assert - No exception thrown
    }

    [Fact]
    public async Task DisposeAsync_CanBeCalledMultipleTimes()
    {
        // Arrange
        var options = CreateTestOptions();
        var context = new DatabaseContext(_mockLogger.Object, options);

        // Act
        await context.DisposeAsync();
        await context.DisposeAsync(); // Should not throw

        // Assert - No exception thrown
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Constructor_WithEnableWALOption_CreatesContext(bool enableWal)
    {
        // Arrange
        var options = CreateTestOptions(enableWal: enableWal);

        // Act
        await using var context = new DatabaseContext(_mockLogger.Object, options);

        // Assert
        Assert.NotNull(context);
    }

    [Theory]
    [InlineData(1000)]
    [InlineData(5000)]
    [InlineData(30000)]
    public async Task Constructor_WithBusyTimeoutOption_CreatesContext(int busyTimeout)
    {
        // Arrange
        var options = CreateTestOptions(busyTimeout: busyTimeout);

        // Act
        await using var context = new DatabaseContext(_mockLogger.Object, options);

        // Assert
        Assert.NotNull(context);
    }

    [Fact]
    public async Task Constructor_WithLongPath_HandlesPathCorrectly()
    {
        // Arrange
        var longPath = Path.Combine(
            Path.GetTempPath(),
            "very_long_directory_name_that_tests_path_handling",
            "subdirectory",
            "another_subdirectory",
            "test.db");

        var options = Options.Create(new PersistenceOptions
        {
            DatabasePath = longPath,
            EnableWAL = true,
            BusyTimeout = 5000,
            MaxPoolSize = 10
        });

        // Act
        await using var context = new DatabaseContext(_mockLogger.Object, options);

        // Assert
        Assert.Equal(longPath, context.DatabasePath);
    }

    [Fact]
    public async Task Constructor_WithRelativePath_HandlesPathCorrectly()
    {
        // Arrange
        var relativePath = Path.Combine(".", "data", "test.db");
        var options = Options.Create(new PersistenceOptions
        {
            DatabasePath = relativePath,
            EnableWAL = true,
            BusyTimeout = 5000,
            MaxPoolSize = 10
        });

        // Act
        await using var context = new DatabaseContext(_mockLogger.Object, options);

        // Assert
        Assert.Contains("test.db", context.DatabasePath);
    }

    [Fact]
    public async Task DatabasePath_RemainsConsistent_AfterMultipleAccesses()
    {
        // Arrange
        var options = CreateTestOptions();
        await using var context = new DatabaseContext(_mockLogger.Object, options);

        // Act
        var path1 = context.DatabasePath;
        var path2 = context.DatabasePath;
        var path3 = context.DatabasePath;

        // Assert
        Assert.Equal(path1, path2);
        Assert.Equal(path2, path3);
    }

    private IOptions<PersistenceOptions> CreateTestOptions(bool enableWal = true, int busyTimeout = 5000)
    {
        return Options.Create(new PersistenceOptions
        {
            DatabasePath = _testDbPath,
            EnableWAL = enableWal,
            BusyTimeout = busyTimeout,
            MaxPoolSize = 10
        });
    }
}
