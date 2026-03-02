namespace Daiv3.Persistence;

/// <summary>
/// Observer pattern interface for learning system events.
/// Enables real-time observability and audit trail collection for all learning operations.
/// Implements LM-NFR-002: Learnings SHOULD be transparent and auditable.
/// </summary>
public interface ILearningObserver
{
    /// <summary>
    /// Called when a learning is created.
    /// </summary>
    /// <param name="learningId">The unique identifier of the created learning.</param>
    /// <param name="title">The title of the learning.</param>
    /// <param name="triggerType">The trigger type (UserFeedback, SelfCorrection, etc.).</param>
    /// <param name="scope">The scope of the learning (Skill, Agent, Project, Domain, Global).</param>
    /// <param name="confidence">The confidence score (0-1).</param>
    /// <param name="sourceAgent">The source agent that created the learning.</param>
    /// <param name="createdAt">The creation timestamp.</param>
    Task OnLearningCreatedAsync(
        string learningId,
        string title,
        string triggerType,
        string scope,
        double confidence,
        string? sourceAgent,
        long createdAt);

    /// <summary>
    /// Called when learnings are retrieved for injection.
    /// </summary>
    /// <param name="retrievalId">A unique identifier for this retrieval operation.</param>
    /// <param name="count">Number of learnings retrieved.</param>
    /// <param name="queryText">The query used for retrieval (if applicable).</param>
    /// <param name="similarityThreshold">The threshold used for filtering (if applicable).</param>
    /// <param name="durationMs">How long the retrieval took.</param>
    Task OnLearningsRetrievedAsync(
        string retrievalId,
        int count,
        string? queryText,
        double? similarityThreshold,
        long durationMs);

    /// <summary>
    /// Called when learnings are injected into an agent prompt.
    /// </summary>
    /// <param name="learningIds">The IDs of learnings being injected.</param>
    /// <param name="agentId">The agent receiving the injection.</param>
    /// <param name="totalTokens">Total tokens used for learning injection.</param>
    /// <param name="injectedAt">The timestamp of injection.</param>
    Task OnLearningsInjectedAsync(
        IReadOnlyList<string> learningIds,
        string agentId,
        int totalTokens,
        long injectedAt);

    /// <summary>
    /// Called when a learning status changes (e.g., Active → Suppressed).
    /// </summary>
    /// <param name="learningId">The learning being modified.</param>
    /// <param name="previousStatus">The previous status.</param>
    /// <param name="newStatus">The new status.</param>
    /// <param name="modificationReason">Reason for the change (user action, system operation, etc.).</param>
    /// <param name="modifiedAt">The modification timestamp.</param>
    Task OnLearningStatusChangedAsync(
        string learningId,
        string previousStatus,
        string newStatus,
        string modificationReason,
        long modifiedAt);

    /// <summary>
    /// Called when a learning scope is promoted.
    /// </summary>
    /// <param name="learningId">The learning being promoted.</param>
    /// <param name="previousScope">The scope before promotion.</param>
    /// <param name="newScope">The scope after promotion.</param>
    /// <param name="promotedAt">The promotion timestamp.</param>
    Task OnLearningPromotedAsync(
        string learningId,
        string previousScope,
        string newScope,
        long promotedAt);

    /// <summary>
    /// Called when a learning is manually suppressed.
    /// </summary>
    /// <param name="learningId">The learning being suppressed.</param>
    /// <param name="suppressionReason">Reason for suppression (if provided).</param>
    /// <param name="suppressedAt">The suppression timestamp.</param>
    Task OnLearningSuppressionAsync(
        string learningId,
        string? suppressionReason,
        long suppressedAt);

    /// <summary>
    /// Called when a learning is marked as superseded.
    /// </summary>
    /// <param name="learningId">The learning being superseded.</param>
    /// <param name="supersedingLearningId">The ID of the learning that supersedes it (if known).</param>
    /// <param name="supersededAt">The supersession timestamp.</param>
    Task OnLearningSupersededAsync(
        string learningId,
        string? supersedingLearningId,
        long supersededAt);

    /// <summary>
    /// Called when a learning is applied (times_applied incremented).
    /// </summary>
    /// <param name="learningId">The learning being applied.</param>
    /// <param name="appliedBy">The agent or system applying the learning.</param>
    /// <param name="applicationType">Type of application (Injected, Manual, Automatic, etc.).</param>
    /// <param name="appliedAt">The application timestamp.</param>
    Task OnLearningAppliedAsync(
        string learningId,
        string appliedBy,
        string applicationType,
        long appliedAt);

    /// <summary>
    /// Called when learning metrics are captured.
    /// </summary>
    /// <param name="metrics">The current metrics snapshot.</param>
    Task OnMetricsCapturedAsync(LearningMetrics metrics);

    /// <summary>
    /// Called when an error occurs in learning operations.
    /// </summary>
    /// <param name="operationType">The type of operation that failed (Create, Retrieve, Inject, etc.).</param>
    /// <param name="errorMessage">The error message.</param>
    /// <param name="learningId">The learning ID involved (if applicable).</param>
    /// <param name="errorTime">The timestamp of the error.</param>
    Task OnLearningOperationErrorAsync(
        string operationType,
        string errorMessage,
        string? learningId,
        long errorTime);
}
