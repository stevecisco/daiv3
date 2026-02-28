using Daiv3.ModelExecution.Models;

namespace Daiv3.ModelExecution.Interfaces;

/// <summary>
/// Selects appropriate model for execution based on task type and user preferences.
/// </summary>
/// <remarks>
/// - Maps task types to preferred models (local or online)
/// - Respects user preferences and overrides
/// - Considers model availability and capabilities
/// </remarks>
public interface IModelSelector
{
    /// <summary>
    /// Selects the appropriate model for a task type.
    /// </summary>
    /// <param name="taskType">Task type to select model for</param>
    /// <param name="preferences">Optional user preferences (null uses defaults)</param>
    /// <returns>Model ID to use for execution</returns>
    /// <exception cref="InvalidOperationException">If no suitable model is available</exception>
    string SelectModel(TaskType taskType, ModelSelectionPreferences? preferences = null);

    /// <summary>
    /// Gets the default model for a task type.
    /// </summary>
    /// <param name="taskType">Task type</param>
    /// <returns>Default model ID</returns>
    string GetDefaultModel(TaskType taskType);

    /// <summary>
    /// Checks if a model is available for use.
    /// </summary>
    /// <param name="modelId">Model ID to check</param>
    /// <returns>True if model is available</returns>
    bool IsModelAvailable(string modelId);
}
