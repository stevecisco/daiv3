namespace Daiv3.App.Maui.Models;

/// <summary>
/// Complete system metrics for the System Admin Dashboard.
/// Implements CT-REQ-010: System Admin Dashboard with real-time infrastructure metrics.
/// </summary>
public record AdminDashboardMetrics(
    CpuMetrics Cpu,
    GpuMetrics Gpu,
    NpuMetrics Npu,
    MemoryMetrics Memory,
    StorageMetrics Storage,
    QueueMetricsDetailed Queue,
    IReadOnlyList<AgentMetricsDetailed> Agents,
    DateTimeOffset LastUpdated,
    string? CollectionError = null)
{
    /// <summary>
    /// Gets a value indicating whether critical data was collected successfully.
    /// </summary>
    public bool IsHealthy => string.IsNullOrEmpty(CollectionError);
}

/// <summary>
/// Detailed CPU metrics including per-core breakdown and thermal status.
/// </summary>
public record CpuMetrics(
    double OverallUtilizationPercent,
    IReadOnlyList<CoreMetric> PerCoreUtilization,
    int CoreCount,
    int ThreadCount,
    double? BaseFrequencyGhz,
    double? MaxFrequencyGhz,
    double? TemperatureCelsius = null,
    bool ThermalThrottling = false,
    IReadOnlyList<ProcessCpuMetric>? TopProcesses = null);

/// <summary>
/// Per-core CPU utilization.
/// </summary>
public record CoreMetric(
    int CoreIndex,
    double UtilizationPercent,
    double FrequencyGhz = 0);

/// <summary>
/// Process-level CPU usage information.
/// </summary>
public record ProcessCpuMetric(
    string ProcessName,
    int ProcessId,
    double CpuUtilizationPercent,
    long MemoryBytesRss);

/// <summary>
/// GPU metrics including memory and active processes.
/// </summary>
public record GpuMetrics(
    bool IsAvailable,
    double UtilizationPercent = 0,
    long MemoryUsedBytes = 0,
    long MemoryTotalBytes = 0,
    double? TemperatureCelsius = null,
    IReadOnlyList<string>? ActiveProcesses = null,
    IReadOnlyList<GpuProcessMemory>? ProcessMemoryDetails = null);

/// <summary>
/// GPU memory allocation per process.
/// </summary>
public record GpuProcessMemory(
    string ProcessName,
    int ProcessId,
    long MemoryBytes);

/// <summary>
/// NPU (Neural Processing Unit) metrics.
/// </summary>
public record NpuMetrics(
    bool IsAvailable,
    string ExecutionProvider = "CPU",
    double UtilizationPercent = 0,
    long MemoryAvailableBytes = 0,
    long MemoryTotalBytes = 0);

/// <summary>
/// Memory metrics for the system and individual processes.
/// </summary>
public record MemoryMetrics(
    long PhysicalRamUsedBytes,
    long PhysicalRamTotalBytes,
    long AvailableMemoryBytes,
    long VirtualMemoryUsedBytes,
    double MemoryTrendPercent = 0,
    IReadOnlyList<ProcessMemoryMetric>? ProcessMetrics = null);

/// <summary>
/// Process-level memory information.
/// </summary>
public record ProcessMemoryMetric(
    string ProcessName,
    int ProcessId,
    long MemoryBytesRss,
    string MemoryTrend); // "Growing", "Stable", "Shrinking"

/// <summary>
/// Storage and disk space metrics.
/// </summary>
public record StorageMetrics(
    long KnowledgeBaseSizeBytes,
    long DocumentsSizeBytes,
    long EmbeddingsDatabaseSizeBytes,
    long ModelCacheSizeBytes,
    long AvailableDiskSpaceBytes,
    long TotalDiskSpaceBytes,
    IReadOnlyList<ModelStorageMetric>? ModelStorageBreakdown = null);

/// <summary>
/// Per-model storage information.
/// </summary>
public record ModelStorageMetric(
    string ModelName,
    long SizeBytes,
    DateTimeOffset? LastAccessTime = null);

/// <summary>
/// Detailed queue metrics with priority breakdown.
/// </summary>
public record QueueMetricsDetailed(
    int TotalQueuedItems,
    int CriticalPriorityCount,
    int HighPriorityCount,
    int NormalPriorityCount,
    int LowPriorityCount,
    string? CurrentlyProcessing = null,
    double ElapsedProcessingSeconds = 0,
    string? OldestItemInQueue = null,
    double OldestItemAgeSeconds = 0,
    double ThroughputItemsPerMinute = 0,
    double? AverageWaitTimeSeconds = null,
    bool IsBottlenecked = false);

/// <summary>
/// Detailed agent metrics including task assignment and resource usage.
/// </summary>
public record AgentMetricsDetailed(
    string AgentId,
    string AgentName,
    string State, // "Running", "Idle", "Blocked", "Error"
    long MemoryBytesRss,
    double CpuPercent,
    int ThreadCount,
    string? CurrentTask = null,
    string? AssignedProject = null,
    int? TokensUsed = null,
    int? IterationCount = null,
    string? BlockedReason = null,
    string? ErrorMessage = null);

/// <summary>
/// Raw system metrics snapshot used for trends and history.
/// </summary>
public record SystemMetricsSnapshot(
    DateTimeOffset Timestamp,
    double CpuUtilizationPercent,
    double MemoryUtilizationPercent,
    double GpuUtilizationPercent,
    int QueuedItemCount,
    int ActiveAgentCount,
    long AvailableDiskBytesRemaining);

/// <summary>
/// Dashboard alert configuration and state.
/// </summary>
public record DashboardAlerts(
    bool HighCpuAlert,
    bool HighGpuAlert,
    bool HighMemoryAlert,
    bool LowDiskAlert,
    bool QueueBottleneckAlert,
    bool ThermalThrottlingAlert,
    IReadOnlyList<string> AlertMessages);
