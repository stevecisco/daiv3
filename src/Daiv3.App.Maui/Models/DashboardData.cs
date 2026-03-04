namespace Daiv3.App.Maui.Models;

/// <summary>
/// Aggregated dashboard telemetry data.
/// Implements CT-REQ-003: The system SHALL provide a real-time transparency dashboard.
/// Provides a comprehensive snapshot of system state for display in the dashboard UI.
/// </summary>
public class DashboardData
{
    /// <summary>
    /// Gets or sets the timestamp when this data was collected.
    /// </summary>
    public DateTimeOffset CollectedAt { get; set; }

    /// <summary>
    /// Gets or sets hardware status information.
    /// </summary>
    public HardwareStatus Hardware { get; set; } = new();

    /// <summary>
    /// Gets or sets model queue status information.
    /// </summary>
    public QueueStatus Queue { get; set; } = new();

    /// <summary>
    /// Gets or sets indexing progress information.
    /// </summary>
    public IndexingStatus Indexing { get; set; } = new();

    /// <summary>
    /// Gets or sets agent activity information.
    /// </summary>
    public AgentStatus Agent { get; set; } = new();

    /// <summary>
    /// Gets or sets system resource metrics.
    /// </summary>
    public SystemResourceMetrics SystemResources { get; set; } = new();

    /// <summary>
    /// Gets or sets any error messages encountered during data collection.
    /// Null means no errors.
    /// </summary>
    public string? CollectionError { get; set; }

    /// <summary>
    /// Gets a value indicating whether the data is valid (no critical errors).
    /// </summary>
    public bool IsValid => string.IsNullOrEmpty(CollectionError);
}

/// <summary>
/// Hardware status information.
/// </summary>
public class HardwareStatus
{
    /// <summary>
    /// Overall hardware readiness state.
    /// </summary>
    public string OverallStatus { get; set; } = "Initializing...";

    /// <summary>
    /// NPU (Neural Processing Unit) availability status.
    /// </summary>
    public string NpuStatus { get; set; } = "Detecting...";

    /// <summary>
    /// GPU availability status.
    /// </summary>
    public string GpuStatus { get; set; } = "Detecting...";

    /// <summary>
    /// CPU availability status.
    /// </summary>
    public string CpuStatus { get; set; } = "Available";

    /// <summary>
    /// Platform information (e.g., "Windows 11 Copilot+").
    /// </summary>
    public string? PlatformInfo { get; set; }
}

/// <summary>
/// Model queue status information (CT-REQ-004 data).
/// </summary>
public class QueueStatus
{
    /// <summary>
    /// Number of pending tasks in the queue.
    /// </summary>
    public int PendingCount { get; set; }

    /// <summary>
    /// Number of completed tasks in the current session.
    /// </summary>
    public int CompletedCount { get; set; }

    /// <summary>
    /// Name of the currently active/loaded model, if any.
    /// </summary>
    public string? CurrentModel { get; set; }

    /// <summary>
    /// Estimated wait time for the oldest pending task (seconds).
    /// </summary>
    public int? EstimatedWaitSeconds { get; set; }

    /// <summary>
    /// Average task processing time (seconds).
    /// </summary>
    public double? AverageTaskDurationSeconds { get; set; }

    /// <summary>
    /// Top priority items in queue (CT-REQ-004 requirement).
    /// Limited to top 3 items.
    /// </summary>
    public List<QueueItemSummary> TopItems { get; set; } = [];
}

/// <summary>
/// Summary of a queue item for dashboard display.
/// </summary>
public class QueueItemSummary
{
    /// <summary>
    /// Queue item ID.
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Item priority (e.g., "High", "Normal", "Low").
    /// </summary>
    public string? Priority { get; set; }

    /// <summary>
    /// Item status (e.g., "Queued", "Processing", "Failed").
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// Associated project ID if applicable.
    /// </summary>
    public string? ProjectId { get; set; }

    /// <summary>
    /// When the item was added to queue.
    /// </summary>
    public DateTimeOffset? EnqueuedAt { get; set; }
}

/// <summary>
/// Indexing progress information (CT-REQ-005 data).
/// </summary>
public class IndexingStatus
{
    /// <summary>
    /// Whether indexing is currently active.
    /// </summary>
    public bool IsIndexing { get; set; }

    /// <summary>
    /// Number of files indexed in current session.
    /// </summary>
    public int FilesIndexed { get; set; }

    /// <summary>
    /// Number of files currently being processed.
    /// </summary>
    public int FilesInProgress { get; set; }

    /// <summary>
    /// Number of files with errors.
    /// </summary>
    public int FilesWithErrors { get; set; }

    /// <summary>
    /// Percentage complete (0-100).
    /// </summary>
    public double ProgressPercentage { get; set; }

    /// <summary>
    /// Last time the filesystem was scanned.
    /// </summary>
    public DateTimeOffset? LastScanTime { get; set; }

    /// <summary>
    /// Any error messages from indexing.
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// Total documents in index.
    /// </summary>
    public int TotalDocuments { get; set; }
}

/// <summary>
/// Agent activity information (CT-REQ-006 data).
/// </summary>
public class AgentStatus
{
    /// <summary>
    /// Number of active agents currently running.
    /// </summary>
    public int ActiveAgentCount { get; set; }

    /// <summary>
    /// Number of total iterations across all agents.
    /// </summary>
    public int TotalIterations { get; set; }

    /// <summary>
    /// Total tokens used across all agents in current session.
    /// </summary>
    public long TotalTokensUsed { get; set; }

    /// <summary>
    /// Summary of individual agent activities.
    /// </summary>
    public List<IndividualAgentActivity> Activities { get; set; } = [];
}

/// <summary>
/// Activity summary for a single agent.
/// </summary>
public class IndividualAgentActivity
{
    /// <summary>
    /// Agent ID or name.
    /// </summary>
    public string? AgentId { get; set; }

    /// <summary>
    /// Current state of the agent.
    /// </summary>
    public string? State { get; set; }

    /// <summary>
    /// Number of iterations this agent has completed.
    /// </summary>
    public int IterationCount { get; set; }

    /// <summary>
    /// Tokens used by this agent.
    /// </summary>
    public long TokensUsed { get; set; }

    /// <summary>
    /// When the agent last executed.
    /// </summary>
    public DateTimeOffset? LastExecutedAt { get; set; }
}

/// <summary>
/// System resource metrics (CT-REQ-006 data).
/// </summary>
public class SystemResourceMetrics
{
    /// <summary>
    /// CPU utilization percentage (0-100).
    /// </summary>
    public double CpuUtilizationPercent { get; set; }

    /// <summary>
    /// Available memory percentage (0-100, 0 = no memory available).
    /// </summary>
    public double MemoryAvailablePercent { get; set; }

    /// <summary>
    /// GPU utilization percentage if available (0-100, null if no GPU).
    /// </summary>
    public double? GpuUtilizationPercent { get; set; }

    /// <summary>
    /// NPU utilization percentage if available (0-100, null if no NPU).
    /// </summary>
    public double? NpuUtilizationPercent { get; set; }

    /// <summary>
    /// Available disk space in bytes.
    /// </summary>
    public long AvailableDiskBytes { get; set; }

    /// <summary>
    /// Total disk space in bytes.
    /// </summary>
    public long TotalDiskBytes { get; set; }

    /// <summary>
    /// Process-specific memory usage in bytes.
    /// </summary>
    public long ProcessMemoryBytes { get; set; }

    /// <summary>
    /// Gets the disk utilization percentage (0-100).
    /// </summary>
    public double DiskUtilizationPercent =>
        TotalDiskBytes > 0 ? ((TotalDiskBytes - AvailableDiskBytes) * 100.0 / TotalDiskBytes) : 0;
}
