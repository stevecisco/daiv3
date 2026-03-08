using Daiv3.Core.Settings;
using Microsoft.Extensions.Logging;

namespace Daiv3.Persistence.Services;

/// <summary>
/// Default implementation of ISettingsInitializer.
/// Populates the database with all application settings on first run.
/// </summary>
public class SettingsInitializer : ISettingsInitializer
{
    private readonly ISettingsService _settingsService;
    private readonly ILogger<SettingsInitializer> _logger;

    public SettingsInitializer(ISettingsService settingsService, ILogger<SettingsInitializer> logger)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<int> InitializeDefaultSettingsAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Initializing default application settings");

        var defaultSettings = GetDefaultSettings();
        var initializedCount = 0;

        foreach (var (key, value, category, description, isSensitive) in defaultSettings)
        {
            try
            {
                // Check if setting already exists
                var existing = await _settingsService.GetSettingAsync(key, ct).ConfigureAwait(false);
                if (existing != null)
                {
                    _logger.LogDebug("Setting {Key} already exists, skipping", key);
                    continue;
                }

                // Create new setting with default value
                await _settingsService.SaveSettingAsync(
                    key: key,
                    value: value,
                    category: category,
                    description: description,
                    isSensitive: isSensitive,
                    reason: "initial_setup",
                    ct: ct
                ).ConfigureAwait(false);

                initializedCount++;
                _logger.LogDebug("Initialized setting {Key} with default value", key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize setting {Key}", key);
            }
        }

        // Mark first run as completed
        await _settingsService.SaveSettingAsync(
            key: ApplicationSettings.General.FirstRunCompleted,
            value: true,
            category: ApplicationSettings.Categories.General,
            description: ApplicationSettings.Descriptions.FirstRunCompleted,
            reason: "initial_setup_complete",
            ct: ct
        ).ConfigureAwait(false);

        // Record startup time
        await _settingsService.SaveSettingAsync(
            key: ApplicationSettings.General.LastStartupTime,
            value: DateTimeOffset.UtcNow.ToString("O"),
            category: ApplicationSettings.Categories.General,
            description: ApplicationSettings.Descriptions.LastStartupTime,
            reason: "startup",
            ct: ct
        ).ConfigureAwait(false);

        _logger.LogInformation("Initialized {Count} default settings", initializedCount);
        return initializedCount;
    }

    public async Task<bool> AreSettingsInitializedAsync(CancellationToken ct = default)
    {
        try
        {
            var firstRunSetting = await _settingsService.GetSettingValueAsync<bool>(
                ApplicationSettings.General.FirstRunCompleted,
                ct
            ).ConfigureAwait(false);

            return firstRunSetting;
        }
        catch
        {
            // If setting doesn't exist or can't be read, settings are not initialized
            return false;
        }
    }

    public async Task<int> ResetToDefaultsAsync(CancellationToken ct = default)
    {
        _logger.LogWarning("Resetting all settings to defaults - this will overwrite existing values");

        var defaultSettings = GetDefaultSettings();
        var resetCount = 0;

        foreach (var (key, value, category, description, isSensitive) in defaultSettings)
        {
            try
            {
                await _settingsService.SaveSettingAsync(
                    key: key,
                    value: value,
                    category: category,
                    description: description,
                    isSensitive: isSensitive,
                    reason: "reset_to_defaults",
                    ct: ct
                ).ConfigureAwait(false);

                resetCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reset setting {Key}", key);
            }
        }

        _logger.LogInformation("Reset {Count} settings to defaults", resetCount);
        return resetCount;
    }

    /// <summary>
    /// Gets all default settings as tuples of (key, value, category, description, isSensitive).
    /// </summary>
    private static List<(string Key, object Value, string Category, string Description, bool IsSensitive)> GetDefaultSettings()
    {
        return new List<(string, object, string, string, bool)>
        {
            // Paths
            (ApplicationSettings.Paths.DataDirectory, ApplicationSettings.Defaults.DataDirectory, 
                ApplicationSettings.Categories.Paths, ApplicationSettings.Descriptions.DataDirectory, false),
            (ApplicationSettings.Paths.WatchedDirectories, ApplicationSettings.Defaults.WatchedDirectories, 
                ApplicationSettings.Categories.Paths, ApplicationSettings.Descriptions.WatchedDirectories, false),
            (ApplicationSettings.Paths.IncludePatterns, ApplicationSettings.Defaults.IncludePatterns, 
                ApplicationSettings.Categories.Paths, ApplicationSettings.Descriptions.IncludePatterns, false),
            (ApplicationSettings.Paths.ExcludePatterns, ApplicationSettings.Defaults.ExcludePatterns, 
                ApplicationSettings.Categories.Paths, ApplicationSettings.Descriptions.ExcludePatterns, false),
            (ApplicationSettings.Paths.MaxSubDirectoryDepth, ApplicationSettings.Defaults.MaxSubDirectoryDepth, 
                ApplicationSettings.Categories.Paths, ApplicationSettings.Descriptions.MaxSubDirectoryDepth, false),
            (ApplicationSettings.Paths.FileTypeFilters, ApplicationSettings.Defaults.FileTypeFilters, 
                ApplicationSettings.Categories.Paths, ApplicationSettings.Descriptions.FileTypeFilters, false),
            (ApplicationSettings.Paths.KnowledgeBackPropagationPath, ApplicationSettings.Defaults.KnowledgeBackPropagationPath, 
                ApplicationSettings.Categories.Paths, ApplicationSettings.Descriptions.KnowledgeBackPropagationPath, false),

            // Models
            (ApplicationSettings.Models.FoundryLocalDefaultModel, ApplicationSettings.Defaults.FoundryLocalDefaultModel, 
                ApplicationSettings.Categories.Models, ApplicationSettings.Descriptions.FoundryLocalDefaultModel, false),
            (ApplicationSettings.Models.FoundryLocalChatModel, ApplicationSettings.Defaults.FoundryLocalChatModel, 
                ApplicationSettings.Categories.Models, ApplicationSettings.Descriptions.FoundryLocalChatModel, false),
            (ApplicationSettings.Models.FoundryLocalCodeModel, ApplicationSettings.Defaults.FoundryLocalCodeModel, 
                ApplicationSettings.Categories.Models, ApplicationSettings.Descriptions.FoundryLocalCodeModel, false),
            (ApplicationSettings.Models.FoundryLocalReasoningModel, ApplicationSettings.Defaults.FoundryLocalReasoningModel, 
                ApplicationSettings.Categories.Models, ApplicationSettings.Descriptions.FoundryLocalReasoningModel, false),
            (ApplicationSettings.Models.ModelToTaskMappings, ApplicationSettings.Defaults.ModelToTaskMappings, 
                ApplicationSettings.Categories.Models, ApplicationSettings.Descriptions.ModelToTaskMappings, false),
            (ApplicationSettings.Models.EmbeddingModel, ApplicationSettings.Defaults.EmbeddingModel, 
                ApplicationSettings.Categories.Models, ApplicationSettings.Descriptions.EmbeddingModel, false),
            (ApplicationSettings.Models.EmbeddingDimensions, ApplicationSettings.Defaults.EmbeddingDimensions, 
                ApplicationSettings.Categories.Models, ApplicationSettings.Descriptions.EmbeddingDimensions, false),

            // Providers
            (ApplicationSettings.Providers.OnlineAccessMode, ApplicationSettings.Defaults.OnlineAccessMode, 
                ApplicationSettings.Categories.Providers, ApplicationSettings.Descriptions.OnlineAccessMode, false),
            (ApplicationSettings.Providers.OnlineProvidersEnabled, ApplicationSettings.Defaults.OnlineProvidersEnabled, 
                ApplicationSettings.Categories.Providers, ApplicationSettings.Descriptions.OnlineProvidersEnabled, false),
            (ApplicationSettings.Providers.ForceOfflineMode, ApplicationSettings.Defaults.ForceOfflineMode,
                ApplicationSettings.Categories.Providers, ApplicationSettings.Descriptions.ForceOfflineMode, false),
            (ApplicationSettings.Providers.DailyTokenBudget, ApplicationSettings.Defaults.DailyTokenBudget, 
                ApplicationSettings.Categories.Providers, ApplicationSettings.Descriptions.DailyTokenBudget, false),
            (ApplicationSettings.Providers.MonthlyTokenBudget, ApplicationSettings.Defaults.MonthlyTokenBudget, 
                ApplicationSettings.Categories.Providers, ApplicationSettings.Descriptions.MonthlyTokenBudget, false),
            (ApplicationSettings.Providers.TokenBudgetAlertThreshold, ApplicationSettings.Defaults.TokenBudgetAlertThreshold, 
                ApplicationSettings.Categories.Providers, ApplicationSettings.Descriptions.TokenBudgetAlertThreshold, false),
            (ApplicationSettings.Providers.TokenBudgetMode, ApplicationSettings.Defaults.TokenBudgetMode, 
                ApplicationSettings.Categories.Providers, ApplicationSettings.Descriptions.TokenBudgetMode, false),
            
            // Provider-specific (sensitive, empty defaults)
            (ApplicationSettings.Providers.OpenAIApiKey, ApplicationSettings.Defaults.OpenAIApiKey, 
                ApplicationSettings.Categories.Providers, ApplicationSettings.Descriptions.OpenAIApiKey, true),
            (ApplicationSettings.Providers.OpenAIBaseUrl, ApplicationSettings.Defaults.OpenAIBaseUrl, 
                ApplicationSettings.Categories.Providers, ApplicationSettings.Descriptions.OpenAIBaseUrl, false),
            (ApplicationSettings.Providers.AnthropicApiKey, ApplicationSettings.Defaults.AnthropicApiKey, 
                ApplicationSettings.Categories.Providers, ApplicationSettings.Descriptions.AnthropicApiKey, true),
            (ApplicationSettings.Providers.AnthropicBaseUrl, ApplicationSettings.Defaults.AnthropicBaseUrl, 
                ApplicationSettings.Categories.Providers, ApplicationSettings.Descriptions.AnthropicBaseUrl, false),
            (ApplicationSettings.Providers.AzureOpenAIApiKey, ApplicationSettings.Defaults.AzureOpenAIApiKey, 
                ApplicationSettings.Categories.Providers, ApplicationSettings.Descriptions.AzureOpenAIApiKey, true),
            (ApplicationSettings.Providers.AzureOpenAIEndpoint, ApplicationSettings.Defaults.AzureOpenAIEndpoint, 
                ApplicationSettings.Categories.Providers, ApplicationSettings.Descriptions.AzureOpenAIEndpoint, false),
            (ApplicationSettings.Providers.AzureOpenAIDeploymentName, ApplicationSettings.Defaults.AzureOpenAIDeploymentName, 
                ApplicationSettings.Categories.Providers, ApplicationSettings.Descriptions.AzureOpenAIDeploymentName, false),

            // Hardware
            (ApplicationSettings.Hardware.PreferredExecutionProvider, ApplicationSettings.Defaults.PreferredExecutionProvider, 
                ApplicationSettings.Categories.Hardware, ApplicationSettings.Descriptions.PreferredExecutionProvider, false),
            (ApplicationSettings.Hardware.ForceDeviceType, ApplicationSettings.Defaults.ForceDeviceType, 
                ApplicationSettings.Categories.Hardware, ApplicationSettings.Descriptions.ForceDeviceType, false),
            (ApplicationSettings.Hardware.DisableNpu, ApplicationSettings.Defaults.DisableNpu, 
                ApplicationSettings.Categories.Hardware, ApplicationSettings.Descriptions.DisableNpu, false),
            (ApplicationSettings.Hardware.DisableGpu, ApplicationSettings.Defaults.DisableGpu, 
                ApplicationSettings.Categories.Hardware, ApplicationSettings.Descriptions.DisableGpu, false),
            (ApplicationSettings.Hardware.ForceCpuOnly, ApplicationSettings.Defaults.ForceCpuOnly, 
                ApplicationSettings.Categories.Hardware, ApplicationSettings.Descriptions.ForceCpuOnly, false),
            (ApplicationSettings.Hardware.MaxConcurrentModelRequests, ApplicationSettings.Defaults.MaxConcurrentModelRequests, 
                ApplicationSettings.Categories.Hardware, ApplicationSettings.Descriptions.MaxConcurrentModelRequests, false),

            // UI
            (ApplicationSettings.UI.Theme, ApplicationSettings.Defaults.Theme, 
                ApplicationSettings.Categories.UI, ApplicationSettings.Descriptions.Theme, false),
            (ApplicationSettings.UI.DashboardRefreshInterval, ApplicationSettings.Defaults.DashboardRefreshInterval, 
                ApplicationSettings.Categories.UI, ApplicationSettings.Descriptions.DashboardRefreshInterval, false),
            (ApplicationSettings.UI.ShowNotifications, ApplicationSettings.Defaults.ShowNotifications, 
                ApplicationSettings.Categories.UI, ApplicationSettings.Descriptions.ShowNotifications, false),
            (ApplicationSettings.UI.MinimizeToTray, ApplicationSettings.Defaults.MinimizeToTray, 
                ApplicationSettings.Categories.UI, ApplicationSettings.Descriptions.MinimizeToTray, false),
            (ApplicationSettings.UI.AutoStartDashboard, ApplicationSettings.Defaults.AutoStartDashboard, 
                ApplicationSettings.Categories.UI, ApplicationSettings.Descriptions.AutoStartDashboard, false),
            (ApplicationSettings.UI.LogLevel, ApplicationSettings.Defaults.LogLevel, 
                ApplicationSettings.Categories.UI, ApplicationSettings.Descriptions.LogLevel, false),

            // Knowledge
            (ApplicationSettings.Knowledge.AutoIndexOnStartup, ApplicationSettings.Defaults.AutoIndexOnStartup, 
                ApplicationSettings.Categories.Knowledge, ApplicationSettings.Descriptions.AutoIndexOnStartup, false),
            (ApplicationSettings.Knowledge.IndexScanInterval, ApplicationSettings.Defaults.IndexScanInterval, 
                ApplicationSettings.Categories.Knowledge, ApplicationSettings.Descriptions.IndexScanInterval, false),
            (ApplicationSettings.Knowledge.ChunkSize, ApplicationSettings.Defaults.ChunkSize, 
                ApplicationSettings.Categories.Knowledge, ApplicationSettings.Descriptions.ChunkSize, false),
            (ApplicationSettings.Knowledge.ChunkOverlap, ApplicationSettings.Defaults.ChunkOverlap, 
                ApplicationSettings.Categories.Knowledge, ApplicationSettings.Descriptions.ChunkOverlap, false),
            (ApplicationSettings.Knowledge.MaxDocumentsPerBatch, ApplicationSettings.Defaults.MaxDocumentsPerBatch, 
                ApplicationSettings.Categories.Knowledge, ApplicationSettings.Descriptions.MaxDocumentsPerBatch, false),
            (ApplicationSettings.Knowledge.CategoryToPathMappings, ApplicationSettings.Defaults.CategoryToPathMappings, 
                ApplicationSettings.Categories.Knowledge, ApplicationSettings.Descriptions.CategoryToPathMappings, false),
            (ApplicationSettings.Knowledge.EnableKnowledgeGraph, ApplicationSettings.Defaults.EnableKnowledgeGraph, 
                ApplicationSettings.Categories.Knowledge, ApplicationSettings.Descriptions.EnableKnowledgeGraph, false),

            // General
            (ApplicationSettings.General.EnableAgents, ApplicationSettings.Defaults.EnableAgents, 
                ApplicationSettings.Categories.General, ApplicationSettings.Descriptions.EnableAgents, false),
            (ApplicationSettings.General.EnableSkills, ApplicationSettings.Defaults.EnableSkills, 
                ApplicationSettings.Categories.General, ApplicationSettings.Descriptions.EnableSkills, false),
            (ApplicationSettings.General.AgentIterationLimit, ApplicationSettings.Defaults.AgentIterationLimit, 
                ApplicationSettings.Categories.General, ApplicationSettings.Descriptions.AgentIterationLimit, false),
            (ApplicationSettings.General.AgentTokenBudget, ApplicationSettings.Defaults.AgentTokenBudget, 
                ApplicationSettings.Categories.General, ApplicationSettings.Descriptions.AgentTokenBudget, false),
            (ApplicationSettings.General.SkillMarketplaceUrls, ApplicationSettings.Defaults.SkillMarketplaceUrls, 
                ApplicationSettings.Categories.General, ApplicationSettings.Descriptions.SkillMarketplaceUrls, false),
            (ApplicationSettings.General.EnableScheduling, ApplicationSettings.Defaults.EnableScheduling, 
                ApplicationSettings.Categories.General, ApplicationSettings.Descriptions.EnableScheduling, false),
            (ApplicationSettings.General.SchedulerCheckInterval, ApplicationSettings.Defaults.SchedulerCheckInterval, 
                ApplicationSettings.Categories.General, ApplicationSettings.Descriptions.SchedulerCheckInterval, false),
        };
    }
}
