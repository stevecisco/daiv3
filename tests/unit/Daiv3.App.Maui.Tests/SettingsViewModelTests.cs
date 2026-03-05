using Daiv3.App.Maui.ViewModels;
using Daiv3.Core.Settings;
using Daiv3.FoundryLocal.Management;
using Daiv3.Persistence;
using Daiv3.Persistence.Entities;
using Daiv3.Persistence.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Daiv3.App.Maui.Tests;

/// <summary>
/// Unit tests for SettingsViewModel.
/// Updated for CT-REQ-001: Local settings storage integration.
/// </summary>
public class SettingsViewModelTests
{
    private readonly Mock<ILogger<SettingsViewModel>> _mockLogger;
    private readonly Mock<ISettingsService> _mockSettingsService;
    private readonly Mock<ISettingsInitializer> _mockSettingsInitializer;
    private readonly Mock<IFoundryLocalManagementService> _mockFoundryService;

    public SettingsViewModelTests()
    {
        _mockLogger = new Mock<ILogger<SettingsViewModel>>();
        _mockSettingsService = new Mock<ISettingsService>();
        _mockSettingsInitializer = new Mock<ISettingsInitializer>();
        _mockFoundryService = new Mock<IFoundryLocalManagementService>();

        // Setup default behavior for settings service
        _mockSettingsInitializer.Setup(x => x.AreSettingsInitializedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockSettingsService.Setup(x => x.GetSettingValueAsync<string>(
                ApplicationSettings.Paths.DataDirectory, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApplicationSettings.Defaults.DataDirectory);

        _mockSettingsService.Setup(x => x.GetSettingValueAsync<bool>(
                ApplicationSettings.Hardware.DisableNpu, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApplicationSettings.Defaults.DisableNpu);

        _mockSettingsService.Setup(x => x.GetSettingValueAsync<bool>(
                ApplicationSettings.Hardware.DisableGpu, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApplicationSettings.Defaults.DisableGpu);

        _mockSettingsService.Setup(x => x.GetSettingValueAsync<string>(
                ApplicationSettings.UI.Theme, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApplicationSettings.Defaults.Theme);

        _mockSettingsService.Setup(x => x.GetSettingValueAsync<int>(
                ApplicationSettings.Providers.DailyTokenBudget, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApplicationSettings.Defaults.DailyTokenBudget);

        _mockFoundryService.Setup(x => x.GetModelsDirectoryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("(Not configured)");
    }

    [Fact]
    public void Constructor_ShouldInitializeProperties()
    {
        // Act
        var viewModel = new SettingsViewModel(_mockLogger.Object, _mockSettingsService.Object, _mockSettingsInitializer.Object, _mockFoundryService.Object);

        // Assert
        Assert.Equal("Settings", viewModel.Title);
        Assert.NotNull(viewModel.SaveSettingsCommand);
        Assert.NotNull(viewModel.ResetSettingsCommand);
    }

    [Fact]
    public async Task Constructor_ShouldLoadDefaultSettings()
    {
        // Act
        var viewModel = new SettingsViewModel(_mockLogger.Object, _mockSettingsService.Object, _mockSettingsInitializer.Object, _mockFoundryService.Object);
        await Task.Delay(500); // Wait for async loading

        // Assert - Verify settings service was called
        _mockSettingsInitializer.Verify(x => x.AreSettingsInitializedAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        _mockSettingsService.Verify(x => x.GetSettingValueAsync<string>(
            ApplicationSettings.Paths.DataDirectory, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public void DataDirectory_WhenSet_ShouldUpdateProperty()
    {
        // Arrange
        var viewModel = new SettingsViewModel(_mockLogger.Object, _mockSettingsService.Object, _mockSettingsInitializer.Object, _mockFoundryService.Object);
        var newPath = @"C:\Test\Data";

        // Act
        viewModel.DataDirectory = newPath;

        // Assert
        Assert.Equal(newPath, viewModel.DataDirectory);
    }

    [Fact]
    public void ModelsDirectory_WhenSet_ShouldUpdateProperty()
    {
        // Arrange
        var viewModel = new SettingsViewModel(_mockLogger.Object, _mockSettingsService.Object, _mockSettingsInitializer.Object, _mockFoundryService.Object);
        var newPath = @"C:\Test\Models";

        // Act
        viewModel.ModelsDirectory = newPath;

        // Assert
        Assert.Equal(newPath, viewModel.ModelsDirectory);
    }

    [Fact]
    public void UseNpu_WhenSet_ShouldUpdateProperty()
    {
        // Arrange
        var viewModel = new SettingsViewModel(_mockLogger.Object, _mockSettingsService.Object, _mockSettingsInitializer.Object, _mockFoundryService.Object);

        // Act
        viewModel.UseNpu = false;

        // Assert
        Assert.False(viewModel.UseNpu);
    }

    [Fact]
    public void UseGpu_WhenSet_ShouldUpdateProperty()
    {
        // Arrange
        var viewModel = new SettingsViewModel(_mockLogger.Object, _mockSettingsService.Object, _mockSettingsInitializer.Object, _mockFoundryService.Object);

        // Act
        viewModel.UseGpu = false;

        // Assert
        Assert.False(viewModel.UseGpu);
    }

    [Fact]
    public void AllowOnlineProviders_WhenSet_ShouldUpdateProperty()
    {
        // Arrange
        var viewModel = new SettingsViewModel(_mockLogger.Object, _mockSettingsService.Object, _mockSettingsInitializer.Object, _mockFoundryService.Object);

        // Act
        viewModel.AllowOnlineProviders = true;

        // Assert
        Assert.True(viewModel.AllowOnlineProviders);
    }

    [Fact]
    public void TokenBudget_WhenSet_ShouldUpdateProperty()
    {
        // Arrange
        var viewModel = new SettingsViewModel(_mockLogger.Object, _mockSettingsService.Object, _mockSettingsInitializer.Object, _mockFoundryService.Object);
        var newBudget = 16384;

        // Act
        viewModel.TokenBudget = newBudget;

        // Assert
        Assert.Equal(newBudget, viewModel.TokenBudget);
    }

    [Fact]
    public void SelectedTheme_WhenSet_ShouldUpdateProperty()
    {
        // Arrange
        var viewModel = new SettingsViewModel(_mockLogger.Object, _mockSettingsService.Object, _mockSettingsInitializer.Object, _mockFoundryService.Object);
        var newTheme = "Dark";

        // Act
        viewModel.SelectedTheme = newTheme;

        // Assert
        Assert.Equal(newTheme, viewModel.SelectedTheme);
    }

    [Fact]
    public void SaveSettingsCommand_ShouldNotThrow()
    {
        // Arrange
        var viewModel = new SettingsViewModel(_mockLogger.Object, _mockSettingsService.Object, _mockSettingsInitializer.Object, _mockFoundryService.Object);

        // Act & Assert
        var exception = Record.Exception(() => viewModel.SaveSettingsCommand.Execute(null));
        Assert.Null(exception);
    }

    [Fact]
    public void ResetSettingsCommand_ShouldResetToDefaults()
    {
        // Arrange
        var viewModel = new SettingsViewModel(_mockLogger.Object, _mockSettingsService.Object, _mockSettingsInitializer.Object, _mockFoundryService.Object);
        viewModel.UseNpu = false;
        viewModel.TokenBudget = 4096;

        // Act
        viewModel.ResetSettingsCommand.Execute(null);

        // Assert - Verify reset was called
        _mockSettingsInitializer.Verify(x => x.ResetToDefaultsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void BrowseDataDirectoryCommand_ShouldNotThrow()
    {
        // Arrange
        var viewModel = new SettingsViewModel(_mockLogger.Object, _mockSettingsService.Object, _mockSettingsInitializer.Object, _mockFoundryService.Object);

        // Act & Assert
        var exception = Record.Exception(() => viewModel.BrowseDataDirectoryCommand.Execute(null));
        Assert.Null(exception);
    }

    [Fact]
    public void BrowseModelsDirectoryCommand_ShouldNotThrow()
    {
        // Arrange
        var viewModel = new SettingsViewModel(_mockLogger.Object, _mockSettingsService.Object, _mockSettingsInitializer.Object, _mockFoundryService.Object);

        // Act & Assert
        var exception = Record.Exception(() => viewModel.BrowseModelsDirectoryCommand.Execute(null));
        Assert.Null(exception);
    }
}
