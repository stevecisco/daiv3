using Daiv3.Orchestration.Interfaces;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Daiv3.Orchestration;

/// <summary>
/// Registry for managing and executing skills.
/// Supports tracking skill sources (built-in, user-defined, imported).
/// </summary>
public class SkillRegistry : ISkillRegistry
{
    private readonly ILogger<SkillRegistry> _logger;
    private readonly ConcurrentDictionary<string, ISkill> _skills = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, SkillSource> _skillSources = new(StringComparer.OrdinalIgnoreCase);

    public SkillRegistry(ILogger<SkillRegistry> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public void RegisterSkill(ISkill skill)
    {
        RegisterSkill(skill, SkillSource.BuiltIn);
    }

    /// <summary>
    /// Registers a skill with a specified source.
    /// </summary>
    /// <param name="skill">The skill to register.</param>
    /// <param name="source">The skill source (built-in, user-defined, or imported).</param>
    public void RegisterSkill(ISkill skill, SkillSource source)
    {
        ArgumentNullException.ThrowIfNull(skill);
        ArgumentException.ThrowIfNullOrWhiteSpace(skill.Name);

        if (_skills.TryAdd(skill.Name, skill))
        {
            _skillSources[skill.Name] = source;
            _logger.LogInformation(
                "Registered {SourceType} skill '{SkillName}': {Description}",
                source, skill.Name, skill.Description);
        }
        else
        {
            _skillSources[skill.Name] = source;
            _logger.LogWarning(
                "Skill '{SkillName}' already registered, replacing with {SourceType} implementation",
                skill.Name, source);

            _skills[skill.Name] = skill;
        }
    }

    /// <inheritdoc />
    public ISkill? ResolveSkill(string skillName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skillName);

        _skills.TryGetValue(skillName, out var skill);

        if (skill != null)
        {
            _logger.LogDebug("Resolved skill '{SkillName}'", skillName);
        }
        else
        {
            _logger.LogDebug("Skill '{SkillName}' not found", skillName);
        }

        return skill;
    }

    /// <summary>
    /// Gets the source of a registered skill.
    /// </summary>
    /// <param name="skillName">The skill name.</param>
    /// <returns>The skill source, or null if skill is not registered.</returns>
    public SkillSource? GetSkillSource(string skillName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skillName);

        if (_skillSources.TryGetValue(skillName, out var source))
        {
            return source;
        }

        return null;
    }

    /// <inheritdoc />
    public bool UnregisterSkill(string skillName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skillName);

        var removed = _skills.TryRemove(skillName, out _);
        _skillSources.TryRemove(skillName, out _);

        if (removed)
        {
            _logger.LogInformation("Unregistered skill '{SkillName}'", skillName);
        }

        return removed;
    }

    /// <inheritdoc />
    public List<SkillMetadata> ListSkills()
    {
        var metadata = _skills.Values
            .Select(skill => new SkillMetadata
            {
                Name = skill.Name,
                Description = skill.Description,
                Category = skill.Category,
                Source = GetSkillSource(skill.Name) ?? SkillSource.BuiltIn,
                Inputs = new List<ParameterMetadata>(skill.Inputs),
                Outputs = skill.OutputSchema,
                Permissions = new List<string>(skill.Permissions)
            })
            .OrderBy(m => m.Name)
            .ToList();

        _logger.LogInformation("Listed {Count} skill(s)", metadata.Count);
        return metadata;
    }
}
