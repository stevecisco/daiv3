using Xunit;

namespace Daiv3.App.Cli.Tests;

public class AdminDashboardCliHistoryTests
{
    [Fact]
    public void BuildSummary_WithRecentSnapshots_ComputesExpectedTrends()
    {
        var now = new DateTimeOffset(2026, 3, 5, 12, 0, 0, TimeSpan.Zero);
        var snapshots = new List<AdminDashboardCliSnapshot>
        {
            new(
                now.AddHours(-2),
                CpuPercent: 20,
                MemoryPercent: 40,
                DiskUsedPercent: 60,
                DiskFreeGb: 100,
                IsGpuAvailable: true,
                IsNpuAvailable: false,
                ActiveExecutionProvider: "Gpu",
                QueueTotal: 5,
                QueueImmediate: 1,
                QueueNormal: 3,
                QueueBackground: 1,
                ActiveAgents: 2,
                RegisteredAgents: 5,
                KnowledgeBaseBytes: 1000,
                ModelCacheBytes: 2000),
            new(
                now.AddHours(-1),
                CpuPercent: 40,
                MemoryPercent: 60,
                DiskUsedPercent: 62,
                DiskFreeGb: 90,
                IsGpuAvailable: true,
                IsNpuAvailable: false,
                ActiveExecutionProvider: "Gpu",
                QueueTotal: 15,
                QueueImmediate: 2,
                QueueNormal: 10,
                QueueBackground: 3,
                ActiveAgents: 3,
                RegisteredAgents: 5,
                KnowledgeBaseBytes: 1100,
                ModelCacheBytes: 2100),
            new(
                now.AddMinutes(-10),
                CpuPercent: 30,
                MemoryPercent: 50,
                DiskUsedPercent: 65,
                DiskFreeGb: 80,
                IsGpuAvailable: true,
                IsNpuAvailable: true,
                ActiveExecutionProvider: "Npu",
                QueueTotal: 10,
                QueueImmediate: 2,
                QueueNormal: 6,
                QueueBackground: 2,
                ActiveAgents: 4,
                RegisteredAgents: 6,
                KnowledgeBaseBytes: 1200,
                ModelCacheBytes: 2200)
        };

        var summary = AdminDashboardCliHistory.BuildSummary(snapshots, now, historyHours: 24);

        Assert.Equal(3, summary.SampleCount);
        Assert.Equal(20, summary.CpuPercent.Min);
        Assert.Equal(30, summary.CpuPercent.Avg, 1);
        Assert.Equal(40, summary.CpuPercent.Max);

        Assert.Equal(40, summary.MemoryPercent.Min);
        Assert.Equal(50, summary.MemoryPercent.Avg, 1);
        Assert.Equal(60, summary.MemoryPercent.Max);

        Assert.Equal(5, summary.QueueDepth.Min);
        Assert.Equal(10, summary.QueueDepth.Avg, 1);
        Assert.Equal(15, summary.QueueDepth.Max);

        Assert.Equal(80, summary.DiskFreeGb.Min);
        Assert.Equal(90, summary.DiskFreeGb.Avg, 1);
        Assert.Equal(100, summary.DiskFreeGb.Max);
    }

    [Fact]
    public void BuildSummary_ExcludesSnapshotsOutsideWindow()
    {
        var now = new DateTimeOffset(2026, 3, 5, 12, 0, 0, TimeSpan.Zero);
        var snapshots = new List<AdminDashboardCliSnapshot>
        {
            new(
                now.AddHours(-30),
                CpuPercent: 90,
                MemoryPercent: 90,
                DiskUsedPercent: 90,
                DiskFreeGb: 10,
                IsGpuAvailable: false,
                IsNpuAvailable: false,
                ActiveExecutionProvider: "Cpu",
                QueueTotal: 90,
                QueueImmediate: 30,
                QueueNormal: 30,
                QueueBackground: 30,
                ActiveAgents: 1,
                RegisteredAgents: 1,
                KnowledgeBaseBytes: 1,
                ModelCacheBytes: 1)
        };

        var summary = AdminDashboardCliHistory.BuildSummary(snapshots, now, historyHours: 24);

        Assert.Equal(0, summary.SampleCount);
        Assert.Equal(0, summary.CpuPercent.Avg);
        Assert.Equal(0, summary.MemoryPercent.Avg);
        Assert.Equal(0, summary.QueueDepth.Avg);
        Assert.Equal(0, summary.DiskFreeGb.Avg);
    }
}
