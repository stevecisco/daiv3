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
    /// Persists updates to an existing learning.
    /// </summary>
    Task UpdateLearningAsync(Learning learning, CancellationToken ct = default);
}
