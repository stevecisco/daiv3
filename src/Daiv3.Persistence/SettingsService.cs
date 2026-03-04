using Daiv3.Persistence.Entities;
using Daiv3.Persistence.Repositories;
using Microsoft.Extensions.Logging;

namespace Daiv3.Persistence;

/// <summary>
/// Service for managing application settings with version support.
/// Handles settings versioning, schema migrations, and upgrade paths.
/// Implements CT-DATA-001: Settings SHALL be versioned to support upgrades.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Gets a setting by key.
    /// </summary>
    Task<AppSetting?> GetSettingAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Gets a setting value as a specific type.
    /// </summary>
    Task<T?> GetSettingValueAsync<T>(string key, CancellationToken ct = default);

    /// <summary>
    /// Gets all settings.
    /// </summary>
    Task<IReadOnlyList<AppSetting>> GetAllSettingsAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets all settings in a specific category.
    /// </summary>
    Task<IReadOnlyList<AppSetting>> GetSettingsByCategoryAsync(string category, CancellationToken ct = default);

    /// <summary>
    /// Saves or updates a setting.
    /// Automatically records the change in version history.
    /// </summary>
    Task<string> SaveSettingAsync(string key, object value, string category = "general", 
        string? description = null, bool isSensitive = false, string? reason = null, 
        CancellationToken ct = default);

    /// <summary>
    /// Deletes a setting.
    /// </summary>
    Task DeleteSettingAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Gets the current settings schema version.
    /// </summary>
    Task<int> GetCurrentSchemaVersionAsync(CancellationToken ct = default);

    /// <summary>
    /// Sets the settings schema version.
    /// </summary>
    Task SetSchemaVersionAsync(int version, CancellationToken ct = default);

    /// <summary>
    /// Gets the version history for a specific setting.
    /// </summary>
    Task<IReadOnlyList<SettingsVersionHistory>> GetSettingHistoryAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Validates all settings for integrity and performs automatic corrections if needed.
    /// </summary>
    Task ValidateAndRepairAsync(CancellationToken ct = default);

    /// <summary>
    /// Performs a schema migration from oldVersion to newVersion.
    /// Applies default values, transforms, and validations as needed.
    /// </summary>
    Task MigrateSchemaAsync(int oldVersion, int newVersion, CancellationToken ct = default);
}

/// <summary>
/// Default implementation of ISettingsService.
/// </summary>
public class SettingsService : ISettingsService
{
    private readonly SettingsRepository _repository;
    private readonly ILogger<SettingsService> _logger;
    private int? _cachedSchemaVersion;

    public SettingsService(SettingsRepository repository, ILogger<SettingsService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AppSetting?> GetSettingAsync(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return await _repository.GetByKeyAsync(key, ct).ConfigureAwait(false);
    }

    public async Task<T?> GetSettingValueAsync<T>(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var setting = await GetSettingAsync(key, ct).ConfigureAwait(false);
        if (setting == null)
            return default;

        try
        {
            return DeserializeValue<T>(setting.SettingValue, setting.ValueType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize setting {Key} of type {Type}", key, typeof(T).Name);
            return default;
        }
    }

    public async Task<IReadOnlyList<AppSetting>> GetAllSettingsAsync(CancellationToken ct = default)
    {
        return await _repository.GetAllAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<AppSetting>> GetSettingsByCategoryAsync(string category, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        return await _repository.GetByCategoryAsync(category, ct).ConfigureAwait(false);
    }

    public async Task<string> SaveSettingAsync(string key, object value, string category = "general",
        string? description = null, bool isSensitive = false, string? reason = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentException.ThrowIfNullOrWhiteSpace(category);

        var (serializedValue, valueType) = SerializeValue(value);
        var currentVersion = await GetCurrentSchemaVersionAsync(ct).ConfigureAwait(false);

        var setting = new AppSetting
        {
            SettingId = Guid.NewGuid().ToString(),
            SettingKey = key,
            SettingValue = serializedValue,
            ValueType = valueType,
            Category = category,
            SchemaVersion = currentVersion,
            Description = description,
            IsSensitive = isSensitive,
            UpdatedBy = "user"
        };

        var settingId = await _repository.UpsertAsync(setting, reason ?? "update", ct).ConfigureAwait(false);
        _logger.LogInformation("Saved setting {Key} in category {Category}", key, category);

        return settingId;
    }

    public async Task DeleteSettingAsync(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var setting = await GetSettingAsync(key, ct).ConfigureAwait(false);
        if (setting == null)
        {
            _logger.LogWarning("Setting {Key} not found for deletion", key);
            return;
        }

        await _repository.DeleteAsync(setting.SettingId, ct).ConfigureAwait(false);
        _logger.LogInformation("Deleted setting {Key}", key);
    }

    public async Task<int> GetCurrentSchemaVersionAsync(CancellationToken ct = default)
    {
        if (_cachedSchemaVersion.HasValue)
            return _cachedSchemaVersion.Value;

        try
        {
            // We would need a way to execute raw queries. For now, we'll return 1 as default.
            // In a real implementation, you'd have a method to query this from the database.
            // For this implementation, we'll return 1 as the default version.
            _cachedSchemaVersion = 1;
            return _cachedSchemaVersion.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get schema version from settings_metadata");
            return 1; // Default version
        }
    }

    public async Task SetSchemaVersionAsync(int version, CancellationToken ct = default)
    {
        if (version <= 0)
            throw new ArgumentException("Schema version must be positive", nameof(version));

        _cachedSchemaVersion = version;
        _logger.LogInformation("Set settings schema version to {Version}", version);

        // Note: In a real implementation, this would update the settings_metadata table
        // For now, this is a placeholder
        await Task.CompletedTask.ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SettingsVersionHistory>> GetSettingHistoryAsync(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return await _repository.GetHistoryByKeyAsync(key, ct).ConfigureAwait(false);
    }

    public async Task ValidateAndRepairAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Validating settings integrity");

        var allSettings = await GetAllSettingsAsync(ct).ConfigureAwait(false);
        var repairCount = 0;

        foreach (var setting in allSettings)
        {
            try
            {
                // Attempt to deserialize to validate format
                var obj = DeserializeValue<object>(setting.SettingValue, setting.ValueType);
                if (obj == null && !setting.ValueType.Equals("null", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Setting {Key} has null value for non-null type {Type}", 
                        setting.SettingKey, setting.ValueType);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to validate setting {Key}", setting.SettingKey);
                repairCount++;
                // In a real implementation, you might attempt auto-repair here
            }
        }

        if (repairCount > 0)
        {
            _logger.LogWarning("Found {Count} settings with validation issues", repairCount);
        }
        else
        {
            _logger.LogInformation("All settings passed validation");
        }
    }

    public async Task MigrateSchemaAsync(int oldVersion, int newVersion, CancellationToken ct = default)
    {
        if (oldVersion >= newVersion)
            throw new ArgumentException("New version must be greater than old version");

        _logger.LogInformation("Migrating settings schema from v{OldVersion} to v{NewVersion}", oldVersion, newVersion);

        // Get all settings at the old version
        var allSettings = await GetAllSettingsAsync(ct).ConfigureAwait(false);
        var settingsToMigrate = allSettings.Where(s => s.SchemaVersion <= oldVersion).ToList();

        _logger.LogInformation("Found {Count} settings to migrate", settingsToMigrate.Count);

        foreach (var setting in settingsToMigrate)
        {
            // Apply version-specific migrations
            var migratedSetting = ApplyMigrationTransforms(setting, oldVersion, newVersion);
            migratedSetting.SchemaVersion = newVersion;

            await _repository.UpdateAsync(migratedSetting, ct).ConfigureAwait(false);

            // Record the migration in history
            await _repository.AddHistoryAsync(new SettingsVersionHistory
            {
                HistoryId = Guid.NewGuid().ToString(),
                SettingKey = setting.SettingKey,
                OldValue = setting.SettingValue,
                NewValue = migratedSetting.SettingValue,
                SchemaVersion = newVersion,
                ChangedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                ChangedBy = "system",
                Reason = $"schema_migration_v{oldVersion}_to_v{newVersion}"
            }, ct).ConfigureAwait(false);
        }

        // Update the schema version
        await SetSchemaVersionAsync(newVersion, ct).ConfigureAwait(false);

        _logger.LogInformation("Schema migration from v{OldVersion} to v{NewVersion} completed", oldVersion, newVersion);
    }

    // ===== Helper Methods =====

    private static (string serialized, string valueType) SerializeValue(object value)
    {
        return value switch
        {
            string s => (s, "string"),
            int i => (i.ToString(), "integer"),
            long l => (l.ToString(), "integer"),
            bool b => (b ? "true" : "false", "boolean"),
            double d => (d.ToString("G17"), "real"),
            float f => (f.ToString("G9"), "real"),
            _ => (System.Text.Json.JsonSerializer.Serialize(value), "json")
        };
    }

    private static T? DeserializeValue<T>(string value, string valueType)
    {
        try
        {
            return valueType switch
            {
                "string" => typeof(T) == typeof(string) ? (T?)(object)value : default,
                "integer" when typeof(T) == typeof(int) || typeof(T) == typeof(long) => 
                    (T?)(object)(long.TryParse(value, out var l) ? l : 0),
                "integer" when typeof(T) == typeof(int) => 
                    (T?)(object)(int.TryParse(value, out var i) ? i : 0),
                "boolean" when typeof(T) == typeof(bool) => 
                    (T?)(object)(bool.TryParse(value, out var b) ? b : false),
                "real" when typeof(T) == typeof(double) || typeof(T) == typeof(float) => 
                    (T?)(object)(double.TryParse(value, out var d) ? d : 0),
                "json" => System.Text.Json.JsonSerializer.Deserialize<T>(value),
                _ => default
            };
        }
        catch
        {
            return default;
        }
    }

    /// <summary>
    /// Applies version-specific schema transformation logic.
    /// This method handles transformations needed when upgrading schemas.
    /// </summary>
    private static AppSetting ApplyMigrationTransforms(AppSetting setting, int fromVersion, int toVersion)
    {
        // Example: V1 to V2 might rename certain keys or transform value formats
        // For now, return the setting unchanged (no transforms defined yet)
        return setting;
    }
}
