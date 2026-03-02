using Daiv3.Persistence.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Daiv3.Persistence.Repositories;

/// <summary>
/// Repository for managing learning entities.
/// Handles persistence of learning records with provenance and timestamps per LM-DATA-001.
/// </summary>
public class LearningRepository : RepositoryBase<Learning>
{
    public LearningRepository(IDatabaseContext databaseContext, ILogger<LearningRepository> logger)
        : base(databaseContext, logger)
    {
    }

    public override async Task<Learning?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT learning_id, title, description, trigger_type, scope, source_agent, source_task_id,
                   embedding_blob, embedding_dimensions, tags, confidence, status, times_applied,
                   created_at, updated_at, created_by
            FROM learnings
            WHERE learning_id = $id";

        var results = await ExecuteReaderAsync(sql, MapLearning, parameters =>
        {
            parameters.Add(new SqliteParameter("$id", id));
        }, ct).ConfigureAwait(false);

        return results.FirstOrDefault();
    }

    public override async Task<IReadOnlyList<Learning>> GetAllAsync(CancellationToken ct = default)
    {
        const string sql = @"
            SELECT learning_id, title, description, trigger_type, scope, source_agent, source_task_id,
                   embedding_blob, embedding_dimensions, tags, confidence, status, times_applied,
                   created_at, updated_at, created_by
            FROM learnings
            ORDER BY created_at DESC";

        return await ExecuteReaderAsync(sql, MapLearning, null, ct).ConfigureAwait(false);
    }

    public override async Task<string> AddAsync(Learning entity, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.LearningId);
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.Title);
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.Description);
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.TriggerType);
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.Scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.CreatedBy);

        const string sql = @"
            INSERT INTO learnings (
                learning_id, title, description, trigger_type, scope, source_agent, source_task_id,
                embedding_blob, embedding_dimensions, tags, confidence, status, times_applied,
                created_at, updated_at, created_by
            )
            VALUES (
                $learning_id, $title, $description, $trigger_type, $scope, $source_agent, $source_task_id,
                $embedding_blob, $embedding_dimensions, $tags, $confidence, $status, $times_applied,
                $created_at, $updated_at, $created_by
            )";

        await ExecuteNonQueryAsync(sql, parameters =>
        {
            parameters.Add(new SqliteParameter("$learning_id", entity.LearningId));
            parameters.Add(new SqliteParameter("$title", entity.Title));
            parameters.Add(new SqliteParameter("$description", entity.Description));
            parameters.Add(new SqliteParameter("$trigger_type", entity.TriggerType));
            parameters.Add(new SqliteParameter("$scope", entity.Scope));
            parameters.Add(new SqliteParameter("$source_agent", (object?)entity.SourceAgent ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$source_task_id", (object?)entity.SourceTaskId ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$embedding_blob", (object?)entity.EmbeddingBlob ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$embedding_dimensions", entity.EmbeddingDimensions.HasValue ? entity.EmbeddingDimensions.Value : DBNull.Value));
            parameters.Add(new SqliteParameter("$tags", (object?)entity.Tags ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$confidence", entity.Confidence));
            parameters.Add(new SqliteParameter("$status", entity.Status));
            parameters.Add(new SqliteParameter("$times_applied", entity.TimesApplied));
            parameters.Add(new SqliteParameter("$created_at", entity.CreatedAt));
            parameters.Add(new SqliteParameter("$updated_at", entity.UpdatedAt));
            parameters.Add(new SqliteParameter("$created_by", entity.CreatedBy));
        }, ct).ConfigureAwait(false);

        Logger.LogInformation("Added learning {LearningId} '{Title}' (scope: {Scope}, confidence: {Confidence})",
            entity.LearningId, entity.Title, entity.Scope, entity.Confidence);
        return entity.LearningId;
    }

    public override async Task UpdateAsync(Learning entity, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.LearningId);

        const string sql = @"
            UPDATE learnings
            SET title = $title,
                description = $description,
                trigger_type = $trigger_type,
                scope = $scope,
                source_agent = $source_agent,
                source_task_id = $source_task_id,
                embedding_blob = $embedding_blob,
                embedding_dimensions = $embedding_dimensions,
                tags = $tags,
                confidence = $confidence,
                status = $status,
                times_applied = $times_applied,
                updated_at = $updated_at,
                created_by = $created_by
            WHERE learning_id = $learning_id";

        var rowsAffected = await ExecuteNonQueryAsync(sql, parameters =>
        {
            parameters.Add(new SqliteParameter("$learning_id", entity.LearningId));
            parameters.Add(new SqliteParameter("$title", entity.Title));
            parameters.Add(new SqliteParameter("$description", entity.Description));
            parameters.Add(new SqliteParameter("$trigger_type", entity.TriggerType));
            parameters.Add(new SqliteParameter("$scope", entity.Scope));
            parameters.Add(new SqliteParameter("$source_agent", (object?)entity.SourceAgent ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$source_task_id", (object?)entity.SourceTaskId ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$embedding_blob", (object?)entity.EmbeddingBlob ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$embedding_dimensions", entity.EmbeddingDimensions.HasValue ? entity.EmbeddingDimensions.Value : DBNull.Value));
            parameters.Add(new SqliteParameter("$tags", (object?)entity.Tags ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$confidence", entity.Confidence));
            parameters.Add(new SqliteParameter("$status", entity.Status));
            parameters.Add(new SqliteParameter("$times_applied", entity.TimesApplied));
            parameters.Add(new SqliteParameter("$updated_at", entity.UpdatedAt));
            parameters.Add(new SqliteParameter("$created_by", entity.CreatedBy));
        }, ct).ConfigureAwait(false);

        if (rowsAffected == 0)
        {
            Logger.LogWarning("Learning {LearningId} not found for update", entity.LearningId);
        }
        else
        {
            Logger.LogInformation("Updated learning {LearningId} '{Title}'", entity.LearningId, entity.Title);
        }
    }

    public override async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        const string sql = @"
            UPDATE learnings
            SET status = 'Archived',
                updated_at = $updated_at
            WHERE learning_id = $id";

        var rowsAffected = await ExecuteNonQueryAsync(sql, parameters =>
        {
            parameters.Add(new SqliteParameter("$id", id));
            parameters.Add(new SqliteParameter("$updated_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
        }, ct).ConfigureAwait(false);

        if (rowsAffected == 0)
        {
            Logger.LogWarning("Learning {LearningId} not found for deletion", id);
        }
        else
        {
            Logger.LogInformation("Archived learning {LearningId}", id);
        }
    }

    /// <summary>
    /// Gets all active learnings (excluding Suppressed and Archived).
    /// </summary>
    public async Task<IReadOnlyList<Learning>> GetActiveAsync(CancellationToken ct = default)
    {
        const string sql = @"
            SELECT learning_id, title, description, trigger_type, scope, source_agent, source_task_id,
                   embedding_blob, embedding_dimensions, tags, confidence, status, times_applied,
                   created_at, updated_at, created_by
            FROM learnings
            WHERE status = 'Active'
            ORDER BY confidence DESC, times_applied DESC";

        return await ExecuteReaderAsync(sql, MapLearning, null, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets learnings by status.
    /// </summary>
    public async Task<IReadOnlyList<Learning>> GetByStatusAsync(string status, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(status);

        const string sql = @"
            SELECT learning_id, title, description, trigger_type, scope, source_agent, source_task_id,
                   embedding_blob, embedding_dimensions, tags, confidence, status, times_applied,
                   created_at, updated_at, created_by
            FROM learnings
            WHERE status = $status
            ORDER BY created_at DESC";

        return await ExecuteReaderAsync(sql, MapLearning, parameters =>
        {
            parameters.Add(new SqliteParameter("$status", status));
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets learnings by scope.
    /// </summary>
    public async Task<IReadOnlyList<Learning>> GetByScopeAsync(string scope, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);

        const string sql = @"
            SELECT learning_id, title, description, trigger_type, scope, source_agent, source_task_id,
                   embedding_blob, embedding_dimensions, tags, confidence, status, times_applied,
                   created_at, updated_at, created_by
            FROM learnings
            WHERE scope = $scope
            AND status = 'Active'
            ORDER BY confidence DESC, times_applied DESC";

        return await ExecuteReaderAsync(sql, MapLearning, parameters =>
        {
            parameters.Add(new SqliteParameter("$scope", scope));
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets learnings by source agent.
    /// </summary>
    public async Task<IReadOnlyList<Learning>> GetBySourceAgentAsync(string sourceAgent, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceAgent);

        const string sql = @"
            SELECT learning_id, title, description, trigger_type, scope, source_agent, source_task_id,
                   embedding_blob, embedding_dimensions, tags, confidence, status, times_applied,
                   created_at, updated_at, created_by
            FROM learnings
            WHERE source_agent = $source_agent
            ORDER BY created_at DESC";

        return await ExecuteReaderAsync(sql, MapLearning, parameters =>
        {
            parameters.Add(new SqliteParameter("$source_agent", sourceAgent));
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets learnings by source task ID for provenance tracking.
    /// </summary>
    public async Task<IReadOnlyList<Learning>> GetBySourceTaskAsync(string sourceTaskId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceTaskId);

        const string sql = @"
            SELECT learning_id, title, description, trigger_type, scope, source_agent, source_task_id,
                   embedding_blob, embedding_dimensions, tags, confidence, status, times_applied,
                   created_at, updated_at, created_by
            FROM learnings
            WHERE source_task_id = $source_task_id
            ORDER BY created_at DESC";

        return await ExecuteReaderAsync(sql, MapLearning, parameters =>
        {
            parameters.Add(new SqliteParameter("$source_task_id", sourceTaskId));
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Increments the times_applied counter for a learning.
    /// Used when a learning is retrieved and injected into an agent prompt.
    /// </summary>
    public async Task IncrementTimesAppliedAsync(string learningId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(learningId);

        const string sql = @"
            UPDATE learnings
            SET times_applied = times_applied + 1,
                updated_at = $updated_at
            WHERE learning_id = $learning_id";

        var rowsAffected = await ExecuteNonQueryAsync(sql, parameters =>
        {
            parameters.Add(new SqliteParameter("$learning_id", learningId));
            parameters.Add(new SqliteParameter("$updated_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
        }, ct).ConfigureAwait(false);

        if (rowsAffected > 0)
        {
            Logger.LogDebug("Incremented times_applied for learning {LearningId}", learningId);
        }
    }

    /// <summary>
    /// Gets learnings with embeddings for semantic search.
    /// Returns only active learnings that have embeddings.
    /// </summary>
    public async Task<IReadOnlyList<Learning>> GetWithEmbeddingsAsync(CancellationToken ct = default)
    {
        const string sql = @"
            SELECT learning_id, title, description, trigger_type, scope, source_agent, source_task_id,
                   embedding_blob, embedding_dimensions, tags, confidence, status, times_applied,
                   created_at, updated_at, created_by
            FROM learnings
            WHERE status = 'Active'
            AND embedding_blob IS NOT NULL
            ORDER BY confidence DESC, times_applied DESC";

        return await ExecuteReaderAsync(sql, MapLearning, null, ct).ConfigureAwait(false);
    }

    private static Learning MapLearning(SqliteDataReader reader)
    {
        return new Learning
        {
            LearningId = reader.GetString(0),
            Title = reader.GetString(1),
            Description = reader.GetString(2),
            TriggerType = reader.GetString(3),
            Scope = reader.GetString(4),
            SourceAgent = reader.IsDBNull(5) ? null : reader.GetString(5),
            SourceTaskId = reader.IsDBNull(6) ? null : reader.GetString(6),
            EmbeddingBlob = reader.IsDBNull(7) ? null : GetBytes(reader, 7),
            EmbeddingDimensions = reader.IsDBNull(8) ? null : reader.GetInt32(8),
            Tags = reader.IsDBNull(9) ? null : reader.GetString(9),
            Confidence = reader.GetDouble(10),
            Status = reader.GetString(11),
            TimesApplied = reader.GetInt32(12),
            CreatedAt = reader.GetInt64(13),
            UpdatedAt = reader.GetInt64(14),
            CreatedBy = reader.GetString(15)
        };
    }

    private static byte[]? GetBytes(SqliteDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
            return null;

        var length = reader.GetBytes(ordinal, 0, null, 0, 0);
        var buffer = new byte[length];
        reader.GetBytes(ordinal, 0, buffer, 0, (int)length);
        return buffer;
    }
}
