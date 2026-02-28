namespace Daiv3.ModelExecution;

/// <summary>
/// Configuration options for task type classification.
/// </summary>
public class TaskTypeClassifierOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "TaskTypeClassifier";

    /// <summary>
    /// Whether to use explicit task type from request if provided.
    /// Default: true (respects user-provided task type).
    /// </summary>
    public bool UseExplicitTaskType { get; set; } = true;

    /// <summary>
    /// Whether to use case-insensitive pattern matching.
    /// Default: true.
    /// </summary>
    public bool CaseInsensitiveMatching { get; set; } = true;

    /// <summary>
    /// Minimum confidence threshold for classification (0.0 to 1.0).
    /// If confidence is below threshold, returns TaskType.Unknown.
    /// Default: 0.3.
    /// </summary>
    public double MinimumConfidence { get; set; } = 0.3;

    /// <summary>
    /// Custom patterns for task type classification.
    /// Key: TaskType enum name, Value: array of regex patterns or keywords.
    /// </summary>
    public Dictionary<string, string[]> CustomPatterns { get; set; } = new();
}
