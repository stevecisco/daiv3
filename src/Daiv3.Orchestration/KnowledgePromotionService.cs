using Daiv3.Orchestration.Interfaces;
using Daiv3.Orchestration.Models;

namespace Daiv3.Orchestration;

/// <summary>
/// Provides promotion level hierarchy and parsing support for knowledge back-propagation.
/// Implements KBP-REQ-001 level support.
/// </summary>
public sealed class KnowledgePromotionService : IKnowledgePromotionService
{
    private static readonly IReadOnlyList<KnowledgePromotionLevel> SupportedLevels =
    [
        KnowledgePromotionLevel.Context,
        KnowledgePromotionLevel.SubTask,
        KnowledgePromotionLevel.Task,
        KnowledgePromotionLevel.SubTopic,
        KnowledgePromotionLevel.Topic,
        KnowledgePromotionLevel.Project,
        KnowledgePromotionLevel.Organization,
        KnowledgePromotionLevel.Internet
    ];

    private static readonly HashSet<KnowledgePromotionLevel> DisabledLevels =
    [
        KnowledgePromotionLevel.Organization
    ];

    private static readonly Dictionary<string, KnowledgePromotionLevel> LevelAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["context"] = KnowledgePromotionLevel.Context,
        ["subtask"] = KnowledgePromotionLevel.SubTask,
        ["sub-task"] = KnowledgePromotionLevel.SubTask,
        ["task"] = KnowledgePromotionLevel.Task,
        ["subtopic"] = KnowledgePromotionLevel.SubTopic,
        ["sub-topic"] = KnowledgePromotionLevel.SubTopic,
        ["topic"] = KnowledgePromotionLevel.Topic,
        ["project"] = KnowledgePromotionLevel.Project,
        ["organization"] = KnowledgePromotionLevel.Organization,
        ["org"] = KnowledgePromotionLevel.Organization,
        ["internet"] = KnowledgePromotionLevel.Internet
    };

    public IReadOnlyList<KnowledgePromotionLevel> GetSupportedLevels() => SupportedLevels;

    public IReadOnlyList<KnowledgePromotionLevel> GetEnabledLevels() =>
        SupportedLevels.Where(level => !DisabledLevels.Contains(level)).ToArray();

    public bool TryParseLevel(string value, out KnowledgePromotionLevel level)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            level = default;
            return false;
        }

        var normalized = value.Trim();
        return LevelAliases.TryGetValue(normalized, out level);
    }

    public bool IsEnabled(KnowledgePromotionLevel level) => !DisabledLevels.Contains(level);
}
