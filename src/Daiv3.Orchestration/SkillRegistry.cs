using Daiv3.Orchestration.Interfaces;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Daiv3.Orchestration;

/// <summary>
/// Registry for managing and executing skills.
/// </summary>
public class SkillRegistry : ISkillRegistry
{
    private readonly ILogger<SkillRegistry> _logger;
    private readonly ConcurrentDictionary<string, ISkill> _skills = new(StringComparer.OrdinalIgnoreCase);

    public SkillRegistry(ILogger<SkillRegistry> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public void RegisterSkill(ISkill skill)
    {
        ArgumentNullException.ThrowIfNull(skill);
        ArgumentException.ThrowIfNullOrWhiteSpace(skill.Name);

        if (_skills.TryAdd(skill.Name, skill))
        {
            _logger.LogInformation(
                "Registered skill '{SkillName}': {Description}",
                skill.Name, skill.Description);
        }
        else
        {
            _logger.LogWarning(
                "Skill '{SkillName}' already registered, replacing with new implementation",
                skill.Name);
            
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

    /// <inheritdoc />
    public List<SkillMetadata> ListSkills()
    {
        var metadata = _skills.Values
            .Select(skill => new SkillMetadata
            {
                Name = skill.Name,
                Description = skill.Description,
                Category = skill.Category,
                Inputs = new List<ParameterMetadata>(),  // TODO: Extract from skill attributes or reflection
                Outputs = skill.OutputSchema,
                Permissions = new List<string>(skill.Permissions)
            })
            .OrderBy(m => m.Name)
            .ToList();

        _logger.LogInformation("Listed {Count} skill(s)", metadata.Count);
        return metadata;
    }
}
