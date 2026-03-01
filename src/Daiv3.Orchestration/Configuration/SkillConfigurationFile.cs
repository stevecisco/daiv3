using Daiv3.Orchestration.Interfaces;

namespace Daiv3.Orchestration.Configuration;

/// <summary>
/// Represents a skill definition loaded from a JSON or YAML configuration file.
/// This contract maps directly to the declarative skill configuration format, supporting both JSON and YAML parsing.
/// </summary>
public class SkillConfigurationFile
{
    /// <summary>
    /// Skill name (required).
    /// Must be unique within the system.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Skill description (required).
    /// Describes what the skill does.
    /// </summary>
    public required string Description { get; set; }

    /// <summary>
    /// Skill category (optional, defaults to "Other").
    /// Must be one of: ReasoningAndAnalysis, Code, Document, DataAndVisualization, WebAndResearch, ProjectManagement, Communication, Other, Unspecified.
    /// </summary>
    public string Category { get; set; } = "Other";

    /// <summary>
    /// Skill source type (optional, defaults to "UserDefined" for config-loaded skills).
    /// Can be: BuiltIn, UserDefined, or Imported.
    /// </summary>
    public string Source { get; set; } = "UserDefined";

    /// <summary>
    /// List of input parameters this skill accepts (optional).
    /// Each parameter specifies name, type, required flag, and description.
    /// </summary>
    public List<SkillParameterConfiguration> Inputs { get; set; } = new();

    /// <summary>
    /// Output schema describing what the skill produces (required).
    /// Specifies the output type, description, and optional JSON schema.
    /// </summary>
    public SkillOutputSchemaConfiguration? Output { get; set; }

    /// <summary>
    /// List of permissions required by this skill (optional).
    /// Examples: FileSystem.Read, FileSystem.Write, Network.Access, MCP.Invoke, UIAutomation.Windows.
    /// </summary>
    public List<string> Permissions { get; set; } = new();

    /// <summary>
    /// Custom skill configuration or metadata (optional).
    /// Can include implementation details, version, source URL, or other skill-specific settings.
    /// </summary>
    public Dictionary<string, string> Config { get; set; } = new();
}

/// <summary>
/// Represents a skill input parameter configuration.
/// </summary>
public class SkillParameterConfiguration
{
    /// <summary>
    /// Parameter name (required).
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Parameter type (required).
    /// e.g., "string", "int", "double", "bool", "object", "array".
    /// </summary>
    public required string Type { get; set; }

    /// <summary>
    /// Whether this parameter is required (optional, defaults to true).
    /// </summary>
    public bool Required { get; set; } = true;

    /// <summary>
    /// Parameter description (optional).
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
/// Represents a skill output schema configuration.
/// </summary>
public class SkillOutputSchemaConfiguration
{
    /// <summary>
    /// Output type description (required).
    /// e.g., "string", "int", "object", "array".
    /// </summary>
    public required string Type { get; set; }

    /// <summary>
    /// Human-readable description of the output (optional).
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Optional JSON schema describing the output structure (optional).
    /// </summary>
    public string? Schema { get; set; }
}

/// <summary>
/// Represents a collection of skill definitions loaded from a configuration file or directory.
/// Supports both single-skill files and multi-skill configuration batches.
/// </summary>
public class SkillConfigurationBatch
{
    /// <summary>
    /// Human-readable name for this batch (optional).
    /// Used for logging and batch identification.
    /// </summary>
    public string? BatchName { get; set; }

    /// <summary>
    /// Description of the skills in this batch (optional).
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// List of skill configurations in this batch.
    /// Can be empty if this is a single-skill file.
    /// </summary>
    public List<SkillConfigurationFile> Skills { get; set; } = new();

    /// <summary>
    /// Metadata about the configuration (optional).
    /// Can include version, author, date created, source URL, etc.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
/// Validation result for skill configuration.
/// </summary>
public class SkillConfigurationValidationResult
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
