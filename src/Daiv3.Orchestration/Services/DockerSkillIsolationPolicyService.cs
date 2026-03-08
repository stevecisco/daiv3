using Daiv3.Persistence.Entities;
using Microsoft.Extensions.Logging;

namespace Daiv3.Orchestration.Services;

/// <summary>
/// Phase-5 isolation policy stub that gates execution when metadata requires Docker isolation.
/// </summary>
public sealed class DockerSkillIsolationPolicyService : ISkillIsolationPolicyService
{
    private readonly ILogger<DockerSkillIsolationPolicyService> _logger;

    public DockerSkillIsolationPolicyService(ILogger<DockerSkillIsolationPolicyService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SkillIsolationPolicyResult> ValidateExecutionPolicyAsync(
        ExecutableSkill skill,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(skill);

        var requiresIsolation = await RequiresIsolatedEnvironmentAsync(skill, cancellationToken).ConfigureAwait(false);
        if (!requiresIsolation)
        {
            return SkillIsolationPolicyResult.Allow(requiresIsolatedEnvironment: false);
        }

        // Future extension point: detect Docker runtime and execute in isolated container.
        var dockerAvailable = false;

        if (!dockerAvailable)
        {
            const string reason = "Skill requires isolated execution, but Docker runtime is not available in this build. Install Docker Desktop or set 'requiresIsolatedEnvironment: false' in skill metadata.";
            _logger.LogWarning(
                "Isolation policy denied skill {SkillId} ({SkillName}): {Reason}",
                skill.SkillId,
                skill.Name,
                reason);

            return SkillIsolationPolicyResult.Deny(requiresIsolatedEnvironment: true, reason);
        }

        _logger.LogInformation(
            "Skill {SkillId} requires isolated execution. Docker detected, but containerized execution is not implemented yet.",
            skill.SkillId);

        return SkillIsolationPolicyResult.Deny(
            requiresIsolatedEnvironment: true,
            reason: "Isolated execution requested and Docker detected, but isolated skill execution is not implemented yet.");
    }

    private static async Task<bool> RequiresIsolatedEnvironmentAsync(
        ExecutableSkill skill,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(skill.MetadataPath) || !File.Exists(skill.MetadataPath))
        {
            return false;
        }

        var metadata = await File.ReadAllTextAsync(skill.MetadataPath, cancellationToken).ConfigureAwait(false);
        var lines = metadata.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("requiresIsolatedEnvironment", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var separator = trimmed.IndexOf(':');
            if (separator < 0 || separator == trimmed.Length - 1)
            {
                return false;
            }

            var value = trimmed[(separator + 1)..].Trim();
            return bool.TryParse(value, out var parsed) && parsed;
        }

        return false;
    }
}
