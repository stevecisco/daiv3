namespace Daiv3.Persistence;

/// <summary>
/// Metrics snapshot for learning system observability and auditing.
/// Captures aggregated learning statistics for transparency and performance monitoring.
/// Implements LM-NFR-002: Learnings SHOULD be transparent and auditable.
/// </summary>
public record LearningMetrics
{
    /// <summary>
    /// Total learnings created in the system.
    /// </summary>
    public int TotalLearningsCreated { get; init; }

    /// <summary>
    /// Current count of active learnings.
    /// </summary>
    public int ActiveLearningsCount { get; init; }

    /// <summary>
    /// Current count of suppressed learnings.
    /// </summary>
    public int SuppressedLearningsCount { get; init; }

    /// <summary>
    /// Current count of superseded learnings.
    /// </summary>
    public int SupersededLearningsCount { get; init; }

    /// <summary>
    /// Current count of archived learnings.
    /// </summary>
    public int ArchivedLearningsCount { get; init; }

    /// <summary>
    /// Learnings created by trigger type: UserFeedback, SelfCorrection, CompilationError, ToolFailure, KnowledgeConflict, Explicit.
    /// </summary>
    public IReadOnlyDictionary<string, int> CreationsByTriggerType { get; init; } = new Dictionary<string, int>();

    /// <summary>
    /// Learnings distributed by scope: Skill, Agent, Project, Domain, Global.
    /// </summary>
    public IReadOnlyDictionary<string, int> DistributionByScope { get; init; } = new Dictionary<string, int>();

    /// <summary>
    /// Total times learnings have been applied across the system.
    /// </summary>
    public long TotalLearningsApplied { get; init; }

    /// <summary>
    /// Average confidence score of active learnings (0-1).
    /// </summary>
    public double AverageConfidenceScore { get; init; }

    /// <summary>
    /// Total learning retrieval operations performed (for transparency into usage patterns).
    /// </summary>
    public long TotalRetrievalOperations { get; init; }

    /// <summary>
    /// Total learning injections into agent prompts.
    /// </summary>
    public long TotalInjections { get; init; }

    /// <summary>
    /// Average tokens per learning injection (for cost transparency).
    /// </summary>
    public double AverageTokensPerInjection { get; init; }

    /// <summary>
    /// Count of learning promotion operations performed.
    /// </summary>
    public int PromotionOperations { get; init; }

    /// <summary>
    /// Count of learning suppression operations performed.
    /// </summary>
    public int SuppressionOperations { get; init; }

    /// <summary>
    /// Count of learning supersession operations performed.
    /// </summary>
    public int SupersessionOperations { get; init; }

    /// <summary>
    /// Timestamp when this metrics snapshot was captured.
    /// </summary>
    public DateTimeOffset CapturedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Total learnings available for injection (Active + Global-scoped).
    /// </summary>
    public int InjectableLearningsCount { get; init; }
}
