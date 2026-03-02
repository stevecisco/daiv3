namespace Daiv3.Orchestration.Models;

/// <summary>
/// Defines the supported knowledge promotion levels for back-propagation workflows.
/// Ordered from most local context to broadest dissemination scope.
/// </summary>
public enum KnowledgePromotionLevel
{
    Context = 0,
    SubTask = 1,
    Task = 2,
    SubTopic = 3,
    Topic = 4,
    Project = 5,
    Organization = 6,
    Internet = 7
}
