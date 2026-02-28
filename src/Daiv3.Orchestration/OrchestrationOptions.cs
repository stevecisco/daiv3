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
    
    /// <summary>
    /// Default maximum iterations for agent execution.
    /// </summary>
    public int DefaultAgentMaxIterations { get; set; } = 10;
    
    /// <summary>
    /// Default timeout for agent execution in seconds.
    /// </summary>
    public int DefaultAgentTimeoutSeconds { get; set; } = 600;
    
    /// <summary>
    /// Default token budget for agent execution.
    /// </summary>
    public int DefaultAgentTokenBudget { get; set; } = 10_000;
    
    /// <summary>
    /// Whether to enable self-correction by default for agent execution.
    /// </summary>
    public bool DefaultAgentEnableSelfCorrection { get; set; } = true;

    /// <summary>
    /// Whether task-type-driven dynamic agent creation is enabled.
    /// </summary>
    public bool EnableDynamicAgentCreation { get; set; } = true;

    /// <summary>
    /// Prefix used when generating names for dynamic agents.
    /// </summary>
    public string DynamicAgentNamePrefix { get; set; } = "task";

    /// <summary>
    /// Purpose template used when generating dynamic agents.
    /// Supports the placeholder <c>{taskType}</c>.
    /// </summary>
    public string DynamicAgentPurposeTemplate { get; set; } = "Auto-generated agent for task type '{taskType}'.";

    /// <summary>
    /// Default skills assigned to all dynamically created agents.
    /// </summary>
    public List<string> DynamicAgentDefaultSkills { get; set; } = new();

    /// <summary>
    /// Optional task-type specific skill mappings for dynamic agent creation.
    /// The dictionary key is the normalized task type.
    /// </summary>
    public Dictionary<string, List<string>> DynamicAgentSkillsByTaskType { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

