using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Daiv3.App.Maui.Services;

/// <summary>
/// Windows implementation of <see cref="ISystemMetricsService"/> that collects
/// real-time CPU, memory, and disk metrics.
/// Implements CT-REQ-006: System Resource Metrics.
/// </summary>
public sealed class SystemMetricsService : ISystemMetricsService, IDisposable
{
    private readonly ILogger<SystemMetricsService> _logger;

    // CPU sampling state (process-based delta measurement)
    private DateTime _lastCpuSampleTime = DateTime.MinValue;
    private TimeSpan _lastProcessorTime = TimeSpan.Zero;
    private double _lastCpuPercent;
    private readonly object _cpuLock = new();

    // Windows P/Invoke for GlobalMemoryStatusEx
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;        // memory load (0-100)
        public ulong ullTotalPhys;       // total physical memory
        public ulong ullAvailPhys;       // available physical memory
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;

        public static MEMORYSTATUSEX Create()
        {
            var s = default(MEMORYSTATUSEX);
            s.dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>();
            return s;
        }
    }

    public SystemMetricsService(ILogger<SystemMetricsService>? logger = null)
    {
        _logger = logger ?? NullLogger<SystemMetricsService>.Instance;
    }

    /// <inheritdoc />
    public double GetCpuUtilizationPercent()
    {
        lock (_cpuLock)
        {
            try
            {
                using var proc = Process.GetCurrentProcess();
                proc.Refresh();

                var now = DateTime.UtcNow;
                var currentProcessorTime = proc.TotalProcessorTime;

                if (_lastCpuSampleTime != DateTime.MinValue)
                {
                    var elapsedMs = (now - _lastCpuSampleTime).TotalMilliseconds;
                    var cpuMs = (currentProcessorTime - _lastProcessorTime).TotalMilliseconds;

                    if (elapsedMs > 50) // Only update if enough time has passed
                    {
                        var raw = cpuMs / (elapsedMs * Environment.ProcessorCount) * 100.0;
                        _lastCpuPercent = Math.Min(100.0, Math.Max(0.0, raw));
                    }
                }

                _lastCpuSampleTime = now;
                _lastProcessorTime = currentProcessorTime;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to sample process CPU");
            }

            return _lastCpuPercent;
        }
    }

    /// <inheritdoc />
    public (long UsedBytes, long TotalBytes) GetSystemMemory()
    {
        try
        {
            var status = MEMORYSTATUSEX.Create();
            if (GlobalMemoryStatusEx(ref status) && status.ullTotalPhys > 0)
            {
                var total = (long)status.ullTotalPhys;
                var used = total - (long)status.ullAvailPhys;
                return (used, total);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "GlobalMemoryStatusEx failed, falling back to GC info");
        }

        // Fallback: use GC memory info (less accurate but always available)
        try
        {
            var memInfo = GC.GetGCMemoryInfo();
            var total = memInfo.TotalAvailableMemoryBytes;
            var used = GC.GetTotalMemory(forceFullCollection: false);
            return (used, total);
        }
        catch
        {
            return (0, 0);
        }
    }

    /// <inheritdoc />
    public (long AvailableBytes, long TotalBytes) GetDiskInfo()
    {
        try
        {
            // Use the system drive (where Windows is installed)
            var systemDrive = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
            var driveInfo = new DriveInfo(systemDrive);
            if (driveInfo.IsReady)
                return (driveInfo.AvailableFreeSpace, driveInfo.TotalSize);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to read disk info");
        }

        return (0, 1); // Return 1 for total to avoid divide-by-zero
    }

    /// <inheritdoc />
    public long GetProcessMemoryBytes()
    {
        try
        {
            using var proc = Process.GetCurrentProcess();
            proc.Refresh();
            return proc.WorkingSet64;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to read process memory");
            return 0;
        }
    }

    /// <inheritdoc />
    public long GetDirectorySize(string path)
    {
        if (!Directory.Exists(path))
            return 0;

        try
        {
            return Directory
                .EnumerateFiles(path, "*", SearchOption.AllDirectories)
                .Sum(f =>
                {
                    try { return new FileInfo(f).Length; }
                    catch { return 0L; }
                });
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to compute directory size for {Path}", path);
            return 0;
        }
    }

    /// <inheritdoc />
    public void Dispose() { }
}
