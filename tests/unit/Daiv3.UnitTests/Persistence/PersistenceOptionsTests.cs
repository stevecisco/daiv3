using Daiv3.Persistence;
using Xunit;

namespace Daiv3.UnitTests.Persistence;

/// <summary>
/// Unit tests for PersistenceOptions configuration class.
/// Tests path expansion, connection string building, and default values.
/// </summary>
public class PersistenceOptionsTests
{
    [Fact]
    public void Constructor_SetsDefaultValues()
    {
        // Arrange & Act
        var options = new PersistenceOptions();

        // Assert
        Assert.NotNull(options.DatabasePath);
        Assert.True(options.EnableWAL);
        Assert.Equal(5000, options.BusyTimeout);
        Assert.Equal(10, options.MaxPoolSize);
    }

    [Fact]
    public void GetExpandedDatabasePath_ExpandsEnvironmentVariables()
    {
        // Arrange
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var options = new PersistenceOptions();

        // Act
        var expandedPath = options.GetExpandedDatabasePath();

        // Assert
        Assert.Contains(localAppData, expandedPath);
        Assert.EndsWith("daiv3.db", expandedPath);
        Assert.DoesNotContain("%", expandedPath);
    }

    [Fact]
    public void GetExpandedDatabasePath_WithCustomPath_ExpandsVariables()
    {
        // Arrange
        var options = new PersistenceOptions
        {
            DatabasePath = "%TEMP%\\test.db"
        };

        // Act
        var expandedPath = options.GetExpandedDatabasePath();

        // Assert
        Assert.DoesNotContain("%TEMP%", expandedPath);
        Assert.Contains(Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar), expandedPath);
        Assert.EndsWith("test.db", expandedPath);
    }

    [Fact]
    public void BuildConnectionString_ContainsRequiredParameters()
    {
        // Arrange
        var options = new PersistenceOptions
        {
            DatabasePath = "c:\\test\\test.db"
        };

        // Act
        var connectionString = options.BuildConnectionString();

        // Assert
        Assert.Contains("Data Source=", connectionString);
        Assert.Contains("c:\\test\\test.db", connectionString);
        Assert.Contains("Mode=ReadWriteCreate", connectionString);
        Assert.Contains("Cache=Shared", connectionString);
        Assert.Contains("Pooling=True", connectionString);
    }

    [Fact]
    public void BuildConnectionString_ExpandsEnvironmentVariables()
    {
        // Arrange
        var options = new PersistenceOptions
        {
            DatabasePath = "%TEMP%\\test.db"
        };

        // Act
        var connectionString = options.BuildConnectionString();

        // Assert
        Assert.DoesNotContain("%TEMP%", connectionString);
        Assert.Contains(Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar), connectionString);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EnableWAL_CanBeSetAndRetrieved(bool enableWal)
    {
        // Arrange
        var options = new PersistenceOptions
        {
            EnableWAL = enableWal
        };

        // Act & Assert
        Assert.Equal(enableWal, options.EnableWAL);
    }

    [Theory]
    [InlineData(1000)]
    [InlineData(5000)]
    [InlineData(30000)]
    public void BusyTimeout_CanBeSetAndRetrieved(int timeout)
    {
        // Arrange
        var options = new PersistenceOptions
        {
            BusyTimeout = timeout
        };

        // Act & Assert
        Assert.Equal(timeout, options.BusyTimeout);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(50)]
    public void MaxPoolSize_CanBeSetAndRetrieved(int poolSize)
    {
        // Arrange
        var options = new PersistenceOptions
        {
            MaxPoolSize = poolSize
        };

        // Act & Assert
        Assert.Equal(poolSize, options.MaxPoolSize);
    }
}
