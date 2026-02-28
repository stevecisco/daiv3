using Daiv3.ModelExecution.Models;

namespace Daiv3.ModelExecution.Interfaces;

/// <summary>
/// Classifies execution requests by task type for model selection and priority assignment.
/// </summary>
/// <remarks>
/// - Uses pattern-based classification in v0.1 (fast, deterministic)
/// - Can be enhanced with ML-based classification in future versions
/// - Classification determines model selection and queue priority
/// </remarks>
public interface ITaskTypeClassifier
{
    /// <summary>
    /// Classifies a request by analyzing its content and context.
    /// </summary>
    /// <param name="request">Execution request to classify</param>
    /// <returns>Classified task type</returns>
    /// <exception cref="ArgumentNullException">If request is null</exception>
    TaskType Classify(ExecutionRequest request);

    /// <summary>
    /// Classifies based on content string alone (convenience method).
    /// </summary>
    /// <param name="content">Request content</param>
    /// <returns>Classified task type</returns>
    /// <exception cref="ArgumentNullException">If content is null</exception>
    TaskType Classify(string content);
}
