using Daiv3.Orchestration.Models;
using Daiv3.Persistence.Entities;

namespace Daiv3.Orchestration.Interfaces;

/// <summary>
/// Service for creating and managing learning records.
/// Handles learning creation triggers from various sources per LM-REQ-001.
/// </summary>
public interface ILearningService
{
    /// <summary>
    /// Creates a learning record from a trigger context.
    /// Generates embeddings for semantic retrieval and persists to database.
    /// </summary>
    /// <param name="context">The trigger context containing learning details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created learning record.</returns>
    /// <exception cref="ArgumentNullException">If context is null.</exception>
    /// <exception cref="InvalidOperationException">If learning creation or persistence fails.</exception>
    Task<Learning> CreateLearningAsync(LearningTriggerContext context, CancellationToken ct = default);

    /// <summary>
    /// Creates a learning from agent self-correction.
    /// Automatically extracts relevant information from the correction context.
    /// </summary>
    /// <param name="context">Self-correction trigger context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created learning record.</returns>
    Task<Learning> CreateSelfCorrectionLearningAsync(
        SelfCorrectionTriggerContext context,
        CancellationToken ct = default);

    /// <summary>
    /// Creates a learning from user feedback.
    /// High confidence since user corrections are authoritative.
    /// </summary>
    /// <param name="context">User feedback trigger context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created learning record.</returns>
    Task<Learning> CreateUserFeedbackLearningAsync(
        UserFeedbackTriggerContext context,
        CancellationToken ct = default);

    /// <summary>
    /// Creates a learning from a compilation or runtime error resolution.
    /// Captures before/after code states and error messages.
    /// </summary>
    /// <param name="context">Compilation error trigger context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created learning record.</returns>
    Task<Learning> CreateCompilationErrorLearningAsync(
        CompilationErrorTriggerContext context,
        CancellationToken ct = default);

    /// <summary>
    /// Creates a learning from a tool invocation failure and subsequent success.
    /// Captures incorrect and correct invocation patterns.
    /// </summary>
    /// <param name="context">Tool failure trigger context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created learning record.</returns>
    Task<Learning> CreateToolFailureLearningAsync(
        ToolFailureTriggerContext context,
        CancellationToken ct = default);

    /// <summary>
    /// Creates a learning from contradicting knowledge reconciliation.
    /// Captures previous belief, new information, and resolution.
    /// </summary>
    /// <param name="context">Knowledge conflict trigger context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created learning record.</returns>
    Task<Learning> CreateKnowledgeConflictLearningAsync(
        KnowledgeConflictTriggerContext context,
        CancellationToken ct = default);

    /// <summary>
    /// Creates a learning from an explicit agent or skill call.
    /// Agent decides to record something worth remembering.
    /// </summary>
    /// <param name="context">Explicit trigger context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created learning record.</returns>
    Task<Learning> CreateExplicitLearningAsync(
        ExplicitTriggerContext context,
        CancellationToken ct = default);
}
