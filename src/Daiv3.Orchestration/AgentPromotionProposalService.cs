using Daiv3.Orchestration.Interfaces;
using Daiv3.Persistence;
using Daiv3.Persistence.Entities;
using Daiv3.Persistence.Repositories;
using Microsoft.Extensions.Logging;

namespace Daiv3.Orchestration;

/// <summary>
/// Service for implementing agent-proposed learning promotions requiring user confirmation.
/// Implements KBP-REQ-003: Agents MAY propose promotions but SHALL require user confirmation.
/// </summary>
public class AgentPromotionProposalService : IAgentPromotionProposalService
{
    private readonly ILogger<AgentPromotionProposalService> _logger;
    private readonly AgentPromotionProposalRepository _proposalRepository;
    private readonly LearningRepository _learningRepository;
    private readonly PromotionRepository _promotionRepository;
    private readonly LearningStorageService _learningService;

    public AgentPromotionProposalService(
        ILogger<AgentPromotionProposalService> logger,
        AgentPromotionProposalRepository proposalRepository,
        LearningRepository learningRepository,
        PromotionRepository promotionRepository,
        LearningStorageService learningService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _proposalRepository = proposalRepository ?? throw new ArgumentNullException(nameof(proposalRepository));
        _learningRepository = learningRepository ?? throw new ArgumentNullException(nameof(learningRepository));
        _promotionRepository = promotionRepository ?? throw new ArgumentNullException(nameof(promotionRepository));
        _learningService = learningService ?? throw new ArgumentNullException(nameof(learningService));
    }

    public async Task<string> CreateProposalAsync(
        string agentId,
        string learningId,
        string targetScope,
        string? justification = null,
        double confidenceScore = 0.5,
        string? sourceTaskId = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(learningId);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetScope);

        if (confidenceScore < 0.0 || confidenceScore > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(confidenceScore), "Confidence score must be between 0.0 and 1.0");
        }

        try
        {
            // Verify the learning exists
            var learning = await _learningRepository.GetByIdAsync(learningId, ct).ConfigureAwait(false);
            if (learning == null)
            {
                _logger.LogWarning(
                    "Agent {AgentId} attempted to propose promotion for non-existent learning {LearningId}",
                    agentId, learningId);
                throw new InvalidOperationException($"Learning {learningId} not found");
            }

            // Validate target scope
            var validScopes = new[] { "Global", "Agent", "Skill", "Project", "Domain" };
            if (!validScopes.Contains(targetScope, StringComparer.Ordinal))
            {
                throw new ArgumentException(
                    $"Invalid target scope '{targetScope}'. Must be one of: {string.Join(", ", validScopes)}",
                    nameof(targetScope));
            }

            // Validate that we're not proposing to the same scope
            if (learning.Scope.Equals(targetScope, StringComparison.Ordinal))
            {
                _logger.LogWarning(
                    "Agent {AgentId} proposed promotion to same scope {Scope} for learning {LearningId}",
                    agentId, targetScope, learningId);
                throw new InvalidOperationException(
                    $"Learning is already at scope {targetScope}");
            }

            // Create the proposal
            var proposal = new AgentPromotionProposal
            {
                ProposalId = Guid.NewGuid().ToString(),
                LearningId = learningId,
                ProposingAgent = agentId,
                SourceTaskId = sourceTaskId,
                FromScope = learning.Scope,
                SuggestedTargetScope = targetScope,
                Justification = justification,
                ConfidenceScore = confidenceScore,
                Status = "Pending",
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                ReviewedBy = null,
                ReviewedAt = null,
                RejectionReason = null
            };

            await _proposalRepository.AddAsync(proposal, ct).ConfigureAwait(false);

            _logger.LogInformation(
                "Created promotion proposal {ProposalId} from agent {AgentId} for learning {LearningId}: " +
                "{FromScope} → {ToScope} (confidence: {Confidence:P}, task: {TaskId})",
                proposal.ProposalId, agentId, learningId, proposal.FromScope, targetScope,
                confidenceScore, sourceTaskId ?? "none");

            return proposal.ProposalId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to create promotion proposal from agent {AgentId} for learning {LearningId}",
                agentId, learningId);
            throw;
        }
    }

    public async Task<AgentPromotionProposal?> GetProposalAsync(string proposalId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(proposalId);
        return await _proposalRepository.GetByIdAsync(proposalId, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<AgentPromotionProposal>> GetPendingProposalsAsync(CancellationToken ct = default)
    {
        return await _proposalRepository.GetPendingProposalsAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<AgentPromotionProposal>> GetProposalsForLearningAsync(
        string learningId,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(learningId);
        return await _proposalRepository.GetByLearningIdAsync(learningId, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<AgentPromotionProposal>> GetProposalsFromAgentAsync(
        string agentId,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        return await _proposalRepository.GetByProposingAgentAsync(agentId, ct).ConfigureAwait(false);
    }

    public async Task<bool> ApproveProposalAsync(
        string proposalId,
        string reviewedBy = "user",
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(proposalId);
        ArgumentException.ThrowIfNullOrWhiteSpace(reviewedBy);

        try
        {
            var proposal = await _proposalRepository.GetByIdAsync(proposalId, ct).ConfigureAwait(false);
            if (proposal == null)
            {
                _logger.LogWarning("Attempted to approve non-existent proposal {ProposalId}", proposalId);
                return false;
            }

            if (!proposal.Status.Equals("Pending", StringComparison.Ordinal))
            {
                _logger.LogWarning(
                    "Attempted to approve proposal {ProposalId} with status {Status}",
                    proposalId, proposal.Status);
                return false;
            }

            // Promote the learning to the suggested scope
            var newScope = await _learningService.PromoteLearningAsync(
                proposal.LearningId,
                promotedBy: reviewedBy,
                sourceTaskId: proposal.SourceTaskId,
                sourceAgent: proposal.ProposingAgent,
                notes: proposal.Justification ?? $"Approved proposal {proposalId}",
                ct: ct).ConfigureAwait(false);

            if (newScope == null)
            {
                _logger.LogError(
                    "Failed to promote learning {LearningId} for proposal {ProposalId}",
                    proposal.LearningId, proposalId);
                return false;
            }

            // Update proposal status
            proposal.Status = "Approved";
            proposal.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            proposal.ReviewedBy = reviewedBy;
            proposal.ReviewedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await _proposalRepository.UpdateAsync(proposal, ct).ConfigureAwait(false);

            _logger.LogInformation(
                "Approved proposal {ProposalId}: learning {LearningId} promoted from {FromScope} to {ToScope} by {ReviewedBy}",
                proposalId, proposal.LearningId, proposal.FromScope, newScope, reviewedBy);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to approve proposal {ProposalId}", proposalId);
            throw;
        }
    }

    public async Task<bool> RejectProposalAsync(
        string proposalId,
        string? rejectionReason = null,
        string reviewedBy = "user",
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(proposalId);
        ArgumentException.ThrowIfNullOrWhiteSpace(reviewedBy);

        try
        {
            var proposal = await _proposalRepository.GetByIdAsync(proposalId, ct).ConfigureAwait(false);
            if (proposal == null)
            {
                _logger.LogWarning("Attempted to reject non-existent proposal {ProposalId}", proposalId);
                return false;
            }

            if (!proposal.Status.Equals("Pending", StringComparison.Ordinal))
            {
                _logger.LogWarning(
                    "Attempted to reject proposal {ProposalId} with status {Status}",
                    proposalId, proposal.Status);
                return false;
            }

            // Update proposal status
            proposal.Status = "Rejected";
            proposal.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            proposal.ReviewedBy = reviewedBy;
            proposal.ReviewedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            proposal.RejectionReason = rejectionReason;
            await _proposalRepository.UpdateAsync(proposal, ct).ConfigureAwait(false);

            _logger.LogInformation(
                "Rejected proposal {ProposalId} for learning {LearningId} by {ReviewedBy}: {Reason}",
                proposalId, proposal.LearningId, reviewedBy, rejectionReason ?? "No reason provided");

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reject proposal {ProposalId}", proposalId);
            throw;
        }
    }

    public async Task<AgentPromotionProposalStats> GetStatisticsAsync(CancellationToken ct = default)
    {
        try
        {
            var allProposals = await _proposalRepository.GetAllAsync(ct).ConfigureAwait(false);

            var pendingProposals = allProposals.Where(p => p.Status.Equals("Pending", StringComparison.Ordinal)).ToList();
            var approvedProposals = allProposals.Where(p => p.Status.Equals("Approved", StringComparison.Ordinal)).ToList();
            var rejectedProposals = allProposals.Where(p => p.Status.Equals("Rejected", StringComparison.Ordinal)).ToList();

            var averagePendingConfidence = pendingProposals.Count > 0
                ? pendingProposals.Average(p => p.ConfidenceScore)
                : 0.0;

            var proposalsByAgent = allProposals
                .GroupBy(p => p.ProposingAgent)
                .ToDictionary(g => g.Key, g => g.Count());

            return new AgentPromotionProposalStats
            {
                PendingCount = pendingProposals.Count,
                ApprovedCount = approvedProposals.Count,
                RejectedCount = rejectedProposals.Count,
                AveragePendingConfidence = averagePendingConfidence,
                ProposalsByAgent = proposalsByAgent
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to calculate promotion proposal statistics");
            throw;
        }
    }
}
