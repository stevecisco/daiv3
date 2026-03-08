using Daiv3.Persistence.Entities;

namespace Daiv3.Orchestration.Services;

/// <summary>
/// Evaluates executable-skill isolation requirements before execution.
/// </summary>
public interface ISkillIsolationPolicyService
{
    /// <summary>
    /// Validates whether a skill can execute under current isolation runtime availability.
    /// </summary>
    /// <param name="skill">Executable skill record.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Policy decision indicating allow/deny and reason.</returns>
    Task<SkillIsolationPolicyResult> ValidateExecutionPolicyAsync(
        ExecutableSkill skill,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of evaluating an executable-skill isolation policy.
/// </summary>
/// <param name="IsExecutionAllowed">Whether execution is allowed by policy.</param>
/// <param name="RequiresIsolatedEnvironment">Whether metadata requires isolated execution.</param>
/// <param name="Reason">Optional policy denial reason.</param>
public record SkillIsolationPolicyResult(
    bool IsExecutionAllowed,
    bool RequiresIsolatedEnvironment,
    string? Reason = null)
{
    public static SkillIsolationPolicyResult Allow(bool requiresIsolatedEnvironment = false) =>
        new(true, requiresIsolatedEnvironment, null);

    public static SkillIsolationPolicyResult Deny(bool requiresIsolatedEnvironment, string reason) =>
        new(false, requiresIsolatedEnvironment, reason);
}
