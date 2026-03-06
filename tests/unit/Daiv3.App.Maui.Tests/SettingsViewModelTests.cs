using Daiv3.App.Maui.ViewModels;
using Daiv3.Core.Settings;
using Daiv3.FoundryLocal.Management;
using Daiv3.Persistence;
using Daiv3.Persistence.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Daiv3.App.Maui.Tests;

/// <summary>
/// Unit tests for SettingsViewModel.
/// Verifies CT-REQ-002 settings load/save coverage across configuration domains.
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

        SetupCommonSettings();
    }

    [Fact]
    public void Constructor_ShouldInitializeProperties()
    {
        var viewModel = CreateViewModel();

        Assert.Equal("Settings", viewModel.Title);
        Assert.NotNull(viewModel.SaveSettingsCommand);
        Assert.NotNull(viewModel.ResetSettingsCommand);
    }

    [Fact]
    public async Task Constructor_ShouldLoadCtReq002Settings()
    {
        var viewModel = CreateViewModel();
        await Task.Delay(500);

        _mockSettingsService.Verify(x => x.GetSettingValueAsync<string>(
            ApplicationSettings.Paths.WatchedDirectories,
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        _mockSettingsService.Verify(x => x.GetSettingValueAsync<string>(
            ApplicationSettings.Models.FoundryLocalDefaultModel,
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        _mockSettingsService.Verify(x => x.GetSettingValueAsync<string>(
            ApplicationSettings.Providers.OnlineAccessMode,
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        _mockSettingsService.Verify(x => x.GetSettingValueAsync<bool>(
            ApplicationSettings.General.EnableAgents,
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        _mockSettingsService.Verify(x => x.GetSettingValueAsync<string>(
            ApplicationSettings.General.SkillMarketplaceUrls,
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        _mockSettingsService.Verify(x => x.GetSettingValueAsync<string>(
            ApplicationSettings.Providers.OnlineProvidersEnabled,
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task Constructor_ShouldDisableOnlineProvidersWhenModeIsNever()
    {
        _mockSettingsService.Setup(x => x.GetSettingValueAsync<string>(
                ApplicationSettings.Providers.OnlineAccessMode,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("never");

        CreateViewModel();
        await Task.Delay(500);

        _mockSettingsService.Verify(x => x.GetSettingValueAsync<string>(
            ApplicationSettings.Providers.OnlineAccessMode,
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task SaveSettingsCommand_ShouldPersistCtReq002Settings()
    {
        var viewModel = CreateViewModel();
        await Task.Delay(500);

        viewModel.WatchedDirectories = "C:\\docs\nD:\\repo";
        viewModel.KnowledgeBackPropagationPath = "D:\\daiv3\\backprop";
        viewModel.DefaultModel = "phi-4-mini";
        viewModel.ChatModel = "phi-4-chat";
        viewModel.CodeModel = "phi-4-code";
        viewModel.ReasoningModel = "phi-4-reason";
        viewModel.AllowOnlineProviders = true;
        viewModel.OnlineAccessMode = "per_task";
        viewModel.OnlineProvidersEnabled = "openai, azure_openai";
        viewModel.TokenBudget = 90000;
        viewModel.MonthlyTokenBudget = 1200000;
        viewModel.TokenBudgetAlertThreshold = 75;
        viewModel.TokenBudgetMode = "hard_stop";
        viewModel.EnableAgents = true;
        viewModel.EnableSkills = false;
        viewModel.AgentIterationLimit = 12;
        viewModel.AgentTokenBudget = 15000;
        viewModel.EnableScheduling = true;
        viewModel.SchedulerCheckInterval = 30;
        viewModel.SkillMarketplaceUrls = "https://skills1.test/skills.json\nhttps://skills2.test/skills.json";
        viewModel.SelectedTheme = "Dark";

        viewModel.SaveSettingsCommand.Execute(null);
        await Task.Delay(500);

        _mockSettingsService.Verify(x => x.SaveSettingAsync(
            ApplicationSettings.Paths.WatchedDirectories,
            It.Is<object>(v => v.ToString() == "[\"C:\\\\docs\",\"D:\\\\repo\"]"),
            ApplicationSettings.Categories.Paths,
            ApplicationSettings.Descriptions.WatchedDirectories,
            false,
            "user_ui_change",
            It.IsAny<CancellationToken>()), Times.Once);

        _mockSettingsService.Verify(x => x.SaveSettingAsync(
            ApplicationSettings.Models.FoundryLocalDefaultModel,
            "phi-4-mini",
            ApplicationSettings.Categories.Models,
            ApplicationSettings.Descriptions.FoundryLocalDefaultModel,
            false,
            "user_ui_change",
            It.IsAny<CancellationToken>()), Times.Once);

        _mockSettingsService.Verify(x => x.SaveSettingAsync(
            ApplicationSettings.Providers.OnlineAccessMode,
            "per_task",
            ApplicationSettings.Categories.Providers,
            ApplicationSettings.Descriptions.OnlineAccessMode,
            false,
            "user_ui_change",
            It.IsAny<CancellationToken>()), Times.Once);

        _mockSettingsService.Verify(x => x.SaveSettingAsync(
            ApplicationSettings.General.EnableAgents,
            true,
            ApplicationSettings.Categories.General,
            ApplicationSettings.Descriptions.EnableAgents,
            false,
            "user_ui_change",
            It.IsAny<CancellationToken>()), Times.Once);

        _mockSettingsService.Verify(x => x.SaveSettingAsync(
            ApplicationSettings.General.EnableScheduling,
            true,
            ApplicationSettings.Categories.General,
            ApplicationSettings.Descriptions.EnableScheduling,
            false,
            "user_ui_change",
            It.IsAny<CancellationToken>()), Times.Once);

        _mockSettingsService.Verify(x => x.SaveSettingAsync(
            ApplicationSettings.General.SkillMarketplaceUrls,
            It.Is<object>(v => v.ToString() == "[\"https://skills1.test/skills.json\",\"https://skills2.test/skills.json\"]"),
            ApplicationSettings.Categories.General,
            ApplicationSettings.Descriptions.SkillMarketplaceUrls,
            false,
            "user_ui_change",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SaveSettingsCommand_ShouldForceNeverOnlineMode_WhenOnlineDisabled()
    {
        var viewModel = CreateViewModel();
        await Task.Delay(500);

        viewModel.AllowOnlineProviders = false;
        viewModel.OnlineAccessMode = "per_task";

        viewModel.SaveSettingsCommand.Execute(null);
        await Task.Delay(500);

        _mockSettingsService.Verify(x => x.SaveSettingAsync(
            ApplicationSettings.Providers.OnlineAccessMode,
            "never",
            ApplicationSettings.Categories.Providers,
            ApplicationSettings.Descriptions.OnlineAccessMode,
            false,
            "user_ui_change",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void ResetSettingsCommand_ShouldResetToDefaults()
    {
        var viewModel = CreateViewModel();

        viewModel.ResetSettingsCommand.Execute(null);

        _mockSettingsInitializer.Verify(x => x.ResetToDefaultsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void BrowseCommands_ShouldNotThrow()
    {
        var viewModel = CreateViewModel();

        var browseDataException = Record.Exception(() => viewModel.BrowseDataDirectoryCommand.Execute(null));
        var browseModelsException = Record.Exception(() => viewModel.BrowseModelsDirectoryCommand.Execute(null));

        Assert.Null(browseDataException);
        Assert.Null(browseModelsException);
    }

    private SettingsViewModel CreateViewModel()
    {
        return new SettingsViewModel(
            _mockLogger.Object,
            _mockSettingsService.Object,
            _mockSettingsInitializer.Object,
            _mockFoundryService.Object);
    }

    private void SetupCommonSettings()
    {
        _mockSettingsInitializer.Setup(x => x.AreSettingsInitializedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockSettingsService.Setup(x => x.GetSettingValueAsync<string>(ApplicationSettings.Paths.DataDirectory, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApplicationSettings.Defaults.DataDirectory);
        _mockSettingsService.Setup(x => x.GetSettingValueAsync<string>(ApplicationSettings.Paths.WatchedDirectories, It.IsAny<CancellationToken>()))
            .ReturnsAsync("[\"C:\\\\docs\",\"D:\\\\repo\"]");
        _mockSettingsService.Setup(x => x.GetSettingValueAsync<string>(ApplicationSettings.Paths.KnowledgeBackPropagationPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync("D:\\daiv3\\backprop");

        _mockSettingsService.Setup(x => x.GetSettingValueAsync<bool>(ApplicationSettings.Hardware.DisableNpu, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _mockSettingsService.Setup(x => x.GetSettingValueAsync<bool>(ApplicationSettings.Hardware.DisableGpu, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _mockSettingsService.Setup(x => x.GetSettingValueAsync<string>(ApplicationSettings.Models.FoundryLocalDefaultModel, It.IsAny<CancellationToken>()))
            .ReturnsAsync("phi-3-mini");
        _mockSettingsService.Setup(x => x.GetSettingValueAsync<string>(ApplicationSettings.Models.FoundryLocalChatModel, It.IsAny<CancellationToken>()))
            .ReturnsAsync("phi-3-mini");
        _mockSettingsService.Setup(x => x.GetSettingValueAsync<string>(ApplicationSettings.Models.FoundryLocalCodeModel, It.IsAny<CancellationToken>()))
            .ReturnsAsync("phi-3-mini");
        _mockSettingsService.Setup(x => x.GetSettingValueAsync<string>(ApplicationSettings.Models.FoundryLocalReasoningModel, It.IsAny<CancellationToken>()))
            .ReturnsAsync("phi-3-mini");

        _mockSettingsService.Setup(x => x.GetSettingValueAsync<string>(ApplicationSettings.Providers.OnlineAccessMode, It.IsAny<CancellationToken>()))
            .ReturnsAsync("ask");
        _mockSettingsService.Setup(x => x.GetSettingValueAsync<string>(ApplicationSettings.Providers.OnlineProvidersEnabled, It.IsAny<CancellationToken>()))
            .ReturnsAsync("[\"openai\",\"azure_openai\"]");
        _mockSettingsService.Setup(x => x.GetSettingValueAsync<int>(ApplicationSettings.Providers.DailyTokenBudget, It.IsAny<CancellationToken>()))
            .ReturnsAsync(50000);
        _mockSettingsService.Setup(x => x.GetSettingValueAsync<int>(ApplicationSettings.Providers.MonthlyTokenBudget, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1000000);
        _mockSettingsService.Setup(x => x.GetSettingValueAsync<int>(ApplicationSettings.Providers.TokenBudgetAlertThreshold, It.IsAny<CancellationToken>()))
            .ReturnsAsync(80);
        _mockSettingsService.Setup(x => x.GetSettingValueAsync<string>(ApplicationSettings.Providers.TokenBudgetMode, It.IsAny<CancellationToken>()))
            .ReturnsAsync("user_confirm");

        _mockSettingsService.Setup(x => x.GetSettingValueAsync<bool>(ApplicationSettings.General.EnableAgents, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockSettingsService.Setup(x => x.GetSettingValueAsync<bool>(ApplicationSettings.General.EnableSkills, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockSettingsService.Setup(x => x.GetSettingValueAsync<int>(ApplicationSettings.General.AgentIterationLimit, It.IsAny<CancellationToken>()))
            .ReturnsAsync(10);
        _mockSettingsService.Setup(x => x.GetSettingValueAsync<int>(ApplicationSettings.General.AgentTokenBudget, It.IsAny<CancellationToken>()))
            .ReturnsAsync(10000);
        _mockSettingsService.Setup(x => x.GetSettingValueAsync<bool>(ApplicationSettings.General.EnableScheduling, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockSettingsService.Setup(x => x.GetSettingValueAsync<int>(ApplicationSettings.General.SchedulerCheckInterval, It.IsAny<CancellationToken>()))
            .ReturnsAsync(60);
        _mockSettingsService.Setup(x => x.GetSettingValueAsync<string>(ApplicationSettings.General.SkillMarketplaceUrls, It.IsAny<CancellationToken>()))
            .ReturnsAsync("[\"https://skills1.test/skills.json\"]");

        _mockSettingsService.Setup(x => x.GetSettingValueAsync<string>(ApplicationSettings.UI.Theme, It.IsAny<CancellationToken>()))
            .ReturnsAsync("system");

        _mockSettingsService.Setup(x => x.SaveSettingAsync(
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<bool>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid().ToString());

        _mockFoundryService.Setup(x => x.GetModelsDirectoryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("(Not configured)");
    }
}
