namespace Daiv3.Orchestration.Interfaces;

/// <summary>
/// Evaluates whether agent output meets success criteria.
/// Used to determine if self-correction is needed.
/// </summary>
public interface ISuccessCriteriaEvaluator
{
    /// <summary>
    /// Evaluates whether the given output satisfies the success criteria.
    /// </summary>
    /// <param name="successCriteria">The criteria to evaluate against (null/empty = always success).</param>
    /// <param name="output">The output from the agent step to evaluate.</param>
    /// <param name="context">Additional context (task goal, previous steps, etc.).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Evaluation result indicating success and confidence.</returns>
    Task<SuccessEvaluationResult> EvaluateAsync(
        string? successCriteria,
        string output,
        SuccessCriteriaContext context,
        CancellationToken ct = default);

    /// <summary>
    /// Validates that success criteria are syntactically valid.
    /// </summary>
    /// <param name="successCriteria">The criteria to validate.</param>
    /// <returns>Validation result with any errors.</returns>
    SuccessCriteriaValidationResult Validate(string? successCriteria);
}

/// <summary>
/// Result of evaluating success criteria against agent output.
/// </summary>
public class SuccessEvaluationResult
{
    /// <summary>
    /// Whether the output meets the success criteria.
    /// </summary>
    public required bool MeetsCriteria { get; set; }

    /// <summary>
    /// Confidence score for the evaluation (0.0 to 1.0).
    /// Higher scores indicate more confident evaluation.
    /// </summary>
    public decimal ConfidenceScore { get; set; } = 1.0m;

    /// <summary>
    /// Detailed explanation of the evaluation result.
    /// Useful for logs and diagnostics.
    /// </summary>
    public string? EvaluationMessage { get; set; }

    /// <summary>
    /// Suggested corrections if criteria are not met.
    /// Used for self-correction iteration context.
    /// </summary>
    public string? SuggestedCorrection { get; set; }

    /// <summary>
    /// The evaluation method used (Pattern/Keyword/LlmBased/etc).
    /// </summary>
    public string EvaluationMethod { get; set; } = "Unknown";
}

/// <summary>
/// Context information for success criteria evaluation.
/// </summary>
public class SuccessCriteriaContext
{
    /// <summary>
    /// The original task goal.
    /// </summary>
    public string? TaskGoal { get; set; }

    /// <summary>
    /// The iteration number for context.
    /// </summary>
    public int IterationNumber { get; set; }

    /// <summary>
    /// Previous step outputs (for context).
    /// </summary>
    public List<string>? PreviousStepOutputs { get; set; }

    /// <summary>
    /// Any failure context from previous iterations.
    /// </summary>
    public string? FailureContext { get; set; }

    /// <summary>
    /// Additional metadata for evaluation.
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Result of validating success criteria syntax.
/// </summary>
public class SuccessCriteriaValidationResult
{
    /// <summary>
    /// Whether the criteria syntax is valid.
    /// </summary>
    public bool IsValid { get; set; } = true;

    /// <summary>
    /// Error messages if validation failed.
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Warning messages (criteria are valid but may need attention).
    /// </summary>
    public List<string> Warnings { get; set; } = new();
}
