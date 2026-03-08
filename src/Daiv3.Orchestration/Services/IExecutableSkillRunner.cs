using Daiv3.Core.Authorization;

namespace Daiv3.Orchestration.Services;

/// <summary>
/// Service for executing approved executable skills with pre-execution validation.
/// </summary>
public interface IExecutableSkillRunner
{
    /// <summary>
    /// Validates that a skill can be executed (approval status and integrity checks).
    /// </summary>
    /// <param name="skillId">The unique identifier of the skill to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A validation result indicating whether execution is allowed and any error details.</returns>
    Task<SkillValidationResult> ValidateBeforeExecutionAsync(string skillId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an approved executable skill with the provided parameters.
    /// </summary>
    /// <param name="skillId">The unique identifier of the skill to execute.</param>
    /// <param name="parameters">Dictionary of parameter name-value pairs to pass to the skill.</param>
    /// <param name="principal">The principal executing the skill (for audit logging).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the skill execution including success status, output, and logs.</returns>
    Task<SkillExecutionResult> ExecuteAsync(
        string skillId,
        IDictionary<string, string> parameters,
        SystemPrincipal principal,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of pre-execution validation for an executable skill.
/// </summary>
/// <param name="IsValid">Whether the skill can be executed.</param>
/// <param name="ErrorMessage">Error message if validation failed (null if valid).</param>
/// <param name="ErrorCode">Error code category (ApprovalRequired, IntegrityFailure, NotFound, etc.).</param>
public record SkillValidationResult(
    bool IsValid,
    string? ErrorMessage = null,
    string? ErrorCode = null)
{
    public static SkillValidationResult Success() => new(true);

    public static SkillValidationResult Failure(string errorCode, string errorMessage) =>
        new(false, errorMessage, errorCode);
}

/// <summary>
/// Result of executing an executable skill.
/// </summary>
/// <param name="Success">Whether the skill executed successfully.</param>
/// <param name="Output">Structured output from the skill (typically JSON).</param>
/// <param name="StandardOutput">Raw stdout from the skill execution.</param>
/// <param name="StandardError">Raw stderr from the skill execution.</param>
/// <param name="ExitCode">Process exit code (0 = success, non-zero = error).</param>
/// <param name="ExecutionTimeMs">Execution duration in milliseconds.</param>
/// <param name="ErrorMessage">High-level error message if execution failed.</param>
public record SkillExecutionResult(
    bool Success,
    string? Output = null,
    string? StandardOutput = null,
    string? StandardError = null,
    int? ExitCode = null,
    long? ExecutionTimeMs = null,
    string? ErrorMessage = null)
{
    public static SkillExecutionResult SuccessResult(
        string output,
        string standardOutput,
        int exitCode,
        long executionTimeMs) =>
        new(true, output, standardOutput, null, exitCode, executionTimeMs);

    public static SkillExecutionResult ErrorResult(
        string errorMessage,
        string? standardError = null,
        int? exitCode = null) =>
        new(false, null, null, standardError, exitCode, null, errorMessage);
}
