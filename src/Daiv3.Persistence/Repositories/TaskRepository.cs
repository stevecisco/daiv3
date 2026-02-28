using Daiv3.Persistence.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Daiv3.Persistence.Repositories;

/// <summary>
/// Repository for managing project task entities.
/// </summary>
public class TaskRepository : RepositoryBase<ProjectTask>
{
    public TaskRepository(IDatabaseContext databaseContext, ILogger<TaskRepository> logger)
        : base(databaseContext, logger)
    {
    }

    public override async Task<ProjectTask?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT task_id, project_id, title, description, status, priority, scheduled_at, completed_at, dependencies_json, result_json, created_at, updated_at
            FROM tasks
            WHERE task_id = $id";

        var results = await ExecuteReaderAsync(sql, MapTask, parameters =>
        {
            parameters.Add(new SqliteParameter("$id", id));
        }, ct).ConfigureAwait(false);

        return results.FirstOrDefault();
    }

    public override async Task<IReadOnlyList<ProjectTask>> GetAllAsync(CancellationToken ct = default)
    {
        const string sql = @"
            SELECT task_id, project_id, title, description, status, priority, scheduled_at, completed_at, dependencies_json, result_json, created_at, updated_at
            FROM tasks
            ORDER BY priority DESC, created_at ASC";

        return await ExecuteReaderAsync(sql, MapTask, null, ct).ConfigureAwait(false);
    }

    public override async Task<string> AddAsync(ProjectTask entity, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        const string sql = @"
            INSERT INTO tasks (task_id, project_id, title, description, status, priority, scheduled_at, completed_at, dependencies_json, result_json, created_at, updated_at)
            VALUES ($task_id, $project_id, $title, $description, $status, $priority, $scheduled_at, $completed_at, $dependencies_json, $result_json, $created_at, $updated_at)";

        await ExecuteNonQueryAsync(sql, parameters =>
        {
            parameters.Add(new SqliteParameter("$task_id", entity.TaskId));
            parameters.Add(new SqliteParameter("$project_id", (object?)entity.ProjectId ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$title", entity.Title));
            parameters.Add(new SqliteParameter("$description", (object?)entity.Description ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$status", entity.Status));
            parameters.Add(new SqliteParameter("$priority", entity.Priority));
            parameters.Add(new SqliteParameter("$scheduled_at", (object?)entity.ScheduledAt ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$completed_at", (object?)entity.CompletedAt ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$dependencies_json", (object?)entity.DependenciesJson ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$result_json", (object?)entity.ResultJson ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$created_at", entity.CreatedAt));
            parameters.Add(new SqliteParameter("$updated_at", entity.UpdatedAt));
        }, ct).ConfigureAwait(false);

        Logger.LogInformation("Added task {TaskId} to project {ProjectId}", entity.TaskId, entity.ProjectId);
        return entity.TaskId;
    }

    public override async Task UpdateAsync(ProjectTask entity, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        const string sql = @"
            UPDATE tasks
            SET project_id = $project_id,
                title = $title,
                description = $description,
                status = $status,
                priority = $priority,
                scheduled_at = $scheduled_at,
                completed_at = $completed_at,
                dependencies_json = $dependencies_json,
                result_json = $result_json,
                updated_at = $updated_at
            WHERE task_id = $task_id";

        var rowsAffected = await ExecuteNonQueryAsync(sql, parameters =>
        {
            parameters.Add(new SqliteParameter("$task_id", entity.TaskId));
            parameters.Add(new SqliteParameter("$project_id", (object?)entity.ProjectId ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$title", entity.Title));
            parameters.Add(new SqliteParameter("$description", (object?)entity.Description ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$status", entity.Status));
            parameters.Add(new SqliteParameter("$priority", entity.Priority));
            parameters.Add(new SqliteParameter("$scheduled_at", (object?)entity.ScheduledAt ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$completed_at", (object?)entity.CompletedAt ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$dependencies_json", (object?)entity.DependenciesJson ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$result_json", (object?)entity.ResultJson ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$updated_at", entity.UpdatedAt));
        }, ct).ConfigureAwait(false);

        if (rowsAffected == 0)
        {
            Logger.LogWarning("Task {TaskId} not found for update", entity.TaskId);
        }
        else
        {
            Logger.LogInformation("Updated task {TaskId}", entity.TaskId);
        }
    }

    public override async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        const string sql = "DELETE FROM tasks WHERE task_id = $id";

        var rowsAffected = await ExecuteNonQueryAsync(sql, parameters =>
        {
            parameters.Add(new SqliteParameter("$id", id));
        }, ct).ConfigureAwait(false);

        if (rowsAffected == 0)
        {
            Logger.LogWarning("Task {TaskId} not found for deletion", id);
        }
        else
        {
            Logger.LogInformation("Deleted task {TaskId}", id);
        }
    }

    /// <summary>
    /// Gets all tasks for a specific project.
    /// </summary>
    public async Task<IReadOnlyList<ProjectTask>> GetByProjectIdAsync(string projectId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT task_id, project_id, title, description, status, priority, scheduled_at, completed_at, dependencies_json, result_json, created_at, updated_at
            FROM tasks
            WHERE project_id = $project_id
            ORDER BY priority DESC, created_at ASC";

        return await ExecuteReaderAsync(sql, MapTask, parameters =>
        {
            parameters.Add(new SqliteParameter("$project_id", projectId));
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets tasks by status.
    /// </summary>
    public async Task<IReadOnlyList<ProjectTask>> GetByStatusAsync(string status, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT task_id, project_id, title, description, status, priority, scheduled_at, completed_at, dependencies_json, result_json, created_at, updated_at
            FROM tasks
            WHERE status = $status
            ORDER BY priority DESC, created_at ASC";

        return await ExecuteReaderAsync(sql, MapTask, parameters =>
        {
            parameters.Add(new SqliteParameter("$status", status));
        }, ct).ConfigureAwait(false);
    }

    private static ProjectTask MapTask(SqliteDataReader reader)
    {
        return new ProjectTask
        {
            TaskId = reader.GetString(0),
            ProjectId = reader.IsDBNull(1) ? null : reader.GetString(1),
            Title = reader.GetString(2),
            Description = reader.IsDBNull(3) ? null : reader.GetString(3),
            Status = reader.GetString(4),
            Priority = reader.GetInt32(5),
            ScheduledAt = reader.IsDBNull(6) ? null : reader.GetInt64(6),
            CompletedAt = reader.IsDBNull(7) ? null : reader.GetInt64(7),
            DependenciesJson = reader.IsDBNull(8) ? null : reader.GetString(8),
            ResultJson = reader.IsDBNull(9) ? null : reader.GetString(9),
            CreatedAt = reader.GetInt64(10),
            UpdatedAt = reader.GetInt64(11)
        };
    }
}