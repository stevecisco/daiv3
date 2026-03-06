using System.Text.Json;
using System.Windows.Input;
using Daiv3.Core.Settings;
using Daiv3.FoundryLocal.Management;
using Daiv3.Persistence;
using Daiv3.Persistence.Services;
using Microsoft.Extensions.Logging;

namespace Daiv3.App.Maui.ViewModels;

/// <summary>
/// ViewModel for the Settings interface.
/// Manages user preferences, directories, model settings, and system configuration.
/// Implements CT-REQ-001 and CT-REQ-002 settings persistence and UI integration.
/// </summary>
public class SettingsViewModel : BaseViewModel
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    private readonly ILogger<SettingsViewModel> _logger;
    private readonly ISettingsService _settingsService;
    private readonly ISettingsInitializer _settingsInitializer;
    private readonly IFoundryLocalManagementService _foundryService;

    private string _dataDirectory = string.Empty;
    private string _modelsDirectory = string.Empty;
    private string _foundryModelsDirectory = string.Empty;
    private string _watchedDirectories = string.Empty;
    private string _knowledgeBackPropagationPath = string.Empty;

    private bool _useNpu = true;
    private bool _useGpu = true;

    private string _defaultModel = ApplicationSettings.Defaults.FoundryLocalDefaultModel;
    private string _chatModel = ApplicationSettings.Defaults.FoundryLocalChatModel;
    private string _codeModel = ApplicationSettings.Defaults.FoundryLocalCodeModel;
    private string _reasoningModel = ApplicationSettings.Defaults.FoundryLocalReasoningModel;

    private bool _allowOnlineProviders;
    private string _onlineAccessMode = ApplicationSettings.Defaults.OnlineAccessMode;
    private string _onlineProvidersEnabled = string.Empty;
    private int _tokenBudget = ApplicationSettings.Defaults.DailyTokenBudget;
    private int _monthlyTokenBudget = ApplicationSettings.Defaults.MonthlyTokenBudget;
    private int _tokenBudgetAlertThreshold = ApplicationSettings.Defaults.TokenBudgetAlertThreshold;
    private string _tokenBudgetMode = ApplicationSettings.Defaults.TokenBudgetMode;

    private bool _enableAgents = ApplicationSettings.Defaults.EnableAgents;
    private bool _enableSkills = ApplicationSettings.Defaults.EnableSkills;
    private int _agentIterationLimit = ApplicationSettings.Defaults.AgentIterationLimit;
    private int _agentTokenBudget = ApplicationSettings.Defaults.AgentTokenBudget;
    private bool _enableScheduling = ApplicationSettings.Defaults.EnableScheduling;
    private int _schedulerCheckInterval = ApplicationSettings.Defaults.SchedulerCheckInterval;
    private string _skillMarketplaceUrls = string.Empty;

    private string _selectedTheme = "System";

    public SettingsViewModel(
        ILogger<SettingsViewModel> logger,
        ISettingsService settingsService,
        ISettingsInitializer settingsInitializer,
        IFoundryLocalManagementService foundryService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _settingsInitializer = settingsInitializer ?? throw new ArgumentNullException(nameof(settingsInitializer));
        _foundryService = foundryService ?? throw new ArgumentNullException(nameof(foundryService));

        Title = "Settings";
        SaveSettingsCommand = new Command(OnSaveSettings);
        ResetSettingsCommand = new Command(OnResetSettings);
        BrowseDataDirectoryCommand = new Command(OnBrowseDataDirectory);
        BrowseModelsDirectoryCommand = new Command(OnBrowseModelsDirectory);

        _logger.LogInformation("SettingsViewModel initialized");
        LoadSettings();
    }

    #region Properties

    public string DataDirectory
    {
        get => _dataDirectory;
        set => SetProperty(ref _dataDirectory, value);
    }

    public string ModelsDirectory
    {
        get => _modelsDirectory;
        set => SetProperty(ref _modelsDirectory, value);
    }

    public string FoundryModelsDirectory
    {
        get => _foundryModelsDirectory;
        private set => SetProperty(ref _foundryModelsDirectory, value);
    }

    public string WatchedDirectories
    {
        get => _watchedDirectories;
        set => SetProperty(ref _watchedDirectories, value);
    }

    public string KnowledgeBackPropagationPath
    {
        get => _knowledgeBackPropagationPath;
        set => SetProperty(ref _knowledgeBackPropagationPath, value);
    }

    public bool UseNpu
    {
        get => _useNpu;
        set => SetProperty(ref _useNpu, value);
    }

    public bool UseGpu
    {
        get => _useGpu;
        set => SetProperty(ref _useGpu, value);
    }

    public string DefaultModel
    {
        get => _defaultModel;
        set => SetProperty(ref _defaultModel, value);
    }

    public string ChatModel
    {
        get => _chatModel;
        set => SetProperty(ref _chatModel, value);
    }

    public string CodeModel
    {
        get => _codeModel;
        set => SetProperty(ref _codeModel, value);
    }

    public string ReasoningModel
    {
        get => _reasoningModel;
        set => SetProperty(ref _reasoningModel, value);
    }

    public bool AllowOnlineProviders
    {
        get => _allowOnlineProviders;
        set => SetProperty(ref _allowOnlineProviders, value);
    }

    public string OnlineAccessMode
    {
        get => _onlineAccessMode;
        set => SetProperty(ref _onlineAccessMode, value);
    }

    public string OnlineProvidersEnabled
    {
        get => _onlineProvidersEnabled;
        set => SetProperty(ref _onlineProvidersEnabled, value);
    }

    public int TokenBudget
    {
        get => _tokenBudget;
        set => SetProperty(ref _tokenBudget, value);
    }

    public int MonthlyTokenBudget
    {
        get => _monthlyTokenBudget;
        set => SetProperty(ref _monthlyTokenBudget, value);
    }

    public int TokenBudgetAlertThreshold
    {
        get => _tokenBudgetAlertThreshold;
        set => SetProperty(ref _tokenBudgetAlertThreshold, value);
    }

    public string TokenBudgetMode
    {
        get => _tokenBudgetMode;
        set => SetProperty(ref _tokenBudgetMode, value);
    }

    public bool EnableAgents
    {
        get => _enableAgents;
        set => SetProperty(ref _enableAgents, value);
    }

    public bool EnableSkills
    {
        get => _enableSkills;
        set => SetProperty(ref _enableSkills, value);
    }

    public int AgentIterationLimit
    {
        get => _agentIterationLimit;
        set => SetProperty(ref _agentIterationLimit, value);
    }

    public int AgentTokenBudget
    {
        get => _agentTokenBudget;
        set => SetProperty(ref _agentTokenBudget, value);
    }

    public bool EnableScheduling
    {
        get => _enableScheduling;
        set => SetProperty(ref _enableScheduling, value);
    }

    public int SchedulerCheckInterval
    {
        get => _schedulerCheckInterval;
        set => SetProperty(ref _schedulerCheckInterval, value);
    }

    public string SkillMarketplaceUrls
    {
        get => _skillMarketplaceUrls;
        set => SetProperty(ref _skillMarketplaceUrls, value);
    }

    public string SelectedTheme
    {
        get => _selectedTheme;
        set => SetProperty(ref _selectedTheme, value);
    }

    #endregion

    public ICommand SaveSettingsCommand { get; }
    public ICommand ResetSettingsCommand { get; }
    public ICommand BrowseDataDirectoryCommand { get; }
    public ICommand BrowseModelsDirectoryCommand { get; }

    private void LoadSettings()
    {
        IsBusy = true;

        Task.Run(async () =>
        {
            try
            {
                var areInitialized = await _settingsInitializer.AreSettingsInitializedAsync().ConfigureAwait(false);
                if (!areInitialized)
                {
                    _logger.LogInformation("Initializing settings for first run");
                    await _settingsInitializer.InitializeDefaultSettingsAsync().ConfigureAwait(false);
                }

                var dataDir = await _settingsService.GetSettingValueAsync<string>(ApplicationSettings.Paths.DataDirectory).ConfigureAwait(false);
                var watchedDirectories = await _settingsService.GetSettingValueAsync<string>(ApplicationSettings.Paths.WatchedDirectories).ConfigureAwait(false);
                var knowledgeBackPropagationPath = await _settingsService.GetSettingValueAsync<string>(ApplicationSettings.Paths.KnowledgeBackPropagationPath).ConfigureAwait(false);

                var useNpu = await _settingsService.GetSettingValueAsync<bool>(ApplicationSettings.Hardware.DisableNpu).ConfigureAwait(false);
                var useGpu = await _settingsService.GetSettingValueAsync<bool>(ApplicationSettings.Hardware.DisableGpu).ConfigureAwait(false);

                var defaultModel = await _settingsService.GetSettingValueAsync<string>(ApplicationSettings.Models.FoundryLocalDefaultModel).ConfigureAwait(false);
                var chatModel = await _settingsService.GetSettingValueAsync<string>(ApplicationSettings.Models.FoundryLocalChatModel).ConfigureAwait(false);
                var codeModel = await _settingsService.GetSettingValueAsync<string>(ApplicationSettings.Models.FoundryLocalCodeModel).ConfigureAwait(false);
                var reasoningModel = await _settingsService.GetSettingValueAsync<string>(ApplicationSettings.Models.FoundryLocalReasoningModel).ConfigureAwait(false);

                var onlineAccessMode = await _settingsService.GetSettingValueAsync<string>(ApplicationSettings.Providers.OnlineAccessMode).ConfigureAwait(false);
                var onlineProvidersEnabled = await _settingsService.GetSettingValueAsync<string>(ApplicationSettings.Providers.OnlineProvidersEnabled).ConfigureAwait(false);
                var dailyBudget = await _settingsService.GetSettingValueAsync<int>(ApplicationSettings.Providers.DailyTokenBudget).ConfigureAwait(false);
                var monthlyBudget = await _settingsService.GetSettingValueAsync<int>(ApplicationSettings.Providers.MonthlyTokenBudget).ConfigureAwait(false);
                var alertThreshold = await _settingsService.GetSettingValueAsync<int>(ApplicationSettings.Providers.TokenBudgetAlertThreshold).ConfigureAwait(false);
                var budgetMode = await _settingsService.GetSettingValueAsync<string>(ApplicationSettings.Providers.TokenBudgetMode).ConfigureAwait(false);

                var enableAgents = await _settingsService.GetSettingValueAsync<bool>(ApplicationSettings.General.EnableAgents).ConfigureAwait(false);
                var enableSkills = await _settingsService.GetSettingValueAsync<bool>(ApplicationSettings.General.EnableSkills).ConfigureAwait(false);
                var agentIterationLimit = await _settingsService.GetSettingValueAsync<int>(ApplicationSettings.General.AgentIterationLimit).ConfigureAwait(false);
                var agentTokenBudget = await _settingsService.GetSettingValueAsync<int>(ApplicationSettings.General.AgentTokenBudget).ConfigureAwait(false);
                var enableScheduling = await _settingsService.GetSettingValueAsync<bool>(ApplicationSettings.General.EnableScheduling).ConfigureAwait(false);
                var schedulerCheckInterval = await _settingsService.GetSettingValueAsync<int>(ApplicationSettings.General.SchedulerCheckInterval).ConfigureAwait(false);
                var skillMarketplaceUrls = await _settingsService.GetSettingValueAsync<string>(ApplicationSettings.General.SkillMarketplaceUrls).ConfigureAwait(false);

                var theme = await _settingsService.GetSettingValueAsync<string>(ApplicationSettings.UI.Theme).ConfigureAwait(false);

                var foundryModelsDir = await _foundryService.GetModelsDirectoryAsync(CancellationToken.None).ConfigureAwait(false)
                    ?? "(Not configured)";

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    DataDirectory = string.IsNullOrWhiteSpace(dataDir)
                        ? ApplicationSettings.Defaults.DataDirectory
                        : dataDir;
                    ModelsDirectory = Path.Combine(DataDirectory, "models");
                    FoundryModelsDirectory = foundryModelsDir;

                    WatchedDirectories = ConvertJsonArrayToMultiline(
                        string.IsNullOrWhiteSpace(watchedDirectories)
                            ? ApplicationSettings.Defaults.WatchedDirectories
                            : watchedDirectories);
                    KnowledgeBackPropagationPath = string.IsNullOrWhiteSpace(knowledgeBackPropagationPath)
                        ? ApplicationSettings.Defaults.KnowledgeBackPropagationPath
                        : knowledgeBackPropagationPath;

                    UseNpu = !useNpu;
                    UseGpu = !useGpu;

                    DefaultModel = string.IsNullOrWhiteSpace(defaultModel)
                        ? ApplicationSettings.Defaults.FoundryLocalDefaultModel
                        : defaultModel;
                    ChatModel = string.IsNullOrWhiteSpace(chatModel)
                        ? ApplicationSettings.Defaults.FoundryLocalChatModel
                        : chatModel;
                    CodeModel = string.IsNullOrWhiteSpace(codeModel)
                        ? ApplicationSettings.Defaults.FoundryLocalCodeModel
                        : codeModel;
                    ReasoningModel = string.IsNullOrWhiteSpace(reasoningModel)
                        ? ApplicationSettings.Defaults.FoundryLocalReasoningModel
                        : reasoningModel;

                    OnlineAccessMode = string.IsNullOrWhiteSpace(onlineAccessMode)
                        ? ApplicationSettings.Defaults.OnlineAccessMode
                        : onlineAccessMode;
                    OnlineProvidersEnabled = ConvertJsonArrayToCommaSeparated(
                        string.IsNullOrWhiteSpace(onlineProvidersEnabled)
                            ? ApplicationSettings.Defaults.OnlineProvidersEnabled
                            : onlineProvidersEnabled);
                    AllowOnlineProviders = !string.Equals(OnlineAccessMode, "never", StringComparison.OrdinalIgnoreCase);
                    TokenBudget = dailyBudget > 0 ? dailyBudget : ApplicationSettings.Defaults.DailyTokenBudget;
                    MonthlyTokenBudget = monthlyBudget > 0 ? monthlyBudget : ApplicationSettings.Defaults.MonthlyTokenBudget;
                    TokenBudgetAlertThreshold = alertThreshold > 0 ? alertThreshold : ApplicationSettings.Defaults.TokenBudgetAlertThreshold;
                    TokenBudgetMode = string.IsNullOrWhiteSpace(budgetMode)
                        ? ApplicationSettings.Defaults.TokenBudgetMode
                        : budgetMode;

                    EnableAgents = enableAgents;
                    EnableSkills = enableSkills;
                    AgentIterationLimit = agentIterationLimit > 0 ? agentIterationLimit : ApplicationSettings.Defaults.AgentIterationLimit;
                    AgentTokenBudget = agentTokenBudget > 0 ? agentTokenBudget : ApplicationSettings.Defaults.AgentTokenBudget;
                    EnableScheduling = enableScheduling;
                    SchedulerCheckInterval = schedulerCheckInterval > 0 ? schedulerCheckInterval : ApplicationSettings.Defaults.SchedulerCheckInterval;
                    SkillMarketplaceUrls = ConvertJsonArrayToMultiline(
                        string.IsNullOrWhiteSpace(skillMarketplaceUrls)
                            ? ApplicationSettings.Defaults.SkillMarketplaceUrls
                            : skillMarketplaceUrls);

                    SelectedTheme = NormalizeThemeForPicker(theme);

                    IsBusy = false;
                    _logger.LogInformation("Settings loaded successfully");
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load settings");

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    DataDirectory = ApplicationSettings.Defaults.DataDirectory;
                    ModelsDirectory = Path.Combine(DataDirectory, "models");
                    FoundryModelsDirectory = "(Not configured)";
                    WatchedDirectories = string.Empty;
                    KnowledgeBackPropagationPath = ApplicationSettings.Defaults.KnowledgeBackPropagationPath;

                    UseNpu = !ApplicationSettings.Defaults.DisableNpu;
                    UseGpu = !ApplicationSettings.Defaults.DisableGpu;

                    DefaultModel = ApplicationSettings.Defaults.FoundryLocalDefaultModel;
                    ChatModel = ApplicationSettings.Defaults.FoundryLocalChatModel;
                    CodeModel = ApplicationSettings.Defaults.FoundryLocalCodeModel;
                    ReasoningModel = ApplicationSettings.Defaults.FoundryLocalReasoningModel;

                    OnlineAccessMode = ApplicationSettings.Defaults.OnlineAccessMode;
                    OnlineProvidersEnabled = string.Empty;
                    AllowOnlineProviders = true;
                    TokenBudget = ApplicationSettings.Defaults.DailyTokenBudget;
                    MonthlyTokenBudget = ApplicationSettings.Defaults.MonthlyTokenBudget;
                    TokenBudgetAlertThreshold = ApplicationSettings.Defaults.TokenBudgetAlertThreshold;
                    TokenBudgetMode = ApplicationSettings.Defaults.TokenBudgetMode;

                    EnableAgents = ApplicationSettings.Defaults.EnableAgents;
                    EnableSkills = ApplicationSettings.Defaults.EnableSkills;
                    AgentIterationLimit = ApplicationSettings.Defaults.AgentIterationLimit;
                    AgentTokenBudget = ApplicationSettings.Defaults.AgentTokenBudget;
                    EnableScheduling = ApplicationSettings.Defaults.EnableScheduling;
                    SchedulerCheckInterval = ApplicationSettings.Defaults.SchedulerCheckInterval;
                    SkillMarketplaceUrls = string.Empty;

                    SelectedTheme = NormalizeThemeForPicker(ApplicationSettings.Defaults.Theme);
                    IsBusy = false;
                });
            }
        });
    }

    private async void OnSaveSettings()
    {
        IsBusy = true;
        _logger.LogInformation("Saving settings...");

        try
        {
            var effectiveOnlineAccessMode = AllowOnlineProviders
                ? (string.IsNullOrWhiteSpace(OnlineAccessMode) ? ApplicationSettings.Defaults.OnlineAccessMode : OnlineAccessMode)
                : "never";

            await _settingsService.SaveSettingAsync(
                ApplicationSettings.Paths.DataDirectory,
                DataDirectory,
                ApplicationSettings.Categories.Paths,
                ApplicationSettings.Descriptions.DataDirectory,
                reason: "user_ui_change").ConfigureAwait(false);

            await _settingsService.SaveSettingAsync(
                ApplicationSettings.Paths.WatchedDirectories,
                ConvertLinesToJsonArray(WatchedDirectories),
                ApplicationSettings.Categories.Paths,
                ApplicationSettings.Descriptions.WatchedDirectories,
                reason: "user_ui_change").ConfigureAwait(false);

            await _settingsService.SaveSettingAsync(
                ApplicationSettings.Paths.KnowledgeBackPropagationPath,
                KnowledgeBackPropagationPath,
                ApplicationSettings.Categories.Paths,
                ApplicationSettings.Descriptions.KnowledgeBackPropagationPath,
                reason: "user_ui_change").ConfigureAwait(false);

            await _settingsService.SaveSettingAsync(
                ApplicationSettings.Models.FoundryLocalDefaultModel,
                DefaultModel,
                ApplicationSettings.Categories.Models,
                ApplicationSettings.Descriptions.FoundryLocalDefaultModel,
                reason: "user_ui_change").ConfigureAwait(false);

            await _settingsService.SaveSettingAsync(
                ApplicationSettings.Models.FoundryLocalChatModel,
                ChatModel,
                ApplicationSettings.Categories.Models,
                ApplicationSettings.Descriptions.FoundryLocalChatModel,
                reason: "user_ui_change").ConfigureAwait(false);

            await _settingsService.SaveSettingAsync(
                ApplicationSettings.Models.FoundryLocalCodeModel,
                CodeModel,
                ApplicationSettings.Categories.Models,
                ApplicationSettings.Descriptions.FoundryLocalCodeModel,
                reason: "user_ui_change").ConfigureAwait(false);

            await _settingsService.SaveSettingAsync(
                ApplicationSettings.Models.FoundryLocalReasoningModel,
                ReasoningModel,
                ApplicationSettings.Categories.Models,
                ApplicationSettings.Descriptions.FoundryLocalReasoningModel,
                reason: "user_ui_change").ConfigureAwait(false);

            await _settingsService.SaveSettingAsync(
                ApplicationSettings.Hardware.DisableNpu,
                !UseNpu,
                ApplicationSettings.Categories.Hardware,
                ApplicationSettings.Descriptions.DisableNpu,
                reason: "user_ui_change").ConfigureAwait(false);

            await _settingsService.SaveSettingAsync(
                ApplicationSettings.Hardware.DisableGpu,
                !UseGpu,
                ApplicationSettings.Categories.Hardware,
                ApplicationSettings.Descriptions.DisableGpu,
                reason: "user_ui_change").ConfigureAwait(false);

            await _settingsService.SaveSettingAsync(
                ApplicationSettings.Providers.OnlineAccessMode,
                effectiveOnlineAccessMode,
                ApplicationSettings.Categories.Providers,
                ApplicationSettings.Descriptions.OnlineAccessMode,
                reason: "user_ui_change").ConfigureAwait(false);

            await _settingsService.SaveSettingAsync(
                ApplicationSettings.Providers.OnlineProvidersEnabled,
                ConvertCommaSeparatedToJsonArray(OnlineProvidersEnabled),
                ApplicationSettings.Categories.Providers,
                ApplicationSettings.Descriptions.OnlineProvidersEnabled,
                reason: "user_ui_change").ConfigureAwait(false);

            await _settingsService.SaveSettingAsync(
                ApplicationSettings.Providers.DailyTokenBudget,
                TokenBudget,
                ApplicationSettings.Categories.Providers,
                ApplicationSettings.Descriptions.DailyTokenBudget,
                reason: "user_ui_change").ConfigureAwait(false);

            await _settingsService.SaveSettingAsync(
                ApplicationSettings.Providers.MonthlyTokenBudget,
                MonthlyTokenBudget,
                ApplicationSettings.Categories.Providers,
                ApplicationSettings.Descriptions.MonthlyTokenBudget,
                reason: "user_ui_change").ConfigureAwait(false);

            await _settingsService.SaveSettingAsync(
                ApplicationSettings.Providers.TokenBudgetAlertThreshold,
                TokenBudgetAlertThreshold,
                ApplicationSettings.Categories.Providers,
                ApplicationSettings.Descriptions.TokenBudgetAlertThreshold,
                reason: "user_ui_change").ConfigureAwait(false);

            await _settingsService.SaveSettingAsync(
                ApplicationSettings.Providers.TokenBudgetMode,
                TokenBudgetMode,
                ApplicationSettings.Categories.Providers,
                ApplicationSettings.Descriptions.TokenBudgetMode,
                reason: "user_ui_change").ConfigureAwait(false);

            await _settingsService.SaveSettingAsync(
                ApplicationSettings.General.EnableAgents,
                EnableAgents,
                ApplicationSettings.Categories.General,
                ApplicationSettings.Descriptions.EnableAgents,
                reason: "user_ui_change").ConfigureAwait(false);

            await _settingsService.SaveSettingAsync(
                ApplicationSettings.General.EnableSkills,
                EnableSkills,
                ApplicationSettings.Categories.General,
                ApplicationSettings.Descriptions.EnableSkills,
                reason: "user_ui_change").ConfigureAwait(false);

            await _settingsService.SaveSettingAsync(
                ApplicationSettings.General.AgentIterationLimit,
                AgentIterationLimit,
                ApplicationSettings.Categories.General,
                ApplicationSettings.Descriptions.AgentIterationLimit,
                reason: "user_ui_change").ConfigureAwait(false);

            await _settingsService.SaveSettingAsync(
                ApplicationSettings.General.AgentTokenBudget,
                AgentTokenBudget,
                ApplicationSettings.Categories.General,
                ApplicationSettings.Descriptions.AgentTokenBudget,
                reason: "user_ui_change").ConfigureAwait(false);

            await _settingsService.SaveSettingAsync(
                ApplicationSettings.General.EnableScheduling,
                EnableScheduling,
                ApplicationSettings.Categories.General,
                ApplicationSettings.Descriptions.EnableScheduling,
                reason: "user_ui_change").ConfigureAwait(false);

            await _settingsService.SaveSettingAsync(
                ApplicationSettings.General.SchedulerCheckInterval,
                SchedulerCheckInterval,
                ApplicationSettings.Categories.General,
                ApplicationSettings.Descriptions.SchedulerCheckInterval,
                reason: "user_ui_change").ConfigureAwait(false);

            await _settingsService.SaveSettingAsync(
                ApplicationSettings.General.SkillMarketplaceUrls,
                ConvertLinesToJsonArray(SkillMarketplaceUrls),
                ApplicationSettings.Categories.General,
                ApplicationSettings.Descriptions.SkillMarketplaceUrls,
                reason: "user_ui_change").ConfigureAwait(false);

            await _settingsService.SaveSettingAsync(
                ApplicationSettings.UI.Theme,
                NormalizeThemeForStorage(SelectedTheme),
                ApplicationSettings.Categories.UI,
                ApplicationSettings.Descriptions.Theme,
                reason: "user_ui_change").ConfigureAwait(false);

            IsBusy = false;
            _logger.LogInformation("Settings saved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings");
            IsBusy = false;
        }
    }

    private async void OnResetSettings()
    {
        try
        {
            _logger.LogInformation("Resetting settings to defaults");
            IsBusy = true;

            await _settingsInitializer.ResetToDefaultsAsync().ConfigureAwait(false);

            LoadSettings();

            _logger.LogInformation("Settings reset successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset settings");
            IsBusy = false;
        }
    }

    private void OnBrowseDataDirectory()
    {
        _logger.LogInformation("Browse data directory requested");
    }

    private void OnBrowseModelsDirectory()
    {
        _logger.LogInformation("Browse models directory requested");
    }

    private static string ConvertLinesToJsonArray(string multilineValue)
    {
        var values = multilineValue
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(v => v.Trim())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return JsonSerializer.Serialize(values, JsonOptions);
    }

    private static string ConvertCommaSeparatedToJsonArray(string commaSeparated)
    {
        var values = commaSeparated
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(v => v.Trim())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return JsonSerializer.Serialize(values, JsonOptions);
    }

    private static string ConvertJsonArrayToMultiline(string json)
    {
        try
        {
            var values = JsonSerializer.Deserialize<string[]>(json) ?? [];
            return string.Join(Environment.NewLine, values);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string ConvertJsonArrayToCommaSeparated(string json)
    {
        try
        {
            var values = JsonSerializer.Deserialize<string[]>(json) ?? [];
            return string.Join(", ", values);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string NormalizeThemeForPicker(string? theme)
    {
        return theme?.ToLowerInvariant() switch
        {
            "light" => "Light",
            "dark" => "Dark",
            _ => "System"
        };
    }

    private static string NormalizeThemeForStorage(string? theme)
    {
        return theme?.ToLowerInvariant() switch
        {
            "light" => "light",
            "dark" => "dark",
            _ => "system"
        };
    }
}
