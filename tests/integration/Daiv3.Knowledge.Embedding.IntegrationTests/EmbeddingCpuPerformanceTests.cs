using Daiv3.Infrastructure.Shared.Hardware;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Daiv3.Knowledge.Embedding.IntegrationTests;

public class EmbeddingCpuPerformanceTests
{
    [Fact]
    public void HardwareOverrides_ConfigureDetectionCorrectly()
    {
        var originalForceCpu = Environment.GetEnvironmentVariable("DAIV3_FORCE_CPU_ONLY");
        var originalDisableNpu = Environment.GetEnvironmentVariable("DAIV3_DISABLE_NPU");
        var originalDisableGpu = Environment.GetEnvironmentVariable("DAIV3_DISABLE_GPU");

        try
        {
            TestCpuOnlyOverride();
            TestDisableNpuOverride();
            TestDisableGpuOverride();
            TestNoOverrides();
        }
        finally
        {
            Environment.SetEnvironmentVariable("DAIV3_FORCE_CPU_ONLY", originalForceCpu);
            Environment.SetEnvironmentVariable("DAIV3_DISABLE_NPU", originalDisableNpu);
            Environment.SetEnvironmentVariable("DAIV3_DISABLE_GPU", originalDisableGpu);
        }
    }

    private static void TestCpuOnlyOverride()
    {
        Environment.SetEnvironmentVariable("DAIV3_FORCE_CPU_ONLY", "true");
        Environment.SetEnvironmentVariable("DAIV3_DISABLE_NPU", null);
        Environment.SetEnvironmentVariable("DAIV3_DISABLE_GPU", null);

        var provider = new HardwareDetectionProvider(NullLogger<HardwareDetectionProvider>.Instance);
        var tiers = provider.GetAvailableTiers();

        Assert.Single(tiers);
        Assert.Equal(HardwareAccelerationTier.Cpu, tiers[0]);
    }

    private static void TestDisableNpuOverride()
    {
        Environment.SetEnvironmentVariable("DAIV3_FORCE_CPU_ONLY", null);
        Environment.SetEnvironmentVariable("DAIV3_DISABLE_NPU", "1");
        Environment.SetEnvironmentVariable("DAIV3_DISABLE_GPU", null);

        var provider = new HardwareDetectionProvider(NullLogger<HardwareDetectionProvider>.Instance);
        var tiers = provider.GetAvailableTiers();

        Assert.DoesNotContain(HardwareAccelerationTier.Npu, tiers);
        Assert.Contains(HardwareAccelerationTier.Cpu, tiers);
    }

    private static void TestDisableGpuOverride()
    {
        Environment.SetEnvironmentVariable("DAIV3_FORCE_CPU_ONLY", null);
        Environment.SetEnvironmentVariable("DAIV3_DISABLE_NPU", null);
        Environment.SetEnvironmentVariable("DAIV3_DISABLE_GPU", "1");

        var provider = new HardwareDetectionProvider(NullLogger<HardwareDetectionProvider>.Instance);
        var tiers = provider.GetAvailableTiers();

        Assert.DoesNotContain(HardwareAccelerationTier.Gpu, tiers);
        Assert.Contains(HardwareAccelerationTier.Cpu, tiers);
    }

    private static void TestNoOverrides()
    {
        Environment.SetEnvironmentVariable("DAIV3_FORCE_CPU_ONLY", null);
        Environment.SetEnvironmentVariable("DAIV3_DISABLE_NPU", null);
        Environment.SetEnvironmentVariable("DAIV3_DISABLE_GPU", null);

        var provider = new HardwareDetectionProvider(NullLogger<HardwareDetectionProvider>.Instance);
        var tiers = provider.GetAvailableTiers();

        Assert.Contains(HardwareAccelerationTier.Cpu, tiers);
    }
}
