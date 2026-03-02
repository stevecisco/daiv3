using Daiv3.Persistence.Entities;

namespace Daiv3.Orchestration.Interfaces;

/// <summary>
/// Manages agent-proposed learning promotions requiring user confirmation.
/// Implements KBP-REQ-003: Agents MAY propose promotions but SHALL require user confirmation.
/// </summary>
public interface IAgentPromotionProposalService
{
    /// <summary>
    /// Creates a new promotion proposal from an agent.
    /// The proposal will remain in "Pending" status until confirmed by a user.
    /// </summary>
    Task<string> CreateProposalAsync(
        string agentId,
        string learningId,
        string targetScope,
        string? justification = null,
        double confidenceScore = 0.5,
        string? sourceTaskId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Gets a proposal by ID.
    /// </summary>
    Task<AgentPromotionProposal?> GetProposalAsync(string proposalId, CancellationToken ct = default);

    /// <summary>
    /// Gets all pending proposals that require user confirmation.
    /// </summary>
    Task<IReadOnlyList<AgentPromotionProposal>> GetPendingProposalsAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets proposals for a specific learning.
    /// </summary>
    Task<IReadOnlyList<AgentPromotionProposal>> GetProposalsForLearningAsync(string learningId, CancellationToken ct = default);

    /// <summary>
    /// Gets proposals from a specific agent.
    /// </summary>
    Task<IReadOnlyList<AgentPromotionProposal>> GetProposalsFromAgentAsync(string agentId, CancellationToken ct = default);

    /// <summary>
    /// Approves a proposal, promoting the learning to the suggested scope.
    /// </summary>
    Task<bool> ApproveProposalAsync(
        string proposalId,
        string reviewedBy = "user",
        CancellationToken ct = default);

    /// <summary>
    /// Rejects a proposal with optional explanation.
    /// </summary>
    Task<bool> RejectProposalAsync(
        string proposalId,
        string? rejectionReason = null,
        string reviewedBy = "user",
        CancellationToken ct = default);

    /// <summary>
    /// Gets summary statistics for proposals.
    /// </summary>
    Task<AgentPromotionProposalStats> GetStatisticsAsync(CancellationToken ct = default);
}

/// <summary>
/// Statistics about agent promotion proposals.
/// </summary>
public record AgentPromotionProposalStats
{
    /// <summary>
    /// Total number of pending proposals.
    /// </summary>
    public int PendingCount { get; init; }

    /// <summary>
    /// Total number of approved proposals.
    /// </summary>
    public int ApprovedCount { get; init; }

    /// <summary>
    /// Total number of rejected proposals.
    /// </summary>
    public int RejectedCount { get; init; }

    /// <summary>
    /// Average confidence score of pending proposals.
    /// </summary>
    public double AveragePendingConfidence { get; init; }

    /// <summary>
    /// Number of proposals by agent.
    /// </summary>
    public Dictionary<string, int> ProposalsByAgent { get; init; } = new();
}
