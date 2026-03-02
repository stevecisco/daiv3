namespace Daiv3.Orchestration.Models;

/// <summary>
/// Base class for learning trigger contexts.
/// Contains common data needed to create a learning record.
/// </summary>
public abstract class LearningTriggerContext
{
    /// <summary>
    /// The trigger type (UserFeedback, SelfCorrection, CompilationError, ToolFailure, KnowledgeConflict, Explicit).
    /// </summary>
    public string TriggerType { get; init; } = string.Empty;
    
    /// <summary>
    /// Short human-readable summary of what was learned.
    /// </summary>
    public required string Title { get; set; }
    
    /// <summary>
    /// Full explanation: what happened, what was wrong, what the correct approach is.
    /// </summary>
    public required string Description { get; set; }
    
    /// <summary>
    /// Scope where this applies: Global, Agent, Skill, Project, Domain.
    /// </summary>
    public string Scope { get; set; } = "Global";
    
    /// <summary>
    /// The agent or skill that generated this learning (nullable).
    /// </summary>
    public string? SourceAgent { get; set; }
    
    /// <summary>
    /// The task or session in which the learning occurred (nullable).
    /// </summary>
    public string? SourceTaskId { get; set; }
    
    /// <summary>
    /// Comma-separated tags for filtering (e.g., 'csharp', 'file-io', 'prompt-format').
    /// </summary>
    public string? Tags { get; set; }
    
    /// <summary>
    /// Confidence score 0.0-1.0 (default: 0.7).
    /// High confidence = injected automatically, low confidence = injected as suggestion.
    /// </summary>
    public double Confidence { get; set; } = 0.7;
    
    /// <summary>
    /// Agent ID or 'user' if manually entered.
    /// </summary>
    public string CreatedBy { get; set; } = "system";
}

/// <summary>
/// Context for learnings triggered by user feedback.
/// </summary>
public class UserFeedbackTriggerContext : LearningTriggerContext
{
    public UserFeedbackTriggerContext()
    {
        TriggerType = "UserFeedback";
        Confidence = 0.95; // User feedback is highly trustworthy
    }
    
    /// <summary>
    /// The original output that the user corrected.
    /// </summary>
    public string? OriginalOutput { get; set; }
    
    /// <summary>
    /// The corrected output provided by the user.
    /// </summary>
    public string? CorrectedOutput { get; set; }
    
    /// <summary>
    /// User's explanation of the correction (if provided).
    /// </summary>
    public string? UserExplanation { get; set; }
}

/// <summary>
/// Context for learnings triggered by agent self-correction.
/// </summary>
public class SelfCorrectionTriggerContext : LearningTriggerContext
{
    public SelfCorrectionTriggerContext()
    {
        TriggerType = "SelfCorrection";
        Confidence = 0.8; // Self-corrections are generally reliable
    }
    
    /// <summary>
    /// The iteration number where the failure occurred.
    /// </summary>
    public int FailedIteration { get; set; }
    
    /// <summary>
    /// The output from the failed iteration.
    /// </summary>
    public string? FailedOutput { get; set; }
    
    /// <summary>
    /// The reason the iteration failed criteria evaluation.
    /// </summary>
    public string? FailureReason { get; set; }
    
    /// <summary>
    /// The iteration number where success was achieved.
    /// </summary>
    public int SuccessIteration { get; set; }
    
    /// <summary>
    /// The corrected output that met criteria.
    /// </summary>
    public string? SuccessOutput { get; set; }
    
    /// <summary>
    /// The suggested correction from the evaluator.
    /// </summary>
    public string? SuggestedCorrection { get; set; }
}

/// <summary>
/// Context for learnings triggered by compilation or runtime errors.
/// </summary>
public class CompilationErrorTriggerContext : LearningTriggerContext
{
    public CompilationErrorTriggerContext()
    {
        TriggerType = "CompilationError";
        Confidence = 0.85; // Compilation errors have clear before/after states
    }
    
    /// <summary>
    /// The code that caused the compilation error.
    /// </summary>
    public string? ErrorCode { get; set; }
    
    /// <summary>
    /// The compilation error message.
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// The corrected code that compiles successfully.
    /// </summary>
    public string? FixedCode { get; set; }
    
    /// <summary>
    /// Programming language (e.g., "csharp", "python").
    /// </summary>
    public string? Language { get; set; }
}

/// <summary>
/// Context for learnings triggered by tool invocation failures.
/// </summary>
public class ToolFailureTriggerContext : LearningTriggerContext
{
    public ToolFailureTriggerContext()
    {
        TriggerType = "ToolFailure";
        Confidence = 0.8; // Tool failures have clear invocation patterns
    }
    
    /// <summary>
    /// The name of the tool that failed.
    /// </summary>
    public string? ToolName { get; set; }
    
    /// <summary>
    /// The incorrect invocation parameters.
    /// </summary>
    public string? IncorrectInvocation { get; set; }
    
    /// <summary>
    /// The error returned by the tool.
    /// </summary>
    public string? ToolError { get; set; }
    
    /// <summary>
    /// The correct invocation parameters that succeeded.
    /// </summary>
    public string? CorrectInvocation { get; set; }
}

/// <summary>
/// Context for learnings triggered by contradicting knowledge.
/// </summary>
public class KnowledgeConflictTriggerContext : LearningTriggerContext
{
    public KnowledgeConflictTriggerContext()
    {
        TriggerType = "KnowledgeConflict";
        Confidence = 0.6; // Conflicts require human judgment
    }
    
    /// <summary>
    /// The previously held belief or knowledge.
    /// </summary>
    public string? PreviousBelief { get; set; }
    
    /// <summary>
    /// The new contradicting information.
    /// </summary>
    public string? NewInformation { get; set; }
    
    /// <summary>
    /// The source of the new information (document, API, user, etc.).
    /// </summary>
    public string? ConflictSource { get; set; }
    
    /// <summary>
    /// The resolved belief after reconciliation.
    /// </summary>
    public string? Resolution { get; set; }
}

/// <summary>
/// Context for learnings explicitly created by agents or skills.
/// </summary>
public class ExplicitTriggerContext : LearningTriggerContext
{
    public ExplicitTriggerContext()
    {
        TriggerType = "Explicit";
        Confidence = 0.75; // Explicit learnings depend on agent quality
    }
    
    /// <summary>
    /// The reason the agent decided to create this learning.
    /// </summary>
    public string? AgentReasoning { get; set; }
    
    /// <summary>
    /// Any supporting evidence or context the agent captured.
    /// </summary>
    public string? SupportingEvidence { get; set; }
}
