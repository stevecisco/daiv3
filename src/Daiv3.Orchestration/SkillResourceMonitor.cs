using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Daiv3.Orchestration;
/// <summary>
/// Monitors resource usage during skill execution.
/// Tracks memory and CPU consumption, triggers cancellation on violations.
/// </summary>
public sealed class SkillResourceMonitor : IDisposable
{
    private readonly ILogger<SkillResourceMonitor> _logger;
    private readonly SkillSandboxConfiguration _sandboxConfig;
    private readonly string _skillName;
    private readonly long _maxMemoryBytes;
    private readonly int _maxCpuPercentage;
    private readonly CancellationTokenSource _cancellationSource;
    private readonly Stopwatch _stopwatch;
    private readonly Task? _monitoringTask;

    private long _currentMemoryBytes;
    private long _peakMemoryBytes;
    private int _cpuPercentage;
    private bool _limitsExceeded;
    private string? _violationDetails;
    private bool _disposed;

    public SkillResourceMonitor(
        ILogger<SkillResourceMonitor> logger,
        SkillSandboxConfiguration sandboxConfig,
        string skillName,
        CancellationTokenSource cancellationSource)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _sandboxConfig = sandboxConfig ?? throw new ArgumentNullException(nameof(sandboxConfig));
        _skillName = skillName ?? throw new ArgumentNullException(nameof(skillName));
        _cancellationSource = cancellationSource ?? throw new ArgumentNullException(nameof(cancellationSource));

        _maxMemoryBytes = sandboxConfig.GetEffectiveMaxMemory(skillName);
        _maxCpuPercentage = sandboxConfig.GetEffectiveMaxCpu(skillName);
        _stopwatch = Stopwatch.StartNew();

        _logger.LogDebug(
            "Starting resource monitoring for skill '{SkillName}' with limits: Memory={MaxMemoryMB}MB, CPU={MaxCpu}%",
            skillName,
            _maxMemoryBytes / (1024 * 1024),
            _maxCpuPercentage);

        // Start monitoring task
        _monitoringTask = Task.Run(MonitorResourcesAsync);
    }

    /// <summary>
    /// Gets a snapshot of current resource metrics.
    /// </summary>
    public SkillResourceMetrics GetSnapshot()
    {
        return new SkillResourceMetrics
        {
            CurrentMemoryBytes = _currentMemoryBytes,
            PeakMemoryBytes = _peakMemoryBytes,
            CpuPercentage = _cpuPercentage,
            ExecutionDuration = _stopwatch.Elapsed,
            LimitsExceeded = _limitsExceeded,
            ViolationDetails = _violationDetails
        };
    }

    /// <summary>
    /// Background task that monitors resource usage.
    /// </summary>
    private async Task MonitorResourcesAsync()
    {
        try
        {
            var process = Process.GetCurrentProcess();
            var lastCpuTime = process.TotalProcessorTime;
            var lastCheckTime = DateTime.UtcNow;

            while (!_cancellationSource.Token.IsCancellationRequested && !_disposed)
            {
                await Task.Delay(_sandboxConfig.ResourceCheckIntervalMs, _cancellationSource.Token).ConfigureAwait(false);

                // Update memory metrics
                process.Refresh();
                _currentMemoryBytes = process.WorkingSet64;
                _peakMemoryBytes = Math.Max(_peakMemoryBytes, _currentMemoryBytes);

                // Calculate CPU percentage
                var currentTime = DateTime.UtcNow;
                var currentCpuTime = process.TotalProcessorTime;
                var cpuUsedMs = (currentCpuTime - lastCpuTime).TotalMilliseconds;
                var totalMsPassed = (currentTime - lastCheckTime).TotalMilliseconds;
                var cpuUsageRatio = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);
                _cpuPercentage = Math.Min(100, (int)(cpuUsageRatio * 100));

                lastCpuTime = currentCpuTime;
                lastCheckTime = currentTime;

                // Check for violations
                CheckMemoryViolation();
                CheckCpuViolation();

                if (_limitsExceeded)
                {
                    break; // Stop monitoring after violation
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation path
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Resource monitoring for skill '{SkillName}' encountered an error",
                _skillName);
        }
    }

    /// <summary>
    /// Checks for memory limit violations.
    /// </summary>
    private void CheckMemoryViolation()
    {
        if (_currentMemoryBytes > _maxMemoryBytes)
        {
            _limitsExceeded = true;
            _violationDetails = $"Memory limit exceeded: {_currentMemoryBytes / (1024 * 1024)}MB > {_maxMemoryBytes / (1024 * 1024)}MB";

            _logger.LogError(
                "Skill '{SkillName}' exceeded memory limit: {CurrentMB}MB > {MaxMB}MB - cancelling execution",
                _skillName,
                _currentMemoryBytes / (1024 * 1024),
                _maxMemoryBytes / (1024 * 1024));

            // Trigger cancellation
            _cancellationSource.Cancel();
        }
    }

    /// <summary>
    /// Checks for CPU limit violations.
    /// </summary>
    private void CheckCpuViolation()
    {
        if (_cpuPercentage > _maxCpuPercentage)
        {
            _limitsExceeded = true;
            _violationDetails = $"CPU limit exceeded: {_cpuPercentage}% > {_maxCpuPercentage}%";

            _logger.LogError(
                "Skill '{SkillName}' exceeded CPU limit: {CurrentCpu}% > {MaxCpu}% - cancelling execution",
                _skillName,
                _cpuPercentage,
                _maxCpuPercentage);

            // Trigger cancellation
            _cancellationSource.Cancel();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _stopwatch.Stop();

        // Wait for monitoring task to complete (with timeout)
        if (_monitoringTask != null)
        {
            try
            {
                _monitoringTask.Wait(TimeSpan.FromSeconds(2));
            }
            catch (AggregateException)
            {
                // Ignore cancellation exceptions during disposal
            }
        }

        _logger.LogDebug(
            "Resource monitoring stopped for skill '{SkillName}'. Peak memory: {PeakMB}MB, Duration: {DurationMs}ms",
            _skillName,
            _peakMemoryBytes / (1024 * 1024),
            _stopwatch.ElapsedMilliseconds);
    }
}

/// <summary>
/// Factory for creating SkillResourceMonitor instances.
/// Isolates resource monitoring creation for testability.
/// </summary>
public class SkillResourceMonitorFactory
{
    private readonly ILogger<SkillResourceMonitor> _logger;
    private readonly SkillSandboxConfiguration _sandboxConfig;

    public SkillResourceMonitorFactory(
        ILogger<SkillResourceMonitor> logger,
        SkillSandboxConfiguration sandboxConfig)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _sandboxConfig = sandboxConfig ?? throw new ArgumentNullException(nameof(sandboxConfig));
    }

    /// <summary>
    /// Creates a new resource monitor for a skill.
    /// </summary>
    public SkillResourceMonitor Create(string skillName, CancellationTokenSource cancellationSource)
    {
        return new SkillResourceMonitor(_logger, _sandboxConfig, skillName, cancellationSource);
    }
}
