using Daiv3.Infrastructure.Shared.Hardware;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Daiv3.UnitTests.Infrastructure.Shared.Hardware;

/// <summary>
/// Unit tests for hardware detection provider.
/// Verifies that hardware capabilities are correctly detected and reported.
/// </summary>
public class HardwareDetectionProviderTests
{
    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        ILogger<HardwareDetectionProvider>? nullLogger = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new HardwareDetectionProvider(nullLogger!));
    }

    [Fact]
    public void GetAvailableTiers_AlwaysIncludeCpu()
    {
        // Arrange
        var provider = new HardwareDetectionProvider(NullLogger<HardwareDetectionProvider>.Instance);

        // Act
        var tiers = provider.GetAvailableTiers();

        // Assert
        Assert.NotNull(tiers);
        Assert.NotEmpty(tiers);
        Assert.Contains(HardwareAccelerationTier.Cpu, tiers);
    }

    [Fact]
    public void GetAvailableTiers_OnCopilotPlusPC_DetectsMultipleTiers()
    {
        // Arrange
        var provider = new HardwareDetectionProvider(NullLogger<HardwareDetectionProvider>.Instance);

        // Act
        var tiers = provider.GetAvailableTiers();

        // Assert
        Assert.NotNull(tiers);
        Assert.NotEmpty(tiers);
        
        // The actual behavior depends on the runtime platform and DirectML availability
        // On Windows 11 Copilot+ with DirectML installed, we should detect NPU and/or GPU in addition to CPU
        // On non-Windows platforms or without DirectML, only CPU will be available
        
        // Always verify CPU is present (it's the universal fallback)
        Assert.Contains(HardwareAccelerationTier.Cpu, tiers);
        
        // If we detect multiple tiers, verify they include NPU or GPU (hardware acceleration)
        if (tiers.Count > 1)
        {
            Assert.True(
                tiers.Contains(HardwareAccelerationTier.Npu) || tiers.Contains(HardwareAccelerationTier.Gpu),
                $"Expected NPU or GPU to be detected along with CPU, but only found: {string.Join(", ", tiers)}");
        }
    }

    [Fact]
    public void GetAvailableTiers_OnSnapdragonXElite_DetectsNPU()
    {
        // Arrange
        var provider = new HardwareDetectionProvider(NullLogger<HardwareDetectionProvider>.Instance);

        // Act
        var tiers = provider.GetAvailableTiers();
        var diagnostics = provider.GetDiagnosticInfo();

        // Debug: Check what conditional compilation symbols are active
        var compilationSymbols = new List<string>();
#if NET10_0_WINDOWS10_0_26100_OR_GREATER
        compilationSymbols.Add("NET10_0_WINDOWS10_0_26100_OR_GREATER");
#else
        compilationSymbols.Add("NOT Windows 11");
#endif

        Console.WriteLine($"Active Compilation Symbols: {string.Join(", ", compilationSymbols)}");
        Console.WriteLine($"Diagnostics: {diagnostics}");
        Console.WriteLine($"Detected tiers: {string.Join(", ", tiers)}");

        // Assert - Document actual hardware detected
        Assert.NotNull(tiers);
        
#if NET10_0_WINDOWS10_0_26100_OR_GREATER
        // On Snapdragon X Elite/Plus with Windows 11 24H2+, NPU should be detected
        var osVersion = Environment.OSVersion.Version;
        if (osVersion.Build >= 26100 && File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "DirectML.dll")))
        {
            Assert.True(tiers.Count >= 2, $"Expected multiple acceleration tiers on Copilot+ PC, but got: {string.Join(", ", tiers)}. Diagnostics: {diagnostics}");
            Assert.Contains(HardwareAccelerationTier.Npu, tiers);
            Assert.Contains(HardwareAccelerationTier.Gpu, tiers);
            Assert.Contains(HardwareAccelerationTier.Cpu, tiers);
            
            // Verify ordering: NPU should be first (best), then GPU, then CPU
            Assert.Equal(HardwareAccelerationTier.Npu, tiers[0]);
        }
#endif
    }

    [Fact]
    public void GetAvailableTiers_ReturnsSortedByPerference()
    {
        // Arrange
        var provider = new HardwareDetectionProvider(NullLogger<HardwareDetectionProvider>.Instance);

        // Act
        var tiers = provider.GetAvailableTiers();

        // Assert
        Assert.NotNull(tiers);
        // Verify tiers are in order (each tier's index should be >= previous)
        // since we iterate from NPU (best) to CPU (worst)
        for (int i = 1; i < tiers.Count; i++)
        {
            Assert.True(
                (int)tiers[i - 1] <= (int)tiers[i],
                $"Tier order violated: {tiers[i - 1]} should come before {tiers[i]}");
        }
    }

    [Fact]
    public void IsTierAvailable_Cpu_ReturnsTrue()
    {
        // Arrange
        var provider = new HardwareDetectionProvider(NullLogger<HardwareDetectionProvider>.Instance);

        // Act
        var isAvailable = provider.IsTierAvailable(HardwareAccelerationTier.Cpu);

        // Assert
        Assert.True(isAvailable, "CPU should always be available");
    }

    [Fact]
    public void IsTierAvailable_None_ReturnsFalse()
    {
        // Arrange
        var provider = new HardwareDetectionProvider(NullLogger<HardwareDetectionProvider>.Instance);

        // Act
        var isAvailable = provider.IsTierAvailable(HardwareAccelerationTier.None);

        // Assert
        Assert.False(isAvailable, "None tier should never be available");
    }

    [Fact]
    public void GetBestAvailableTier_ReturnsCpu_WhenCpuIsOnly()
    {
        // Arrange
        var provider = new HardwareDetectionProvider(NullLogger<HardwareDetectionProvider>.Instance);

        // Act
        var best = provider.GetBestAvailableTier();

        // Assert
        // In default implementation, CPU should be best available (or only available)
        Assert.True(
            best == HardwareAccelerationTier.Cpu ||
            (provider.GetAvailableTiers().Count > 0 && best == provider.GetAvailableTiers()[0]),
            "Best tier should be the first available tier");
    }

    [Fact]
    public void GetDiagnosticInfo_ReturnsNonEmptyString()
    {
        // Arrange
        var provider = new HardwareDetectionProvider(NullLogger<HardwareDetectionProvider>.Instance);

        // Act
        var diagnostic = provider.GetDiagnosticInfo();

        // Assert
        Assert.NotNull(diagnostic);
        Assert.NotEmpty(diagnostic);
        Assert.Contains("Platform", diagnostic);
        Assert.Contains("tiers", diagnostic);
        Assert.Contains("Best", diagnostic);
    }

    [Fact]
    public void GetDiagnosticInfo_IncludesBestTier()
    {
        // Arrange
        var provider = new HardwareDetectionProvider(NullLogger<HardwareDetectionProvider>.Instance);
        var best = provider.GetBestAvailableTier();

        // Act
        var diagnostic = provider.GetDiagnosticInfo();

        // Assert
        Assert.Contains(best.ToString(), diagnostic);
    }

    [Fact]
    public void Service_CanBeRegisteredViaExtension()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddDebug());

        // Act
        services.AddHardwareDetection();
        var provider = services.BuildServiceProvider();

        // Assert
        var hardwareDetection = provider.GetService<IHardwareDetectionProvider>();
        Assert.NotNull(hardwareDetection);
        Assert.IsType<HardwareDetectionProvider>(hardwareDetection);
    }

    [Fact]
    public void Service_RegisteredAsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddDebug());
        services.AddHardwareDetection();
        var provider = services.BuildServiceProvider();

        // Act
        var instance1 = provider.GetService<IHardwareDetectionProvider>();
        var instance2 = provider.GetService<IHardwareDetectionProvider>();

        // Assert
        Assert.NotNull(instance1);
        Assert.NotNull(instance2);
        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void GetAvailableTiers_MultipleCallsReturnConsistentResults()
    {
        // Arrange
        var provider = new HardwareDetectionProvider(NullLogger<HardwareDetectionProvider>.Instance);

        // Act
        var tiers1 = provider.GetAvailableTiers();
        var tiers2 = provider.GetAvailableTiers();

        // Assert
        Assert.NotNull(tiers1);
        Assert.NotNull(tiers2);
        Assert.Equal(tiers1.Count, tiers2.Count);
        for (int i = 0; i < tiers1.Count; i++)
        {
            Assert.Equal(tiers1[i], tiers2[i]);
        }
    }

    [Theory]
    [InlineData(HardwareAccelerationTier.None)]
    [InlineData(HardwareAccelerationTier.Npu)]
    [InlineData(HardwareAccelerationTier.Gpu)]
    [InlineData(HardwareAccelerationTier.Cpu)]
    public void IsTierAvailable_ConsistentWithGetAvailableTiers(HardwareAccelerationTier tier)
    {
        // Arrange
        var provider = new HardwareDetectionProvider(NullLogger<HardwareDetectionProvider>.Instance);
        var tiers = provider.GetAvailableTiers();

        // Act
        var isAvailable = provider.IsTierAvailable(tier);

        // Assert
        if (tiers.Contains(tier))
        {
            Assert.True(isAvailable, $"{tier} reported as available in GetAvailableTiers but not by IsTierAvailable");
        }
        else
        {
            Assert.False(isAvailable, $"{tier} reported as unavailable in GetAvailableTiers but available by IsTierAvailable");
        }
    }

    [Fact]
    public void HardwareDetectionDemo_Runs()
    {
        // This test runs the hardware detection demo to show actual detected hardware
        // Useful for manual verification of hardware detection on different systems
        
        // Redirect console output to capture the demo output
        var originalOut = Console.Out;
        using var writer = new System.IO.StringWriter();
        Console.SetOut(writer);

        try
        {
            // Run the demo
            HardwareDetectionDemo.Run();
            
            // Get the output
            var output = writer.ToString();
            
            // Restore console
            Console.SetOut(originalOut);
            
            // Write output to test results for visibility
            Console.WriteLine("Hardware Detection Demo Output:");
            Console.WriteLine(output);
            
            // Basic validation that demo ran
            Assert.Contains("Hardware Detection", output);
            Assert.Contains("Hardware Acceleration Tiers", output);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }
}
