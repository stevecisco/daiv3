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
    /// Gets or sets resource alert state (CT-REQ-006: Resource Alerts).
    /// </summary>
    public ResourceAlerts Alerts { get; set; } = new();

    /// <summary>
    /// Gets or sets online provider token usage and budget status (CT-REQ-007).
    /// </summary>
    public OnlineProviderUsage OnlineUsage { get; set; } = new();

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
/// Displays model queue status with top 3 items and per-project filtering.
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
    /// Time when the current model was last switched (UTC).
    /// </summary>
    public DateTimeOffset? LastModelSwitchTime { get; set; }

    /// <summary>
    /// Estimated wait time for the oldest pending task (seconds).
    /// </summary>
    public double? EstimatedWaitSeconds { get; set; }

    /// <summary>
    /// Average task processing time (seconds).
    /// </summary>
    public double? AverageTaskDurationSeconds { get; set; }

    /// <summary>
    /// Queue throughput: tasks processed per minute.
    /// </summary>
    public double? ThroughputPerMinute { get; set; }

    /// <summary>
    /// Model utilization percentage (0-100).
    /// </summary>
    public int? ModelUtilizationPercent { get; set; }

    /// <summary>
    /// Count of immediate priority (P0) items.
    /// </summary>
    public int ImmediateCount { get; set; }

    /// <summary>
    /// Count of normal priority (P1) items.
    /// </summary>
    public int NormalCount { get; set; }

    /// <summary>
    /// Count of background priority (P2) items.
    /// </summary>
    public int BackgroundCount { get; set; }

    /// <summary>
    /// Top priority items in queue (CT-REQ-004 requirement).
    /// Limited to top 3 items.
    /// </summary>
    public List<QueueItemSummary> TopItems { get; set; } = [];

    /// <summary>
    /// Optional: All pending items for detailed views (not always populated).
    /// </summary>
    public List<QueueItemSummary> AllPendingItems { get; set; } = [];

    /// <summary>
    /// Get items filtered by project ID.
    /// </summary>
    /// <param name="projectId">Project ID filter (null for all projects).</param>
    /// <returns>Filtered queue items.</returns>
    public List<QueueItemSummary> GetItemsByProject(string? projectId = null)
    {
        if (string.IsNullOrEmpty(projectId))
            return [..AllPendingItems];
        
        return AllPendingItems.Where(item => item.ProjectId == projectId).ToList();
    }

    /// <summary>
    /// Get count of pending items for a specific project.
    /// </summary>
    public int GetPendingCountByProject(string? projectId) =>
        string.IsNullOrEmpty(projectId) ? PendingCount : GetItemsByProject(projectId).Count;
}

/// <summary>
/// Summary of a queue item for dashboard display.
/// CT-REQ-004: Displays queue items with priority, estimated times, and project info.
/// </summary>
public class QueueItemSummary
{
    /// <summary>
    /// Queue item ID (Request ID).
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Item priority (e.g., "Immediate", "Normal", "Background").
    /// </summary>
    public string? Priority { get; set; }

    /// <summary>
    /// Item status (e.g., "Queued", "Processing", "Failed").
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// Task type (e.g., "chat", "code", "summarize").
    /// </summary>
    public string? TaskType { get; set; }

    /// <summary>
    /// Brief description of the request (first 100 chars of content).
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Associated project ID if applicable.
    /// </summary>
    public string? ProjectId { get; set; }

    /// <summary>
    /// When the item was added to queue.
    /// </summary>
    public DateTimeOffset? EnqueuedAt { get; set; }

    /// <summary>
    /// Estimated start time for this item (based on queue position and processing time).
    /// </summary>
    public DateTimeOffset? EstimatedStartTime { get; set; }

    /// <summary>
    /// Model affinity/preference for this request.
    /// </summary>
    public string? PreferredModel { get; set; }

    /// <summary>
    /// Queue position (0 = first in queue, -1 = processing/unknown).
    /// </summary>
    public int QueuePosition { get; set; } = -1;
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
    /// Agent display name.
    /// </summary>
    public string? AgentName { get; set; }

    /// <summary>
    /// Current task goal or description.
    /// </summary>
    public string? CurrentTask { get; set; }

    /// <summary>
    /// Current state of the agent (Running, Paused, Stopped, Idle, Error).
    /// </summary>
    public string? State { get; set; }

    /// <summary>
    /// More detailed status description.
    /// </summary>
    public string? StatusDetail { get; set; }

    /// <summary>
    /// Number of iterations this agent has completed.
    /// </summary>
    public int IterationCount { get; set; }

    /// <summary>
    /// Tokens used by this agent in the current task.
    /// </summary>
    public long TokensUsed { get; set; }

    /// <summary>
    /// When agent execution started.
    /// </summary>
    public DateTimeOffset? StartTime { get; set; }

    /// <summary>
    /// Total elapsed time of execution (wall-clock).
    /// </summary>
    public TimeSpan ElapsedTime { get; set; }

    /// <summary>
    /// When the agent last executed.
    /// </summary>
    public DateTimeOffset? LastExecutedAt { get; set; }

    /// <summary>
    /// Gets a formatted elapsed time string (e.g. "2m 30s").
    /// </summary>
    public string ElapsedTimeText
    {
        get
        {
            var t = ElapsedTime;
            if (t.TotalHours >= 1)
                return $"{(int)t.TotalHours}h {t.Minutes}m";
            if (t.TotalMinutes >= 1)
                return $"{(int)t.TotalMinutes}m {t.Seconds}s";
            return $"{t.Seconds}s";
        }
    }
}

/// <summary>
/// System resource metrics (CT-REQ-006 data).
/// </summary>
public class SystemResourceMetrics
{
    /// <summary>
    /// CPU utilization percentage (0-100). Process-level measurement.
    /// </summary>
    public double CpuUtilizationPercent { get; set; }

    /// <summary>
    /// Available memory percentage (0-100, 0 = no memory available).
    /// </summary>
    public double MemoryAvailablePercent { get; set; }

    /// <summary>
    /// Physical RAM currently in use (bytes).
    /// </summary>
    public long MemoryUsedBytes { get; set; }

    /// <summary>
    /// Total physical RAM installed (bytes).
    /// </summary>
    public long MemoryTotalBytes { get; set; }

    /// <summary>
    /// GPU utilization percentage if available (0-100, null if no GPU or not yet integrated).
    /// Pending HW-NFR-002 hardware integration.
    /// </summary>
    public double? GpuUtilizationPercent { get; set; }

    /// <summary>
    /// NPU utilization percentage if available (0-100, null if no NPU or not yet integrated).
    /// Pending HW-NFR-002 hardware integration.
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
    /// Process-specific memory usage in bytes (working set).
    /// </summary>
    public long ProcessMemoryBytes { get; set; }

    /// <summary>
    /// Active execution provider: "CPU", "GPU", or "NPU".
    /// Pending HW-NFR-002 hardware integration; defaults to "CPU".
    /// </summary>
    public string ActiveExecutionProvider { get; set; } = "CPU";

    /// <summary>
    /// Number of logical CPU cores.
    /// </summary>
    public int CpuCoreCount { get; set; }

    /// <summary>
    /// Knowledge base storage size (embeddings + documents) in bytes.
    /// </summary>
    public long KnowledgeBaseSizeBytes { get; set; }

    /// <summary>
    /// Model cache storage size in bytes.
    /// </summary>
    public long ModelCacheSizeBytes { get; set; }

    /// <summary>
    /// Gets the disk utilization percentage (0-100).
    /// </summary>
    public double DiskUtilizationPercent =>
        TotalDiskBytes > 0 ? ((TotalDiskBytes - AvailableDiskBytes) * 100.0 / TotalDiskBytes) : 0;

    /// <summary>
    /// Gets the memory utilization percentage (0-100).
    /// </summary>
    public double MemoryUtilizationPercent =>
        MemoryTotalBytes > 0 ? ((double)MemoryUsedBytes / MemoryTotalBytes * 100.0) : 0;
}

/// <summary>
/// Resource alert state for the dashboard (CT-REQ-006: Resource Alerts).
/// </summary>
public class ResourceAlerts
{
    /// <summary>
    /// High CPU alert (triggered at >85% sustained utilization).
    /// </summary>
    public bool HasHighCpuAlert { get; set; }

    /// <summary>
    /// High memory alert (triggered at >80% memory usage).
    /// </summary>
    public bool HasHighMemoryAlert { get; set; }

    /// <summary>
    /// Low disk alert (triggered at &lt;1 GB free space).
    /// </summary>
    public bool HasLowDiskAlert { get; set; }

    /// <summary>
    /// Thermal alert (CPU/GPU throttling detected). Pending HW-NFR-002.
    /// </summary>
    public bool HasThermalAlert { get; set; }

    /// <summary>
    /// Gets whether any alert is active.
    /// </summary>
    public bool HasAnyAlert =>
        HasHighCpuAlert || HasHighMemoryAlert || HasLowDiskAlert || HasThermalAlert;

    /// <summary>
    /// Human-readable alert messages for display.
    /// </summary>
    public List<string> AlertMessages { get; set; } = [];
}

/// <summary>
/// Online provider token usage and budget status (CT-REQ-007 data).
/// Displays token usage versus budget per provider with daily/monthly tracking.
/// </summary>
public class OnlineProviderUsage
{
    /// <summary>
    /// Whether any online providers are configured.
    /// </summary>
    public bool HasOnlineProviders { get; set; }

    /// <summary>
    /// Whether any online providers have active usage.
    /// </summary>
    public bool HasActiveUsage { get; set; }

    /// <summary>
    /// Per-provider token usage information.
    /// </summary>
    public List<ProviderUsageSummary> Providers { get; set; } = [];

    /// <summary>
    /// Total tokens consumed across all providers today.
    /// </summary>
    public long TotalDailyTokens => Providers.Sum(p => p.DailyInputTokens + p.DailyOutputTokens);

    /// <summary>
    /// Total tokens consumed across all providers this month.
    /// </summary>
    public long TotalMonthlyTokens => Providers.Sum(p => p.MonthlyInputTokens + p.MonthlyOutputTokens);

    /// <summary>
    /// Total budget remaining across all providers today.
    /// </summary>
    public long TotalDailyBudgetRemaining => Providers.Sum(p => p.DailyBudgetRemaining);

    /// <summary>
    /// Total budget remaining across all providers this month.
    /// </summary>
    public long TotalMonthlyBudgetRemaining => Providers.Sum(p => p.MonthlyBudgetRemaining);

    /// <summary>
    /// Gets the provider with highest usage percentage.
    /// </summary>
    public ProviderUsageSummary? HighestUsageProvider =>
        Providers.OrderByDescending(p => p.HighestUsagePercent).FirstOrDefault();

    /// <summary>
    /// Gets whether any provider is near or over budget (>80% utilization).
    /// </summary>
    public bool HasBudgetAlert => Providers.Any(p => p.HighestUsagePercent >= 80);
}

/// <summary>
/// Token usage summary for a single online provider.
/// </summary>
public class ProviderUsageSummary
{
    /// <summary>
    /// Provider name (e.g., "openai", "azure-openai", "anthropic").
    /// </summary>
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>
    /// Display name for UI (e.g., "OpenAI", "Azure OpenAI", "Claude").
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Input tokens used today.
    /// </summary>
    public int DailyInputTokens { get; set; }

    /// <summary>
    /// Output tokens used today.
    /// </summary>
    public int DailyOutputTokens { get; set; }

    /// <summary>
    /// Input tokens used this month.
    /// </summary>
    public int MonthlyInputTokens { get; set; }

    /// <summary>
    /// Output tokens used this month.
    /// </summary>
    public int MonthlyOutputTokens { get; set; }

    /// <summary>
    /// Daily input token limit (budget).
    /// </summary>
    public int DailyInputLimit { get; set; }

    /// <summary>
    /// Daily output token limit (budget).
    /// </summary>
    public int DailyOutputLimit { get; set; }

    /// <summary>
    /// Monthly input token limit (budget).
    /// </summary>
    public int MonthlyInputLimit { get; set; }

    /// <summary>
    /// Monthly output token limit (budget).
    /// </summary>
    public int MonthlyOutputLimit { get; set; }

    /// <summary>
    /// When the daily counters will reset (UTC).
    /// </summary>
    public DateTimeOffset ResetDate { get; set; }

    /// <summary>
    /// Gets total daily tokens (input + output).
    /// </summary>
    public int DailyTotalTokens => DailyInputTokens + DailyOutputTokens;

    /// <summary>
    /// Gets total monthly tokens (input + output).
    /// </summary>
    public int MonthlyTotalTokens => MonthlyInputTokens + MonthlyOutputTokens;

    /// <summary>
    /// Gets total daily limit (input + output).
    /// </summary>
    public int DailyTotalLimit => DailyInputLimit + DailyOutputLimit;

    /// <summary>
    /// Gets total monthly limit (input + output).
    /// </summary>
    public int MonthlyTotalLimit => MonthlyInputLimit + MonthlyOutputLimit;

    /// <summary>
    /// Gets daily budget remaining (input + output combined).
    /// </summary>
    public int DailyBudgetRemaining => Math.Max(0, DailyTotalLimit - DailyTotalTokens);

    /// <summary>
    /// Gets monthly budget remaining (input + output combined).
    /// </summary>
    public int MonthlyBudgetRemaining => Math.Max(0, MonthlyTotalLimit - MonthlyTotalTokens);

    /// <summary>
    /// Gets daily usage percentage (0-100).
    /// </summary>
    public double DailyUsagePercent =>
        DailyTotalLimit > 0 ? (DailyTotalTokens * 100.0 / DailyTotalLimit) : 0;

    /// <summary>
    /// Gets monthly usage percentage (0-100).
    /// </summary>
    public double MonthlyUsagePercent =>
        MonthlyTotalLimit > 0 ? (MonthlyTotalTokens * 100.0 / MonthlyTotalLimit) : 0;

    /// <summary>
    /// Gets the highest usage percentage between daily and monthly.
    /// </summary>
    public double HighestUsagePercent => Math.Max(DailyUsagePercent, MonthlyUsagePercent);

    /// <summary>
    /// Gets whether this provider is over budget (any limit exceeded).
    /// </summary>
    public bool IsOverBudget =>
        DailyInputTokens >= DailyInputLimit ||
        DailyOutputTokens >= DailyOutputLimit ||
        MonthlyInputTokens >= MonthlyInputLimit ||
        MonthlyOutputTokens >= MonthlyOutputLimit;

    /// <summary>
    /// Gets whether this provider is near budget (&gt;= 80% utilization).
    /// </summary>
    public bool IsNearBudget => HighestUsagePercent >= 80;

    /// <summary>
    /// Gets a status indicator: "Over Budget", "Near Budget", or "OK".
    /// </summary>
    public string BudgetStatus
    {
        get
        {
            if (IsOverBudget) return "Over Budget";
            if (IsNearBudget) return "Near Budget";
            return "OK";
        }
    }
}
