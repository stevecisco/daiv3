using Daiv3.FoundryLocal.Management;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Daiv3.UnitTests.FoundryLocal.Management;

public class HardwareOverrideManagementTests
{
    [Fact]
    public void GetHardwareOverrides_ReturnsCurrentSettings()
    {
        var service = new FoundryLocalManagementService(
            NullLogger<FoundryLocalManagementService>.Instance);

        var settings = service.GetHardwareOverrides();

        Assert.NotNull(settings);
    }

    [Fact]
    public void SetHardwareOverrides_UpdatesEnvironment()
    {
        var original = new
        {
            ForceCpu = Environment.GetEnvironmentVariable("DAIV3_FORCE_CPU_ONLY"),
            DisableNpu = Environment.GetEnvironmentVariable("DAIV3_DISABLE_NPU"),
            DisableGpu = Environment.GetEnvironmentVariable("DAIV3_DISABLE_GPU")
        };

        try
        {
            var service = new FoundryLocalManagementService(
                NullLogger<FoundryLocalManagementService>.Instance);

            var settings = new HardwareOverrideSettings
            {
                ForceCpuOnly = false,
                DisableNpu = true,
                DisableGpu = false
            };

            service.SetHardwareOverrides(settings);

            var retrieved = service.GetHardwareOverrides();
            Assert.False(retrieved.ForceCpuOnly);
            Assert.True(retrieved.DisableNpu);
            Assert.False(retrieved.DisableGpu);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DAIV3_FORCE_CPU_ONLY", original.ForceCpu);
            Environment.SetEnvironmentVariable("DAIV3_DISABLE_NPU", original.DisableNpu);
            Environment.SetEnvironmentVariable("DAIV3_DISABLE_GPU", original.DisableGpu);
        }
    }

    [Fact]
    public void ClearHardwareOverrides_RemovesAllSettings()
    {
        var original = new
        {
            ForceCpu = Environment.GetEnvironmentVariable("DAIV3_FORCE_CPU_ONLY"),
            DisableNpu = Environment.GetEnvironmentVariable("DAIV3_DISABLE_NPU"),
            DisableGpu = Environment.GetEnvironmentVariable("DAIV3_DISABLE_GPU")
        };

        try
        {
            var service = new FoundryLocalManagementService(
                NullLogger<FoundryLocalManagementService>.Instance);

            service.SetHardwareOverrides(new HardwareOverrideSettings
            {
                ForceCpuOnly = true,
                DisableNpu = true,
                DisableGpu = true
            });

            service.ClearHardwareOverrides();

            var retrieved = service.GetHardwareOverrides();
            Assert.False(retrieved.ForceCpuOnly);
            Assert.False(retrieved.DisableNpu);
            Assert.False(retrieved.DisableGpu);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DAIV3_FORCE_CPU_ONLY", original.ForceCpu);
            Environment.SetEnvironmentVariable("DAIV3_DISABLE_NPU", original.DisableNpu);
            Environment.SetEnvironmentVariable("DAIV3_DISABLE_GPU", original.DisableGpu);
        }
    }
}
