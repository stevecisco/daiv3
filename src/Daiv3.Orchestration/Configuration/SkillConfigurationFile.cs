using Daiv3.Orchestration.Interfaces;

namespace Daiv3.Orchestration.Configuration;

/// <summary>
/// Represents a skill definition loaded from a JSON, YAML, or Markdown configuration file.
/// This contract maps directly to the declarative skill configuration format and supports progressive hierarchy composition.
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

    /// <summary>
    /// Optional high-level skill domain (for catalog slicing/search).
    /// </summary>
    public string? Domain { get; set; }

    /// <summary>
    /// Optional language hint for instructions/inputs (for catalog slicing/search).
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// Hierarchy level where this skill definition applies.
    /// Valid values: Global, Project, SubProject, Task.
    /// </summary>
    public string ScopeLevel { get; set; } = "Global";

    /// <summary>
    /// Optional project identifier for project/task-scoped skills.
    /// </summary>
    public string? ProjectId { get; set; }

    /// <summary>
    /// Optional sub-project identifier for sub-project/task-scoped skills.
    /// </summary>
    public string? SubProjectId { get; set; }

    /// <summary>
    /// Optional task identifier for task-scoped skills.
    /// </summary>
    public string? TaskId { get; set; }

    /// <summary>
    /// Optional parent skill name to extend.
    /// </summary>
    public string? ExtendsSkill { get; set; }

    /// <summary>
    /// Override mode for hierarchy composition.
    /// Valid values: Merge (default), Replace.
    /// </summary>
    public string OverrideMode { get; set; } = "Merge";

    /// <summary>
    /// Optional capability declarations for search/indexing and selection.
    /// </summary>
    public List<string> Capabilities { get; set; } = new();

    /// <summary>
    /// Optional restrictions/guardrails declared by the skill.
    /// </summary>
    public List<string> Restrictions { get; set; } = new();

    /// <summary>
    /// Optional keywords/tags for catalog search.
    /// </summary>
    public List<string> Keywords { get; set; } = new();

    /// <summary>
    /// Optional raw instruction body (primarily for markdown-backed skills).
    /// </summary>
    public string? Instructions { get; set; }

    /// <summary>
    /// Physical source path for provenance/debugging.
    /// </summary>
    public string? SourcePath { get; set; }
}

/// <summary>
/// Skill hierarchy levels used for progressive loading.
/// </summary>
public enum SkillHierarchyLevel
{
    Global = 0,
    Project = 1,
    SubProject = 2,
    Task = 3
}

/// <summary>
/// Searchable catalog entry for a loaded skill configuration.
/// </summary>
public sealed class SkillCatalogEntry
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string ScopeLevel { get; init; }
    public string? Domain { get; init; }
    public string? Language { get; init; }
    public string? ProjectId { get; init; }
    public string? SubProjectId { get; init; }
    public string? TaskId { get; init; }
    public string? ExtendsSkill { get; init; }
    public string? SourcePath { get; init; }
    public IReadOnlyList<string> Capabilities { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Restrictions { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Keywords { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Multi-view catalog for loaded skills.
/// </summary>
public sealed class SkillCatalog
{
    public List<SkillCatalogEntry> Entries { get; } = new();

    public IReadOnlyList<SkillCatalogEntry> Search(
        string? query = null,
        string? scopeLevel = null,
        string? domain = null,
        string? language = null,
        string? capability = null)
    {
        IEnumerable<SkillCatalogEntry> matches = Entries;

        if (!string.IsNullOrWhiteSpace(query))
        {
            matches = matches.Where(e =>
                e.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                e.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                e.Keywords.Any(k => k.Contains(query, StringComparison.OrdinalIgnoreCase)));
        }

        if (!string.IsNullOrWhiteSpace(scopeLevel))
        {
            matches = matches.Where(e => string.Equals(e.ScopeLevel, scopeLevel, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(domain))
        {
            matches = matches.Where(e => string.Equals(e.Domain, domain, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(language))
        {
            matches = matches.Where(e => string.Equals(e.Language, language, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(capability))
        {
            matches = matches.Where(e => e.Capabilities.Any(c => c.Contains(capability, StringComparison.OrdinalIgnoreCase)));
        }

        return matches
            .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.ScopeLevel, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
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
