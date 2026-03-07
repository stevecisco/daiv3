using System.Diagnostics;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Daiv3.App.Maui.Models;
using Daiv3.Infrastructure.Shared.Hardware;

namespace Daiv3.App.Maui.Services;

/// <summary>
/// Windows implementation of <see cref="IAdminDashboardService"/>.
/// Collects comprehensive system metrics for the System Admin Dashboard.
/// Implements CT-REQ-010.
/// </summary>
public sealed class AdminDashboardService : IAdminDashboardService, IDisposable
{
    private readonly ISystemMetricsService _systemMetricsService;
    private readonly IHardwareDetectionProvider _hardwareDetectionProvider;
    private readonly IDashboardService _dashboardService;
    private readonly AdminDashboardOptions _options;
    private readonly ILogger<AdminDashboardService> _logger;

    private readonly ConcurrentCircularBuffer<SystemMetricsSnapshot> _metricsHistory;
    private AdminDashboardMetrics? _lastMetrics;
    private DashboardAlerts? _lastAlerts;
    private CancellationTokenSource? _pollingCancellation;
    private Task? _pollingTask;
    private readonly object _lock = new();

    public event EventHandler<AdminDashboardMetrics>? MetricsUpdated;
    public event EventHandler<DashboardAlerts>? AlertsChanged;

    public AdminDashboardService(
        ISystemMetricsService systemMetricsService,
        IHardwareDetectionProvider hardwareDetectionProvider,
        IDashboardService dashboardService,
        IOptions<AdminDashboardOptions> options,
        ILogger<AdminDashboardService> logger)
    {
        _systemMetricsService = systemMetricsService ?? throw new ArgumentNullException(nameof(systemMetricsService));
        _hardwareDetectionProvider = hardwareDetectionProvider ?? throw new ArgumentNullException(nameof(hardwareDetectionProvider));
        _dashboardService = dashboardService ?? throw new ArgumentNullException(nameof(dashboardService));
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new NullReferenceException(nameof(logger));

        // Initialize metrics history - circular buffer for 24 hours of data
        var maxSnapshots = (24 * 60 * 60) / _options.DefaultRefreshIntervalSeconds;
        _metricsHistory = new ConcurrentCircularBuffer<SystemMetricsSnapshot>(maxSnapshots);
    }

    /// <inheritdoc />
    public async Task<AdminDashboardMetrics> GetMetricsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var now = DateTimeOffset.UtcNow;

            // Collect CPU metrics
            var cpuMetrics = CollectCpuMetrics();

            // Collect GPU metrics
            var gpuMetrics = CollectGpuMetrics();

            // Collect NPU metrics
            var npuMetrics = CollectNpuMetrics();

            // Collect memory metrics
            var memoryMetrics = CollectMemoryMetrics();

            // Collect storage metrics
            var storageMetrics = await CollectStorageMetricsAsync(cancellationToken);

            // Collect queue metrics
            var queueMetrics = await CollectQueueMetricsAsync(cancellationToken);

            // Collect agent metrics
            var agentMetrics = await CollectAgentMetricsAsync(cancellationToken);

            var metrics = new AdminDashboardMetrics(
                cpuMetrics,
                gpuMetrics,
                npuMetrics,
                memoryMetrics,
                storageMetrics,
                queueMetrics,
                agentMetrics,
                now);

            lock (_lock)
            {
                _lastMetrics = metrics;

                // Store in history
                var snapshot = new SystemMetricsSnapshot(
                    now,
                    cpuMetrics.OverallUtilizationPercent,
                    (memoryMetrics.PhysicalRamUsedBytes * 100.0) / memoryMetrics.PhysicalRamTotalBytes,
                    gpuMetrics.UtilizationPercent,
                    queueMetrics.TotalQueuedItems,
                    agentMetrics.Count(a => a.State == "Running"),
                    storageMetrics.AvailableDiskSpaceBytes);

                _metricsHistory.Add(snapshot);
            }

            // Check and update alerts
            var alerts = ComputeAlerts(metrics);
            if (_lastAlerts == null || HasAlertsChanged(alerts, _lastAlerts))
            {
                _lastAlerts = alerts;
                AlertsChanged?.Invoke(this, alerts);
            }

            MetricsUpdated?.Invoke(this, metrics);
            return metrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to collect admin dashboard metrics");
            var errorMetrics = new AdminDashboardMetrics(
                new CpuMetrics(0, [], 0, 0, 0, 0),
                new GpuMetrics(false),
                new NpuMetrics(false),
                new MemoryMetrics(0, 0, 0, 0),
                new StorageMetrics(0, 0, 0, 0, 0, 0),
                new QueueMetricsDetailed(0, 0, 0, 0, 0),
                [],
                DateTimeOffset.UtcNow,
                ex.Message);
            return errorMetrics;
        }
    }

    /// <inheritdoc />
    public DashboardAlerts GetAlerts()
    {
        lock (_lock)
        {
            return _lastAlerts ?? new DashboardAlerts(false, false, false, false, false, false, []);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<SystemMetricsSnapshot> GetMetricsHistory(int hoursBack = 24)
    {
        lock (_lock)
        {
            var cutoff = DateTimeOffset.UtcNow.AddHours(-hoursBack);
            return _metricsHistory.ToList()
                .Where(s => s.Timestamp >= cutoff)
                .ToList()
                .AsReadOnly();
        }
    }

    /// <inheritdoc />
    public async Task StartMetricsPollingAsync(int refreshIntervalSeconds = 3, CancellationToken cancellationToken = default)
    {
        if (_pollingTask != null || _pollingCancellation != null)
        {
            _logger.LogWarning("Metrics polling already active");
            return;
        }

        _pollingCancellation?.Dispose();
        _pollingCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        _pollingTask = Task.Run(() => PollingLoopAsync(refreshIntervalSeconds, _pollingCancellation.Token), _pollingCancellation.Token);
        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopMetricsPollingAsync()
    {
        if (_pollingCancellation == null)
            return;

        _pollingCancellation.Cancel();
        if (_pollingTask != null)
        {
            try
            {
                await _pollingTask;
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }

        _pollingCancellation.Dispose();
        _pollingCancellation = null;
        _pollingTask = null;
    }

    public void Dispose()
    {
        StopMetricsPollingAsync().GetAwaiter().GetResult();
        _pollingCancellation?.Dispose();
    }

    // Private methods

    private CpuMetrics CollectCpuMetrics()
    {
        try
        {
            var procCount = Environment.ProcessorCount;
            var overallUtilization = _systemMetricsService.GetCpuUtilizationPercent();

            // Get per-core metrics (simulated for Windows)
            var coreMetrics = new List<CoreMetric>();
            for (int i = 0; i < procCount; i++)
            {
                // Windows API doesn't easily expose per-core metrics from user code
                // For now, distribute overall utilization across cores
                coreMetrics.Add(new CoreMetric(i, overallUtilization / procCount));
            }

            // Get processor info
            var processorInfo = GetProcessorInfo();

            // Collect top processes by CPU
            var topProcesses = GetTopProcessesByCpu(5);

            return new CpuMetrics(
                overallUtilization,
                coreMetrics,
                procCount,
                GetThreadCount(),
                processorInfo.BaseFrequencyGhz,
                processorInfo.MaxFrequencyGhz,
                TryGetCpuTemperature(),
                CheckThermalThrottling(),
                topProcesses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to collect CPU metrics");
            return new CpuMetrics(0, [], 0, 0, 0, 0);
        }
    }

    private GpuMetrics CollectGpuMetrics()
    {
        try
        {
            var isAvailable = _hardwareDetectionProvider.IsTierAvailable(HardwareAccelerationTier.Gpu);
            if (!isAvailable)
                return new GpuMetrics(false);

            // GPU metrics would require GPU-specific APIs (NVIDIA CUDA, DirectML, etc.)
            // For now, return basic structure
            return new GpuMetrics(
                true,
                TryGetGpuUtilization(),
                0, // Memory would require GPU API access
                0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to collect GPU metrics");
            return new GpuMetrics(false);
        }
    }

    private NpuMetrics CollectNpuMetrics()
    {
        try
        {
            var isAvailable = _hardwareDetectionProvider.IsTierAvailable(HardwareAccelerationTier.Npu);
            return new NpuMetrics(
                isAvailable,
                isAvailable ? "NPU" : "CPU");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to collect NPU metrics");
            return new NpuMetrics(false);
        }
    }

    private MemoryMetrics CollectMemoryMetrics()
    {
        try
        {
            var (used, total) = _systemMetricsService.GetSystemMemory();
            var available = total - used;

            var processMetrics = new List<ProcessMemoryMetric>();
            try
            {
                // Add MAUI process
                using var proc = Process.GetCurrentProcess();
                proc.Refresh();
                processMetrics.Add(new ProcessMemoryMetric(
                    proc.ProcessName,
                    proc.Id,
                    proc.WorkingSet64,
                    "Stable"));
            }
            catch { }

            return new MemoryMetrics(
                used,
                total,
                available,
                0,
                0,
                processMetrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to collect memory metrics");
            return new MemoryMetrics(0, 0, 0, 0);
        }
    }

    private async Task<StorageMetrics> CollectStorageMetricsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var (available, total) = _systemMetricsService.GetDiskInfo();

            // Get knowledge base storage sizes
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var daiv3Dir = Path.Combine(localAppData, "Daiv3");

            long knowledgeBaseSize = 0;
            long modelCacheSize = 0;

            if (Directory.Exists(daiv3Dir))
            {
                var kbDir = Path.Combine(daiv3Dir, "knowledge");
                var modelDir = Path.Combine(daiv3Dir, "models");

                if (Directory.Exists(kbDir))
                {
                    knowledgeBaseSize = await GetDirectorySizeAsync(kbDir);
                }

                if (Directory.Exists(modelDir))
                {
                    modelCacheSize = await GetDirectorySizeAsync(modelDir);
                }
            }

            return new StorageMetrics(
                knowledgeBaseSize,
                0, // Documents
                0, // Embeddings database
                modelCacheSize,
                available,
                total);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to collect storage metrics");
            return new StorageMetrics(0, 0, 0, 0, 0, 0);
        }
    }

    private async Task<QueueMetricsDetailed> CollectQueueMetricsAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Get queue status from dashboard service
            var dashboardData = await _dashboardService.GetDashboardDataAsync(cancellationToken);
            var queueStatus = dashboardData?.Queue;

            if (queueStatus == null)
            {
                return new QueueMetricsDetailed(0, 0, 0, 0, 0);
            }

            return new QueueMetricsDetailed(
                queueStatus.PendingCount,
                queueStatus.ImmediateCount,
                0, // High priority - would need queue API access
                queueStatus.NormalCount,
                queueStatus.BackgroundCount,
                queueStatus.CurrentModel,
                0,
                null,
                queueStatus.EstimatedWaitSeconds ?? 0,
                queueStatus.ThroughputPerMinute ?? 0,
                queueStatus.AverageTaskDurationSeconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to collect queue metrics");
            return new QueueMetricsDetailed(0, 0, 0, 0, 0);
        }
    }

    private async Task<IReadOnlyList<AgentMetricsDetailed>> CollectAgentMetricsAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Get agent status from dashboard service
            var dashboardData = await _dashboardService.GetDashboardDataAsync(cancellationToken);
            var agentStatus = dashboardData?.Agent;

            if (agentStatus?.Activities == null || agentStatus.Activities.Count == 0)
            {
                return [];
            }

            var result = new List<AgentMetricsDetailed>();
            foreach (var agent in agentStatus.Activities)
            {
                result.Add(new AgentMetricsDetailed(
                    agent.AgentId ?? "",
                    agent.AgentName ?? "",
                    agent.State ?? "Unknown",
                    0, // Memory would need process-level tracking
                    0, // CPU would need process-level tracking
                    0, // Thread count
                    agent.CurrentTask,
                    null)); // No project assignment in this model yet
            }

            return result.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to collect agent metrics");
            return [];
        }
    }

    private DashboardAlerts ComputeAlerts(AdminDashboardMetrics metrics)
    {
        var alerts = new List<string>();
        var highCpuAlert = metrics.Cpu.OverallUtilizationPercent > _options.CpuThresholdPercent;
        var highGpuAlert = metrics.Gpu.UtilizationPercent > _options.GpuThresholdPercent;
        var highMemoryAlert = (metrics.Memory.PhysicalRamUsedBytes * 100.0 / metrics.Memory.PhysicalRamTotalBytes) > _options.MemoryThresholdPercent;
        var lowDiskAlert = metrics.Storage.AvailableDiskSpaceBytes < (_options.DiskFreeThresholdMb * 1024 * 1024);
        var queueBottleneckAlert = metrics.Queue.TotalQueuedItems > _options.QueueDepthThreshold;
        var thermalAlert = metrics.Cpu.ThermalThrottling;

        if (highCpuAlert) alerts.Add($"CPU utilization high: {metrics.Cpu.OverallUtilizationPercent:F1}%");
        if (highGpuAlert) alerts.Add($"GPU utilization high: {metrics.Gpu.UtilizationPercent:F1}%");
        if (highMemoryAlert) alerts.Add("Memory usage exceeds threshold");
        if (lowDiskAlert) alerts.Add("Low disk space");
        if (queueBottleneckAlert) alerts.Add($"Queue bottleneck: {metrics.Queue.TotalQueuedItems} items pending");
        if (thermalAlert) alerts.Add("CPU thermal throttling detected");

        return new DashboardAlerts(
            highCpuAlert,
            highGpuAlert,
            highMemoryAlert,
            lowDiskAlert,
            queueBottleneckAlert,
            thermalAlert,
            alerts);
    }

    private bool HasAlertsChanged(DashboardAlerts current, DashboardAlerts previous)
    {
        return current.HighCpuAlert != previous.HighCpuAlert ||
               current.HighGpuAlert != previous.HighGpuAlert ||
               current.HighMemoryAlert != previous.HighMemoryAlert ||
               current.LowDiskAlert != previous.LowDiskAlert ||
               current.QueueBottleneckAlert != previous.QueueBottleneckAlert ||
               current.ThermalThrottlingAlert != previous.ThermalThrottlingAlert;
    }

    private async Task PollingLoopAsync(int refreshIntervalSeconds, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await GetMetricsAsync(cancellationToken);
                await Task.Delay(TimeSpan.FromSeconds(refreshIntervalSeconds), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in metrics polling loop");
            }
        }
    }

    // Helper methods

    private (double BaseFrequencyGhz, double MaxFrequencyGhz) GetProcessorInfo()
    {
        // Would require WMI or registry access on Windows
        // For now, return placeholder values
        return (2.0, 4.0);
    }

    private int GetThreadCount()
    {
        return ProcessThreadCollection.Count > 0 ? ProcessThreadCollection.Count : Environment.ProcessorCount * 2;
    }

    private double? TryGetCpuTemperature()
    {
        // Temperature requires hardware-specific APIs or WMI
        // Placeholder for future implementation
        return null;
    }

    private bool CheckThermalThrottling()
    {
        // Would require hardware-specific monitoring
        return false;
    }

    private double TryGetGpuUtilization()
    {
        // GPU utilization requires GPU-specific APIs
        return 0;
    }

    private List<ProcessCpuMetric> GetTopProcessesByCpu(int count)
    {
        try
        {
            var processes = Process.GetProcesses()
                .Where(p => p.ProcessName != null)
                .OrderByDescending(p =>
                {
                    try
                    {
                        p.Refresh();
                        return p.TotalProcessorTime.TotalMilliseconds;
                    }
                    catch { return 0; }
                })
                .Take(count)
                .Select(p => new ProcessCpuMetric(
                    p.ProcessName,
                    p.Id,
                    0, // Would need delta sampling
                    0))
                .ToList();

            return processes;
        }
        catch
        {
            return [];
        }
    }

    private async Task<long> GetDirectorySizeAsync(string directory)
    {
        return await Task.Run(() =>
        {
            try
            {
                var dirInfo = new DirectoryInfo(directory);
                if (!dirInfo.Exists)
                    return 0;

                return dirInfo.GetFiles("*", SearchOption.AllDirectories)
                    .Sum(f =>
                    {
                        try { return f.Length; }
                        catch { return 0; }
                    });
            }
            catch
            {
                return 0;
            }
        });
    }

    private ProcessThreadCollection ProcessThreadCollection => Process.GetCurrentProcess().Threads;
}

/// <summary>
/// Circular buffer for metrics history.
/// </summary>
public class ConcurrentCircularBuffer<T>
{
    private readonly T[] _buffer;
    private int _head;
    private int _count;
    private readonly object _lock = new();

    public ConcurrentCircularBuffer(int capacity)
    {
        if (capacity <= 0) throw new ArgumentException("Capacity must be positive");
        _buffer = new T[capacity];
    }

    public void Add(T item)
    {
        lock (_lock)
        {
            _buffer[_head] = item;
            _head = (_head + 1) % _buffer.Length;
            if (_count < _buffer.Length)
                _count++;
        }
    }

    public List<T> ToList()
    {
        lock (_lock)
        {
            var result = new List<T>(_count);
            for (int i = 0; i < _count; i++)
            {
                result.Add(_buffer[(_head - _count + i + _buffer.Length) % _buffer.Length]);
            }
            return result;
        }
    }
}

/// <summary>
/// Configuration options for admin dashboard.
/// </summary>
public class AdminDashboardOptions
{
    public const string Section = "Dashboard:AdminMetrics";

    public bool Enabled { get; set; } = true;
    public int DefaultRefreshIntervalSeconds { get; set; } = 3;
    public int HistoryRetentionHours { get; set; } = 24;
    public int CpuThresholdPercent { get; set; } = 85;
    public int GpuThresholdPercent { get; set; } = 90;
    public int MemoryThresholdPercent { get; set; } = 80;
    public int DiskFreeThresholdMb { get; set; } = 1024;
    public int QueueDepthThreshold { get; set; } = 100;
}
