namespace Daiv3.App.Maui.Services;

/// <summary>
/// Provides real-time system resource metrics for the dashboard.
/// Implements CT-REQ-006: System Resource Metrics (CPU, Memory, Disk, GPU/NPU).
/// </summary>
public interface ISystemMetricsService
{
    /// <summary>
    /// Gets the process CPU utilization as a percentage (0-100).
    /// Measures Daiv3 process CPU across all logical cores.
    /// Note: System-wide CPU pending HW-NFR-002 hardware integration.
    /// </summary>
    /// <returns>CPU utilization percentage, or 0 if measurement is unavailable.</returns>
    double GetCpuUtilizationPercent();

    /// <summary>
    /// Gets physical RAM usage: (used bytes, total bytes).
    /// Returns (0, 0) if the information cannot be read.
    /// </summary>
    (long UsedBytes, long TotalBytes) GetSystemMemory();

    /// <summary>
    /// Gets disk info for the system drive: (available bytes, total bytes).
    /// Returns (0, 1) if unavailable to avoid divide-by-zero.
    /// </summary>
    (long AvailableBytes, long TotalBytes) GetDiskInfo();

    /// <summary>
    /// Gets the current process working-set memory in bytes.
    /// </summary>
    long GetProcessMemoryBytes();

    /// <summary>
    /// Gets the total size (bytes) of all files under <paramref name="path"/>, recursively.
    /// Returns 0 if the path does not exist or cannot be read.
    /// </summary>
    long GetDirectorySize(string path);
}
