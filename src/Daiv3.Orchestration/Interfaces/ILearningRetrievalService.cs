using Daiv3.Persistence.Entities;

namespace Daiv3.Orchestration.Interfaces;

/// <summary>
/// Service for retrieving relevant learnings for agent execution per LM-REQ-005.
/// Uses semantic similarity to find learnings relevant to the current task context.
/// </summary>
public interface ILearningRetrievalService
{
    /// <summary>
    /// Retrieves relevant learnings for agent task execution.
    /// Uses semantic similarity between task context and learning descriptions.
    /// </summary>
    /// <param name="context">The learning retrieval context containing task information.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of retrieved learnings with similarity scores, ranked by relevance.</returns>
    /// <exception cref="ArgumentNullException">If context is null.</exception>
    Task<IReadOnlyList<RetrievedLearning>> RetrieveLearningsAsync(
        LearningRetrievalContext context,
        CancellationToken ct = default);
}

/// <summary>
/// Context information for learning retrieval.
/// Contains task details and filtering criteria.
/// </summary>
public class LearningRetrievalContext
{
    /// <summary>
    /// The task goal or description to find relevant learnings for.
    /// Used to generate query embedding for semantic search.
    /// </summary>
    public required string TaskGoal { get; init; }

    /// <summary>
    /// Optional agent identifier for agent-specific learning filtering.
    /// If specified, includes both agent-specific and global learnings.
    /// </summary>
    public string? AgentId { get; init; }

    /// <summary>
    /// Optional scope filter. Common scopes: Global, Agent, Skill, Project, Domain.
    /// If null, learnings from all scopes are considered.
    /// </summary>
    public string? Scope { get; init; }

    /// <summary>
    /// Minimum confidence threshold for retrieved learnings (0.0 to 1.0).
    /// Learnings with confidence below this threshold are excluded.
    /// Default: 0.5
    /// </summary>
    public double MinConfidence { get; init; } = 0.5;

    /// <summary>
    /// Minimum similarity score for retrieved learnings (0.0 to 1.0).
    /// Learnings with similarity below this threshold are excluded.
    /// Default: 0.3
    /// </summary>
    public double MinSimilarity { get; init; } = 0.3;

    /// <summary>
    /// Maximum number of learnings to retrieve.
    /// Default: 5
    /// </summary>
    public int MaxResults { get; init; } = 5;

    /// <summary>
    /// Maximum allowed retrieval latency in milliseconds.
    /// Retrieval that exceeds this budget is cancelled to avoid blocking foreground flows.
    /// Default: 150ms
    /// </summary>
    public int MaxRetrievalTimeMs { get; init; } = 150;

    /// <summary>
    /// Warning threshold for retrieval latency in milliseconds.
    /// Retrievals above this threshold are logged for observability.
    /// Default: 75ms
    /// </summary>
    public int SlowRetrievalWarningMs { get; init; } = 75;

    /// <summary>
    /// Maximum number of candidate learnings scored for similarity.
    /// This limits CPU work and keeps retrieval latency bounded.
    /// Default: 256
    /// </summary>
    public int MaxCandidatesToScore { get; init; } = 256;

    /// <summary>
    /// Optional additional context for semantic search.
    /// Appended to TaskGoal when generating query embedding.
    /// </summary>
    public Dictionary<string, string>? AdditionalContext { get; init; }
}

/// <summary>
/// A learning retrieved with its semantic similarity score.
/// </summary>
public class RetrievedLearning
{
    /// <summary>
    /// The learning record.
    /// </summary>
    public required Learning Learning { get; init; }

    /// <summary>
    /// Semantic similarity score between the task context and this learning (0.0 to 1.0).
    /// Higher scores indicate greater relevance.
    /// </summary>
    public required double SimilarityScore { get; init; }

    /// <summary>
    /// Rank of this learning in the result set (1-indexed).
    /// </summary>
    public required int Rank { get; init; }
}
