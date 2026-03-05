using Daiv3.Persistence;
using Daiv3.Persistence.Entities;
using Daiv3.Persistence.Repositories;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Daiv3.Persistence.Tests;

/// <summary>
/// Unit tests for SettingsRepository.
/// Tests CRUD operations, versioning, and history tracking.
/// </summary>
public class SettingsRepositoryTests
{
    private readonly Mock<IDatabaseContext> _mockDatabaseContext;
    private readonly Mock<ILogger<SettingsRepository>> _mockLogger;

    public SettingsRepositoryTests()
    {
        _mockDatabaseContext = new Mock<IDatabaseContext>();
        _mockLogger = new Mock<ILogger<SettingsRepository>>();
    }

    [Fact]
    public void Constructor_WithValidDependencies_InitializesSuccessfully()
    {
        // Act
        var repository = new SettingsRepository(_mockDatabaseContext.Object, _mockLogger.Object);

        // Assert
        Assert.NotNull(repository);
    }

    [Fact]
    public void Constructor_WithNullDatabaseContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new SettingsRepository(null!, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new SettingsRepository(_mockDatabaseContext.Object, null!));
    }

    [Fact]
    public void Constructor_WithBothNullDependencies_ThrowsArgumentNullException()
    {
        // Act & Assert
        // Should throw for database context first
        Assert.Throws<ArgumentNullException>(() => 
            new SettingsRepository(null!, null!));
    }

    [Fact]
    public async Task GetByKeyAsync_WithNullKey_ThrowsArgumentNullException()
    {
        // Arrange
        var repository = new SettingsRepository(_mockDatabaseContext.Object, _mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => 
            repository.GetByKeyAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task GetByKeyAsync_WithEmptyKey_ThrowsArgumentException()
    {
        // Arrange
        var repository = new SettingsRepository(_mockDatabaseContext.Object, _mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            repository.GetByKeyAsync("", CancellationToken.None));
    }

    [Fact]
    public async Task GetByCategoryAsync_WithNullCategory_ThrowsArgumentNullException()
    {
        // Arrange
        var repository = new SettingsRepository(_mockDatabaseContext.Object, _mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => 
            repository.GetByCategoryAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task GetByCategoryAsync_WithEmptyCategory_ThrowsArgumentException()
    {
        // Arrange
        var repository = new SettingsRepository(_mockDatabaseContext.Object, _mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            repository.GetByCategoryAsync("", CancellationToken.None));
    }

    [Fact]
    public async Task AddAsync_WithNullEntity_ThrowsArgumentNullException()
    {
        // Arrange
        var repository = new SettingsRepository(_mockDatabaseContext.Object, _mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => 
            repository.AddAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task AddAsync_WithNullSettingId_ThrowsArgumentException()
    {
        // Arrange
        var repository = new SettingsRepository(_mockDatabaseContext.Object, _mockLogger.Object);
        var setting = new AppSetting { SettingId = "", SettingKey = "key1", SettingValue = "value1" };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            repository.AddAsync(setting, CancellationToken.None));
    }

    [Fact]
    public async Task AddAsync_WithNullSettingKey_ThrowsArgumentException()
    {
        // Arrange
        var repository = new SettingsRepository(_mockDatabaseContext.Object, _mockLogger.Object);
        var setting = new AppSetting { SettingId = "id1", SettingKey = "", SettingValue = "value1" };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            repository.AddAsync(setting, CancellationToken.None));
    }

    [Fact]
    public async Task AddAsync_WithNullSettingValue_ThrowsArgumentException()
    {
        // Arrange
        var repository = new SettingsRepository(_mockDatabaseContext.Object, _mockLogger.Object);
        var setting = new AppSetting { SettingId = "id1", SettingKey = "key1", SettingValue = "" };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            repository.AddAsync(setting, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateAsync_WithNullEntity_ThrowsArgumentNullException()
    {
        // Arrange
        var repository = new SettingsRepository(_mockDatabaseContext.Object, _mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => 
            repository.UpdateAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateAsync_WithNullSettingId_ThrowsArgumentException()
    {
        // Arrange
        var repository = new SettingsRepository(_mockDatabaseContext.Object, _mockLogger.Object);
        var setting = new AppSetting { SettingId = "", SettingKey = "key1", SettingValue = "value1" };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            repository.UpdateAsync(setting, CancellationToken.None));
    }

    [Fact]
    public async Task DeleteAsync_WithNullId_ThrowsArgumentNullException()
    {
        // Arrange
        var repository = new SettingsRepository(_mockDatabaseContext.Object, _mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => 
            repository.DeleteAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task DeleteAsync_WithEmptyId_ThrowsArgumentException()
    {
        // Arrange
        var repository = new SettingsRepository(_mockDatabaseContext.Object, _mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            repository.DeleteAsync("", CancellationToken.None));
    }

    [Fact]
    public async Task UpsertAsync_WithNullEntity_ThrowsArgumentNullException()
    {
        // Arrange
        var repository = new SettingsRepository(_mockDatabaseContext.Object, _mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => 
            repository.UpsertAsync(null!, null, CancellationToken.None));
    }

    [Fact]
    public async Task UpsertAsync_WithNullSettingKey_ThrowsArgumentException()
    {
        // Arrange
        var repository = new SettingsRepository(_mockDatabaseContext.Object, _mockLogger.Object);
        var setting = new AppSetting { SettingId = "id1", SettingKey = "", SettingValue = "value1" };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            repository.UpsertAsync(setting, null, CancellationToken.None));
    }

    [Fact]
    public async Task GetHistoryByKeyAsync_WithNullKey_ThrowsArgumentNullException()
    {
        // Arrange
        var repository = new SettingsRepository(_mockDatabaseContext.Object, _mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => 
            repository.GetHistoryByKeyAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task GetHistoryByKeyAsync_WithEmptyKey_ThrowsArgumentException()
    {
        // Arrange
        var repository = new SettingsRepository(_mockDatabaseContext.Object, _mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            repository.GetHistoryByKeyAsync("", CancellationToken.None));
    }

    [Fact]
    public async Task AddHistoryAsync_WithNullHistory_ThrowsArgumentNullException()
    {
        // Arrange
        var repository = new SettingsRepository(_mockDatabaseContext.Object, _mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => 
            repository.AddHistoryAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task AddHistoryAsync_WithNullHistoryId_ThrowsArgumentException()
    {
        // Arrange
        var repository = new SettingsRepository(_mockDatabaseContext.Object, _mockLogger.Object);
        var history = new SettingsVersionHistory 
        { 
            HistoryId = "", 
            SettingKey = "key1", 
            NewValue = "value1" 
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            repository.AddHistoryAsync(history, CancellationToken.None));
    }

    [Fact]
    public async Task AddHistoryAsync_WithNullSettingKey_ThrowsArgumentException()
    {
        // Arrange
        var repository = new SettingsRepository(_mockDatabaseContext.Object, _mockLogger.Object);
        var history = new SettingsVersionHistory 
        { 
            HistoryId = "hist1", 
            SettingKey = "", 
            NewValue = "value1" 
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            repository.AddHistoryAsync(history, CancellationToken.None));
    }
}
