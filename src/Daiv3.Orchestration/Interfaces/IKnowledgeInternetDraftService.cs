using Daiv3.Orchestration.Models;
using Daiv3.Persistence.Entities;

namespace Daiv3.Orchestration.Interfaces;

/// <summary>
/// Creates reviewable draft artifacts for Internet-level knowledge promotion.
/// Implements KBP-REQ-005.
/// </summary>
public interface IKnowledgeInternetDraftService
{
    /// <summary>
    /// Creates a markdown draft artifact for learnings promoted to Internet scope.
    /// </summary>
    /// <param name="promotedLearnings">Successfully promoted learnings.</param>
    /// <param name="targetScopes">Requested target scopes by learning ID.</param>
    /// <param name="summary">Generated promotion summary.</param>
    /// <param name="sourceTaskId">Source task ID when available.</param>
    /// <param name="promotedBy">User or agent that triggered promotion.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Generated draft artifact metadata.</returns>
    Task<KnowledgeDraftArtifact> CreateDraftArtifactAsync(
        IReadOnlyList<Learning> promotedLearnings,
        IReadOnlyDictionary<string, string> targetScopes,
        KnowledgeSummary summary,
        string? sourceTaskId,
        string promotedBy,
        CancellationToken ct = default);
}
