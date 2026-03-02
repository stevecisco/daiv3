using Daiv3.Orchestration.Models;

namespace Daiv3.Orchestration.Interfaces;

/// <summary>
/// Provides promotion-level capabilities for knowledge back-propagation workflows.
/// </summary>
public interface IKnowledgePromotionService
{
    /// <summary>
    /// Gets all supported promotion levels in hierarchy order.
    /// </summary>
    IReadOnlyList<KnowledgePromotionLevel> GetSupportedLevels();

    /// <summary>
    /// Gets all currently enabled promotion levels in hierarchy order.
    /// </summary>
    IReadOnlyList<KnowledgePromotionLevel> GetEnabledLevels();

    /// <summary>
    /// Attempts to parse a user or system-provided promotion level name.
    /// Supports aliases such as "sub-task" and "org".
    /// </summary>
    bool TryParseLevel(string value, out KnowledgePromotionLevel level);

    /// <summary>
    /// Returns true if the level is enabled for promotion actions.
    /// </summary>
    bool IsEnabled(KnowledgePromotionLevel level);
}
