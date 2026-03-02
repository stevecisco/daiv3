namespace Daiv3.Orchestration.Interfaces;

/// <summary>
/// Result from skill execution.
/// </summary>
public class SkillExecutionResult
{
    /// <summary>
    /// Whether the skill executed successfully.
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// The skill's output.
    /// </summary>
    public object? Output { get; set; }
    
    /// <summary>
    /// Error message if execution failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// Exception details if an unhandled error occurred.
    /// </summary>
    public Exception? Exception { get; set; }
    
    /// <summary>
    /// Execution time in milliseconds.
    /// </summary>
    public long ElapsedMilliseconds { get; set; }
    
    /// <summary>
    /// Resource usage metrics during execution (if sandboxing enabled).
    /// </summary>
    public SkillResourceMetrics? ResourceMetrics { get; set; }
}

/// <summary>
/// Request to execute a skill with parameters.
/// </summary>
public class SkillExecutionRequest
{
    /// <summary>
    /// Skill name to execute.
    /// </summary>
    public required string SkillName { get; set; }
    
    /// <summary>
    /// Parameters for skill execution.
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; } = new();
    
    /// <summary>
    /// Optional timeout in seconds. If null, uses default timeout.
    /// </summary>
    public int? TimeoutSeconds { get; set; }
    
    /// <summary>
    /// Optional caller context for logging and tracing.
    /// </summary>
    public string? CallerContext { get; set; }
}

/// <summary>
/// Service for executing skills directly or within agent workflows.
/// </summary>
public interface ISkillExecutor
{
    /// <summary>
    /// Executes a skill with the provided parameters.
    /// </summary>
    /// <param name="request">The skill execution request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing output or error information.</returns>
    Task<SkillExecutionResult> ExecuteAsync(SkillExecutionRequest request, CancellationToken ct = default);
    
    /// <summary>
    /// Validates that a skill exists and can be executed.
    /// </summary>
    /// <param name="skillName">The skill to validate.</param>
    /// <returns>True if the skill exists and is executable, false otherwise.</returns>
    bool CanExecute(string skillName);
    
    /// <summary>
    /// Gets metadata about whether the skill can be executed with the given parameters.
    /// </summary>
    /// <param name="skillName">The skill name.</param>
    /// <param name="parameters">The parameters to validate.</param>
    /// <returns>Validation result with error messages if validation fails.</returns>
    SkillParameterValidationResult ValidateParameters(string skillName, Dictionary<string, object> parameters);
}

/// <summary>
/// Result from parameter validation.
/// </summary>
public class SkillParameterValidationResult
{
    /// <summary>
    /// Whether parameters are valid.
    /// </summary>
    public bool IsValid { get; set; }
    
    /// <summary>
    /// Validation error messages if any.
    /// </summary>
    public List<string> Errors { get; set; } = new();
    
    /// <summary>
    /// Validation warnings (non-blocking issues).
    /// </summary>
    public List<string> Warnings { get; set; } = new();
}
