using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Daiv3.Persistence.Services;

/// <summary>
/// Default implementation of ISettingsValidator.
/// Validates settings values according to constraint rules.
/// Implements CT-NFR-002: Settings changes SHOULD be validated and applied safely.
/// </summary>
public class SettingsValidator : ISettingsValidator
{
    private readonly ILogger<SettingsValidator> _logger;

    public SettingsValidator(ILogger<SettingsValidator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SettingsValidationResult> ValidateAsync(string key, object value, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        var result = ValidateSetting(key, value);
        
        if (!result.IsValid)
        {
            _logger.LogWarning("Settings validation failed for key {Key}: {Error}", key, result.ErrorMessage);
        }
        else if (!string.IsNullOrEmpty(result.WarningMessage))
        {
            _logger.LogInformation("Settings validation warning for key {Key}: {Warning}", key, result.WarningMessage);
        }
        else
        {
            _logger.LogDebug("Settings validation passed for key {Key}", key);
        }

        return await Task.FromResult(result);
    }

    public async Task<IReadOnlyList<SettingsValidationResult>> ValidateBatchAsync(
        IDictionary<string, object> settings,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var results = new List<SettingsValidationResult>();

        foreach (var kvp in settings)
        {
            var result = ValidateSetting(kvp.Key, kvp.Value);
            results.Add(result);

            if (!result.IsValid)
            {
                _logger.LogWarning("Batch validation failed for key {Key}: {Error}", kvp.Key, result.ErrorMessage);
            }
        }

        return await Task.FromResult(results.AsReadOnly());
    }

    /// <summary>
    /// Internal validation logic for a single setting.
    /// </summary>
    private static SettingsValidationResult ValidateSetting(string key, object value)
    {
        // Null/empty string check
        if (value == null)
        {
            // Some settings can be null/empty
            if (IsNullableKey(key))
                return new SettingsValidationResult(key, true);
            return new SettingsValidationResult(key, false, $"Setting cannot be null");
        }

        // Route to specific validators
        return key switch
        {
            // Path settings
            Daiv3.Core.Settings.ApplicationSettings.Paths.DataDirectory => 
                ValidateDataDirectory(key, value),
            Daiv3.Core.Settings.ApplicationSettings.Paths.WatchedDirectories =>
                ValidateWatchedDirectories(key, value),
            Daiv3.Core.Settings.ApplicationSettings.Paths.KnowledgeBackPropagationPath =>
                ValidatePathSetting(key, value, "Knowledge back-propagation path"),

            // Numeric settings - token budgets
            Daiv3.Core.Settings.ApplicationSettings.Providers.DailyTokenBudget =>
                ValidatePositiveInteger(key, value, "Daily token budget"),
            Daiv3.Core.Settings.ApplicationSettings.Providers.MonthlyTokenBudget =>
                ValidatePositiveInteger(key, value, "Monthly token budget"),
            Daiv3.Core.Settings.ApplicationSettings.Providers.TokenBudgetAlertThreshold =>
                ValidatePercentage(key, value, "Token budget alert threshold"),
            
            // Numeric settings - agents
            Daiv3.Core.Settings.ApplicationSettings.General.AgentIterationLimit =>
                ValidatePositiveInteger(key, value, "Agent iteration limit"),
            Daiv3.Core.Settings.ApplicationSettings.General.AgentTokenBudget =>
                ValidatePositiveInteger(key, value, "Agent token budget"),

            // Numeric settings - scheduling
            Daiv3.Core.Settings.ApplicationSettings.General.SchedulerCheckInterval =>
                ValidatePositiveInteger(key, value, "Scheduler check interval"),
            Daiv3.Core.Settings.ApplicationSettings.Knowledge.IndexScanInterval =>
                ValidatePositiveInteger(key, value, "Index scan interval"),

            // Numeric settings - knowledge processing
            Daiv3.Core.Settings.ApplicationSettings.Knowledge.ChunkSize =>
                ValidatePositiveInteger(key, value, "Chunk size"),
            Daiv3.Core.Settings.ApplicationSettings.Knowledge.ChunkOverlap =>
                ValidateNonNegativeInteger(key, value, "Chunk overlap"),
            Daiv3.Core.Settings.ApplicationSettings.Knowledge.MaxDocumentsPerBatch =>
                ValidatePositiveInteger(key, value, "Max documents per batch"),

            // Numeric settings - hardware
            Daiv3.Core.Settings.ApplicationSettings.Hardware.MaxConcurrentModelRequests =>
                ValidatePositiveInteger(key, value, "Max concurrent model requests"),
            
            // Numeric settings - embedding
            Daiv3.Core.Settings.ApplicationSettings.Models.EmbeddingDimensions =>
                ValidatePositiveInteger(key, value, "Embedding dimensions"),

            // Numeric settings - UI
            Daiv3.Core.Settings.ApplicationSettings.UI.DashboardRefreshInterval =>
                ValidatePositiveInteger(key, value, "Dashboard refresh interval"),
            
            // Numeric settings - subdirectory depth
            Daiv3.Core.Settings.ApplicationSettings.Paths.MaxSubDirectoryDepth =>
                ValidatePositiveInteger(key, value, "Max subdirectory depth"),

            // Enum/Choice settings
            Daiv3.Core.Settings.ApplicationSettings.UI.Theme =>
                ValidateTheme(key, value),
            Daiv3.Core.Settings.ApplicationSettings.Providers.OnlineAccessMode =>
                ValidateOnlineAccessMode(key, value),
            Daiv3.Core.Settings.ApplicationSettings.Providers.TokenBudgetMode =>
                ValidateTokenBudgetMode(key, value),
            Daiv3.Core.Settings.ApplicationSettings.UI.LogLevel =>
                ValidateLogLevel(key, value),
            Daiv3.Core.Settings.ApplicationSettings.Hardware.PreferredExecutionProvider =>
                ValidateExecutionProvider(key, value),

            // Model settings
            Daiv3.Core.Settings.ApplicationSettings.Models.FoundryLocalDefaultModel =>
                ValidateModelName(key, value),
            Daiv3.Core.Settings.ApplicationSettings.Models.FoundryLocalChatModel =>
                ValidateModelName(key, value),
            Daiv3.Core.Settings.ApplicationSettings.Models.FoundryLocalCodeModel =>
                ValidateModelName(key, value),
            Daiv3.Core.Settings.ApplicationSettings.Models.FoundryLocalReasoningModel =>
                ValidateModelName(key, value),
            Daiv3.Core.Settings.ApplicationSettings.Models.EmbeddingModel =>
                ValidateModelName(key, value),

            // URL settings
            Daiv3.Core.Settings.ApplicationSettings.Providers.OpenAIBaseUrl =>
                ValidateUrl(key, value),
            Daiv3.Core.Settings.ApplicationSettings.Providers.AnthropicBaseUrl =>
                ValidateUrl(key, value),
            Daiv3.Core.Settings.ApplicationSettings.Providers.AzureOpenAIEndpoint =>
                ValidateUrl(key, value),

            // Array settings
            Daiv3.Core.Settings.ApplicationSettings.Providers.OnlineProvidersEnabled =>
                ValidateJsonArray(key, value),
            Daiv3.Core.Settings.ApplicationSettings.General.SkillMarketplaceUrls =>
                ValidateJsonArray(key, value),

            // JSON object settings
            Daiv3.Core.Settings.ApplicationSettings.Models.ModelToTaskMappings =>
                ValidateJsonObject(key, value),
            Daiv3.Core.Settings.ApplicationSettings.Knowledge.CategoryToPathMappings =>
                ValidateJsonObject(key, value),

            // Boolean and other settings are generally accepted
            _ => new SettingsValidationResult(key, true)
        };
    }

    #region Specialized Validators

    private static bool IsNullableKey(string key)
    {
        // Some settings can be null or empty
        return key == Daiv3.Core.Settings.ApplicationSettings.Providers.OpenAIApiKey
            || key == Daiv3.Core.Settings.ApplicationSettings.Providers.AnthropicApiKey
            || key == Daiv3.Core.Settings.ApplicationSettings.Providers.AzureOpenAIApiKey
            || key == Daiv3.Core.Settings.ApplicationSettings.Hardware.ForceDeviceType
            || key == Daiv3.Core.Settings.ApplicationSettings.UI.LogLevel
            || key == Daiv3.Core.Settings.ApplicationSettings.UI.MinimizeToTray
            || key == Daiv3.Core.Settings.ApplicationSettings.UI.AutoStartDashboard
            || string.Equals(key, Daiv3.Core.Settings.ApplicationSettings.General.LastStartupTime, StringComparison.OrdinalIgnoreCase);
    }

    private static SettingsValidationResult ValidateDataDirectory(string key, object value)
    {
        if (value is not string pathStr || string.IsNullOrWhiteSpace(pathStr))
            return new SettingsValidationResult(key, false, "Data directory must be a non-empty string");

        try
        {
            var path = Path.GetFullPath(pathStr);
            // Check if path can be created (parent must exist or be creatable)
            var parent = Path.GetDirectoryName(path);
            if (parent != null && !Directory.Exists(parent))
            {
                return new SettingsValidationResult(key, false,
                    $"Cannot create data directory: parent path does not exist: {parent}");
            }
            return new SettingsValidationResult(key, true);
        }
        catch (Exception ex)
        {
            return new SettingsValidationResult(key, false, $"Invalid path: {ex.Message}");
        }
    }

    private static SettingsValidationResult ValidatePathSetting(string key, object value, string settingName)
    {
        if (value is not string pathStr || string.IsNullOrWhiteSpace(pathStr))
            return new SettingsValidationResult(key, false, $"{settingName} must be a non-empty string");

        try
        {
            // Validate it's a valid path format
            var path = Path.GetFullPath(pathStr);
            return new SettingsValidationResult(key, true);
        }
        catch (Exception ex)
        {
            return new SettingsValidationResult(key, false, $"Invalid path: {ex.Message}");
        }
    }

    private static SettingsValidationResult ValidateWatchedDirectories(string key, object value)
    {
        if (value is not string jsonStr)
            return new SettingsValidationResult(key, false, "Watched directories must be a JSON array string");

        try
        {
            using var doc = JsonDocument.Parse(jsonStr);
            var element = doc.RootElement;
            if (element.ValueKind != System.Text.Json.JsonValueKind.Array)
                return new SettingsValidationResult(key, false,
                    "Watched directories must be a JSON array");

            // Validate each path in the array
            var invalidPaths = new List<string>();
#pragma warning disable IDISP004 // Don't ignore created IDisposable (foreach properly disposes enumerators)
            foreach (var item in element.EnumerateArray())
#pragma warning restore IDISP004
            {
                if (item.ValueKind != System.Text.Json.JsonValueKind.String)
                {
                    invalidPaths.Add("(non-string element)");
                    continue;
                }

                var pathStr = item.GetString();
                if (string.IsNullOrWhiteSpace(pathStr))
                {
                    invalidPaths.Add("(empty path)");
                }
            }

            if (invalidPaths.Count > 0)
                return new SettingsValidationResult(key, false,
                    $"Invalid paths in watched directories: {string.Join(", ", invalidPaths)}");

            return new SettingsValidationResult(key, true);
        }
        catch (JsonException ex)
        {
            return new SettingsValidationResult(key, false, $"Invalid JSON format: {ex.Message}");
        }
    }

    private static SettingsValidationResult ValidatePositiveInteger(string key, object value, string settingName)
    {
        if (!TryGetInteger(value, out var intValue))
            return new SettingsValidationResult(key, false, $"{settingName} must be an integer");

        if (intValue <= 0)
            return new SettingsValidationResult(key, false, $"{settingName} must be greater than 0");

        return new SettingsValidationResult(key, true);
    }

    private static SettingsValidationResult ValidateNonNegativeInteger(string key, object value, string settingName)
    {
        if (!TryGetInteger(value, out var intValue))
            return new SettingsValidationResult(key, false, $"{settingName} must be an integer");

        if (intValue < 0)
            return new SettingsValidationResult(key, false, $"{settingName} must be non-negative");

        return new SettingsValidationResult(key, true);
    }

    private static SettingsValidationResult ValidatePercentage(string key, object value, string settingName)
    {
        if (!TryGetInteger(value, out var intValue))
            return new SettingsValidationResult(key, false, $"{settingName} must be an integer");

        if (intValue < 0 || intValue > 100)
            return new SettingsValidationResult(key, false, $"{settingName} must be between 0 and 100");

        return new SettingsValidationResult(key, true);
    }

    private static SettingsValidationResult ValidateTheme(string key, object value)
    {
        if (value is not string themeStr)
            return new SettingsValidationResult(key, false, "Theme must be a string");

        var validThemes = new[] { "light", "dark", "system" };
        if (!validThemes.Contains(themeStr.ToLowerInvariant()))
            return new SettingsValidationResult(key, false,
                $"Theme must be one of: {string.Join(", ", validThemes)}");

        return new SettingsValidationResult(key, true);
    }

    private static SettingsValidationResult ValidateOnlineAccessMode(string key, object value)
    {
        if (value is not string modeStr)
            return new SettingsValidationResult(key, false, "Online access mode must be a string");

        var validModes = new[] { "never", "ask", "auto_within_budget", "per_task" };
        if (!validModes.Contains(modeStr.ToLowerInvariant()))
            return new SettingsValidationResult(key, false,
                $"Online access mode must be one of: {string.Join(", ", validModes)}");

        return new SettingsValidationResult(key, true);
    }

    private static SettingsValidationResult ValidateTokenBudgetMode(string key, object value)
    {
        if (value is not string modeStr)
            return new SettingsValidationResult(key, false, "Token budget mode must be a string");

        var validModes = new[] { "hard_stop", "user_confirm" };
        if (!validModes.Contains(modeStr.ToLowerInvariant()))
            return new SettingsValidationResult(key, false,
                $"Token budget mode must be one of: {string.Join(", ", validModes)}");

        return new SettingsValidationResult(key, true);
    }

    private static SettingsValidationResult ValidateLogLevel(string key, object value)
    {
        if (value is not string levelStr)
            return new SettingsValidationResult(key, false, "Log level must be a string");

        var validLevels = new[] { "verbose", "debug", "information", "warning", "error", "critical" };
        if (!validLevels.Contains(levelStr.ToLowerInvariant()))
            return new SettingsValidationResult(key, false,
                $"Log level must be one of: {string.Join(", ", validLevels)}");

        return new SettingsValidationResult(key, true);
    }

    private static SettingsValidationResult ValidateExecutionProvider(string key, object value)
    {
        if (value is not string providerStr)
            return new SettingsValidationResult(key, false, "Execution provider must be a string");

        var validProviders = new[] { "auto", "npu", "gpu", "cpu" };
        if (!validProviders.Contains(providerStr.ToLowerInvariant()))
            return new SettingsValidationResult(key, false,
                $"Execution provider must be one of: {string.Join(", ", validProviders)}");

        return new SettingsValidationResult(key, true);
    }

    private static SettingsValidationResult ValidateModelName(string key, object value)
    {
        if (value is not string modelStr)
            return new SettingsValidationResult(key, false, "Model name must be a string");

        if (string.IsNullOrWhiteSpace(modelStr))
            return new SettingsValidationResult(key, false, "Model name cannot be empty");

        return new SettingsValidationResult(key, true);
    }

    private static SettingsValidationResult ValidateUrl(string key, object value)
    {
        if (value is not string urlStr)
            return new SettingsValidationResult(key, false, "URL must be a string");

        // Allow empty URLs for optional endpoints
        if (string.IsNullOrWhiteSpace(urlStr))
            return new SettingsValidationResult(key, true);

        try
        {
            _ = new Uri(urlStr);
            return new SettingsValidationResult(key, true);
        }
        catch (UriFormatException ex)
        {
            return new SettingsValidationResult(key, false, $"Invalid URL format: {ex.Message}");
        }
    }

    private static SettingsValidationResult ValidateJsonArray(string key, object value)
    {
        if (value is not string jsonStr)
            return new SettingsValidationResult(key, false, "Value must be a JSON string");

        try
        {
            using var doc = JsonDocument.Parse(jsonStr);
            var element = doc.RootElement;
            if (element.ValueKind != System.Text.Json.JsonValueKind.Array)
                return new SettingsValidationResult(key, false, "Value must be a JSON array");

            return new SettingsValidationResult(key, true);
        }
        catch (JsonException ex)
        {
            return new SettingsValidationResult(key, false, $"Invalid JSON format: {ex.Message}");
        }
    }

    private static SettingsValidationResult ValidateJsonObject(string key, object value)
    {
        if (value is not string jsonStr)
            return new SettingsValidationResult(key, false, "Value must be a JSON string");

        try
        {
            using var doc = JsonDocument.Parse(jsonStr);
            var element = doc.RootElement;
            if (element.ValueKind != System.Text.Json.JsonValueKind.Object)
                return new SettingsValidationResult(key, false, "Value must be a JSON object");

            return new SettingsValidationResult(key, true);
        }
        catch (JsonException ex)
        {
            return new SettingsValidationResult(key, false, $"Invalid JSON format: {ex.Message}");
        }
    }

    #endregion

    private static bool TryGetInteger(object value, out long result)
    {
        result = 0;
        if (value is int i)
        {
            result = i;
            return true;
        }
        if (value is long l)
        {
            result = l;
            return true;
        }
        if (value is string s && long.TryParse(s, out l))
        {
            result = l;
            return true;
        }
        return false;
    }
}
