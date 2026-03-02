using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Daiv3.Orchestration.Models;
using Daiv3.Persistence.Entities;

namespace Daiv3.Orchestration.Interfaces;

/// <summary>
/// Service for generating summaries of knowledge promotions per KBP-REQ-004.
/// Creates human-readable summaries for audit trail and user transparency.
/// </summary>
public interface IKnowledgeSummaryService
{
    /// <summary>
    /// Generates a summary of a knowledge promotion operation.
    /// </summary>
    /// <param name="promotedLearnings">List of successfully promoted learnings with original scope.</param>
    /// <param name="targetScopes">Dictionary mapping learning ID to target scope.</param>
    /// <param name="sourceTaskId">Source task ID if promotion was triggered from task completion.</param>
    /// <param name="promotedBy">User or agent who triggered the promotion.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Knowledge summary with human-readable text and metadata.</returns>
    Task<KnowledgeSummary> GenerateSummaryAsync(
        IReadOnlyList<Learning> promotedLearnings,
        IReadOnlyDictionary<string, string> targetScopes,
        string? sourceTaskId,
        string promotedBy,
        CancellationToken ct = default);
}
