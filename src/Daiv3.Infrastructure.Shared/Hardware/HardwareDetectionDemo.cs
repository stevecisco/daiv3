using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Daiv3.Infrastructure.Shared.Hardware;

/// <summary>
/// Demonstration utility for hardware detection capabilities.
/// Use this to verify what hardware tiers are detected on your system.
/// </summary>
public static class HardwareDetectionDemo
{
    /// <summary>
    /// Runs hardware detection and prints detailed results to the console.
    /// Useful for validating hardware detection on different Copilot+ PCs.
    /// </summary>
    public static void Run()
    {
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine("  DAIv3 Hardware Detection - Copilot+ PC Support");
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine();

        var provider = new HardwareDetectionProvider(NullLogger<HardwareDetectionProvider>.Instance);

        // Get detected tiers
        var availableTiers = provider.GetAvailableTiers();
        var bestTier = provider.GetBestAvailableTier();
        var diagnostics = provider.GetDiagnosticInfo();

        // Display system information
        Console.WriteLine($"Operating System: {Environment.OSVersion}");
        Console.WriteLine($"OS Version: {Environment.OSVersion.Version}");
        Console.WriteLine($"Platform: {(Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit")}");
        Console.WriteLine($"Processor Architecture: {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}");
        Console.WriteLine();

        // Display detected hardware tiers
        Console.WriteLine("Detected Hardware Acceleration Tiers:");
        Console.WriteLine("─────────────────────────────────────────────────────────────");
        
        if (availableTiers.Count == 0)
        {
            Console.WriteLine("  ⚠️  No hardware acceleration detected (fallback to CPU)");
        }
        else
        {
            for (int i = 0; i < availableTiers.Count; i++)
            {
                var tier = availableTiers[i];
                var isBest = tier == bestTier;
                var prefix = isBest ? "  ✅" : "  ▫️";
                var suffix = isBest ? " (BEST - will be used for inference)" : "";
                
                Console.WriteLine($"{prefix} {tier}{suffix}");
                
                // Provide context for each tier
                switch (tier)
                {
                    case HardwareAccelerationTier.Npu:
                        Console.WriteLine($"      → Neural Processing Unit (Snapdragon X Elite/Plus, Intel Core Ultra)");
                        Console.WriteLine($"      → Optimized for AI workloads, lowest power consumption");
                        break;
                    case HardwareAccelerationTier.Gpu:
                        Console.WriteLine($"      → Graphics Processing Unit via DirectML");
                        Console.WriteLine($"      → High performance for parallel computations");
                        break;
                    case HardwareAccelerationTier.Cpu:
                        Console.WriteLine($"      → Central Processing Unit with SIMD acceleration");
                        Console.WriteLine($"      → Universal fallback, works on all platforms");
                        break;
                }
                Console.WriteLine();
            }
        }

        Console.WriteLine("Diagnostics:");
        Console.WriteLine("─────────────────────────────────────────────────────────────");
        Console.WriteLine($"  {diagnostics}");
        Console.WriteLine();

        // Hardware-specific checks
#if NET10_0_WINDOWS10_0_26100_OR_GREATER
        Console.WriteLine("Windows 11 Copilot+ Checks:");
        Console.WriteLine("─────────────────────────────────────────────────────────────");
        
        var directMLPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            "DirectML.dll");
        
        bool hasDirectML = File.Exists(directMLPath);
        Console.WriteLine($"  DirectML Available: {(hasDirectML ? "✅ Yes" : "❌ No")}");
        if (hasDirectML)
        {
            Console.WriteLine($"  DirectML Path: {directMLPath}");
        }
        
        var osVersion = Environment.OSVersion.Version;
        bool isCopilotPlusSupported = osVersion.Major >= 10 && osVersion.Build >= 26100;
        Console.WriteLine($"  Windows 11 24H2+ (Build 26100+): {(isCopilotPlusSupported ? "✅ Yes" : "❌ No")}");
        Console.WriteLine($"  Actual Build: {osVersion.Build}");
        Console.WriteLine();
#endif

        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine($"Best Available Tier: {bestTier}");
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
    }
}
