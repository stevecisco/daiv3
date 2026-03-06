using Daiv3.Persistence.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Daiv3.Persistence.Repositories;

/// <summary>
/// Repository for managing Project entities.
/// </summary>
public class ProjectRepository : RepositoryBase<Project>
{
    public ProjectRepository(IDatabaseContext databaseContext, ILogger<ProjectRepository> logger)
        : base(databaseContext, logger)
    {
    }

    public override async Task<Project?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT project_id, name, description, root_paths, created_at, updated_at, status, config_json,
                   priority, progress_percent, deadline, assigned_agent, estimated_cost, actual_cost, completed_at, parent_project_id
            FROM projects
            WHERE project_id = $id";

        var results = await ExecuteReaderAsync(sql, MapProject, parameters =>
        {
            parameters.Add(new SqliteParameter("$id", id));
        }, ct).ConfigureAwait(false);

        return results.FirstOrDefault();
    }

    public override async Task<IReadOnlyList<Project>> GetAllAsync(CancellationToken ct = default)
    {
        const string sql = @"
            SELECT project_id, name, description, root_paths, created_at, updated_at, status, config_json,
                   priority, progress_percent, deadline, assigned_agent, estimated_cost, actual_cost, completed_at, parent_project_id
            FROM projects
            ORDER BY updated_at DESC";

        return await ExecuteReaderAsync(sql, MapProject, null, ct).ConfigureAwait(false);
    }

    public override async Task<string> AddAsync(Project entity, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (string.IsNullOrWhiteSpace(entity.RootPaths))
        {
            throw new ArgumentException("Project root paths are required.", nameof(entity));
        }

        const string sql = @"
            INSERT INTO projects (project_id, name, description, root_paths, created_at, updated_at, status, config_json,
                                priority, progress_percent, deadline, assigned_agent, estimated_cost, actual_cost, completed_at, parent_project_id)
            VALUES ($project_id, $name, $description, $root_paths, $created_at, $updated_at, $status, $config_json,
                    $priority, $progress_percent, $deadline, $assigned_agent, $estimated_cost, $actual_cost, $completed_at, $parent_project_id)";

        await ExecuteNonQueryAsync(sql, parameters =>
        {
            parameters.Add(new SqliteParameter("$project_id", entity.ProjectId));
            parameters.Add(new SqliteParameter("$name", entity.Name));
            parameters.Add(new SqliteParameter("$description", (object?)entity.Description ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$root_paths", entity.RootPaths));
            parameters.Add(new SqliteParameter("$created_at", entity.CreatedAt));
            parameters.Add(new SqliteParameter("$updated_at", entity.UpdatedAt));
            parameters.Add(new SqliteParameter("$status", entity.Status));
            parameters.Add(new SqliteParameter("$config_json", (object?)entity.ConfigJson ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$priority", entity.Priority));
            parameters.Add(new SqliteParameter("$progress_percent", entity.ProgressPercent));
            parameters.Add(new SqliteParameter("$deadline", (object?)entity.Deadline ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$assigned_agent", (object?)entity.AssignedAgent ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$estimated_cost", (object?)entity.EstimatedCost ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$actual_cost", (object?)entity.ActualCost ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$completed_at", (object?)entity.CompletedAt ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$parent_project_id", (object?)entity.ParentProjectId ?? DBNull.Value));
        }, ct).ConfigureAwait(false);

        Logger.LogInformation("Added project {ProjectId}: {Name}", entity.ProjectId, entity.Name);
        return entity.ProjectId;
    }

    public override async Task UpdateAsync(Project entity, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (string.IsNullOrWhiteSpace(entity.RootPaths))
        {
            throw new ArgumentException("Project root paths are required.", nameof(entity));
        }

        const string sql = @"
            UPDATE projects
            SET name = $name,
                description = $description,
                root_paths = $root_paths,
                updated_at = $updated_at,
                status = $status,
                config_json = $config_json,
                priority = $priority,
                progress_percent = $progress_percent,
                deadline = $deadline,
                assigned_agent = $assigned_agent,
                estimated_cost = $estimated_cost,
                actual_cost = $actual_cost,
                completed_at = $completed_at,
                parent_project_id = $parent_project_id
            WHERE project_id = $project_id";

        var rowsAffected = await ExecuteNonQueryAsync(sql, parameters =>
        {
            parameters.Add(new SqliteParameter("$project_id", entity.ProjectId));
            parameters.Add(new SqliteParameter("$name", entity.Name));
            parameters.Add(new SqliteParameter("$description", (object?)entity.Description ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$root_paths", entity.RootPaths));
            parameters.Add(new SqliteParameter("$updated_at", entity.UpdatedAt));
            parameters.Add(new SqliteParameter("$status", entity.Status));
            parameters.Add(new SqliteParameter("$config_json", (object?)entity.ConfigJson ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$priority", entity.Priority));
            parameters.Add(new SqliteParameter("$progress_percent", entity.ProgressPercent));
            parameters.Add(new SqliteParameter("$deadline", (object?)entity.Deadline ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$assigned_agent", (object?)entity.AssignedAgent ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$estimated_cost", (object?)entity.EstimatedCost ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$actual_cost", (object?)entity.ActualCost ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$completed_at", (object?)entity.CompletedAt ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$parent_project_id", (object?)entity.ParentProjectId ?? DBNull.Value));
        }, ct).ConfigureAwait(false);

        if (rowsAffected == 0)
        {
            Logger.LogWarning("Project {ProjectId} not found for update", entity.ProjectId);
        }
        else
        {
            Logger.LogInformation("Updated project {ProjectId}", entity.ProjectId);
        }
    }

    public override async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        const string sql = "DELETE FROM projects WHERE project_id = $id";

        var rowsAffected = await ExecuteNonQueryAsync(sql, parameters =>
        {
            parameters.Add(new SqliteParameter("$id", id));
        }, ct).ConfigureAwait(false);

        if (rowsAffected == 0)
        {
            Logger.LogWarning("Project {ProjectId} not found for deletion", id);
        }
        else
        {
            Logger.LogInformation("Deleted project {ProjectId}", id);
        }
    }

    /// <summary>
    /// Gets projects by status.
    /// </summary>
    public async Task<IReadOnlyList<Project>> GetByStatusAsync(string status, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT project_id, name, description, root_paths, created_at, updated_at, status, config_json,
                   priority, progress_percent, deadline, assigned_agent, estimated_cost, actual_cost, completed_at, parent_project_id
            FROM projects
            WHERE status = $status
            ORDER BY updated_at DESC";

        return await ExecuteReaderAsync(sql, MapProject, parameters =>
        {
            parameters.Add(new SqliteParameter("$status", status));
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets a project by name.
    /// </summary>
    public async Task<Project?> GetByNameAsync(string name, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT project_id, name, description, root_paths, created_at, updated_at, status, config_json,
                   priority, progress_percent, deadline, assigned_agent, estimated_cost, actual_cost, completed_at, parent_project_id
            FROM projects
            WHERE name = $name";

        var results = await ExecuteReaderAsync(sql, MapProject, parameters =>
        {
            parameters.Add(new SqliteParameter("$name", name));
        }, ct).ConfigureAwait(false);

        return results.FirstOrDefault();
    }

    /// <summary>
    /// Gets projects assigned to a specific agent (CT-REQ-011: By Assignment view).
    /// </summary>
    public async Task<IReadOnlyList<Project>> GetByAssignedAgentAsync(string? agentId, CancellationToken ct = default)
    {
        string sql;
        if (agentId == null)
        {
            sql = @"
                SELECT project_id, name, description, root_paths, created_at, updated_at, status, config_json,
                       priority, progress_percent, deadline, assigned_agent, estimated_cost, actual_cost, completed_at, parent_project_id
                FROM projects
                WHERE assigned_agent IS NULL
                ORDER BY priority, updated_at DESC";
            return await ExecuteReaderAsync(sql, MapProject, null, ct).ConfigureAwait(false);
        }
        else
        {
            sql = @"
                SELECT project_id, name, description, root_paths, created_at, updated_at, status, config_json,
                       priority, progress_percent, deadline, assigned_agent, estimated_cost, actual_cost, completed_at, parent_project_id
                FROM projects
                WHERE assigned_agent = $agent_id
                ORDER BY priority, updated_at DESC";
            return await ExecuteReaderAsync(sql, MapProject, parameters =>
            {
                parameters.Add(new SqliteParameter("$agent_id", agentId));
            }, ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Gets projects by priority (CT-REQ-011: Priority view).
    /// </summary>
    public async Task<IReadOnlyList<Project>> GetByPriorityAsync(CancellationToken ct = default)
    {
        const string sql = @"
            SELECT project_id, name, description, root_paths, created_at, updated_at, status, config_json,
                   priority, progress_percent, deadline, assigned_agent, estimated_cost, actual_cost, completed_at, parent_project_id
            FROM projects
            WHERE status != 'deleted'
            ORDER BY priority, deadline ASC NULLS LAST, updated_at DESC";

        return await ExecuteReaderAsync(sql, MapProject, null, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets sub-projects for a specific parent project (CT-REQ-011: hierarchical tree view).
    /// </summary>
    public async Task<IReadOnlyList<Project>> GetSubProjectsAsync(string parentProjectId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT project_id, name, description, root_paths, created_at, updated_at, status, config_json,
                   priority, progress_percent, deadline, assigned_agent, estimated_cost, actual_cost, completed_at, parent_project_id
            FROM projects
            WHERE parent_project_id = $parent_id
            ORDER BY priority, updated_at DESC";

        return await ExecuteReaderAsync(sql, MapProject, parameters =>
        {
            parameters.Add(new SqliteParameter("$parent_id", parentProjectId));
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets root-level projects (no parent) for hierarchical tree view (CT-REQ-011).
    /// </summary>
    public async Task<IReadOnlyList<Project>> GetRootProjectsAsync(CancellationToken ct = default)
    {
        const string sql = @"
            SELECT project_id, name, description, root_paths, created_at, updated_at, status, config_json,
                   priority, progress_percent, deadline, assigned_agent, estimated_cost, actual_cost, completed_at, parent_project_id
            FROM projects
            WHERE parent_project_id IS NULL AND status != 'deleted'
            ORDER BY priority, updated_at DESC";

        return await ExecuteReaderAsync(sql, MapProject, null, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets projects approaching deadline (within specified days) for dashboard alerts (CT-REQ-011).
    /// </summary>
    public async Task<IReadOnlyList<Project>> GetProjectsApproachingDeadlineAsync(int daysAhead, CancellationToken ct = default)
    {
        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var futureUnix = DateTimeOffset.UtcNow.AddDays(daysAhead).ToUnixTimeSeconds();

        const string sql = @"
            SELECT project_id, name, description, root_paths, created_at, updated_at, status, config_json,
                   priority, progress_percent, deadline, assigned_agent, estimated_cost, actual_cost, completed_at, parent_project_id
            FROM projects
            WHERE deadline IS NOT NULL 
              AND deadline BETWEEN $now AND $future
              AND status IN ('active', 'pending')
            ORDER BY deadline ASC";

        return await ExecuteReaderAsync(sql, MapProject, parameters =>
        {
            parameters.Add(new SqliteParameter("$now", nowUnix));
            parameters.Add(new SqliteParameter("$future", futureUnix));
        }, ct).ConfigureAwait(false);
    }

    private static Project MapProject(SqliteDataReader reader)
    {
        return new Project
        {
            ProjectId = reader.GetString(0),
            Name = reader.GetString(1),
            Description = reader.IsDBNull(2) ? null : reader.GetString(2),
            RootPaths = reader.GetString(3),
            CreatedAt = reader.GetInt64(4),
            UpdatedAt = reader.GetInt64(5),
            Status = reader.GetString(6),
            ConfigJson = reader.IsDBNull(7) ? null : reader.GetString(7),
            Priority = reader.GetInt32(8),
            ProgressPercent = reader.GetDouble(9),
            Deadline = reader.IsDBNull(10) ? null : reader.GetInt64(10),
            AssignedAgent = reader.IsDBNull(11) ? null : reader.GetString(11),
            EstimatedCost = reader.IsDBNull(12) ? null : reader.GetDouble(12),
            ActualCost = reader.IsDBNull(13) ? null : reader.GetDouble(13),
            CompletedAt = reader.IsDBNull(14) ? null : reader.GetInt64(14),
            ParentProjectId = reader.IsDBNull(15) ? null : reader.GetString(15)
        };
    }
}
