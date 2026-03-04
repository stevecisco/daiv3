using Daiv3.Orchestration.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Daiv3.Orchestration;

/// <summary>
/// Validates skill permissions against sandbox configuration.
/// Enforces permission policies before skill execution.
/// </summary>
public class SkillPermissionValidator
{
    private readonly ILogger<SkillPermissionValidator> _logger;
    private readonly SkillSandboxConfiguration _sandboxConfig;

    public SkillPermissionValidator(
        ILogger<SkillPermissionValidator> logger,
        IOptions<SkillSandboxConfiguration> sandboxOptions)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _sandboxConfig = sandboxOptions?.Value ?? new SkillSandboxConfiguration();
    }

    /// <summary>
    /// Validates whether a skill is allowed to execute based on its permissions.
    /// </summary>
    /// <param name="skill">The skill to validate.</param>
    /// <param name="skillName">Skill name for configuration lookup.</param>
    /// <returns>Permission check result.</returns>
    public SkillPermissionCheckResult ValidatePermissions(ISkill skill, string skillName)
    {
        ArgumentNullException.ThrowIfNull(skill);
        ArgumentException.ThrowIfNullOrWhiteSpace(skillName);

        var result = new SkillPermissionCheckResult
        {
            RequestedPermissions = skill.Permissions ?? new List<string>()
        };

        // Check if skill is trusted (no permissions = implicitly trusted built-in skill)
        if (result.RequestedPermissions.Count == 0)
        {
            if (!_sandboxConfig.AllowUntrustedSkills)
            {
                _logger.LogWarning(
                    "Skill '{SkillName}' has no declared permissions but AllowUntrustedSkills=false - blocking execution",
                    skillName);

                result.IsAllowed = false;
                result.DenialReason = "Skill has no declared permissions and untrusted skills are not allowed";
                return result;
            }

            // Allow execution but log warning
            _logger.LogInformation(
                "Skill '{SkillName}' has no declared permissions - allowing as untrusted skill",
                skillName);

            result.IsAllowed = true;
            return result;
        }

        // Get skill-specific overrides if available
        var allowedPermissions = GetEffectiveAllowedPermissions(skillName);
        var deniedPermissions = GetEffectiveDeniedPermissions(skillName);

        // Check each requested permission
        foreach (var requestedPermission in result.RequestedPermissions)
        {
            // Check deny list first (highest priority)
            if (IsPermissionDenied(requestedPermission, deniedPermissions))
            {
                result.DeniedPermissions.Add(requestedPermission);
                _logger.LogWarning(
                    "Skill '{SkillName}' requested denied permission: {Permission}",
                    skillName, requestedPermission);
                continue;
            }

            // Check allow list (if configured)
            if (allowedPermissions.Count > 0 && !IsPermissionAllowed(requestedPermission, allowedPermissions))
            {
                result.DeniedPermissions.Add(requestedPermission);
                _logger.LogWarning(
                    "Skill '{SkillName}' requested permission not in allow list: {Permission}",
                    skillName, requestedPermission);
            }
        }

        // Determine final result
        result.IsAllowed = result.DeniedPermissions.Count == 0;

        if (!result.IsAllowed)
        {
            result.DenialReason = $"Permission denied: {string.Join(", ", result.DeniedPermissions)}";

            _logger.LogWarning(
                "Skill '{SkillName}' permission check failed. Denied: [{Denied}], Requested: [{Requested}]",
                skillName,
                string.Join(", ", result.DeniedPermissions),
                string.Join(", ", result.RequestedPermissions));
        }
        else
        {
            _logger.LogDebug(
                "Skill '{SkillName}' permission check passed. Permissions: [{Permissions}]",
                skillName,
                string.Join(", ", result.RequestedPermissions));
        }

        return result;
    }

    /// <summary>
    /// Gets the effective allowed permissions for a skill (global + overrides).
    /// </summary>
    private List<string> GetEffectiveAllowedPermissions(string skillName)
    {
        // Check for skill-specific override
        if (_sandboxConfig.SkillOverrides.TryGetValue(skillName, out var overrideConfig)
            && overrideConfig.AllowedPermissions != null)
        {
            return overrideConfig.AllowedPermissions;
        }

        // Use global allowed list
        return _sandboxConfig.GlobalAllowedPermissions;
    }

    /// <summary>
    /// Gets the effective denied permissions for a skill (global + overrides).
    /// </summary>
    private List<string> GetEffectiveDeniedPermissions(string skillName)
    {
        // Denied permissions are additive: global + skill-specific
        var denied = new List<string>(_sandboxConfig.GlobalDeniedPermissions);

        if (_sandboxConfig.SkillOverrides.TryGetValue(skillName, out var overrideConfig)
            && overrideConfig.DeniedPermissions != null)
        {
            denied.AddRange(overrideConfig.DeniedPermissions);
        }

        return denied;
    }

    /// <summary>
    /// Checks if a permission matches any pattern in the allowed list.
    /// Supports wildcards (e.g., "FileSystem.*" matches "FileSystem.Read").
    /// </summary>
    private bool IsPermissionAllowed(string permission, List<string> allowedList)
    {
        foreach (var allowedPattern in allowedList)
        {
            if (MatchesPermissionPattern(permission, allowedPattern))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if a permission matches any pattern in the denied list.
    /// Deny patterns take precedence over allow patterns.
    /// </summary>
    private bool IsPermissionDenied(string permission, List<string> deniedList)
    {
        foreach (var deniedPattern in deniedList)
        {
            if (MatchesPermissionPattern(permission, deniedPattern))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Matches a permission string against a pattern (supports wildcards).
    /// </summary>
    /// <param name="permission">The permission to check (e.g., "FileSystem.Read").</param>
    /// <param name="pattern">The pattern to match against (e.g., "FileSystem.*" or "*").</param>
    /// <returns>True if the permission matches the pattern.</returns>
    private bool MatchesPermissionPattern(string permission, string pattern)
    {
        // Exact match
        if (permission.Equals(pattern, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Wildcard: "*" matches everything
        if (pattern == "*")
        {
            return true;
        }

        // Hierarchical wildcard: "FileSystem.*" matches "FileSystem.Read", "FileSystem.Write", etc.
        if (pattern.EndsWith(".*"))
        {
            var prefix = pattern[..^2]; // Remove ".*"
            return permission.StartsWith(prefix + ".", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}
