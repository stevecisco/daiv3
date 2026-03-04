using Daiv3.Orchestration.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace Daiv3.Orchestration;

/// <summary>
/// Manages metrics collection and observation for agent executions.
/// Provides centralized access to execution metrics and notifies observers of events.
/// </summary>
public class AgentExecutionMetricsCollector : IAgentExecutionObserver
{
    private readonly ILogger<AgentExecutionMetricsCollector> _logger;
    private readonly IOptions<AgentExecutionObservabilityOptions> _options;
    private readonly ConcurrentDictionary<Guid, AgentExecutionMetrics> _metrics = new();
    private readonly List<IAgentExecutionObserver> _observers = new();
    private readonly object _observerLock = new();

    public AgentExecutionMetricsCollector(
        ILogger<AgentExecutionMetricsCollector> logger,
        IOptions<AgentExecutionObservabilityOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Subscribes an observer to receive execution events.
    /// </summary>
    public void Subscribe(IAgentExecutionObserver observer)
    {
        ArgumentNullException.ThrowIfNull(observer);

        lock (_observerLock)
        {
            if (!_observers.Contains(observer))
            {
                _observers.Add(observer);
                _logger.LogDebug("Subscribed observer {ObserverType}", observer.GetType().Name);
            }
        }
    }

    /// <summary>
    /// Unsubscribes an observer from receiving execution events.
    /// </summary>
    public void Unsubscribe(IAgentExecutionObserver observer)
    {
        lock (_observerLock)
        {
            if (_observers.Remove(observer))
            {
                _logger.LogDebug("Unsubscribed observer {ObserverType}", observer.GetType().Name);
            }
        }
    }

    /// <summary>
    /// Gets the current metrics for an execution.
    /// </summary>
    public AgentExecutionMetrics? GetMetrics(Guid executionId)
    {
        _metrics.TryGetValue(executionId, out var metrics);
        return metrics;
    }

    /// <summary>
    /// Gets a snapshot of the current metrics for an execution.
    /// </summary>
    public AgentExecutionMetricsSnapshot? GetMetricsSnapshot(Guid executionId)
    {
        if (_metrics.TryGetValue(executionId, out var metrics))
        {
            return metrics.CreateSnapshot();
        }

        return null;
    }

    /// <summary>
    /// Removes metrics for a completed execution.
    /// </summary>
    public void ClearMetrics(Guid executionId)
    {
        if (_metrics.TryRemove(executionId, out var removed))
        {
            _logger.LogDebug("Cleared metrics for execution {ExecutionId}", executionId);
        }
    }

    /// <summary>
    /// Creates a new metrics container for an execution.
    /// </summary>
    public AgentExecutionMetrics CreateMetrics(Guid executionId, Guid agentId)
    {
        var metrics = new AgentExecutionMetrics(executionId, agentId);
        _metrics[executionId] = metrics;
        _logger.LogDebug("Created metrics container for execution {ExecutionId} of agent {AgentId}", executionId, agentId);
        return metrics;
    }

    /// <inheritdoc />
    public async Task OnExecutionStartedAsync(Guid executionId, Guid agentId, string taskGoal)
    {
        if (!_options.Value.Enabled)
            return;

        _logger.LogInformation("Agent execution started: ExecutionId={ExecutionId}, AgentId={AgentId}, Goal={Goal}", executionId, agentId, taskGoal);

        await NotifyObserversAsync(observer => observer.OnExecutionStartedAsync(executionId, agentId, taskGoal)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task OnIterationStartedAsync(Guid executionId, int iterationNumber)
    {
        if (!_options.Value.Enabled)
            return;

        _logger.LogDebug("Iteration {IterationNumber} started for execution {ExecutionId}", iterationNumber, executionId);

        await NotifyObserversAsync(observer => observer.OnIterationStartedAsync(executionId, iterationNumber)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task OnIterationCompletedAsync(Guid executionId, int iterationNumber, AgentExecutionMetricsSnapshot metrics)
    {
        if (!_options.Value.Enabled)
            return;

        _logger.LogDebug(
            "Iteration {IterationNumber} completed for execution {ExecutionId}: Duration={Duration}ms, Tokens={Tokens}",
            iterationNumber, executionId, metrics.AverageIterationDuration.TotalMilliseconds, metrics.AverageTokensPerIteration);

        // Check for slow iterations
        if (metrics.AverageIterationDuration.TotalSeconds > _options.Value.SlowIterationThresholdSeconds)
        {
            await OnPerformanceWarningAsync(
                executionId,
                "SlowIteration",
                $"Iteration {iterationNumber} took {metrics.AverageIterationDuration.TotalSeconds:F2} seconds (threshold: {_options.Value.SlowIterationThresholdSeconds}s)")
                .ConfigureAwait(false);
        }

        // Check for high token consumption
        if (metrics.AverageTokensPerIteration > _options.Value.HighTokenConsumptionPerIterationThreshold)
        {
            await OnPerformanceWarningAsync(
                executionId,
                "HighTokenConsumption",
                $"Iteration {iterationNumber} consumed {metrics.AverageTokensPerIteration:F0} tokens (threshold: {_options.Value.HighTokenConsumptionPerIterationThreshold})")
                .ConfigureAwait(false);
        }

        await NotifyObserversAsync(observer => observer.OnIterationCompletedAsync(executionId, iterationNumber, metrics)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task OnStepCompletedAsync(Guid executionId, AgentExecutionMetrics.StepMetrics stepMetrics)
    {
        if (!_options.Value.Enabled || !_options.Value.CollectStepMetrics)
            return;

        _logger.LogDebug(
            "Step {StepNumber} ({StepType}) completed for execution {ExecutionId}: Duration={Duration}ms, Tokens={Tokens}",
            stepMetrics.StepNumber, stepMetrics.StepType, executionId, stepMetrics.Duration.TotalMilliseconds, stepMetrics.TokensConsumed);

        if (_metrics.TryGetValue(executionId, out var metrics))
        {
            // Trim old step metrics if we exceed the retention limit
            if (metrics.StepMetricsCollection.Count >= _options.Value.MaxStepMetricsToRetain)
            {
                var toRemove = metrics.StepMetricsCollection.Count - _options.Value.MaxStepMetricsToRetain + 1;
                metrics.StepMetricsCollection.RemoveRange(0, toRemove);
                _logger.LogDebug(
                    "Trimmed {RemovedCount} old step metrics (retention limit: {Limit})",
                    toRemove, _options.Value.MaxStepMetricsToRetain);
            }

            metrics.StepMetricsCollection.Add(stepMetrics);
            metrics.TotalSteps++;
        }

        await NotifyObserversAsync(observer => observer.OnStepCompletedAsync(executionId, stepMetrics)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task OnExecutionPausedAsync(Guid executionId, AgentExecutionMetricsSnapshot metrics)
    {
        if (!_options.Value.Enabled)
            return;

        _logger.LogInformation("Execution {ExecutionId} paused. Active duration: {Duration}ms", executionId, metrics.ActiveDuration.TotalMilliseconds);

        if (_metrics.TryGetValue(executionId, out var currentMetrics))
        {
            currentMetrics.PauseCount++;
            currentMetrics.ExecutionStatus = "Paused";
        }

        await NotifyObserversAsync(observer => observer.OnExecutionPausedAsync(executionId, metrics)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task OnExecutionResumedAsync(Guid executionId, AgentExecutionMetricsSnapshot metrics)
    {
        if (!_options.Value.Enabled)
            return;

        _logger.LogInformation(
            "Execution {ExecutionId} resumed. Total paused: {PausedDuration}ms ({PausedPercent:F1}%)",
            executionId, metrics.TotalPausedDuration.TotalMilliseconds, metrics.PausedPercentage);

        if (_metrics.TryGetValue(executionId, out var currentMetrics))
        {
            currentMetrics.ResumeCount++;
            currentMetrics.ExecutionStatus = "Running";
        }

        await NotifyObserversAsync(observer => observer.OnExecutionResumedAsync(executionId, metrics)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task OnExecutionStoppedAsync(Guid executionId, AgentExecutionMetricsSnapshot metrics)
    {
        if (!_options.Value.Enabled)
            return;

        _logger.LogInformation(
            "Execution {ExecutionId} stopped. Total duration: {Duration}ms, Iterations: {Iterations}, Tokens: {Tokens}",
            executionId, metrics.TotalDuration.TotalMilliseconds, metrics.TotalIterations, metrics.TotalTokensConsumed);

        if (_metrics.TryGetValue(executionId, out var currentMetrics))
        {
            currentMetrics.StopCount++;
            currentMetrics.ExecutionStatus = "Stopped";
        }

        await NotifyObserversAsync(observer => observer.OnExecutionStoppedAsync(executionId, metrics)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task OnToolInvocationStartedAsync(Guid executionId, string toolName, string toolBackend)
    {
        if (!_options.Value.Enabled || !_options.Value.CollectToolMetrics)
            return;

        _logger.LogDebug("Tool invocation started: ExecutionId={ExecutionId}, Tool={Tool}, Backend={Backend}", executionId, toolName, toolBackend);

        await NotifyObserversAsync(observer => observer.OnToolInvocationStartedAsync(executionId, toolName, toolBackend)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task OnToolInvocationCompletedAsync(Guid executionId, string toolName, string toolBackend, bool success, TimeSpan duration, string? errorMessage = null)
    {
        if (!_options.Value.Enabled || !_options.Value.CollectToolMetrics)
            return;

        _logger.LogDebug(
            "Tool invocation completed: ExecutionId={ExecutionId}, Tool={Tool}, Backend={Backend}, Success={Success}, Duration={Duration}ms",
            executionId, toolName, toolBackend, success, duration.TotalMilliseconds);

        if (_metrics.TryGetValue(executionId, out var metrics))
        {
            metrics.ToolInvocations++;
            metrics.TotalToolDuration += duration;
        }

        await NotifyObserversAsync(observer => observer.OnToolInvocationCompletedAsync(executionId, toolName, toolBackend, success, duration, errorMessage)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task OnSkillExecutionStartedAsync(Guid executionId, string skillName)
    {
        if (!_options.Value.Enabled)
            return;

        _logger.LogDebug("Skill execution started: ExecutionId={ExecutionId}, Skill={Skill}", executionId, skillName);

        await NotifyObserversAsync(observer => observer.OnSkillExecutionStartedAsync(executionId, skillName)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task OnSkillExecutionCompletedAsync(Guid executionId, string skillName, bool success, TimeSpan duration, string? errorMessage = null)
    {
        if (!_options.Value.Enabled)
            return;

        _logger.LogDebug(
            "Skill execution completed: ExecutionId={ExecutionId}, Skill={Skill}, Success={Success}, Duration={Duration}ms",
            executionId, skillName, success, duration.TotalMilliseconds);

        if (_metrics.TryGetValue(executionId, out var metrics))
        {
            metrics.SkillExecutions++;
        }

        await NotifyObserversAsync(observer => observer.OnSkillExecutionCompletedAsync(executionId, skillName, success, duration, errorMessage)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task OnExecutionCompletedAsync(Guid executionId, AgentExecutionMetricsSnapshot metrics, string terminationReason)
    {
        if (!_options.Value.Enabled)
            return;

        _logger.LogInformation(
            "Execution {ExecutionId} completed: Reason={Reason}, Duration={Duration}ms, Iterations={Iterations}, Tokens={Tokens}, Tools={Tools}, Skills={Skills}, Paused={PausedPercent:F1}%",
            executionId, terminationReason, metrics.TotalDuration.TotalMilliseconds, metrics.TotalIterations,
            metrics.TotalTokensConsumed, metrics.ToolInvocations, metrics.SkillExecutions, metrics.PausedPercentage);

        if (_metrics.TryGetValue(executionId, out var currentMetrics))
        {
            currentMetrics.ExecutionStatus = "Completed";
            currentMetrics.TerminationReason = terminationReason;
            currentMetrics.CompletedAt = DateTimeOffset.UtcNow;
        }

        // Check for excessive pausing
        if (metrics.PausedPercentage > _options.Value.PausedPercentageWarningThreshold)
        {
            await OnPerformanceWarningAsync(
                executionId,
                "ExcessivePausing",
                $"Execution was paused {metrics.PausedPercentage:F1}% of the time (threshold: {_options.Value.PausedPercentageWarningThreshold}%)")
                .ConfigureAwait(false);
        }

        await NotifyObserversAsync(observer => observer.OnExecutionCompletedAsync(executionId, metrics, terminationReason)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task OnMetricsSnapshotAsync(Guid executionId, AgentExecutionMetricsSnapshot metrics)
    {
        if (!_options.Value.Enabled)
            return;

        await NotifyObserversAsync(observer => observer.OnMetricsSnapshotAsync(executionId, metrics)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task OnPerformanceWarningAsync(Guid executionId, string warningType, string message)
    {
        if (!_options.Value.Enabled)
            return;

        _logger.LogWarning("Performance warning for execution {ExecutionId}: {Type} - {Message}", executionId, warningType, message);

        await NotifyObserversAsync(observer => observer.OnPerformanceWarningAsync(executionId, warningType, message)).ConfigureAwait(false);
    }

    /// <summary>
    /// Notifies all registered observers about an event.
    /// </summary>
    private async Task NotifyObserversAsync(Func<IAgentExecutionObserver, Task> notificationFunc)
    {
        List<IAgentExecutionObserver> observers;
        lock (_observerLock)
        {
            observers = new List<IAgentExecutionObserver>(_observers);
        }

        foreach (var observer in observers)
        {
            try
            {
                await notificationFunc(observer).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error notifying observer {ObserverType}", observer.GetType().Name);
            }
        }
    }
}
