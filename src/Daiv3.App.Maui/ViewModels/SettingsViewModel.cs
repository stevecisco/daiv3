using System.Windows.Input;
using Microsoft.Extensions.Logging;

namespace Daiv3.App.Maui.ViewModels;

/// <summary>
/// ViewModel for the Settings interface.
/// Manages user preferences, directories, model settings, and system configuration.
/// </summary>
public class SettingsViewModel : BaseViewModel
{
    private readonly ILogger<SettingsViewModel> _logger;
    private string _dataDirectory = string.Empty;
    private string _modelsDirectory = string.Empty;
    private bool _useNpu = true;
    private bool _useGpu = true;
    private bool _allowOnlineProviders = false;
    private int _tokenBudget = 8192;
    private string _selectedTheme = "System";

    public SettingsViewModel(ILogger<SettingsViewModel> logger)
    {
        _logger = logger;
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
    /// Gets or sets the models directory path.
    /// </summary>
    public string ModelsDirectory
    {
        get => _modelsDirectory;
        set => SetProperty(ref _modelsDirectory, value);
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

        // TODO: Load settings from configuration service
        Task.Run(async () =>
        {
            await Task.Delay(200); // Simulate loading

            MainThread.BeginInvokeOnMainThread(() =>
            {
                // Set default values (TODO: Load from config)
                DataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Daiv3", "Data");
                ModelsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Daiv3", "Models");
                UseNpu = true;
                UseGpu = true;
                AllowOnlineProviders = false;
                TokenBudget = 8192;
                SelectedTheme = "System";

                IsBusy = false;
                _logger.LogInformation("Settings loaded");
            });
        });
    }

    private async void OnSaveSettings()
    {
        IsBusy = true;
        _logger.LogInformation("Saving settings...");

        // TODO: Persist settings to configuration service
        await Task.Delay(300); // Simulate save

        IsBusy = false;
        _logger.LogInformation("Settings saved successfully");

        // TODO: Show confirmation to user
    }

    private void OnResetSettings()
    {
        _logger.LogInformation("Resetting settings to defaults");
        LoadSettings();
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
