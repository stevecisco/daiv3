namespace Daiv3.Orchestration.Models;

/// <summary>
/// Comprehensive transparency view data aggregating model usage, indexing, queue, and agent activity.
/// Implements ES-REQ-004: The system SHALL expose a transparency view that shows model usage,
/// indexing status, queue state, and agent activity.
/// </summary>
public class TransparencyViewData
{
    /// <summary>
    /// Gets or sets the timestamp when this data was collected.
    /// </summary>
    public DateTimeOffset CollectedAt { get; set; }

    /// <summary>
    /// Gets or sets model usage and execution statistics.
    /// </summary>
    public ModelUsageStatus ModelUsage { get; set; } = new();

    /// <summary>
    /// Gets or sets real-time indexing progress and status.
    /// </summary>
    public IndexingStatusExtended IndexingStatus { get; set; } = new();

    /// <summary>
    /// Gets or sets current queue state and task visibility.
    /// </summary>
    public QueueStateExtended QueueState { get; set; } = new();

    /// <summary>
    /// Gets or sets active agent execution statistics and status.
    /// </summary>
    public AgentActivityExtended AgentActivity { get; set; } = new();

    /// <summary>
    /// Gets or sets any error messages encountered during collection.
    /// Null means no errors.
    /// </summary>
    public string? CollectionError { get; set; }

    /// <summary>
    /// Gets a value indicating whether the data is valid (no critical errors).
    /// </summary>
    public bool IsValid => string.IsNullOrEmpty(CollectionError);
}

/// <summary>
/// Model usage and execution statistics for transparency view.
/// </summary>
public class ModelUsageStatus
{
    /// <summary>
    /// Gets or sets the currently loaded model ID or name.
    /// </summary>
    public string? CurrentModel { get; set; }

    /// <summary>
    /// Gets or sets the total number of model task executions in this session.
    /// </summary>
    public long TotalExecutions { get; set; }

    /// <summary>
    /// Gets or sets the average execution time in milliseconds.
    /// </summary>
    public double AverageExecutionMs { get; set; }

    /// <summary>
    /// Gets or sets the number of model switches during this session.
    /// </summary>
    public int ModelSwitchCount { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the last model switch.
    /// </summary>
    public DateTimeOffset? LastModelSwitchAt { get; set; }

    /// <summary>
    /// Gets or sets the duration the current model has been actively loaded (milliseconds).
    /// </summary>
    public long ActiveModelLoadDurationMs { get; set; }
}

/// <summary>
/// Extended indexing status with real-time progress tracking.
/// </summary>
public class IndexingStatusExtended
{
    /// <summary>
    /// Gets or sets a value indicating whether indexing is currently active.
    /// </summary>
    public bool IsIndexing { get; set; }

    /// <summary>
    /// Gets or sets the number of files queued for processing.
    /// </summary>
    public int FilesQueued { get; set; }

    /// <summary>
    /// Gets or sets the number of successfully indexed files.
    /// </summary>
    public int FilesIndexed { get; set; }

    /// <summary>
    /// Gets or sets the number of files currently being processed.
    /// </summary>
    public int FilesInProgress { get; set; }

    /// <summary>
    /// Gets or sets the number of files with indexing errors.
    /// </summary>
    public int FilesWithErrors { get; set; }

    /// <summary>
    /// Gets or sets the overall progress percentage (0-100).
    /// </summary>
    public double ProgressPercentage { get; set; }

    /// <summary>
    /// Gets or sets human-readable error descriptions.
    /// </summary>
    public string[] ErrorDetailsFormatted { get; set; } = [];

    /// <summary>
    /// Gets or sets the last filesystem scan timestamp.
    /// </summary>
    public DateTimeOffset? LastScanTime { get; set; }

    /// <summary>
    /// Gets or sets the total number of documents stored in knowledge base.
    /// </summary>
    public int TotalDocumentsStored { get; set; }

    /// <summary>
    /// Gets or sets the total number of chunks stored in knowledge base.
    /// </summary>
    public int TotalChunksStored { get; set; }

    /// <summary>
    /// Gets or sets the approximate knowledge base size in bytes.
    /// </summary>
    public long EstimatedStorageBytesUsed { get; set; }
}

/// <summary>
/// Extended queue state with task visibility for transparency view.
/// </summary>
public class QueueStateExtended
{
    /// <summary>
    /// Gets or sets the total number of pending tasks.
    /// </summary>
    public int PendingCount { get; set; }

    /// <summary>
    /// Gets or sets the total number of completed tasks in this session.
    /// </summary>
    public int CompletedCount { get; set; }

    /// <summary>
    /// Gets or sets the number of immediate priority (P0) tasks.
    /// </summary>
    public int ImmediateCount { get; set; }

    /// <summary>
    /// Gets or sets the number of normal priority (P1) tasks.
    /// </summary>
    public int NormalCount { get; set; }

    /// <summary>
    /// Gets or sets the number of background priority (P2) tasks.
    /// </summary>
    public int BackgroundCount { get; set; }

    /// <summary>
    /// Gets or sets the average task execution time in milliseconds.
    /// </summary>
    public double AverageTaskDurationMs { get; set; }

    /// <summary>
    /// Gets or sets the estimated wait time for new tasks in milliseconds.
    /// </summary>
    public double EstimatedWaitMs { get; set; }

    /// <summary>
    /// Gets or sets the model utilization percentage (0-100).
    /// </summary>
    public int ModelUtilizationPercent { get; set; }

    /// <summary>
    /// Gets or sets the top pending tasks by priority and timestamp (up to 5).
    /// </summary>
    public QueuedTaskSummary[] TopPendingTasks { get; set; } = [];
}

/// <summary>
/// Minimal task representation for transparency view.
/// </summary>
public class QueuedTaskSummary
{
    /// <summary>
    /// Gets or sets the unique task identifier.
    /// </summary>
    public string? TaskId { get; set; }

    /// <summary>
    /// Gets or sets the target model name or affinity.
    /// </summary>
    public string? ModelAffinity { get; set; }

    /// <summary>
    /// Gets or sets the priority level (Immediate/Normal/Background).
    /// </summary>
    public string? Priority { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the task was queued.
    /// </summary>
    public DateTimeOffset QueuedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the current task status.
    /// </summary>
    public string? Status { get; set; }
}

/// <summary>
/// Extended agent activity tracking for transparency view.
/// </summary>
public class AgentActivityExtended
{
    /// <summary>
    /// Gets or sets the number of currently active agents.
    /// </summary>
    public int ActiveAgentCount { get; set; }

    /// <summary>
    /// Gets or sets the total number of unique agents executed this session.
    /// </summary>
    public int TotalAgentsExecuted { get; set; }

    /// <summary>
    /// Gets or sets the cumulative iterations across all agents.
    /// </summary>
    public int TotalIterations { get; set; }

    /// <summary>
    /// Gets or sets the cumulative tokens consumed across all agents.
    /// </summary>
    public long TotalTokensUsed { get; set; }

    /// <summary>
    /// Gets or sets the per-agent activity details.
    /// </summary>
    public IndividualAgentActivityExtended[] Activities { get; set; } = [];
}

/// <summary>
/// Enhanced agent activity tracking.
/// </summary>
public class IndividualAgentActivityExtended
{
    /// <summary>
    /// Gets or sets the agent identifier.
    /// </summary>
    public string? AgentId { get; set; }

    /// <summary>
    /// Gets or sets the human-readable agent name.
    /// </summary>
    public string? AgentName { get; set; }

    /// <summary>
    /// Gets or sets the description of the current task.
    /// </summary>
    public string? CurrentTask { get; set; }

    /// <summary>
    /// Gets or sets the agent state (Running/Paused/Stopped/Error).
    /// </summary>
    public string? State { get; set; }

    /// <summary>
    /// Gets or sets the number of iterations this agent has executed.
    /// </summary>
    public int IterationCount { get; set; }

    /// <summary>
    /// Gets or sets the tokens consumed by this agent.
    /// </summary>
    public long TokensUsed { get; set; }

    /// <summary>
    /// Gets or sets when the agent started execution.
    /// </summary>
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>
    /// Gets or sets how long the agent has been executing.
    /// </summary>
    public TimeSpan ElapsedTime { get; set; }

    /// <summary>
    /// Gets or sets the number of errors encountered.
    /// </summary>
    public int ErrorCount { get; set; }

    /// <summary>
    /// Gets or sets the most recent error message, if any.
    /// </summary>
    public string? LastErrorMessage { get; set; }
}
