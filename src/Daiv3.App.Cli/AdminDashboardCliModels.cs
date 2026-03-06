namespace Daiv3.App.Cli;

/// <summary>
/// Snapshot used by CLI admin dashboard output and trend calculations.
/// </summary>
public sealed record AdminDashboardCliSnapshot(
    DateTimeOffset TimestampUtc,
    double CpuPercent,
    double MemoryPercent,
    double DiskUsedPercent,
    double DiskFreeGb,
    bool IsGpuAvailable,
    bool IsNpuAvailable,
    string ActiveExecutionProvider,
    int QueueTotal,
    int QueueImmediate,
    int QueueNormal,
    int QueueBackground,
    int ActiveAgents,
    int RegisteredAgents,
    long KnowledgeBaseBytes,
    long ModelCacheBytes);

/// <summary>
/// Min/average/max trend values for a metric.
/// </summary>
public sealed record MetricTrend(double Min, double Avg, double Max);

/// <summary>
/// Summary for recent admin dashboard history.
/// </summary>
public sealed record AdminDashboardHistorySummary(
    DateTimeOffset WindowStartUtc,
    DateTimeOffset WindowEndUtc,
    int SampleCount,
    MetricTrend CpuPercent,
    MetricTrend MemoryPercent,
    MetricTrend QueueDepth,
    MetricTrend DiskFreeGb);

/// <summary>
/// Utility methods for CLI dashboard history calculations.
/// </summary>
public static class AdminDashboardCliHistory
{
    public static AdminDashboardHistorySummary BuildSummary(
        IReadOnlyList<AdminDashboardCliSnapshot> snapshots,
        DateTimeOffset nowUtc,
        int historyHours = 24)
    {
        var windowStart = nowUtc.AddHours(-historyHours);
        var recent = snapshots
            .Where(s => s.TimestampUtc >= windowStart)
            .OrderBy(s => s.TimestampUtc)
            .ToList();

        if (recent.Count == 0)
        {
            var empty = new MetricTrend(0, 0, 0);
            return new AdminDashboardHistorySummary(windowStart, nowUtc, 0, empty, empty, empty, empty);
        }

        return new AdminDashboardHistorySummary(
            windowStart,
            nowUtc,
            recent.Count,
            ComputeTrend(recent.Select(s => s.CpuPercent)),
            ComputeTrend(recent.Select(s => s.MemoryPercent)),
            ComputeTrend(recent.Select(s => (double)s.QueueTotal)),
            ComputeTrend(recent.Select(s => s.DiskFreeGb)));
    }

    public static MetricTrend ComputeTrend(IEnumerable<double> values)
    {
        var list = values.ToList();
        if (list.Count == 0)
        {
            return new MetricTrend(0, 0, 0);
        }

        return new MetricTrend(
            Min: list.Min(),
            Avg: list.Average(),
            Max: list.Max());
    }
}
