using System.Diagnostics;

namespace Daiv3.Orchestration;

/// <summary>
/// Provides comprehensive metrics and observability for agent execution.
/// </summary>
public class AgentExecutionMetrics
{
    /// <summary>
    /// Gets the unique execution identifier.
    /// </summary>
    public Guid ExecutionId { get; }

    /// <summary>
    /// Gets the agent identifier.
    /// </summary>
    public Guid AgentId { get; }

    /// <summary>
    /// Gets or sets the total number of iterations executed.
    /// </summary>
    public int TotalIterations { get; set; }

    /// <summary>
    /// Gets or sets the total number of steps executed.
    /// </summary>
    public int TotalSteps { get; set; }

    /// <summary>
    /// Gets or sets the total tokens consumed during execution.
    /// </summary>
    public long TotalTokensConsumed { get; set; }

    /// <summary>
    /// Gets or sets the total duration of execution (wall-clock time).
    /// </summary>
    public TimeSpan TotalDuration { get; set; }

    /// <summary>
    /// Gets or sets the total time spent paused by the user.
    /// </summary>
    public TimeSpan TotalPausedDuration { get; set; }

    /// <summary>
    /// Gets or sets the computed active duration (TotalDuration - TotalPausedDuration).
    /// </summary>
    public TimeSpan ActiveDuration => TotalDuration - TotalPausedDuration;

    /// <summary>
    /// Gets or sets the number of times execution was paused.
    /// </summary>
    public int PauseCount { get; set; }

    /// <summary>
    /// Gets or sets the number of times execution was resumed.
    /// </summary>
    public int ResumeCount { get; set; }

    /// <summary>
    /// Gets or sets the number of times execution was stopped.
    /// </summary>
    public int StopCount { get; set; }

    /// <summary>
    /// Gets or sets the execution status: Running, Paused, Stopped, Completed, Failed.
    /// </summary>
    public string ExecutionStatus { get; set; } = "Running";

    /// <summary>
    /// Gets or sets the human-readable response of why execution terminated.
    /// Examples: Success, MaxIterations, Timeout, TokenBudgetExceeded, Error, Cancelled, UserStopped.
    /// </summary>
    public string? TerminationReason { get; set; }

    /// <summary>
    /// Gets or sets the number of tool invocations performed.
    /// </summary>
    public int ToolInvocations { get; set; }

    /// <summary>
    /// Gets or sets the number of skill executions.
    /// </summary>
    public int SkillExecutions { get; set; }

    /// <summary>
    /// Gets or sets the total duration of all tool invocations.
    /// </summary>
    public TimeSpan TotalToolDuration { get; set; }

    /// <summary>
    /// Gets or sets the average duration per iteration (average across all iterations).
    /// </summary>
    public TimeSpan AverageIterationDuration => TotalIterations > 0 ? ActiveDuration / TotalIterations : TimeSpan.Zero;

    /// <summary>
    /// Gets or sets the average tokens consumed per iteration.
    /// </summary>
    public double AverageTokensPerIteration => TotalIterations > 0 ? (double)TotalTokensConsumed / TotalIterations : 0;

    /// <summary>
    /// Gets or sets the average tokens consumed per second of active execution.
    /// </summary>
    public double TokensPerSecond => ActiveDuration.TotalSeconds > 0 ? TotalTokensConsumed / ActiveDuration.TotalSeconds : 0;

    /// <summary>
    /// Gets or sets the number of steps per iteration (average).
    /// </summary>
    public double AverageStepsPerIteration => TotalIterations > 0 ? (double)TotalSteps / TotalIterations : 0;

    /// <summary>
    /// Gets or sets when the execution started.
    /// </summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>
    /// Gets or sets when the execution completed.
    /// </summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>
    /// Gets or sets an error message if execution failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets the percentage of execution time spent paused.
    /// </summary>
    public double PausedPercentage => TotalDuration.TotalSeconds > 0 ? (TotalPausedDuration.TotalSeconds / TotalDuration.TotalSeconds) * 100 : 0;

    /// <summary>
    /// Step-level metrics captured during execution.
    /// </summary>
    public List<StepMetrics> StepMetricsCollection { get; } = new();

    public AgentExecutionMetrics(Guid executionId, Guid agentId)
    {
        ExecutionId = executionId;
        AgentId = agentId;
        StartedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Records metrics for a single execution step.
    /// </summary>
    public class StepMetrics
    {
        /// <summary>
        /// Gets or sets the step number (1-based).
        /// </summary>
        public int StepNumber { get; set; }

        /// <summary>
        /// Gets or sets the step type: Planning, ToolExecution, Evaluation, Completion.
        /// </summary>
        public string StepType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the duration of this step.
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Gets or sets the tokens consumed by this step.
        /// </summary>
        public long TokensConsumed { get; set; }

        /// <summary>
        /// Gets or sets whether this step succeeded.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the step started.
        /// </summary>
        public DateTimeOffset StartedAt { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the step ended.
        /// </summary>
        public DateTimeOffset? EndedAt { get; set; }

        /// <summary>
        /// Gets or sets an error message if the step failed.
        /// </summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Creates a snapshot of the current metrics.
    /// </summary>
    public AgentExecutionMetricsSnapshot CreateSnapshot()
    {
        return new AgentExecutionMetricsSnapshot
        {
            ExecutionId = ExecutionId,
            AgentId = AgentId,
            TotalIterations = TotalIterations,
            TotalSteps = TotalSteps,
            TotalTokensConsumed = TotalTokensConsumed,
            TotalDuration = TotalDuration,
            TotalPausedDuration = TotalPausedDuration,
            ActiveDuration = ActiveDuration,
            PauseCount = PauseCount,
            ResumeCount = ResumeCount,
            StopCount = StopCount,
            ExecutionStatus = ExecutionStatus,
            TerminationReason = TerminationReason,
            ToolInvocations = ToolInvocations,
            SkillExecutions = SkillExecutions,
            TotalToolDuration = TotalToolDuration,
            AverageIterationDuration = AverageIterationDuration,
            AverageTokensPerIteration = AverageTokensPerIteration,
            TokensPerSecond = TokensPerSecond,
            AverageStepsPerIteration = AverageStepsPerIteration,
            StartedAt = StartedAt,
            CompletedAt = CompletedAt,
            ErrorMessage = ErrorMessage,
            PausedPercentage = PausedPercentage,
            StepMetricsCount = StepMetricsCollection.Count
        };
    }
}

/// <summary>
/// Represents a snapshot of agent execution metrics at a point in time.
/// </summary>
public class AgentExecutionMetricsSnapshot
{
    /// <summary>
    /// Gets the unique execution identifier.
    /// </summary>
    public Guid ExecutionId { get; set; }

    /// <summary>
    /// Gets the agent identifier.
    /// </summary>
    public Guid AgentId { get; set; }

    /// <summary>
    /// Gets the total number of iterations executed.
    /// </summary>
    public int TotalIterations { get; set; }

    /// <summary>
    /// Gets the total number of steps executed.
    /// </summary>
    public int TotalSteps { get; set; }

    /// <summary>
    /// Gets the total tokens consumed during execution.
    /// </summary>
    public long TotalTokensConsumed { get; set; }

    /// <summary>
    /// Gets the total duration of execution (wall-clock time).
    /// </summary>
    public TimeSpan TotalDuration { get; set; }

    /// <summary>
    /// Gets the total time spent paused by the user.
    /// </summary>
    public TimeSpan TotalPausedDuration { get; set; }

    /// <summary>
    /// Gets the computed active duration (TotalDuration - TotalPausedDuration).
    /// </summary>
    public TimeSpan ActiveDuration { get; set; }

    /// <summary>
    /// Gets the number of times execution was paused.
    /// </summary>
    public int PauseCount { get; set; }

    /// <summary>
    /// Gets the number of times execution was resumed.
    /// </summary>
    public int ResumeCount { get; set; }

    /// <summary>
    /// Gets the number of times execution was stopped.
    /// </summary>
    public int StopCount { get; set; }

    /// <summary>
    /// Gets the execution status: Running, Paused, Stopped, Completed, Failed.
    /// </summary>
    public string ExecutionStatus { get; set; } = "Running";

    /// <summary>
    /// Gets the reason why execution terminated.
    /// </summary>
    public string? TerminationReason { get; set; }

    /// <summary>
    /// Gets the number of tool invocations performed.
    /// </summary>
    public int ToolInvocations { get; set; }

    /// <summary>
    /// Gets the number of skill executions.
    /// </summary>
    public int SkillExecutions { get; set; }

    /// <summary>
    /// Gets the total duration of all tool invocations.
    /// </summary>
    public TimeSpan TotalToolDuration { get; set; }

    /// <summary>
    /// Gets the average duration per iteration.
    /// </summary>
    public TimeSpan AverageIterationDuration { get; set; }

    /// <summary>
    /// Gets the average tokens consumed per iteration.
    /// </summary>
    public double AverageTokensPerIteration { get; set; }

    /// <summary>
    /// Gets the tokens consumed per second of active execution.
    /// </summary>
    public double TokensPerSecond { get; set; }

    /// <summary>
    /// Gets the average number of steps per iteration.
    /// </summary>
    public double AverageStepsPerIteration { get; set; }

    /// <summary>
    /// Gets when the execution started.
    /// </summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>
    /// Gets when the execution completed.
    /// </summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>
    /// Gets an error message if execution failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets the percentage of execution time spent paused.
    /// </summary>
    public double PausedPercentage { get; set; }

    /// <summary>
    /// Gets the number of step metrics recorded.
    /// </summary>
    public int StepMetricsCount { get; set; }
}

/// <summary>
/// Configuration options for agent execution observability.
/// </summary>
public class AgentExecutionObservabilityOptions
{
    /// <summary>
    /// Gets or sets whether metrics collection is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets whether detailed step metrics should be collected.
    /// </summary>
    public bool CollectStepMetrics { get; set; } = true;

    /// <summary>
    /// Gets or sets whether tool invocation metrics should be collected.
    /// </summary>
    public bool CollectToolMetrics { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of step metrics to retain (to avoid memory bloat).
    /// Oldest steps are discarded when this limit is exceeded.
    /// </summary>
    public int MaxStepMetricsToRetain { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the warning threshold for iteration duration (in seconds).
    /// If an iteration takes longer than this, it's logged as a warning.
    /// </summary>
    public double SlowIterationThresholdSeconds { get; set; } = 30.0;

    /// <summary>
    /// Gets or sets the warning threshold for tokens consumed per iteration.
    /// If an iteration consumes more tokens than this, it's logged as a warning.
    /// </summary>
    public long HighTokenConsumptionPerIterationThreshold { get; set; } = 2000;

    /// <summary>
    /// Gets or sets the warning threshold for paused percentage.
    /// If execution is paused more than this percentage of the time, it's logged as a warning.
    /// </summary>
    public double PausedPercentageWarningThreshold { get; set; } = 50.0;
}
