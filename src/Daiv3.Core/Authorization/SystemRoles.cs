namespace Daiv3.Core.Authorization;

/// <summary>
/// Defines authorization roles for system operations.
/// Implements ES-ACC-002 Phase 2: Administrative approval gates.
/// </summary>
public static class SystemRoles
{
    /// <summary>
    /// Administrator role that can approve/revoke executable skills.
    /// Required for skill approval workflow operations.
    /// </summary>
    public const string SkillAdministrator = "SkillAdministrator";

    /// <summary>
    /// Standard user role - can create and request approval for skills.
    /// </summary>
    public const string User = "User";

    /// <summary>
    /// System role - used for automated system operations.
    /// </summary>
    public const string System = "System";

    /// <summary>
    /// Checks if a role is an administrative role.
    /// </summary>
    public static bool IsAdministrativeRole(string role)
    {
        return role == SkillAdministrator;
    }
}

/// <summary>
/// Principal representing the currently executing user or system entity.
/// Simple implementation for Phase 2 - can be extended later with full identity system.
/// </summary>
public class SystemPrincipal
{
    /// <summary>
    /// Unique identifier for the principal (user ID, agent ID, or "system").
    /// </summary>
    public string PrincipalId { get; init; } = string.Empty;

    /// <summary>
    /// Roles assigned to this principal.
    /// </summary>
    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Checks if the principal has a specific role.
    /// </summary>
    public bool HasRole(string role)
    {
        return Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if the principal has administrator privileges.
    /// </summary>
    public bool IsAdministrator()
    {
        return HasRole(SystemRoles.SkillAdministrator);
    }

    /// <summary>
    /// Creates a system principal (for automated operations).
    /// </summary>
    public static SystemPrincipal CreateSystem()
    {
        return new SystemPrincipal
        {
            PrincipalId = "system",
            Roles = new[] { SystemRoles.System, SystemRoles.SkillAdministrator }
        };
    }

    /// <summary>
    /// Creates a user principal with specified roles.
    /// </summary>
    public static SystemPrincipal CreateUser(string userId, params string[] roles)
    {
        return new SystemPrincipal
        {
            PrincipalId = userId,
            Roles = roles
        };
    }
}
