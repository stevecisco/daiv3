using Daiv3.ModelExecution.Models;

namespace Daiv3.ModelExecution.Interfaces;

/// <summary>
/// Assigns queue priority based on task type and context.
/// </summary>
/// <remarks>
/// Priority assignment considers:
/// - Task type (chat = immediate, batch analysis = background)
/// - User context (user-facing vs background processing)
/// - System state (queue depth, model availability)
/// </remarks>
public interface IPriorityAssigner
{
    /// <summary>
    /// Assigns execution priority based on task type and context.
    /// </summary>
    /// <param name="taskType">Task type</param>
    /// <param name="context">Optional context information</param>
    /// <returns>Assigned execution priority</returns>
    ExecutionPriority AssignPriority(TaskType taskType, PriorityContext? context = null);

    /// <summary>
    /// Gets the default priority for a task type.
    /// </summary>
    /// <param name="taskType">Task type</param>
    /// <returns>Default priority</returns>
    ExecutionPriority GetDefaultPriority(TaskType taskType);
}
