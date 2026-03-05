using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Daiv3.Persistence;
using Daiv3.Persistence.Services;
using Daiv3.Core.Settings;
using Daiv3.ModelExecution;
using Daiv3.FoundryLocal.Management;

namespace Daiv3.App.Maui.ViewModels;

/// <summary>
/// ViewModel for the Settings interface.
/// Manages user preferences, directories, model settings, and system configuration.
/// Implements CT-REQ-001: The system SHALL store all settings locally.
/// </summary>
public class SettingsViewModel : BaseViewModel
{
    private readonly ILogger<SettingsViewModel> _logger;
    private readonly ISettingsService _settingsService;
    private readonly ISettingsInitializer _settingsInitializer;
    private readonly IFoundryLocalManagementService _foundryService;
    
    private string _dataDirectory = string.Empty;
    private string _modelsDirectory = string.Empty;
    private string _foundryModelsDirectory = string.Empty;
    private bool _useNpu = true;
    private bool _useGpu = true;
    private bool _allowOnlineProviders = false;
    private int _tokenBudget = 8192;
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

    /// <summary>
    /// Gets or sets the data/database directory path.
    /// </summary>
    public string DataDirectory
    {
        get => _dataDirectory;
        set => SetProperty(ref _dataDirectory, value);
    }

    /// <summary>
    /// Gets or sets the embedding models directory path.
    /// </summary>
    public string ModelsDirectory
    {
        get => _modelsDirectory;
        set => SetProperty(ref _modelsDirectory, value);
    }

    /// <summary>
    /// Gets the Foundry models directory path (read-only).
    /// </summary>
    public string FoundryModelsDirectory
    {
        get => _foundryModelsDirectory;
        private set => SetProperty(ref _foundryModelsDirectory, value);
    }

    /// <summary>
    /// Gets or sets whether NPU usage is enabled.
    /// </summary>
    public bool UseNpu
    {
        get => _useNpu;
        set => SetProperty(ref _useNpu, value);
    }

    /// <summary>
    /// Gets or sets whether GPU usage is enabled.
    /// </summary>
    public bool UseGpu
    {
        get => _useGpu;
        set => SetProperty(ref _useGpu, value);
    }

    /// <summary>
    /// Gets or sets whether online providers are allowed.
    /// </summary>
    public bool AllowOnlineProviders
    {
        get => _allowOnlineProviders;
        set => SetProperty(ref _allowOnlineProviders, value);
    }

    /// <summary>
    /// Gets or sets the token budget for model execution.
    /// </summary>
    public int TokenBudget
    {
        get => _tokenBudget;
        set => SetProperty(ref _tokenBudget, value);
    }

    /// <summary>
    /// Gets or sets the selected theme (Light, Dark, System).
    /// </summary>
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
                // Check if settings are initialized, if not, initialize them
                var areInitialized = await _settingsInitializer.AreSettingsInitializedAsync();
                if (!areInitialized)
                {
                    _logger.LogInformation("Initializing settings for first run");
                    await _settingsInitializer.InitializeDefaultSettingsAsync();
                }

                // Load settings from persistence layer
                var dataDir = await _settingsService.GetSettingValueAsync<string>(ApplicationSettings.Paths.DataDirectory);
                var useNpu = await _settingsService.GetSettingValueAsync<bool>(ApplicationSettings.Hardware.DisableNpu);
                var useGpu = await _settingsService.GetSettingValueAsync<bool>(ApplicationSettings.Hardware.DisableGpu);
                var theme = await _settingsService.GetSettingValueAsync<string>(ApplicationSettings.UI.Theme);
                var dailyBudget = await _settingsService.GetSettingValueAsync<int>(ApplicationSettings.Providers.DailyTokenBudget);

                // Load Foundry models directory
                var foundryModelsDir = await _foundryService.GetModelsDirectoryAsync(CancellationToken.None).ConfigureAwait(false) ?? "(Not configured)";

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    DataDirectory = dataDir ?? ApplicationSettings.Defaults.DataDirectory;
                    ModelsDirectory = Path.Combine(DataDirectory, "models");
                    FoundryModelsDirectory = foundryModelsDir;
                    UseNpu = !useNpu; // Inverted: setting stores "Disable" flag
                    UseGpu = !useGpu; // Inverted: setting stores "Disable" flag
                    AllowOnlineProviders = false; // TODO: Load from online access mode setting
                    TokenBudget = dailyBudget;
                    SelectedTheme = theme ?? ApplicationSettings.Defaults.Theme;

                    IsBusy = false;
                    _logger.LogInformation("Settings loaded successfully");
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load settings");
                
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    // Fall back to defaults
                    DataDirectory = ApplicationSettings.Defaults.DataDirectory;
                    ModelsDirectory = Path.Combine(DataDirectory, "models");
                    FoundryModelsDirectory = "(Not configured)";
                    UseNpu = !ApplicationSettings.Defaults.DisableNpu;
                    UseGpu = !ApplicationSettings.Defaults.DisableGpu;
                    AllowOnlineProviders = false;
                    TokenBudget = ApplicationSettings.Defaults.DailyTokenBudget;
                    SelectedTheme = ApplicationSettings.Defaults.Theme;

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
            // Persist settings to database
            await _settingsService.SaveSettingAsync(
                ApplicationSettings.Paths.DataDirectory,
                DataDirectory,
                ApplicationSettings.Categories.Paths,
                ApplicationSettings.Descriptions.DataDirectory,
                reason: "user_ui_change");

            await _settingsService.SaveSettingAsync(
                ApplicationSettings.Hardware.DisableNpu,
                !UseNpu, // Inverted: UI shows "Use", setting stores "Disable"
                ApplicationSettings.Categories.Hardware,
                ApplicationSettings.Descriptions.DisableNpu,
                reason: "user_ui_change");

            await _settingsService.SaveSettingAsync(
                ApplicationSettings.Hardware.DisableGpu,
                !UseGpu, // Inverted: UI shows "Use", setting stores "Disable"
                ApplicationSettings.Categories.Hardware,
                ApplicationSettings.Descriptions.DisableGpu,
                reason: "user_ui_change");

            await _settingsService.SaveSettingAsync(
                ApplicationSettings.UI.Theme,
                SelectedTheme,
                ApplicationSettings.Categories.UI,
                ApplicationSettings.Descriptions.Theme,
                reason: "user_ui_change");

            await _settingsService.SaveSettingAsync(
                ApplicationSettings.Providers.DailyTokenBudget,
                TokenBudget,
                ApplicationSettings.Categories.Providers,
                ApplicationSettings.Descriptions.DailyTokenBudget,
                reason: "user_ui_change");

            IsBusy = false;
            _logger.LogInformation("Settings saved successfully");

            // TODO: Show confirmation to user (Toast/Snackbar)
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings");
            IsBusy = false;
            // TODO: Show error to user
        }
    }

    private async void OnResetSettings()
    {
        try
        {
            _logger.LogInformation("Resetting settings to defaults");
            IsBusy = true;

            await _settingsInitializer.ResetToDefaultsAsync();
            
            // Reload settings from database
            LoadSettings();
            
            _logger.LogInformation("Settings reset successfully");
            // TODO: Show confirmation to user
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset settings");
            IsBusy = false;
            // TODO: Show error to user
        }
    }

    private async void OnBrowseDataDirectory()
    {
        // TODO: Implement folder picker
        _logger.LogInformation("Browse data directory requested");
    }

    private async void OnBrowseModelsDirectory()
    {
        // TODO: Implement folder picker
        _logger.LogInformation("Browse models directory requested");
    }
}
