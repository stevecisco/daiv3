namespace Daiv3.ModelExecution;

/// <summary>
/// Maps task types to preferred online provider models.
/// </summary>
/// <remarks>
/// Implements part of MQ-REQ-012: Route online tasks based on model-to-task mappings.
/// </remarks>
public class TaskToModelMapping
{
    /// <summary>
    /// Unique identifier for this mapping configuration.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// List of task types this model is suitable for.
    /// </summary>
    /// <remarks>
    /// When tasks matching these types are routed online, this model will be considered.
    /// Examples: ["Chat", "QuestionAnswer", "Analysis"]
    /// </remarks>
    public List<string> ApplicableTaskTypes { get; set; } = new();

    /// <summary>
    /// Priority for this mapping (higher = preferred).
    /// </summary>
    /// <remarks>
    /// When multiple providers support a task type, the one with highest priority is selected.
    /// Default: 0
    /// </remarks>
    public int Priority { get; set; } = 0;

    /// <summary>
    /// Whether this mapping is currently enabled.
    /// </summary>
    /// <remarks>
    /// Disabled mappings are skipped during provider selection.
    /// Default: true
    /// </remarks>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Cost estimate per 1000 input tokens (for decision-making).
    /// </summary>
    /// <remarks>
    /// Used to help optimize provider selection when budget allows multiple options.
    /// Example: 0.0005 for GPT-3.5, 0.001 for GPT-4
    /// Default: 0
    /// </remarks>
    public decimal CostPer1KInputTokens { get; set; } = 0;

    /// <summary>
    /// Maximum context window size in tokens.
    /// </summary>
    /// <remarks>
    /// Used to prevent routing requests that exceed the model's capacity.
    /// Example: 4096 for smaller models, 128000 for Claude 3.5 Sonnet
    /// Default: 4096
    /// </remarks>
    public int MaxContextWindowTokens { get; set; } = 4096;

    /// <summary>
    /// Custom notes or description of when this mapping should be used.
    /// </summary>
    public string? Notes { get; set; }
}

/// <summary>
/// Provider-to-model mapping configuration for online task routing.
/// </summary>
/// <remarks>
/// Implements MQ-REQ-012: Route online tasks based on model-to-task mappings, budgets, availability.
/// </remarks>
public class TaskToModelMappingConfiguration
{
    /// <summary>
    /// Mapping entries keyed by provider name.
    /// </summary>
    /// <remarks>
    /// Example:
    /// {
    ///   "openai": [
    ///     { "ApplicableTaskTypes": ["Chat", "Analysis"], "Priority": 10 },
    ///     { "ApplicableTaskTypes": ["Code"], "Priority": 5 }
    ///   ],
    ///   "anthropic": [
    ///     { "ApplicableTaskTypes": ["Chat"], "Priority": 5 }
    ///   ]
    /// }
    /// </remarks>
    public Dictionary<string, List<TaskToModelMapping>> ProviderMappings { get; set; } = new();

    /// <summary>
    /// Default provider to use when no task-specific mapping is found.
    /// </summary>
    /// <remarks>
    /// If null/empty, first configured provider is used.
    /// Default: null
    /// </remarks>
    public string? DefaultProviderFallback { get; set; }

    /// <summary>
    /// Whether to prefer cheaper models when multiple options are available.
    /// </summary>
    /// <remarks>
    /// If true, routes to lowest-cost provider that can handle the task type and meets context window req.
    /// If false, uses priority-based selection.
    /// Default: false (use priority)
    /// </remarks>
    public bool PreferLowerCost { get; set; } = false;

    /// <summary>
    /// Whether to allow routing to multiple providers in parallel.
    /// </summary>
    /// <remarks>
    /// If true (default), tasks can be sent to different providers concurrently (MQ-REQ-016).
    /// If false, executes sequentially.
    /// Default: true
    /// </remarks>
    public bool AllowParallelProviderExecution { get; set; } = true;

    /// <summary>
    /// Maximum number of concurrent requests to any single provider.
    /// </summary>
    /// <remarks>
    /// Prevents overwhelming provider rate limits (MQ-REQ-017).
    /// Default: 10
    /// </remarks>
    public int MaxConcurrentRequestsPerProvider { get; set; } = 10;

    /// <summary>
    /// Gets the best provider for a given task type, considering budget budget and availability.
    /// </summary>
    /// <param name="taskType">Task type to find provider for</param>
    /// <param name="estimatedInputTokens">Estimated input tokens (for context checking)</param>
    /// <returns>Provider name, or null if no suitable provider found</returns>
    public string? GetBestProviderForTaskType(string taskType, int estimatedInputTokens = 0)
    {
        var candidates = new List<(string Provider, TaskToModelMapping Mapping)>();

        // Find all enabled mappings that support this task type
        foreach (var (provider, mappings) in ProviderMappings)
        {
            var matchingMappings = mappings
                .Where(m => m.Enabled &&
                           m.ApplicableTaskTypes.Contains(taskType, StringComparer.OrdinalIgnoreCase) &&
                           (estimatedInputTokens == 0 || m.MaxContextWindowTokens >= estimatedInputTokens))
                .ToList();

            foreach (var mapping in matchingMappings)
            {
                candidates.Add((provider, mapping));
            }
        }

        if (candidates.Count == 0)
        {
            return DefaultProviderFallback;
        }

        // Select best candidate
        if (PreferLowerCost)
        {
            return candidates.MinBy(c => c.Mapping.CostPer1KInputTokens).Provider;
        }

        return candidates.MaxBy(c => c.Mapping.Priority).Provider;
    }
}
