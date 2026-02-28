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
            SELECT project_id, name, description, root_paths, created_at, updated_at, status, config_json
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
            SELECT project_id, name, description, root_paths, created_at, updated_at, status, config_json
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
            INSERT INTO projects (project_id, name, description, root_paths, created_at, updated_at, status, config_json)
            VALUES ($project_id, $name, $description, $root_paths, $created_at, $updated_at, $status, $config_json)";

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
                config_json = $config_json
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
            SELECT project_id, name, description, root_paths, created_at, updated_at, status, config_json
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
            SELECT project_id, name, description, root_paths, created_at, updated_at, status, config_json
            FROM projects
            WHERE name = $name";

        var results = await ExecuteReaderAsync(sql, MapProject, parameters =>
        {
            parameters.Add(new SqliteParameter("$name", name));
        }, ct).ConfigureAwait(false);

        return results.FirstOrDefault();
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
            ConfigJson = reader.IsDBNull(7) ? null : reader.GetString(7)
        };
    }
}
