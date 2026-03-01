namespace Daiv3.Orchestration.Interfaces;

/// <summary>
/// Provides observation and notification of agent execution metrics and lifecycle events.
/// </summary>
public interface IAgentExecutionObserver
{
    /// <summary>
    /// Called when an execution starts.
    /// </summary>
    /// <param name="executionId">The execution identifier.</param>
    /// <param name="agentId">The agent identifier.</param>
    /// <param name="taskGoal">The task goal being executed.</param>
    Task OnExecutionStartedAsync(Guid executionId, Guid agentId, string taskGoal);

    /// <summary>
    /// Called when an iteration begins.
    /// </summary>
    /// <param name="executionId">The execution identifier.</param>
    /// <param name="iterationNumber">The iteration number (1-based).</param>
    Task OnIterationStartedAsync(Guid executionId, int iterationNumber);

    /// <summary>
    /// Called when an iteration completes.
    /// </summary>
    /// <param name="executionId">The execution identifier.</param>
    /// <param name="iterationNumber">The iteration number.</param>
    /// <param name="metrics">The current metrics snapshot.</param>
    Task OnIterationCompletedAsync(Guid executionId, int iterationNumber, AgentExecutionMetricsSnapshot metrics);

    /// <summary>
    /// Called when a step completes.
    /// </summary>
    /// <param name="executionId">The execution identifier.</param>
    /// <param name="stepMetrics">The metrics for this step.</param>
    Task OnStepCompletedAsync(Guid executionId, AgentExecutionMetrics.StepMetrics stepMetrics);

    /// <summary>
    /// Called when execution is paused.
    /// </summary>
    /// <param name="executionId">The execution identifier.</param>
    /// <param name="metrics">The current metrics snapshot.</param>
    Task OnExecutionPausedAsync(Guid executionId, AgentExecutionMetricsSnapshot metrics);

    /// <summary>
    /// Called when execution is resumed.
    /// </summary>
    /// <param name="executionId">The execution identifier.</param>
    /// <param name="metrics">The current metrics snapshot.</param>
    Task OnExecutionResumedAsync(Guid executionId, AgentExecutionMetricsSnapshot metrics);

    /// <summary>
    /// Called when execution is stopped by the user.
    /// </summary>
    /// <param name="executionId">The execution identifier.</param>
    /// <param name="metrics">The current metrics snapshot.</param>
    Task OnExecutionStoppedAsync(Guid executionId, AgentExecutionMetricsSnapshot metrics);

    /// <summary>
    /// Called when a tool invocation starts.
    /// </summary>
    /// <param name="executionId">The execution identifier.</param>
    /// <param name="toolName">The name of the tool being invoked.</param>
    /// <param name="toolBackend">The backend type (e.g., Direct, CLI, MCP, REST).</param>
    Task OnToolInvocationStartedAsync(Guid executionId, string toolName, string toolBackend);

    /// <summary>
    /// Called when a tool invocation completes.
    /// </summary>
    /// <param name="executionId">The execution identifier.</param>
    /// <param name="toolName">The name of the tool.</param>
    /// <param name="toolBackend">The backend type that was used.</param>
    /// <param name="success">Whether the invocation succeeded.</param>
    /// <param name="duration">How long the invocation took.</param>
    /// <param name="errorMessage">Error message if the invocation failed.</param>
    Task OnToolInvocationCompletedAsync(Guid executionId, string toolName, string toolBackend, bool success, TimeSpan duration, string? errorMessage = null);

    /// <summary>
    /// Called when a skill execution starts.
    /// </summary>
    /// <param name="executionId">The execution identifier.</param>
    /// <param name="skillName">The name of the skill being executed.</param>
    Task OnSkillExecutionStartedAsync(Guid executionId, string skillName);

    /// <summary>
    /// Called when a skill execution completes.
    /// </summary>
    /// <param name="executionId">The execution identifier.</param>
    /// <param name="skillName">The name of the skill.</param>
    /// <param name="success">Whether the execution succeeded.</param>
    /// <param name="duration">How long the execution took.</param>
    /// <param name="errorMessage">Error message if the execution failed.</param>
    Task OnSkillExecutionCompletedAsync(Guid executionId, string skillName, bool success, TimeSpan duration, string? errorMessage = null);

    /// <summary>
    /// Called when execution completes (for any reason).
    /// </summary>
    /// <param name="executionId">The execution identifier.</param>
    /// <param name="metrics">The final metrics snapshot.</param>
    /// <param name="terminationReason">Why execution terminated.</param>
    Task OnExecutionCompletedAsync(Guid executionId, AgentExecutionMetricsSnapshot metrics, string terminationReason);

    /// <summary>
    /// Called periodically to provide current metrics snapshot (e.g., for UI updates).
    /// </summary>
    /// <param name="executionId">The execution identifier.</param>
    /// <param name="metrics">The current metrics snapshot.</param>
    Task OnMetricsSnapshotAsync(Guid executionId, AgentExecutionMetricsSnapshot metrics);

    /// <summary>
    /// Called when a performance warning is detected (e.g., slow iteration, high token usage).
    /// </summary>
    /// <param name="executionId">The execution identifier.</param>
    /// <param name="warningType">The type of warning: SlowIteration, HighTokenConsumption, ExcessivePausing, etc.</param>
    /// <param name="message">Human-readable warning message.</param>
    Task OnPerformanceWarningAsync(Guid executionId, string warningType, string message);
}

/// <summary>
/// No-op implementation of IAgentExecutionObserver for cases where observation is not needed.
/// </summary>
public class NoOpAgentExecutionObserver : IAgentExecutionObserver
{
    public Task OnExecutionStartedAsync(Guid executionId, Guid agentId, string taskGoal) => Task.CompletedTask;
    public Task OnIterationStartedAsync(Guid executionId, int iterationNumber) => Task.CompletedTask;
    public Task OnIterationCompletedAsync(Guid executionId, int iterationNumber, AgentExecutionMetricsSnapshot metrics) => Task.CompletedTask;
    public Task OnStepCompletedAsync(Guid executionId, AgentExecutionMetrics.StepMetrics stepMetrics) => Task.CompletedTask;
    public Task OnExecutionPausedAsync(Guid executionId, AgentExecutionMetricsSnapshot metrics) => Task.CompletedTask;
    public Task OnExecutionResumedAsync(Guid executionId, AgentExecutionMetricsSnapshot metrics) => Task.CompletedTask;
    public Task OnExecutionStoppedAsync(Guid executionId, AgentExecutionMetricsSnapshot metrics) => Task.CompletedTask;
    public Task OnToolInvocationStartedAsync(Guid executionId, string toolName, string toolBackend) => Task.CompletedTask;
    public Task OnToolInvocationCompletedAsync(Guid executionId, string toolName, string toolBackend, bool success, TimeSpan duration, string? errorMessage = null) => Task.CompletedTask;
    public Task OnSkillExecutionStartedAsync(Guid executionId, string skillName) => Task.CompletedTask;
    public Task OnSkillExecutionCompletedAsync(Guid executionId, string skillName, bool success, TimeSpan duration, string? errorMessage = null) => Task.CompletedTask;
    public Task OnExecutionCompletedAsync(Guid executionId, AgentExecutionMetricsSnapshot metrics, string terminationReason) => Task.CompletedTask;
    public Task OnMetricsSnapshotAsync(Guid executionId, AgentExecutionMetricsSnapshot metrics) => Task.CompletedTask;
    public Task OnPerformanceWarningAsync(Guid executionId, string warningType, string message) => Task.CompletedTask;
}
