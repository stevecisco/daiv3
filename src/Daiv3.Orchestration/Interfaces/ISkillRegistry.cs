namespace Daiv3.Orchestration.Interfaces;

/// <summary>
/// Registry for managing and executing skills.
/// </summary>
public interface ISkillRegistry
{
    /// <summary>
    /// Registers a skill in the registry.
    /// </summary>
    /// <param name="skill">The skill to register.</param>
    void RegisterSkill(ISkill skill);
    
    /// <summary>
    /// Resolves a skill by name.
    /// </summary>
    /// <param name="skillName">The skill name.</param>
    /// <returns>The skill, or null if not found.</returns>
    ISkill? ResolveSkill(string skillName);
    
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
    /// List of parameters this skill accepts.
    /// </summary>
    public List<ParameterMetadata> Parameters { get; set; } = new();
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
