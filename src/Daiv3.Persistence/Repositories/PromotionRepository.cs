using Daiv3.Persistence.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Daiv3.Persistence.Repositories;

/// <summary>
/// Repository for managing promotion history records.
/// Tracks learning scope promotions for audit trail and provenance per KBP-DATA-001 and KBP-DATA-002.
/// </summary>
public class PromotionRepository : RepositoryBase<Promotion>
{
    public PromotionRepository(IDatabaseContext databaseContext, ILogger<PromotionRepository> logger)
        : base(databaseContext, logger)
    {
    }

    public override async Task<Promotion?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT promotion_id, learning_id, from_scope, to_scope, promoted_at, promoted_by,
                   source_task_id, source_agent, notes
            FROM promotions
            WHERE promotion_id = $id";

        var results = await ExecuteReaderAsync(sql, MapPromotion, parameters =>
        {
            parameters.Add(new SqliteParameter("$id", id));
        }, ct).ConfigureAwait(false);

        return results.FirstOrDefault();
    }

    public override async Task<IReadOnlyList<Promotion>> GetAllAsync(CancellationToken ct = default)
    {
        const string sql = @"
            SELECT promotion_id, learning_id, from_scope, to_scope, promoted_at, promoted_by,
                   source_task_id, source_agent, notes
            FROM promotions
            ORDER BY promoted_at DESC";

        return await ExecuteReaderAsync(sql, MapPromotion, null, ct).ConfigureAwait(false);
    }

    public override async Task<string> AddAsync(Promotion entity, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.PromotionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.LearningId);
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.FromScope);
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.ToScope);
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.PromotedBy);

        const string sql = @"
            INSERT INTO promotions (
                promotion_id, learning_id, from_scope, to_scope, promoted_at, promoted_by,
                source_task_id, source_agent, notes
            )
            VALUES (
                $promotion_id, $learning_id, $from_scope, $to_scope, $promoted_at, $promoted_by,
                $source_task_id, $source_agent, $notes
            )";

        await ExecuteNonQueryAsync(sql, parameters =>
        {
            parameters.Add(new SqliteParameter("$promotion_id", entity.PromotionId));
            parameters.Add(new SqliteParameter("$learning_id", entity.LearningId));
            parameters.Add(new SqliteParameter("$from_scope", entity.FromScope));
            parameters.Add(new SqliteParameter("$to_scope", entity.ToScope));
            parameters.Add(new SqliteParameter("$promoted_at", entity.PromotedAt));
            parameters.Add(new SqliteParameter("$promoted_by", entity.PromotedBy));
            parameters.Add(new SqliteParameter("$source_task_id", (object?)entity.SourceTaskId ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$source_agent", (object?)entity.SourceAgent ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$notes", (object?)entity.Notes ?? DBNull.Value));
        }, ct).ConfigureAwait(false);

        Logger.LogInformation(
            "Recorded promotion {PromotionId} for learning {LearningId}: {FromScope} → {ToScope}",
            entity.PromotionId, entity.LearningId, entity.FromScope, entity.ToScope);
        return entity.PromotionId;
    }

    public override async Task UpdateAsync(Promotion entity, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.PromotionId);

        const string sql = @"
            UPDATE promotions
            SET learning_id = $learning_id,
                from_scope = $from_scope,
                to_scope = $to_scope,
                promoted_at = $promoted_at,
                promoted_by = $promoted_by,
                source_task_id = $source_task_id,
                source_agent = $source_agent,
                notes = $notes
            WHERE promotion_id = $promotion_id";

        var rowsAffected = await ExecuteNonQueryAsync(sql, parameters =>
        {
            parameters.Add(new SqliteParameter("$promotion_id", entity.PromotionId));
            parameters.Add(new SqliteParameter("$learning_id", entity.LearningId));
            parameters.Add(new SqliteParameter("$from_scope", entity.FromScope));
            parameters.Add(new SqliteParameter("$to_scope", entity.ToScope));
            parameters.Add(new SqliteParameter("$promoted_at", entity.PromotedAt));
            parameters.Add(new SqliteParameter("$promoted_by", entity.PromotedBy));
            parameters.Add(new SqliteParameter("$source_task_id", (object?)entity.SourceTaskId ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$source_agent", (object?)entity.SourceAgent ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$notes", (object?)entity.Notes ?? DBNull.Value));
        }, ct).ConfigureAwait(false);

        if (rowsAffected == 0)
        {
            Logger.LogWarning("Attempted to update non-existent promotion {PromotionId}", entity.PromotionId);
        }
        else
        {
            Logger.LogDebug("Updated promotion {PromotionId}", entity.PromotionId);
        }
    }

    public override async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        const string sql = "DELETE FROM promotions WHERE promotion_id = $id";

        var rowsAffected = await ExecuteNonQueryAsync(sql, parameters =>
        {
            parameters.Add(new SqliteParameter("$id", id));
        }, ct).ConfigureAwait(false);

        if (rowsAffected == 0)
        {
            Logger.LogWarning("Attempted to delete non-existent promotion {PromotionId}", id);
        }
        else
        {
            Logger.LogInformation("Deleted promotion {PromotionId}", id);
        }
    }

    /// <summary>
    /// Gets promotion history for a specific learning.
    /// Returns promotions ordered by most recent first.
    /// </summary>
    public async Task<IReadOnlyList<Promotion>> GetByLearningIdAsync(string learningId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT promotion_id, learning_id, from_scope, to_scope, promoted_at, promoted_by,
                   source_task_id, source_agent, notes
            FROM promotions
            WHERE learning_id = $learning_id
            ORDER BY promoted_at DESC, rowid DESC";

        return await ExecuteReaderAsync(sql, MapPromotion, parameters =>
        {
            parameters.Add(new SqliteParameter("$learning_id", learningId));
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets promotions triggered from a specific task/session.
    /// Useful for understanding what knowledge was promoted during a task execution.
    /// </summary>
    public async Task<IReadOnlyList<Promotion>> GetBySourceTaskIdAsync(string sourceTaskId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT promotion_id, learning_id, from_scope, to_scope, promoted_at, promoted_by,
                   source_task_id, source_agent, notes
            FROM promotions
            WHERE source_task_id = $source_task_id
            ORDER BY promoted_at DESC";

        return await ExecuteReaderAsync(sql, MapPromotion, parameters =>
        {
            parameters.Add(new SqliteParameter("$source_task_id", sourceTaskId));
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets promotions to a specific scope (e.g., all promotions to 'Global').
    /// Useful for understanding what knowledge reached global scope.
    /// </summary>
    public async Task<IReadOnlyList<Promotion>> GetByToScopeAsync(string toScope, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT promotion_id, learning_id, from_scope, to_scope, promoted_at, promoted_by,
                   source_task_id, source_agent, notes
            FROM promotions
            WHERE to_scope = $to_scope
            ORDER BY promoted_at DESC";

        return await ExecuteReaderAsync(sql, MapPromotion, parameters =>
        {
            parameters.Add(new SqliteParameter("$to_scope", toScope));
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets promotions performed by a specific user or agent.
    /// </summary>
    public async Task<IReadOnlyList<Promotion>> GetByPromotedByAsync(string promotedBy, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT promotion_id, learning_id, from_scope, to_scope, promoted_at, promoted_by,
                   source_task_id, source_agent, notes
            FROM promotions
            WHERE promoted_by = $promoted_by
            ORDER BY promoted_at DESC";

        return await ExecuteReaderAsync(sql, MapPromotion, parameters =>
        {
            parameters.Add(new SqliteParameter("$promoted_by", promotedBy));
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets promotions within a time range.
    /// </summary>
    public async Task<IReadOnlyList<Promotion>> GetByTimeRangeAsync(
        long startUnixTime,
        long endUnixTime,
        CancellationToken ct = default)
    {
        const string sql = @"
            SELECT promotion_id, learning_id, from_scope, to_scope, promoted_at, promoted_by,
                   source_task_id, source_agent, notes
            FROM promotions
            WHERE promoted_at >= $start AND promoted_at <= $end
            ORDER BY promoted_at DESC";

        return await ExecuteReaderAsync(sql, MapPromotion, parameters =>
        {
            parameters.Add(new SqliteParameter("$start", startUnixTime));
            parameters.Add(new SqliteParameter("$end", endUnixTime));
        }, ct).ConfigureAwait(false);
    }

    private static Promotion MapPromotion(SqliteDataReader reader)
    {
        return new Promotion
        {
            PromotionId = reader.GetString(0),
            LearningId = reader.GetString(1),
            FromScope = reader.GetString(2),
            ToScope = reader.GetString(3),
            PromotedAt = reader.GetInt64(4),
            PromotedBy = reader.GetString(5),
            SourceTaskId = reader.IsDBNull(6) ? null : reader.GetString(6),
            SourceAgent = reader.IsDBNull(7) ? null : reader.GetString(7),
            Notes = reader.IsDBNull(8) ? null : reader.GetString(8)
        };
    }
}
