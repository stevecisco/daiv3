using Daiv3.Persistence.Entities;

namespace Daiv3.Persistence;

/// <summary>
/// Abstraction for learning storage operations used by orchestration flows.
/// </summary>
public interface ILearningStorageService
{
    /// <summary>
    /// Gets learnings that have embeddings available for semantic retrieval.
    /// </summary>
    Task<IReadOnlyList<Learning>> GetEmbeddedLearningsAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets learnings from a specific source task.
    /// </summary>
    Task<IReadOnlyList<Learning>> GetLearningsBySourceTaskAsync(
        string sourceTaskId,
        CancellationToken ct = default);

    /// <summary>
    /// Persists updates to an existing learning.
    /// </summary>
    Task UpdateLearningAsync(Learning learning, CancellationToken ct = default);

    /// <summary>
    /// Promotes multiple learnings from a completed task to specified target scopes.
    /// Implements KBP-REQ-002: Allow users to select promotion targets when task completes.
    /// </summary>
    Task<PromotionBatchResult> PromoteLearningsFromTaskAsync(
        string taskId,
        IReadOnlyList<LearningPromotionSelection> promotions,
        string promotedBy = "user",
        CancellationToken ct = default);
}
