using Daiv3.Persistence.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Daiv3.Persistence.Repositories;

/// <summary>
/// Repository for managing revert promotion records.
/// Tracks when learning promotions are undone for reversibility and audit trail.
/// Implements KBP-NFR-001: Promotions SHOULD be transparent and reversible.
/// </summary>
public class RevertPromotionRepository : RepositoryBase<RevertPromotion>
{
    public RevertPromotionRepository(IDatabaseContext databaseContext, ILogger<RevertPromotionRepository> logger)
        : base(databaseContext, logger)
    {
    }

    public override async Task<RevertPromotion?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT revert_id, promotion_id, learning_id, reverted_at, reverted_by,
                   reverted_from_scope, reverted_to_scope, notes
            FROM revert_promotions
            WHERE revert_id = $id";

        var results = await ExecuteReaderAsync(sql, MapRevertPromotion, parameters =>
        {
            parameters.Add(new SqliteParameter("$id", id));
        }, ct).ConfigureAwait(false);

        return results.FirstOrDefault();
    }

    public override async Task<IReadOnlyList<RevertPromotion>> GetAllAsync(CancellationToken ct = default)
    {
        const string sql = @"
            SELECT revert_id, promotion_id, learning_id, reverted_at, reverted_by,
                   reverted_from_scope, reverted_to_scope, notes
            FROM revert_promotions
            ORDER BY reverted_at DESC";

        return await ExecuteReaderAsync(sql, MapRevertPromotion, null, ct).ConfigureAwait(false);
    }

    public override async Task<string> AddAsync(RevertPromotion entity, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO revert_promotions (
                revert_id, promotion_id, learning_id, reverted_at, reverted_by,
                reverted_from_scope, reverted_to_scope, notes
            ) VALUES (
                $revert_id, $promotion_id, $learning_id, $reverted_at, $reverted_by,
                $reverted_from_scope, $reverted_to_scope, $notes
            )";

        await ExecuteNonQueryAsync(sql, parameters =>
        {
            parameters.Add(new SqliteParameter("$revert_id", entity.RevertId));
            parameters.Add(new SqliteParameter("$promotion_id", entity.PromotionId));
            parameters.Add(new SqliteParameter("$learning_id", entity.LearningId));
            parameters.Add(new SqliteParameter("$reverted_at", entity.RevertedAt));
            parameters.Add(new SqliteParameter("$reverted_by", entity.RevertedBy));
            parameters.Add(new SqliteParameter("$reverted_from_scope", entity.RevertedFromScope));
            parameters.Add(new SqliteParameter("$reverted_to_scope", entity.RevertedToScope));
            parameters.Add(new SqliteParameter("$notes", entity.Notes ?? (object)DBNull.Value));
        }, ct).ConfigureAwait(false);

        return entity.RevertId;
    }

    public override async Task UpdateAsync(RevertPromotion entity, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE revert_promotions
            SET promotion_id = $promotion_id, learning_id = $learning_id, reverted_at = $reverted_at,
                reverted_by = $reverted_by, reverted_from_scope = $reverted_from_scope,
                reverted_to_scope = $reverted_to_scope, notes = $notes
            WHERE revert_id = $revert_id";

        var rowsAffected = await ExecuteNonQueryAsync(sql, parameters =>
        {
            parameters.Add(new SqliteParameter("$revert_id", entity.RevertId));
            parameters.Add(new SqliteParameter("$promotion_id", entity.PromotionId));
            parameters.Add(new SqliteParameter("$learning_id", entity.LearningId));
            parameters.Add(new SqliteParameter("$reverted_at", entity.RevertedAt));
            parameters.Add(new SqliteParameter("$reverted_by", entity.RevertedBy));
            parameters.Add(new SqliteParameter("$reverted_from_scope", entity.RevertedFromScope));
            parameters.Add(new SqliteParameter("$reverted_to_scope", entity.RevertedToScope));
            parameters.Add(new SqliteParameter("$notes", entity.Notes ?? (object)DBNull.Value));
        }, ct).ConfigureAwait(false);

        if (rowsAffected == 0)
        {
            Logger.LogWarning("Attempted to update non-existent revert promotion {RevertId}", entity.RevertId);
        }
    }

    public override async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        const string sql = "DELETE FROM revert_promotions WHERE revert_id = $id";

        var rowsAffected = await ExecuteNonQueryAsync(sql, parameters =>
        {
            parameters.Add(new SqliteParameter("$id", id));
        }, ct).ConfigureAwait(false);

        if (rowsAffected == 0)
        {
            Logger.LogWarning("Attempted to delete non-existent revert promotion {RevertId}", id);
        }
        else
        {
            Logger.LogInformation("Deleted revert promotion {RevertId}", id);
        }
    }

    /// <summary>
    /// Gets all reverts for a specific learning.
    /// Returns reverts ordered by most recent first.
    /// </summary>
    public async Task<IReadOnlyList<RevertPromotion>> GetByLearningIdAsync(string learningId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT revert_id, promotion_id, learning_id, reverted_at, reverted_by,
                   reverted_from_scope, reverted_to_scope, notes
            FROM revert_promotions
            WHERE learning_id = $learning_id
            ORDER BY reverted_at DESC";

        return await ExecuteReaderAsync(sql, MapRevertPromotion, parameters =>
        {
            parameters.Add(new SqliteParameter("$learning_id", learningId));
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the revert record for a specific promotion (if it exists).
    /// Returns null if the promotion has not been reverted.
    /// </summary>
    public async Task<RevertPromotion?> GetByPromotionIdAsync(string promotionId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT revert_id, promotion_id, learning_id, reverted_at, reverted_by,
                   reverted_from_scope, reverted_to_scope, notes
            FROM revert_promotions
            WHERE promotion_id = $promotion_id";

        var results = await ExecuteReaderAsync(sql, MapRevertPromotion, parameters =>
        {
            parameters.Add(new SqliteParameter("$promotion_id", promotionId));
        }, ct).ConfigureAwait(false);

        return results.FirstOrDefault();
    }

    /// <summary>
    /// Gets reverts performed by a specific user or agent.
    /// </summary>
    public async Task<IReadOnlyList<RevertPromotion>> GetByRevertedByAsync(string revertedBy, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT revert_id, promotion_id, learning_id, reverted_at, reverted_by,
                   reverted_from_scope, reverted_to_scope, notes
            FROM revert_promotions
            WHERE reverted_by = $reverted_by
            ORDER BY reverted_at DESC";

        return await ExecuteReaderAsync(sql, MapRevertPromotion, parameters =>
        {
            parameters.Add(new SqliteParameter("$reverted_by", revertedBy));
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets reverts within a time range.
    /// </summary>
    public async Task<IReadOnlyList<RevertPromotion>> GetByTimeRangeAsync(
        long startUnixTime,
        long endUnixTime,
        CancellationToken ct = default)
    {
        const string sql = @"
            SELECT revert_id, promotion_id, learning_id, reverted_at, reverted_by,
                   reverted_from_scope, reverted_to_scope, notes
            FROM revert_promotions
            WHERE reverted_at >= $start_time AND reverted_at <= $end_time
            ORDER BY reverted_at DESC";

        return await ExecuteReaderAsync(sql, MapRevertPromotion, parameters =>
        {
            parameters.Add(new SqliteParameter("$start_time", startUnixTime));
            parameters.Add(new SqliteParameter("$end_time", endUnixTime));
        }, ct).ConfigureAwait(false);
    }

    private static RevertPromotion MapRevertPromotion(SqliteDataReader reader)
    {
        return new RevertPromotion
        {
            RevertId = reader.GetString(0),
            PromotionId = reader.GetString(1),
            LearningId = reader.GetString(2),
            RevertedAt = reader.GetInt64(3),
            RevertedBy = reader.GetString(4),
            RevertedFromScope = reader.GetString(5),
            RevertedToScope = reader.GetString(6),
            Notes = reader.IsDBNull(7) ? null : reader.GetString(7)
        };
    }
}
