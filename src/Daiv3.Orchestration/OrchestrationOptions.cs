namespace Daiv3.Orchestration;

/// <summary>
/// Configuration options for the orchestration layer.
/// </summary>
public class OrchestrationOptions
{
    /// <summary>
    /// Maximum number of tasks that can execute concurrently.
    /// </summary>
    public int MaxConcurrentTasks { get; set; } = 4;
    
    /// <summary>
    /// Task execution timeout in seconds.
    /// </summary>
    public int TaskTimeoutSeconds { get; set; } = 600;
    
    /// <summary>
    /// Default model for intent classification.
    /// </summary>
    public string DefaultIntentModel { get; set; } = "sentence-transformers/all-MiniLM-L6-v2";
    
    /// <summary>
    /// Whether to validate task dependencies before execution.
    /// </summary>
    public bool EnableTaskDependencyValidation { get; set; } = true;
    
    /// <summary>
    /// Minimum confidence threshold for intent classification (0.0 to 1.0).
    /// </summary>
    public decimal MinimumIntentConfidence { get; set; } = 0.5m;
}
