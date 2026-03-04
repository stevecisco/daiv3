namespace Daiv3.Orchestration;

/// <summary>
/// Sandbox isolation mode for skill execution.
/// </summary>
public enum SkillSandboxMode
{
    /// <summary>
    /// No sandboxing - trust all skills completely.
    /// Use for built-in trusted skills only.
    /// </summary>
    None = 0,

    /// <summary>
    /// Enforce permission validation only.
    /// Skills must declare permissions and are blocked if unauthorized.
    /// </summary>
    PermissionChecks = 1,

    /// <summary>
    /// Permission checks + resource monitoring.
    /// Tracks CPU and memory usage, auto-terminates excessive consumption.
    /// </summary>
    ResourceLimits = 2,

    /// <summary>
    /// Full process isolation (future implementation).
    /// Runs skills in separate processes with IPC communication.
    /// </summary>
    ProcessIsolation = 3
}

/// <summary>
/// Result of permission validation for a skill.
/// </summary>
public class SkillPermissionCheckResult
{
    /// <summary>
    /// Whether the skill is allowed to execute.
    /// </summary>
    public bool IsAllowed { get; set; }

    /// <summary>
    /// List of requested permissions.
    /// </summary>
    public List<string> RequestedPermissions { get; set; } = new();

    /// <summary>
    /// List of denied permissions.
    /// </summary>
    public List<string> DeniedPermissions { get; set; } = new();

    /// <summary>
    /// Reason for denial (if IsAllowed = false).
    /// </summary>
    public string? DenialReason { get; set; }
}

/// <summary>
/// Resource usage metrics during skill execution.
/// </summary>
public class SkillResourceMetrics
{
    /// <summary>
    /// Current memory usage in bytes.
    /// </summary>
    public long CurrentMemoryBytes { get; set; }

    /// <summary>
    /// Peak memory usage in bytes.
    /// </summary>
    public long PeakMemoryBytes { get; set; }

    /// <summary>
    /// CPU percentage (0-100).
    /// </summary>
    public int CpuPercentage { get; set; }

    /// <summary>
    /// Total execution duration.
    /// </summary>
    public TimeSpan ExecutionDuration { get; set; }

    /// <summary>
    /// Whether resource limits were exceeded.
    /// </summary>
    public bool LimitsExceeded { get; set; }

    /// <summary>
    /// Details of resource violations (if any).
    /// </summary>
    public string? ViolationDetails { get; set; }
}

/// <summary>
/// Per-skill sandbox configuration override.
/// </summary>
public class SkillSandboxOverride
{
    /// <summary>
    /// Override sandbox mode for this skill.
    /// </summary>
    public SkillSandboxMode? Mode { get; set; }

    /// <summary>
    /// Override allowed permissions for this skill.
    /// </summary>
    public List<string>? AllowedPermissions { get; set; }

    /// <summary>
    /// Override denied permissions for this skill.
    /// </summary>
    public List<string>? DeniedPermissions { get; set; }

    /// <summary>
    /// Override maximum memory limit for this skill (bytes).
    /// </summary>
    public long? MaxMemoryBytes { get; set; }

    /// <summary>
    /// Override maximum CPU percentage for this skill.
    /// </summary>
    public int? MaxCpuPercentage { get; set; }
}

/// <summary>
/// Configuration for skill execution sandboxing.
/// Provides security through permission enforcement and resource monitoring.
/// </summary>
public class SkillSandboxConfiguration
{
    /// <summary>
    /// Default sandbox mode for all skills.
    /// Can be overridden per skill.
    /// </summary>
    public SkillSandboxMode DefaultMode { get; set; } = SkillSandboxMode.PermissionChecks;

    /// <summary>
    /// Maximum memory a skill can consume (bytes).
    /// Default: 500 MB.
    /// </summary>
    public long MaxMemoryBytes { get; set; } = 500 * 1024 * 1024;

    /// <summary>
    /// Maximum CPU percentage a skill can consume (0-100).
    /// Default: 80%.
    /// </summary>
    public int MaxCpuPercentage { get; set; } = 80;

    /// <summary>
    /// Interval for resource monitoring checks (milliseconds).
    /// Shorter intervals = more accurate but higher overhead.
    /// Default: 1000 ms (1 second).
    /// </summary>
    public int ResourceCheckIntervalMs { get; set; } = 1000;

    /// <summary>
    /// Global list of allowed permissions.
    /// Skills requesting other permissions will be blocked.
    /// Empty list = all permissions allowed (use deny list instead).
    /// </summary>
    public List<string> GlobalAllowedPermissions { get; set; } = new();

    /// <summary>
    /// Global list of denied permissions.
    /// Takes precedence over allowed list.
    /// </summary>
    public List<string> GlobalDeniedPermissions { get; set; } = new();

    /// <summary>
    /// Whether to allow skills without explicit trust declaration.
    /// True preserves backward compatibility for existing skills without permissions.
    /// Set to false in hardened environments.
    /// </summary>
    public bool AllowUntrustedSkills { get; set; } = true;

    /// <summary>
    /// Per-skill configuration overrides.
    /// Key = skill name, Value = override configuration.
    /// </summary>
    public Dictionary<string, SkillSandboxOverride> SkillOverrides { get; set; } = new();

    /// <summary>
    /// Gets the effective sandbox mode for a specific skill.
    /// </summary>
    public SkillSandboxMode GetEffectiveMode(string skillName)
    {
        if (SkillOverrides.TryGetValue(skillName, out var overrideConfig)
            && overrideConfig.Mode.HasValue)
        {
            return overrideConfig.Mode.Value;
        }

        return DefaultMode;
    }

    /// <summary>
    /// Gets the effective memory limit for a specific skill.
    /// </summary>
    public long GetEffectiveMaxMemory(string skillName)
    {
        if (SkillOverrides.TryGetValue(skillName, out var overrideConfig)
            && overrideConfig.MaxMemoryBytes.HasValue)
        {
            return overrideConfig.MaxMemoryBytes.Value;
        }

        return MaxMemoryBytes;
    }

    /// <summary>
    /// Gets the effective CPU limit for a specific skill.
    /// </summary>
    public int GetEffectiveMaxCpu(string skillName)
    {
        if (SkillOverrides.TryGetValue(skillName, out var overrideConfig)
            && overrideConfig.MaxCpuPercentage.HasValue)
        {
            return overrideConfig.MaxCpuPercentage.Value;
        }

        return MaxCpuPercentage;
    }
}

/// <summary>
/// Standard permission strings for skill declarations.
/// Skills should use these constants for consistency.
/// </summary>
public static class SkillPermissions
{
    /// <summary>
    /// Read files and directories.
    /// </summary>
    public const string FileSystemRead = "FileSystem.Read";

    /// <summary>
    /// Write, create, delete files and directories.
    /// </summary>
    public const string FileSystemWrite = "FileSystem.Write";

    /// <summary>
    /// All file system operations.
    /// </summary>
    public const string FileSystemAll = "FileSystem.*";

    /// <summary>
    /// Network requests (HTTP, sockets, etc.).
    /// </summary>
    public const string NetworkAccess = "Network.Access";

    /// <summary>
    /// Read Windows Registry.
    /// </summary>
    public const string RegistryRead = "Registry.Read";

    /// <summary>
    /// Write to Windows Registry.
    /// </summary>
    public const string RegistryWrite = "Registry.Write";

    /// <summary>
    /// Launch external processes.
    /// </summary>
    public const string ProcessExecute = "Process.Execute";

    /// <summary>
    /// Read from databases.
    /// </summary>
    public const string DatabaseRead = "Database.Read";

    /// <summary>
    /// Write to databases.
    /// </summary>
    public const string DatabaseWrite = "Database.Write";

    /// <summary>
    /// Call MCP tool servers.
    /// </summary>
    public const string McpInvoke = "MCP.Invoke";

    /// <summary>
    /// Windows UI automation APIs.
    /// </summary>
    public const string UiAutomation = "UI.Automation";

    /// <summary>
    /// Modify system configuration.
    /// </summary>
    public const string SystemConfiguration = "System.Configuration";

    /// <summary>
    /// All permissions (admin/trusted skills only).
    /// </summary>
    public const string All = "*";
}
