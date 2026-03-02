using Daiv3.Persistence.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Daiv3.Persistence.Repositories;

/// <summary>
/// Repository for managing agent-proposed promotion records.
/// Tracks promotion proposals made by agents that require user confirmation.
/// Implements KBP-REQ-003: Agents MAY propose promotions but SHALL require user confirmation.
/// </summary>
public class AgentPromotionProposalRepository : RepositoryBase<AgentPromotionProposal>
{
    public AgentPromotionProposalRepository(IDatabaseContext databaseContext, ILogger<AgentPromotionProposalRepository> logger)
        : base(databaseContext, logger)
    {
    }

    public override async Task<AgentPromotionProposal?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT proposal_id, learning_id, proposing_agent, source_task_id, from_scope,
                   suggested_target_scope, justification, confidence_score, status,
                   created_at, updated_at, reviewed_by, reviewed_at, rejection_reason
            FROM agent_promotion_proposals
            WHERE proposal_id = $id";

        var results = await ExecuteReaderAsync(sql, MapAgentPromotionProposal, parameters =>
        {
            parameters.Add(new SqliteParameter("$id", id));
        }, ct).ConfigureAwait(false);

        return results.FirstOrDefault();
    }

    public override async Task<IReadOnlyList<AgentPromotionProposal>> GetAllAsync(CancellationToken ct = default)
    {
        const string sql = @"
            SELECT proposal_id, learning_id, proposing_agent, source_task_id, from_scope,
                   suggested_target_scope, justification, confidence_score, status,
                   created_at, updated_at, reviewed_by, reviewed_at, rejection_reason
            FROM agent_promotion_proposals
            ORDER BY created_at DESC";

        return await ExecuteReaderAsync(sql, MapAgentPromotionProposal, null, ct).ConfigureAwait(false);
    }

    public override async Task<string> AddAsync(AgentPromotionProposal entity, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.ProposalId);
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.LearningId);
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.ProposingAgent);
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.FromScope);
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.SuggestedTargetScope);

        const string sql = @"
            INSERT INTO agent_promotion_proposals (
                proposal_id, learning_id, proposing_agent, source_task_id, from_scope,
                suggested_target_scope, justification, confidence_score, status,
                created_at, updated_at, reviewed_by, reviewed_at, rejection_reason
            )
            VALUES (
                $proposal_id, $learning_id, $proposing_agent, $source_task_id, $from_scope,
                $suggested_target_scope, $justification, $confidence_score, $status,
                $created_at, $updated_at, $reviewed_by, $reviewed_at, $rejection_reason
            )";

        await ExecuteNonQueryAsync(sql, parameters =>
        {
            parameters.Add(new SqliteParameter("$proposal_id", entity.ProposalId));
            parameters.Add(new SqliteParameter("$learning_id", entity.LearningId));
            parameters.Add(new SqliteParameter("$proposing_agent", entity.ProposingAgent));
            parameters.Add(new SqliteParameter("$source_task_id", (object?)entity.SourceTaskId ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$from_scope", entity.FromScope));
            parameters.Add(new SqliteParameter("$suggested_target_scope", entity.SuggestedTargetScope));
            parameters.Add(new SqliteParameter("$justification", (object?)entity.Justification ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$confidence_score", entity.ConfidenceScore));
            parameters.Add(new SqliteParameter("$status", entity.Status));
            parameters.Add(new SqliteParameter("$created_at", entity.CreatedAt));
            parameters.Add(new SqliteParameter("$updated_at", entity.UpdatedAt));
            parameters.Add(new SqliteParameter("$reviewed_by", (object?)entity.ReviewedBy ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$reviewed_at", (object?)entity.ReviewedAt ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$rejection_reason", (object?)entity.RejectionReason ?? DBNull.Value));
        }, ct).ConfigureAwait(false);

        Logger.LogInformation(
            "Created promotion proposal {ProposalId} from agent {Agent} for learning {LearningId}: " +
            "{FromScope} → {ToScope} (confidence: {Confidence})",
            entity.ProposalId, entity.ProposingAgent, entity.LearningId,
            entity.FromScope, entity.SuggestedTargetScope, entity.ConfidenceScore);
        return entity.ProposalId;
    }

    public override async Task UpdateAsync(AgentPromotionProposal entity, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentException.ThrowIfNullOrWhiteSpace(entity.ProposalId);

        const string sql = @"
            UPDATE agent_promotion_proposals
            SET learning_id = $learning_id,
                proposing_agent = $proposing_agent,
                source_task_id = $source_task_id,
                from_scope = $from_scope,
                suggested_target_scope = $suggested_target_scope,
                justification = $justification,
                confidence_score = $confidence_score,
                status = $status,
                updated_at = $updated_at,
                reviewed_by = $reviewed_by,
                reviewed_at = $reviewed_at,
                rejection_reason = $rejection_reason
            WHERE proposal_id = $proposal_id";

        var rowsAffected = await ExecuteNonQueryAsync(sql, parameters =>
        {
            parameters.Add(new SqliteParameter("$proposal_id", entity.ProposalId));
            parameters.Add(new SqliteParameter("$learning_id", entity.LearningId));
            parameters.Add(new SqliteParameter("$proposing_agent", entity.ProposingAgent));
            parameters.Add(new SqliteParameter("$source_task_id", (object?)entity.SourceTaskId ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$from_scope", entity.FromScope));
            parameters.Add(new SqliteParameter("$suggested_target_scope", entity.SuggestedTargetScope));
            parameters.Add(new SqliteParameter("$justification", (object?)entity.Justification ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$confidence_score", entity.ConfidenceScore));
            parameters.Add(new SqliteParameter("$status", entity.Status));
            parameters.Add(new SqliteParameter("$updated_at", entity.UpdatedAt));
            parameters.Add(new SqliteParameter("$reviewed_by", (object?)entity.ReviewedBy ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$reviewed_at", (object?)entity.ReviewedAt ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$rejection_reason", (object?)entity.RejectionReason ?? DBNull.Value));
        }, ct).ConfigureAwait(false);

        if (rowsAffected == 0)
        {
            Logger.LogWarning("Attempted to update non-existent proposal {ProposalId}", entity.ProposalId);
        }
        else
        {
            Logger.LogDebug("Updated proposal {ProposalId} status to {Status}", entity.ProposalId, entity.Status);
        }
    }

    public override async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        const string sql = "DELETE FROM agent_promotion_proposals WHERE proposal_id = $id";

        var rowsAffected = await ExecuteNonQueryAsync(sql, parameters =>
        {
            parameters.Add(new SqliteParameter("$id", id));
        }, ct).ConfigureAwait(false);

        if (rowsAffected == 0)
        {
            Logger.LogWarning("Attempted to delete non-existent proposal {ProposalId}", id);
        }
        else
        {
            Logger.LogInformation("Deleted proposal {ProposalId}", id);
        }
    }

    /// <summary>
    /// Gets all pending proposals in creation order.
    /// </summary>
    public async Task<IReadOnlyList<AgentPromotionProposal>> GetPendingProposalsAsync(CancellationToken ct = default)
    {
        const string sql = @"
            SELECT proposal_id, learning_id, proposing_agent, source_task_id, from_scope,
                   suggested_target_scope, justification, confidence_score, status,
                   created_at, updated_at, reviewed_by, reviewed_at, rejection_reason
            FROM agent_promotion_proposals
            WHERE status = 'Pending'
            ORDER BY created_at DESC";

        return await ExecuteReaderAsync(sql, MapAgentPromotionProposal, null, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets proposals for a specific learning.
    /// </summary>
    public async Task<IReadOnlyList<AgentPromotionProposal>> GetByLearningIdAsync(string learningId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT proposal_id, learning_id, proposing_agent, source_task_id, from_scope,
                   suggested_target_scope, justification, confidence_score, status,
                   created_at, updated_at, reviewed_by, reviewed_at, rejection_reason
            FROM agent_promotion_proposals
            WHERE learning_id = $learning_id
            ORDER BY created_at DESC";

        return await ExecuteReaderAsync(sql, MapAgentPromotionProposal, parameters =>
        {
            parameters.Add(new SqliteParameter("$learning_id", learningId));
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets proposals from a specific agent.
    /// </summary>
    public async Task<IReadOnlyList<AgentPromotionProposal>> GetByProposingAgentAsync(string agentId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT proposal_id, learning_id, proposing_agent, source_task_id, from_scope,
                   suggested_target_scope, justification, confidence_score, status,
                   created_at, updated_at, reviewed_by, reviewed_at, rejection_reason
            FROM agent_promotion_proposals
            WHERE proposing_agent = $agent_id
            ORDER BY created_at DESC";

        return await ExecuteReaderAsync(sql, MapAgentPromotionProposal, parameters =>
        {
            parameters.Add(new SqliteParameter("$agent_id", agentId));
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets proposals from a specific task.
    /// </summary>
    public async Task<IReadOnlyList<AgentPromotionProposal>> GetBySourceTaskIdAsync(string sourceTaskId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT proposal_id, learning_id, proposing_agent, source_task_id, from_scope,
                   suggested_target_scope, justification, confidence_score, status,
                   created_at, updated_at, reviewed_by, reviewed_at, rejection_reason
            FROM agent_promotion_proposals
            WHERE source_task_id = $source_task_id
            ORDER BY created_at DESC";

        return await ExecuteReaderAsync(sql, MapAgentPromotionProposal, parameters =>
        {
            parameters.Add(new SqliteParameter("$source_task_id", sourceTaskId));
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets proposals by status (Pending, Approved, Rejected).
    /// </summary>
    public async Task<IReadOnlyList<AgentPromotionProposal>> GetByStatusAsync(string status, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT proposal_id, learning_id, proposing_agent, source_task_id, from_scope,
                   suggested_target_scope, justification, confidence_score, status,
                   created_at, updated_at, reviewed_by, reviewed_at, rejection_reason
            FROM agent_promotion_proposals
            WHERE status = $status
            ORDER BY created_at DESC";

        return await ExecuteReaderAsync(sql, MapAgentPromotionProposal, parameters =>
        {
            parameters.Add(new SqliteParameter("$status", status));
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets proposals within a confidence score range.
    /// </summary>
    public async Task<IReadOnlyList<AgentPromotionProposal>> GetByConfidenceRangeAsync(
        double minConfidence,
        double maxConfidence,
        CancellationToken ct = default)
    {
        const string sql = @"
            SELECT proposal_id, learning_id, proposing_agent, source_task_id, from_scope,
                   suggested_target_scope, justification, confidence_score, status,
                   created_at, updated_at, reviewed_by, reviewed_at, rejection_reason
            FROM agent_promotion_proposals
            WHERE confidence_score >= $min AND confidence_score <= $max
            ORDER BY confidence_score DESC";

        return await ExecuteReaderAsync(sql, MapAgentPromotionProposal, parameters =>
        {
            parameters.Add(new SqliteParameter("$min", minConfidence));
            parameters.Add(new SqliteParameter("$max", maxConfidence));
        }, ct).ConfigureAwait(false);
    }

    private static AgentPromotionProposal MapAgentPromotionProposal(SqliteDataReader reader)
    {
        return new AgentPromotionProposal
        {
            ProposalId = reader.GetString(0),
            LearningId = reader.GetString(1),
            ProposingAgent = reader.GetString(2),
            SourceTaskId = reader.IsDBNull(3) ? null : reader.GetString(3),
            FromScope = reader.GetString(4),
            SuggestedTargetScope = reader.GetString(5),
            Justification = reader.IsDBNull(6) ? null : reader.GetString(6),
            ConfidenceScore = reader.GetDouble(7),
            Status = reader.GetString(8),
            CreatedAt = reader.GetInt64(9),
            UpdatedAt = reader.GetInt64(10),
            ReviewedBy = reader.IsDBNull(11) ? null : reader.GetString(11),
            ReviewedAt = reader.IsDBNull(12) ? null : reader.GetInt64(12),
            RejectionReason = reader.IsDBNull(13) ? null : reader.GetString(13)
        };
    }
}
