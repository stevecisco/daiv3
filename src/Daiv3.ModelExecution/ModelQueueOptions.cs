namespace Daiv3.ModelExecution;

/// <summary>
/// Configuration options for the Model Queue.
/// </summary>
public class ModelQueueOptions
{
    /// <summary>Default model for general tasks.</summary>
    public string DefaultModelId { get; set; } = "phi-3-mini";

    /// <summary>Model for chat interactions.</summary>
    public string ChatModelId { get; set; } = "phi-3-mini";

    /// <summary>Model for code generation/review.</summary>
    public string CodeModelId { get; set; } = "phi-3-mini";

    /// <summary>Model for summarization tasks.</summary>
    public string SummarizeModelId { get; set; } = "phi-3-mini";

    /// <summary>Maximum concurrent online requests (online providers support parallelism).</summary>
    public int MaxConcurrentOnlineRequests { get; set; } = 4;

    /// <summary>Timeout for request execution (seconds).</summary>
    public int RequestTimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Short coalescing window used before dominant P1 model selection when the current model
    /// has no pending matches. Improves deterministic behavior for bursty enqueue patterns.
    /// </summary>
    public int DominantP1SelectionWindowMs { get; set; } = 20;
}
