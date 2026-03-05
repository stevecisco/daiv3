using Daiv3.Core.Settings;
using Daiv3.Persistence;
using Daiv3.Persistence.Entities;
using Daiv3.Persistence.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Daiv3.Persistence.Tests;

/// <summary>
/// Unit tests for SettingsInitializer.
/// Tests CT-REQ-001: The system SHALL store all settings locally.
/// </summary>
public class SettingsInitializerTests
{
    private readonly Mock<ISettingsService> _mockSettingsService;
    private readonly Mock<ILogger<SettingsInitializer>> _mockLogger;
    private readonly SettingsInitializer _initializer;

    public SettingsInitializerTests()
    {
        _mockSettingsService = new Mock<ISettingsService>();
        _mockLogger = new Mock<ILogger<SettingsInitializer>>();
        _initializer = new SettingsInitializer(_mockSettingsService.Object, _mockLogger.Object);
    }

    [Fact]
    public void Constructor_WithNullSettingsService_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => 
            new SettingsInitializer(null!, _mockLogger.Object));
        Assert.Equal("settingsService", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new SettingsInitializer(_mockSettingsService.Object, null!));
        Assert.Equal("logger", exception.ParamName);
    }

    [Fact]
    public async Task InitializeDefaultSettingsAsync_WithNoExistingSettings_ShouldCreateAllSettings()
    {
        // Arrange
        _mockSettingsService.Setup(x => x.GetSettingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AppSetting?)null);

        _mockSettingsService.Setup(x => x.SaveSettingAsync(
                It.IsAny<string>(), It.IsAny<object>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid().ToString());

        // Act
        var count = await _initializer.InitializeDefaultSettingsAsync();

        // Assert
        Assert.True(count > 0, "Should initialize at least one setting");
        _mockSettingsService.Verify(x => x.SaveSettingAsync(
            It.IsAny<string>(), It.IsAny<object>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.AtLeast(50));
    }

    [Fact]
    public async Task InitializeDefaultSettingsAsync_WithExistingSettings_ShouldSkipExisting()
    {
        // Arrange
        var existingSetting = new AppSetting
        {
            SettingId = Guid.NewGuid().ToString(),
            SettingKey = ApplicationSettings.Paths.DataDirectory,
            SettingValue = "existing_value",
            ValueType = "string",
            Category = ApplicationSettings.Categories.Paths,
            SchemaVersion = 1,
            Description = "Test",
            IsSensitive = false,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            UpdatedBy = "test"
        };

        _mockSettingsService.Setup(x => x.GetSettingAsync(ApplicationSettings.Paths.DataDirectory, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingSetting);

        _mockSettingsService.Setup(x => x.GetSettingAsync(It.Is<string>(k => k != ApplicationSettings.Paths.DataDirectory), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AppSetting?)null);

        _mockSettingsService.Setup(x => x.SaveSettingAsync(
                It.IsAny<string>(), It.IsAny<object>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid().ToString());

        // Act
        var count = await _initializer.InitializeDefaultSettingsAsync();

        // Assert
        Assert.True(count > 0, "Should initialize new settings");
        _mockSettingsService.Verify(x => x.SaveSettingAsync(
            ApplicationSettings.Paths.DataDirectory, It.IsAny<object>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task InitializeDefaultSettingsAsync_ShouldMarkFirstRunComplete()
    {
        // Arrange
        _mockSettingsService.Setup(x => x.GetSettingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AppSetting?)null);

        _mockSettingsService.Setup(x => x.SaveSettingAsync(
                It.IsAny<string>(), It.IsAny<object>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid().ToString());

        // Act
        await _initializer.InitializeDefaultSettingsAsync();

        // Assert
        _mockSettingsService.Verify(x => x.SaveSettingAsync(
            ApplicationSettings.General.FirstRunCompleted,
            true,
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<bool>(),
            "initial_setup_complete",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InitializeDefaultSettingsAsync_ShouldRecordStartupTime()
    {
        // Arrange
        _mockSettingsService.Setup(x => x.GetSettingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AppSetting?)null);

        _mockSettingsService.Setup(x => x.SaveSettingAsync(
                It.IsAny<string>(), It.IsAny<object>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid().ToString());

        // Act
        await _initializer.InitializeDefaultSettingsAsync();

        // Assert
        _mockSettingsService.Verify(x => x.SaveSettingAsync(
            ApplicationSettings.General.LastStartupTime,
            It.IsAny<object>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<bool>(),
            "startup",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InitializeDefaultSettingsAsync_WithSaveError_ShouldContinueWithOtherSettings()
    {
        // Arrange
        _mockSettingsService.Setup(x => x.GetSettingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AppSetting?)null);

        var callCount = 0;
        _mockSettingsService.Setup(x => x.SaveSettingAsync(
                It.IsAny<string>(), It.IsAny<object>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 5)
                {
                    throw new InvalidOperationException("Simulated save error");
                }
                return Guid.NewGuid().ToString();
            });

        // Act
        var count = await _initializer.InitializeDefaultSettingsAsync();

        // Assert
        Assert.True(count > 0, "Should initialize some settings despite error");
    }

    [Fact]
    public async Task AreSettingsInitializedAsync_WhenFirstRunComplete_ShouldReturnTrue()
    {
        // Arrange
        _mockSettingsService.Setup(x => x.GetSettingValueAsync<bool>(
                ApplicationSettings.General.FirstRunCompleted, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _initializer.AreSettingsInitializedAsync();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task AreSettingsInitializedAsync_WhenFirstRunNotComplete_ShouldReturnFalse()
    {
        // Arrange
        _mockSettingsService.Setup(x => x.GetSettingValueAsync<bool>(
                ApplicationSettings.General.FirstRunCompleted, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _initializer.AreSettingsInitializedAsync();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task AreSettingsInitializedAsync_WhenSettingNotFound_ShouldReturnFalse()
    {
        // Arrange
        _mockSettingsService.Setup(x => x.GetSettingValueAsync<bool>(
                ApplicationSettings.General.FirstRunCompleted, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Setting not found"));

        // Act
        var result = await _initializer.AreSettingsInitializedAsync();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ResetToDefaultsAsync_ShouldResetAllSettings()
    {
        // Arrange
        _mockSettingsService.Setup(x => x.SaveSettingAsync(
                It.IsAny<string>(), It.IsAny<object>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid().ToString());

        // Act
        var count = await _initializer.ResetToDefaultsAsync();

        // Assert
        Assert.True(count > 0, "Should reset at least one setting");
        _mockSettingsService.Verify(x => x.SaveSettingAsync(
            It.IsAny<string>(), It.IsAny<object>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<bool>(), "reset_to_defaults", It.IsAny<CancellationToken>()), Times.AtLeast(50));
    }

    [Fact]
    public async Task ResetToDefaultsAsync_WithSaveError_ShouldContinueWithOtherSettings()
    {
        // Arrange
        var callCount = 0;
        _mockSettingsService.Setup(x => x.SaveSettingAsync(
                It.IsAny<string>(), It.IsAny<object>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 3)
                {
                    throw new InvalidOperationException("Simulated save error");
                }
                return Guid.NewGuid().ToString();
            });

        // Act
        var count = await _initializer.ResetToDefaultsAsync();

        // Assert
        Assert.True(count > 0, "Should reset some settings despite error");
    }

    [Fact]
    public async Task InitializeDefaultSettingsAsync_ShouldCreateSensitiveSettings()
    {
        // Arrange
        _mockSettingsService.Setup(x => x.GetSettingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AppSetting?)null);

        _mockSettingsService.Setup(x => x.SaveSettingAsync(
                It.IsAny<string>(), It.IsAny<object>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid().ToString());

        // Act
        await _initializer.InitializeDefaultSettingsAsync();

        // Assert - Verify sensitive settings are created with isSensitive = true
        _mockSettingsService.Verify(x => x.SaveSettingAsync(
            ApplicationSettings.Providers.OpenAIApiKey,
            It.IsAny<object>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            true, // isSensitive
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InitializeDefaultSettingsAsync_ShouldCreateAllCategories()
    {
        // Arrange
        _mockSettingsService.Setup(x => x.GetSettingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AppSetting?)null);

        var savedSettings = new List<(string key, string category)>();
        _mockSettingsService.Setup(x => x.SaveSettingAsync(
                It.IsAny<string>(), It.IsAny<object>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<string, object, string, string, bool, string?, CancellationToken>(
                (key, value, category, desc, sensitive, reason, ct) =>
                {
                    savedSettings.Add((key, category));
                })
            .ReturnsAsync(Guid.NewGuid().ToString());

        // Act
        await _initializer.InitializeDefaultSettingsAsync();

        // Assert - Verify all categories are represented
        var categories = savedSettings.Select(s => s.category).Distinct().ToList();
        Assert.Contains(ApplicationSettings.Categories.General, categories);
        Assert.Contains(ApplicationSettings.Categories.Paths, categories);
        Assert.Contains(ApplicationSettings.Categories.Models, categories);
        Assert.Contains(ApplicationSettings.Categories.Providers, categories);
        Assert.Contains(ApplicationSettings.Categories.Hardware, categories);
        Assert.Contains(ApplicationSettings.Categories.UI, categories);
        Assert.Contains(ApplicationSettings.Categories.Knowledge, categories);
    }
}
