namespace Daiv3.Orchestration.Interfaces;

/// <summary>
/// Manages agent lifecycle and execution.
/// </summary>
public interface IAgentManager
{
    /// <summary>
    /// Creates a new agent from a definition.
    /// </summary>
    /// <param name="definition">The agent definition.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created agent.</returns>
    Task<Agent> CreateAgentAsync(AgentDefinition definition, CancellationToken ct = default);

    /// <summary>
    /// Gets or dynamically creates an agent for the specified task type.
    /// </summary>
    /// <param name="taskType">The task type (for example: chat, search, analyze).</param>
    /// <param name="options">Optional creation overrides. When null, configured defaults are used.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An existing or newly created agent mapped to the task type.</returns>
    Task<Agent> GetOrCreateAgentForTaskTypeAsync(
        string taskType,
        DynamicAgentCreationOptions? options = null,
        CancellationToken ct = default);
    
    /// <summary>
    /// Retrieves an agent by ID.
    /// </summary>
    /// <param name="agentId">The agent ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The agent, or null if not found.</returns>
    Task<Agent?> GetAgentAsync(Guid agentId, CancellationToken ct = default);
    
    /// <summary>
    /// Lists all agents, optionally filtered by project.
    /// </summary>
    /// <param name="projectId">Optional project ID filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of agents.</returns>
    Task<List<Agent>> ListAgentsAsync(Guid? projectId = null, CancellationToken ct = default);
    
    /// <summary>
    /// Deletes an agent.
    /// </summary>
    /// <param name="agentId">The agent ID to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DeleteAgentAsync(Guid agentId, CancellationToken ct = default);
    
    /// <summary>
    /// Executes a task using the specified agent with multi-step iteration and termination limits.
    /// </summary>
    /// <param name="request">The agent execution request containing task details and options.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The execution result including all steps, iterations, and final output.</returns>
    Task<AgentExecutionResult> ExecuteTaskAsync(AgentExecutionRequest request, CancellationToken ct = default);
}

/// <summary>
/// Optional overrides for dynamic task-type agent creation.
/// </summary>
public class DynamicAgentCreationOptions
{
    /// <summary>
    /// Optional explicit agent name. If null/empty, a name is generated from task type and options defaults.
    /// </summary>
    public string? AgentName { get; set; }

    /// <summary>
    /// Optional purpose text. If null/empty, purpose is generated from options defaults.
    /// </summary>
    public string? Purpose { get; set; }

    /// <summary>
    /// Optional explicit skills. If null/empty, skills are resolved from configured defaults and task mappings.
    /// </summary>
    public List<string>? EnabledSkills { get; set; }

    /// <summary>
    /// Additional configuration values to merge into the dynamically created agent.
    /// </summary>
    public Dictionary<string, string> Config { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Definition for creating a new agent.
/// </summary>
public class AgentDefinition
{
    /// <summary>
    /// Agent name.
    /// </summary>
    public required string Name { get; set; }
    
    /// <summary>
    /// Agent purpose/description.
    /// </summary>
    public required string Purpose { get; set; }
    
    /// <summary>
    /// List of skill names enabled for this agent.
    /// </summary>
    public List<string> EnabledSkills { get; set; } = new();
    
    /// <summary>
    /// Agent-specific configuration.
    /// </summary>
    public Dictionary<string, string> Config { get; set; } = new();
}

/// <summary>
/// Represents an agent instance.
/// </summary>
public class Agent
{
    /// <summary>
    /// Agent unique identifier.
    /// </summary>
    public Guid Id { get; set; }
    
    /// <summary>
    /// Agent name.
    /// </summary>
    public required string Name { get; set; }
    
    /// <summary>
    /// Agent purpose/description.
    /// </summary>
    public required string Purpose { get; set; }
    
    /// <summary>
    /// List of enabled skill names.
    /// </summary>
    public List<string> EnabledSkills { get; set; } = new();
    
    /// <summary>
    /// When the agent was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }
    
    /// <summary>
    /// Agent-specific configuration.
    /// </summary>
    public Dictionary<string, string> Config { get; set; } = new();
}

/// <summary>
/// Request to execute a task using an agent.
/// </summary>
public class AgentExecutionRequest
{
    /// <summary>
    /// The agent ID to use for execution.
    /// </summary>
    public required Guid AgentId { get; set; }
    
    /// <summary>
    /// The task goal or objective.
    /// </summary>
    public required string TaskGoal { get; set; }
    
    /// <summary>
    /// Optional context or input data for the task.
    /// </summary>
    public Dictionary<string, string> Context { get; set; } = new();
    
    /// <summary>
    /// Optional success criteria to evaluate task completion.
    /// </summary>
    public string? SuccessCriteria { get; set; }
    
    /// <summary>
    /// Execution options. If null, uses defaults from configuration.
    /// </summary>
    public AgentExecutionOptions? Options { get; set; }
}

/// <summary>
/// Options controlling agent execution behavior.
/// </summary>
public class AgentExecutionOptions
{
    /// <summary>
    /// Maximum number of iterations before stopping (default: 10).
    /// </summary>
    public int MaxIterations { get; set; } = 10;
    
    /// <summary>
    /// Maximum execution time in seconds (default: 600 = 10 minutes).
    /// </summary>
    public int TimeoutSeconds { get; set; } = 600;
    
    /// <summary>
    /// Maximum token budget for this execution (default: 10,000).
    /// Prevents runaway costs on local/online model calls.
    /// </summary>
    public int TokenBudget { get; set; } = 10_000;
    
    /// <summary>
    /// Whether to enable self-correction on failures (default: true).
    /// </summary>
    public bool EnableSelfCorrection { get; set; } = true;
}

/// <summary>
/// Result of an agent task execution.
/// </summary>
public class AgentExecutionResult
{
    /// <summary>
    /// Unique identifier for this execution session.
    /// </summary>
    public Guid ExecutionId { get; init; } = Guid.NewGuid();
    
    /// <summary>
    /// The agent ID that executed the task.
    /// </summary>
    public required Guid AgentId { get; set; }
    
    /// <summary>
    /// Whether the task completed successfully.
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// Final output or result of the task.
    /// </summary>
    public string Output { get; set; } = string.Empty;
    
    /// <summary>
    /// Error message if execution failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// Total number of iterations executed.
    /// </summary>
    public int IterationsExecuted { get; set; }
    
    /// <summary>
    /// All execution steps performed during the task.
    /// </summary>
    public List<AgentExecutionStep> Steps { get; set; } = new();
    
    /// <summary>
    /// Total tokens consumed during execution.
    /// </summary>
    public int TokensConsumed { get; set; }
    
    /// <summary>
    /// When execution started.
    /// </summary>
    public DateTimeOffset StartedAt { get; set; }
    
    /// <summary>
    /// When execution completed.
    /// </summary>
    public DateTimeOffset? CompletedAt { get; set; }
    
    /// <summary>
    /// Termination reason (MaxIterations, Success, Timeout, TokenBudgetExceeded, Error, Cancelled).
    /// </summary>
    public string TerminationReason { get; set; } = "Unknown";
}

/// <summary>
/// Represents a single step in multi-step agent execution.
/// </summary>
public class AgentExecutionStep
{
    /// <summary>
    /// Step number (1-based).
    /// </summary>
    public int StepNumber { get; set; }
    
    /// <summary>
    /// Step type (e.g., "Reasoning", "ToolCall", "SkillExecution", "Evaluation").
    /// </summary>
    public required string StepType { get; set; }
    
    /// <summary>
    /// Step description or action taken.
    /// </summary>
    public required string Description { get; set; }
    
    /// <summary>
    /// Step output or result.
    /// </summary>
    public string Output { get; set; } = string.Empty;
    
    /// <summary>
    /// Tokens consumed in this step.
    /// </summary>
    public int TokensConsumed { get; set; }
    
    /// <summary>
    /// When this step started.
    /// </summary>
    public DateTimeOffset StartedAt { get; set; }
    
    /// <summary>
    /// When this step completed.
    /// </summary>
    public DateTimeOffset CompletedAt { get; set; }
    
    /// <summary>
    /// Whether this step succeeded.
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// Error message if step failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}
