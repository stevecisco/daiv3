namespace Daiv3.Orchestration.Interfaces;

/// <summary>
/// Skill source enumeration indicating where a skill originates.
/// </summary>
public enum SkillSource
{
    /// <summary>Built-in skill that comes with the system.</summary>
    BuiltIn,

    /// <summary>User-defined skill created by the user.</summary>
    UserDefined,

    /// <summary>Imported skill from an external source.</summary>
    Imported
}

/// <summary>
/// Skill category enumeration.
/// </summary>
public enum SkillCategory
{
    /// <summary>Category not specified.</summary>
    Unspecified,

    /// <summary>Reasoning, analysis, brainstorming, argument mapping.</summary>
    ReasoningAndAnalysis,

    /// <summary>Code generation, review, debugging, test writing.</summary>
    Code,

    /// <summary>Document generation, format conversion, summarization.</summary>
    Document,

    /// <summary>Data transformation, visualization, statistics.</summary>
    DataAndVisualization,

    /// <summary>Web fetch, crawl, content extraction, research.</summary>
    WebAndResearch,

    /// <summary>Task breakdown, scheduling, dependency analysis.</summary>
    ProjectManagement,

    /// <summary>Email, messaging, meeting summaries, action items.</summary>
    Communication,

    /// <summary>Other skill categories.</summary>
    Other
}

/// <summary>
/// Output schema for a skill.
/// </summary>
public class OutputSchema
{
    /// <summary>
    /// Output type description (e.g., "string", "object", "array").
    /// </summary>
    public required string Type { get; set; }

    /// <summary>
    /// Human-readable description of the output.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Optional JSON schema describing the output structure.
    /// </summary>
    public string? Schema { get; set; }
}

/// <summary>
/// Registry for managing and executing skills.
/// </summary>
public interface ISkillRegistry
{
    /// <summary>
    /// Registers a skill in the registry with default (BuiltIn) source.
    /// </summary>
    /// <param name="skill">The skill to register.</param>
    void RegisterSkill(ISkill skill);

    /// <summary>
    /// Registers a skill in the registry with a specified source.
    /// </summary>
    /// <param name="skill">The skill to register.</param>
    /// <param name="source">The skill source (built-in, user-defined, or imported).</param>
    void RegisterSkill(ISkill skill, SkillSource source);

    /// <summary>
    /// Gets the source of a registered skill.
    /// </summary>
    /// <param name="skillName">The skill name.</param>
    /// <returns>The skill source, or null if skill is not registered.</returns>
    SkillSource? GetSkillSource(string skillName);

    /// <summary>
    /// Resolves a skill by name.
    /// </summary>
    /// <param name="skillName">The skill name.</param>
    /// <returns>The skill, or null if not found.</returns>
    ISkill? ResolveSkill(string skillName);

    /// <summary>
    /// Removes a skill from the registry.
    /// </summary>
    /// <param name="skillName">The skill name.</param>
    /// <returns>True when the skill existed and was removed; otherwise false.</returns>
    bool UnregisterSkill(string skillName);

    /// <summary>
    /// Lists all registered skills.
    /// </summary>
    /// <returns>List of skill metadata.</returns>
    List<SkillMetadata> ListSkills();
}

/// <summary>
/// Represents an executable skill.
/// </summary>
public interface ISkill
{
    /// <summary>
    /// Unique skill name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Human-readable skill description.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Skill category.
    /// </summary>
    SkillCategory Category { get; }

    /// <summary>
    /// List of input parameters this skill accepts.
    /// </summary>
    List<ParameterMetadata> Inputs { get; }

    /// <summary>
    /// Output schema describing what the skill produces.
    /// </summary>
    OutputSchema OutputSchema { get; }

    /// <summary>
    /// List of permissions required by this skill (e.g., "FileSystem.Read", "Network.Access", "MCP.Invoke").
    /// </summary>
    List<string> Permissions { get; }

    /// <summary>
    /// Executes the skill with the provided parameters.
    /// </summary>
    /// <param name="parameters">Skill execution parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Skill execution result.</returns>
    Task<object> ExecuteAsync(Dictionary<string, object> parameters, CancellationToken ct = default);
}

/// <summary>
/// Metadata describing a skill.
/// </summary>
public class SkillMetadata
{
    /// <summary>
    /// Skill name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Skill description.
    /// </summary>
    public required string Description { get; set; }

    /// <summary>
    /// Skill category.
    /// </summary>
    public SkillCategory Category { get; set; }

    /// <summary>
    /// Skill source (Built-in, User-Defined, or Imported).
    /// </summary>
    public SkillSource Source { get; set; } = SkillSource.BuiltIn;

    /// <summary>
    /// List of input parameters this skill accepts.
    /// </summary>
    public List<ParameterMetadata> Inputs { get; set; } = new();

    /// <summary>
    /// Output schema describing what the skill produces.
    /// </summary>
    public required OutputSchema Outputs { get; set; }

    /// <summary>
    /// List of permissions required by this skill.
    /// </summary>
    public List<string> Permissions { get; set; } = new();

    /// <summary>
    /// List of parameters this skill accepts (legacy property, use Inputs instead).
    /// </summary>
    [Obsolete("Use Inputs property instead")]
    public List<ParameterMetadata> Parameters
    {
        get => Inputs;
        set => Inputs = value;
    }
}

/// <summary>
/// Metadata describing a skill parameter.
/// </summary>
public class ParameterMetadata
{
    /// <summary>
    /// Parameter name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Parameter type.
    /// </summary>
    public required string Type { get; set; }

    /// <summary>
    /// Whether the parameter is required.
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// Parameter description.
    /// </summary>
    public string? Description { get; set; }
}
