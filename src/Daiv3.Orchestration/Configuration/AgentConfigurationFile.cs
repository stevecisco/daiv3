namespace Daiv3.Orchestration.Configuration;

/// <summary>
/// Represents an agent definition loaded from a JSON or YAML configuration file.
/// This contract maps directly to the declarative configuration format, supporting both JSON and YAML parsing.
/// </summary>
public class AgentConfigurationFile
{
    /// <summary>
    /// Agent name (required).
    /// Must be unique within the system.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Agent purpose or goal description (required).
    /// Describes what the agent is designed to do.
    /// </summary>
    public required string Purpose { get; set; }

    /// <summary>
    /// List of skill names enabled for this agent (optional).
    /// Skills must be registered in the SkillRegistry.
    /// Example: ["skill-search", "skill-analyze", "skill-generate"]
    /// </summary>
    public List<string> EnabledSkills { get; set; } = new();

    /// <summary>
    /// Agent-specific configuration parameters (optional).
    /// Key-value pairs for custom settings, model preferences, iteration limits, and other config.
    /// Examples:
    ///   - "model_preference": "phi-4"
    ///   - "max_iterations": "15"
    ///   - "output_format": "json"
    ///   - "token_budget": "20000"
    /// </summary>
    public Dictionary<string, string> Config { get; set; } = new();
}

/// <summary>
/// Represents a collection of agent definitions loaded from a configuration file or directory.
/// Supports both single-agent files and multi-agent configuration batches.
/// </summary>
public class AgentConfigurationBatch
{
    /// <summary>
    /// Human-readable name for this batch (optional).
    /// Used for logging and batch identification.
    /// </summary>
    public string? BatchName { get; set; }

    /// <summary>
    /// Description of the agents in this batch (optional).
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// List of agent configurations in this batch.
    /// Can be empty if this is a single-agent file.
    /// </summary>
    public List<AgentConfigurationFile> Agents { get; set; } = new();

    /// <summary>
    /// Metadata about the configuration (optional).
    /// Can include version, author, date created, etc.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
/// Validation result for agent configuration.
/// </summary>
public class AgentConfigurationValidationResult
{
    /// <summary>
    /// Whether the configuration is valid.
    /// </summary>
    public bool IsValid { get; set; } = true;

    /// <summary>
    /// List of validation errors (if any).
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// List of validation warnings (non-fatal issues).
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Add a validation error.
    /// </summary>
    public void AddError(string error)
    {
        Errors.Add(error);
        IsValid = false;
    }

    /// <summary>
    /// Add a validation warning.
    /// </summary>
    public void AddWarning(string warning)
    {
        Warnings.Add(warning);
    }
}
