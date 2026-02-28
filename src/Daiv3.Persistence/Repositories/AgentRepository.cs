using Daiv3.Persistence.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Daiv3.Persistence.Repositories;

/// <summary>
/// Repository for managing agent entities.
/// Handles persistence of agent definitions with user-editable JSON configuration.
/// </summary>
public class AgentRepository : RepositoryBase<Agent>
{
    public AgentRepository(IDatabaseContext databaseContext, ILogger<AgentRepository> logger)
        : base(databaseContext, logger)
    {
    }

    public override async Task<Agent?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT agent_id, name, purpose, enabled_skills_json, config_json, status, created_at, updated_at
            FROM agents
            WHERE agent_id = $id";

        var results = await ExecuteReaderAsync(sql, MapAgent, parameters =>
        {
            parameters.Add(new SqliteParameter("$id", id));
        }, ct).ConfigureAwait(false);

        return results.FirstOrDefault();
    }

    public override async Task<IReadOnlyList<Agent>> GetAllAsync(CancellationToken ct = default)
    {
        const string sql = @"
            SELECT agent_id, name, purpose, enabled_skills_json, config_json, status, created_at, updated_at
            FROM agents
            WHERE status != 'deleted'
            ORDER BY created_at DESC";

        return await ExecuteReaderAsync(sql, MapAgent, null, ct).ConfigureAwait(false);
    }

    public override async Task<string> AddAsync(Agent entity, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.AgentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.Purpose);

        const string sql = @"
            INSERT INTO agents (agent_id, name, purpose, enabled_skills_json, config_json, status, created_at, updated_at)
            VALUES ($agent_id, $name, $purpose, $enabled_skills_json, $config_json, $status, $created_at, $updated_at)";

        await ExecuteNonQueryAsync(sql, parameters =>
        {
            parameters.Add(new SqliteParameter("$agent_id", entity.AgentId));
            parameters.Add(new SqliteParameter("$name", entity.Name));
            parameters.Add(new SqliteParameter("$purpose", entity.Purpose));
            parameters.Add(new SqliteParameter("$enabled_skills_json", (object?)entity.EnabledSkillsJson ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$config_json", (object?)entity.ConfigJson ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$status", entity.Status ?? "active"));
            parameters.Add(new SqliteParameter("$created_at", entity.CreatedAt));
            parameters.Add(new SqliteParameter("$updated_at", entity.UpdatedAt));
        }, ct).ConfigureAwait(false);

        Logger.LogInformation("Added agent {AgentId} '{Name}'", entity.AgentId, entity.Name);
        return entity.AgentId;
    }

    public override async Task UpdateAsync(Agent entity, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.AgentId);

        const string sql = @"
            UPDATE agents
            SET name = $name,
                purpose = $purpose,
                enabled_skills_json = $enabled_skills_json,
                config_json = $config_json,
                status = $status,
                updated_at = $updated_at
            WHERE agent_id = $agent_id";

        var rowsAffected = await ExecuteNonQueryAsync(sql, parameters =>
        {
            parameters.Add(new SqliteParameter("$agent_id", entity.AgentId));
            parameters.Add(new SqliteParameter("$name", entity.Name));
            parameters.Add(new SqliteParameter("$purpose", entity.Purpose));
            parameters.Add(new SqliteParameter("$enabled_skills_json", (object?)entity.EnabledSkillsJson ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$config_json", (object?)entity.ConfigJson ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$status", entity.Status ?? "active"));
            parameters.Add(new SqliteParameter("$updated_at", entity.UpdatedAt));
        }, ct).ConfigureAwait(false);

        if (rowsAffected == 0)
        {
            Logger.LogWarning("Agent {AgentId} not found for update", entity.AgentId);
        }
        else
        {
            Logger.LogInformation("Updated agent {AgentId} '{Name}'", entity.AgentId, entity.Name);
        }
    }

    public override async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        const string sql = @"
            UPDATE agents
            SET status = 'deleted',
                updated_at = $updated_at
            WHERE agent_id = $id";

        var rowsAffected = await ExecuteNonQueryAsync(sql, parameters =>
        {
            parameters.Add(new SqliteParameter("$id", id));
            parameters.Add(new SqliteParameter("$updated_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
        }, ct).ConfigureAwait(false);

        if (rowsAffected == 0)
        {
            Logger.LogWarning("Agent {AgentId} not found for deletion", id);
        }
        else
        {
            Logger.LogInformation("Deleted agent {AgentId}", id);
        }
    }

    /// <summary>
    /// Gets all active agents (excluding archived and deleted).
    /// </summary>
    public async Task<IReadOnlyList<Agent>> GetActiveAsync(CancellationToken ct = default)
    {
        const string sql = @"
            SELECT agent_id, name, purpose, enabled_skills_json, config_json, status, created_at, updated_at
            FROM agents
            WHERE status = 'active'
            ORDER BY name ASC";

        return await ExecuteReaderAsync(sql, MapAgent, null, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets agents by status.
    /// </summary>
    public async Task<IReadOnlyList<Agent>> GetByStatusAsync(string status, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(status);

        const string sql = @"
            SELECT agent_id, name, purpose, enabled_skills_json, config_json, status, created_at, updated_at
            FROM agents
            WHERE status = $status
            ORDER BY name ASC";

        return await ExecuteReaderAsync(sql, MapAgent, parameters =>
        {
            parameters.Add(new SqliteParameter("$status", status));
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets an agent by name.
    /// </summary>
    public async Task<Agent?> GetByNameAsync(string name, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        const string sql = @"
            SELECT agent_id, name, purpose, enabled_skills_json, config_json, status, created_at, updated_at
            FROM agents
            WHERE name = $name
            AND status != 'deleted'";

        var results = await ExecuteReaderAsync(sql, MapAgent, parameters =>
        {
            parameters.Add(new SqliteParameter("$name", name));
        }, ct).ConfigureAwait(false);

        return results.FirstOrDefault();
    }

    private static Agent MapAgent(SqliteDataReader reader)
    {
        return new Agent
        {
            AgentId = reader.GetString(0),
            Name = reader.GetString(1),
            Purpose = reader.GetString(2),
            EnabledSkillsJson = reader.IsDBNull(3) ? null : reader.GetString(3),
            ConfigJson = reader.IsDBNull(4) ? null : reader.GetString(4),
            Status = reader.GetString(5),
            CreatedAt = reader.GetInt64(6),
            UpdatedAt = reader.GetInt64(7)
        };
    }
}
