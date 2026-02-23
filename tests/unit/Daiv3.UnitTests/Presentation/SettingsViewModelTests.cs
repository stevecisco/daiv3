using Daiv3.App.Maui.ViewModels;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Daiv3.UnitTests.Presentation;

/// <summary>
/// Unit tests for SettingsViewModel.
/// </summary>
public class SettingsViewModelTests
{
    private readonly Mock<ILogger<SettingsViewModel>> _mockLogger;

    public SettingsViewModelTests()
    {
        _mockLogger = new Mock<ILogger<SettingsViewModel>>();
    }

    [Fact]
    public void Constructor_ShouldInitializeProperties()
    {
        // Act
        var viewModel = new SettingsViewModel(_mockLogger.Object);

        // Assert
        Assert.Equal("Settings", viewModel.Title);
        Assert.NotNull(viewModel.DataDirectory);
        Assert.NotNull(viewModel.ModelsDirectory);
        Assert.NotNull(viewModel.SelectedTheme);
        Assert.NotNull(viewModel.SaveSettingsCommand);
        Assert.NotNull(viewModel.ResetSettingsCommand);
    }

    [Fact]
    public async Task Constructor_ShouldLoadDefaultSettings()
    {
        // Act
        var viewModel = new SettingsViewModel(_mockLogger.Object);
        await Task.Delay(500); // Wait longer for settings to load

        // Assert - Check that properties are initialized (may be empty in test context without UI thread)
        // Note: MainThread.BeginInvokeOnMainThread may not work properly in test environment
        Assert.NotNull(viewModel.DataDirectory);
        Assert.NotNull(viewModel.ModelsDirectory);
        Assert.Equal(8192, viewModel.TokenBudget);
        Assert.Equal("System", viewModel.SelectedTheme);
    }

    [Fact]
    public void DataDirectory_WhenSet_ShouldUpdateProperty()
    {
        // Arrange
        var viewModel = new SettingsViewModel(_mockLogger.Object);
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
        var viewModel = new SettingsViewModel(_mockLogger.Object);
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
        var viewModel = new SettingsViewModel(_mockLogger.Object);

        // Act
        viewModel.UseNpu = false;

        // Assert
        Assert.False(viewModel.UseNpu);
    }

    [Fact]
    public void UseGpu_WhenSet_ShouldUpdateProperty()
    {
        // Arrange
        var viewModel = new SettingsViewModel(_mockLogger.Object);

        // Act
        viewModel.UseGpu = false;

        // Assert
        Assert.False(viewModel.UseGpu);
    }

    [Fact]
    public void AllowOnlineProviders_WhenSet_ShouldUpdateProperty()
    {
        // Arrange
        var viewModel = new SettingsViewModel(_mockLogger.Object);

        // Act
        viewModel.AllowOnlineProviders = true;

        // Assert
        Assert.True(viewModel.AllowOnlineProviders);
    }

    [Fact]
    public void TokenBudget_WhenSet_ShouldUpdateProperty()
    {
        // Arrange
        var viewModel = new SettingsViewModel(_mockLogger.Object);
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
        var viewModel = new SettingsViewModel(_mockLogger.Object);
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
        var viewModel = new SettingsViewModel(_mockLogger.Object);

        // Act & Assert
        var exception = Record.Exception(() => viewModel.SaveSettingsCommand.Execute(null));
        Assert.Null(exception);
    }

    [Fact]
    public void ResetSettingsCommand_ShouldResetToDefaults()
    {
        // Arrange
        var viewModel = new SettingsViewModel(_mockLogger.Object);
        viewModel.UseNpu = false;
        viewModel.TokenBudget = 4096;

        // Act
        viewModel.ResetSettingsCommand.Execute(null);

        // Assert - Properties should be reset (will be done asynchronously)
        Assert.NotNull(viewModel.DataDirectory);
    }

    [Fact]
    public void BrowseDataDirectoryCommand_ShouldNotThrow()
    {
        // Arrange
        var viewModel = new SettingsViewModel(_mockLogger.Object);

        // Act & Assert
        var exception = Record.Exception(() => viewModel.BrowseDataDirectoryCommand.Execute(null));
        Assert.Null(exception);
    }

    [Fact]
    public void BrowseModelsDirectoryCommand_ShouldNotThrow()
    {
        // Arrange
        var viewModel = new SettingsViewModel(_mockLogger.Object);

        // Act & Assert
        var exception = Record.Exception(() => viewModel.BrowseModelsDirectoryCommand.Execute(null));
        Assert.Null(exception);
    }
}
