using Daiv3.Persistence.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Daiv3.Persistence.Repositories;

/// <summary>
/// Repository for managing application settings with version history tracking.
/// Handles CRUD operations for AppSetting entities and maintains an audit trail via SettingsVersionHistory.
/// Implements CT-DATA-001: Settings SHALL be versioned to support upgrades.
/// </summary>
public class SettingsRepository : RepositoryBase<AppSetting>
{
    public SettingsRepository(IDatabaseContext databaseContext, ILogger<SettingsRepository> logger)
        : base(databaseContext, logger)
    {
    }

    /// <summary>
    /// Gets a setting by its key.
    /// </summary>
    public async Task<AppSetting?> GetByKeyAsync(string settingKey, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingKey);

        const string sql = @"
            SELECT setting_id, setting_key, setting_value, value_type, category, schema_version, 
                   description, is_sensitive, created_at, updated_at, updated_by
            FROM app_settings
            WHERE setting_key = $key";

        var results = await ExecuteReaderAsync(sql, MapAppSetting, parameters =>
        {
            parameters.Add(new SqliteParameter("$key", settingKey));
        }, ct).ConfigureAwait(false);

        return results.FirstOrDefault();
    }

    public override async Task<AppSetting?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT setting_id, setting_key, setting_value, value_type, category, schema_version, 
                   description, is_sensitive, created_at, updated_at, updated_by
            FROM app_settings
            WHERE setting_id = $id";

        var results = await ExecuteReaderAsync(sql, MapAppSetting, parameters =>
        {
            parameters.Add(new SqliteParameter("$id", id));
        }, ct).ConfigureAwait(false);

        return results.FirstOrDefault();
    }

    public override async Task<IReadOnlyList<AppSetting>> GetAllAsync(CancellationToken ct = default)
    {
        const string sql = @"
            SELECT setting_id, setting_key, setting_value, value_type, category, schema_version, 
                   description, is_sensitive, created_at, updated_at, updated_by
            FROM app_settings
            ORDER BY category, setting_key";

        return await ExecuteReaderAsync(sql, MapAppSetting, null, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets all settings in a specific category.
    /// </summary>
    public async Task<IReadOnlyList<AppSetting>> GetByCategoryAsync(string category, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);

        const string sql = @"
            SELECT setting_id, setting_key, setting_value, value_type, category, schema_version, 
                   description, is_sensitive, created_at, updated_at, updated_by
            FROM app_settings
            WHERE category = $category
            ORDER BY setting_key";

        return await ExecuteReaderAsync(sql, MapAppSetting, parameters =>
        {
            parameters.Add(new SqliteParameter("$category", category));
        }, ct).ConfigureAwait(false);
    }

    public override async Task<string> AddAsync(AppSetting entity, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.SettingId);
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.SettingKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.SettingValue);

        const string sql = @"
            INSERT INTO app_settings (setting_id, setting_key, setting_value, value_type, category, 
                                     schema_version, description, is_sensitive, created_at, updated_at, updated_by)
            VALUES ($setting_id, $setting_key, $setting_value, $value_type, $category, 
                   $schema_version, $description, $is_sensitive, $created_at, $updated_at, $updated_by)";

        await ExecuteNonQueryAsync(sql, parameters =>
        {
            parameters.Add(new SqliteParameter("$setting_id", entity.SettingId));
            parameters.Add(new SqliteParameter("$setting_key", entity.SettingKey));
            parameters.Add(new SqliteParameter("$setting_value", entity.SettingValue));
            parameters.Add(new SqliteParameter("$value_type", entity.ValueType ?? "json"));
            parameters.Add(new SqliteParameter("$category", entity.Category ?? "general"));
            parameters.Add(new SqliteParameter("$schema_version", entity.SchemaVersion));
            parameters.Add(new SqliteParameter("$description", (object?)entity.Description ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$is_sensitive", entity.IsSensitive ? 1 : 0));
            parameters.Add(new SqliteParameter("$created_at", entity.CreatedAt));
            parameters.Add(new SqliteParameter("$updated_at", entity.UpdatedAt));
            parameters.Add(new SqliteParameter("$updated_by", entity.UpdatedBy ?? "system"));
        }, ct).ConfigureAwait(false);

        Logger.LogInformation("Added setting {SettingKey}", entity.SettingKey);
        return entity.SettingId;
    }

    public override async Task UpdateAsync(AppSetting entity, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.SettingId);

        const string sql = @"
            UPDATE app_settings
            SET setting_value = $setting_value,
                value_type = $value_type,
                category = $category,
                schema_version = $schema_version,
                description = $description,
                is_sensitive = $is_sensitive,
                updated_at = $updated_at,
                updated_by = $updated_by
            WHERE setting_id = $setting_id";

        var rowsAffected = await ExecuteNonQueryAsync(sql, parameters =>
        {
            parameters.Add(new SqliteParameter("$setting_id", entity.SettingId));
            parameters.Add(new SqliteParameter("$setting_value", entity.SettingValue));
            parameters.Add(new SqliteParameter("$value_type", entity.ValueType ?? "json"));
            parameters.Add(new SqliteParameter("$category", entity.Category ?? "general"));
            parameters.Add(new SqliteParameter("$schema_version", entity.SchemaVersion));
            parameters.Add(new SqliteParameter("$description", (object?)entity.Description ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$is_sensitive", entity.IsSensitive ? 1 : 0));
            parameters.Add(new SqliteParameter("$updated_at", entity.UpdatedAt));
            parameters.Add(new SqliteParameter("$updated_by", entity.UpdatedBy ?? "system"));
        }, ct).ConfigureAwait(false);

        if (rowsAffected == 0)
        {
            Logger.LogWarning("Setting {SettingId} not found for update", entity.SettingId);
        }
        else
        {
            Logger.LogInformation("Updated setting {SettingId}", entity.SettingId);
        }
    }

    public override async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        // Also delete associated history
        const string deleteSqlDelete = @"
            DELETE FROM settings_version_history
            WHERE setting_key IN (SELECT setting_key FROM app_settings WHERE setting_id = $id)";

        await ExecuteNonQueryAsync(deleteSqlDelete, parameters =>
        {
            parameters.Add(new SqliteParameter("$id", id));
        }, ct).ConfigureAwait(false);

        const string sql = "DELETE FROM app_settings WHERE setting_id = $id";
        var rowsAffected = await ExecuteNonQueryAsync(sql, parameters =>
        {
            parameters.Add(new SqliteParameter("$id", id));
        }, ct).ConfigureAwait(false);

        if (rowsAffected > 0)
        {
            Logger.LogInformation("Deleted setting {SettingId}", id);
        }
    }

    /// <summary>
    /// Upserts a setting (insert or update based on key).
    /// Also records the change in settings_version_history.
    /// </summary>
    public async Task<string> UpsertAsync(AppSetting entity, string? reason = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.SettingKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.SettingValue);

        // Check if setting exists
        var existing = await GetByKeyAsync(entity.SettingKey, ct).ConfigureAwait(false);

        if (existing == null)
        {
            // Insert new setting
            if (string.IsNullOrWhiteSpace(entity.SettingId))
            {
                entity.SettingId = Guid.NewGuid().ToString();
            }
            entity.CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            entity.UpdatedAt = entity.CreatedAt;

            return await AddAsync(entity, ct).ConfigureAwait(false);
        }
        else
        {
            // Update existing setting and record in history
            entity.SettingId = existing.SettingId;
            entity.CreatedAt = existing.CreatedAt;
            entity.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            await UpdateAsync(entity, ct).ConfigureAwait(false);

            // Record in version history
            var historyEntry = new SettingsVersionHistory
            {
                HistoryId = Guid.NewGuid().ToString(),
                SettingKey = entity.SettingKey,
                OldValue = existing.SettingValue,
                NewValue = entity.SettingValue,
                SchemaVersion = entity.SchemaVersion,
                ChangedAt = entity.UpdatedAt,
                ChangedBy = entity.UpdatedBy ?? "system",
                Reason = reason ?? "update"
            };

            await AddHistoryAsync(historyEntry, ct).ConfigureAwait(false);

            return entity.SettingId;
        }
    }

    // ===== Settings Version History Methods =====

    /// <summary>
    /// Gets the version history for a specific setting key.
    /// </summary>
    public async Task<IReadOnlyList<SettingsVersionHistory>> GetHistoryByKeyAsync(string settingKey, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingKey);

        const string sql = @"
            SELECT history_id, setting_key, old_value, new_value, schema_version, 
                   changed_at, changed_by, reason
            FROM settings_version_history
            WHERE setting_key = $key
            ORDER BY changed_at DESC";

        var connection = await DatabaseContext.GetConnectionAsync(ct).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.Add(new SqliteParameter("$key", settingKey));

            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            var results = new List<SettingsVersionHistory>();
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                results.Add(MapSettingsVersionHistory(reader));
            }
            return results.AsReadOnly();
        }
    }

    /// <summary>
    /// Gets all version history entries.
    /// </summary>
    public async Task<IReadOnlyList<SettingsVersionHistory>> GetAllHistoryAsync(CancellationToken ct = default)
    {
        const string sql = @"
            SELECT history_id, setting_key, old_value, new_value, schema_version, 
                   changed_at, changed_by, reason
            FROM settings_version_history
            ORDER BY changed_at DESC";

        var connection = await DatabaseContext.GetConnectionAsync(CancellationToken.None).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;

            await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
            var results = new List<SettingsVersionHistory>();
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                results.Add(MapSettingsVersionHistory(reader));
            }
            return results.AsReadOnly();
        }
    }

    /// <summary>
    /// Gets version history entries for a specific schema version.
    /// Useful for tracking changes during migrations.
    /// </summary>
    public async Task<IReadOnlyList<SettingsVersionHistory>> GetHistoryBySchemaVersionAsync(int schemaVersion, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT history_id, setting_key, old_value, new_value, schema_version, 
                   changed_at, changed_by, reason
            FROM settings_version_history
            WHERE schema_version = $schema_version
            ORDER BY changed_at DESC";

        var connection = await DatabaseContext.GetConnectionAsync(ct).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.Add(new SqliteParameter("$schema_version", schemaVersion));

            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            var results = new List<SettingsVersionHistory>();
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                results.Add(MapSettingsVersionHistory(reader));
            }
            return results.AsReadOnly();
        }
    }

    /// <summary>
    /// Adds a version history entry.
    /// </summary>
    public async Task<string> AddHistoryAsync(SettingsVersionHistory history, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(history);
        ArgumentException.ThrowIfNullOrWhiteSpace(history.HistoryId);
        ArgumentException.ThrowIfNullOrWhiteSpace(history.SettingKey);

        const string sql = @"
            INSERT INTO settings_version_history (history_id, setting_key, old_value, new_value, 
                                                 schema_version, changed_at, changed_by, reason)
            VALUES ($history_id, $setting_key, $old_value, $new_value, 
                   $schema_version, $changed_at, $changed_by, $reason)";

        await ExecuteNonQueryAsync(sql, parameters =>
        {
            parameters.Add(new SqliteParameter("$history_id", history.HistoryId));
            parameters.Add(new SqliteParameter("$setting_key", history.SettingKey));
            parameters.Add(new SqliteParameter("$old_value", (object?)history.OldValue ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$new_value", history.NewValue));
            parameters.Add(new SqliteParameter("$schema_version", history.SchemaVersion));
            parameters.Add(new SqliteParameter("$changed_at", history.ChangedAt));
            parameters.Add(new SqliteParameter("$changed_by", history.ChangedBy ?? "system"));
            parameters.Add(new SqliteParameter("$reason", (object?)history.Reason ?? DBNull.Value));
        }, ct).ConfigureAwait(false);

        Logger.LogInformation("Recorded setting change for {SettingKey}: {Reason}", history.SettingKey, history.Reason);
        return history.HistoryId;
    }

    // ===== Helper Methods =====

    private static AppSetting MapAppSetting(SqliteDataReader reader)
    {
        return new AppSetting
        {
            SettingId = reader.GetString(0),
            SettingKey = reader.GetString(1),
            SettingValue = reader.GetString(2),
            ValueType = reader.GetString(3),
            Category = reader.GetString(4),
            SchemaVersion = reader.GetInt32(5),
            Description = reader.IsDBNull(6) ? null : reader.GetString(6),
            IsSensitive = reader.GetInt32(7) == 1,
            CreatedAt = reader.GetInt64(8),
            UpdatedAt = reader.GetInt64(9),
            UpdatedBy = reader.GetString(10)
        };
    }

    private static SettingsVersionHistory MapSettingsVersionHistory(SqliteDataReader reader)
    {
        return new SettingsVersionHistory
        {
            HistoryId = reader.GetString(0),
            SettingKey = reader.GetString(1),
            OldValue = reader.IsDBNull(2) ? null : reader.GetString(2),
            NewValue = reader.GetString(3),
            SchemaVersion = reader.GetInt32(4),
            ChangedAt = reader.GetInt64(5),
            ChangedBy = reader.GetString(6),
            Reason = reader.IsDBNull(7) ? null : reader.GetString(7)
        };
    }
}
