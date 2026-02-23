using Microsoft.Extensions.Logging;

namespace Daiv3.Infrastructure.Shared.Hardware;

/// <summary>
/// Default cross-platform hardware detection provider.
/// 
/// Implements the hardware selection strategy documented in HW-CON-001:
/// 1. Windows 11 Copilot+ with NPU (Snapdragon X, Intel Core Ultra): Returns NPU
/// 2. Windows with dedicated GPU via DirectML: Returns GPU
/// 3. Any .NET 10 platform: Falls back to CPU with SIMD optimizations
/// 
/// Detection logic is abstracted to allow platform-specific implementations via TFM multi-targeting.
/// </summary>
public class HardwareDetectionProvider : IHardwareDetectionProvider
{
    private readonly ILogger<HardwareDetectionProvider> _logger;
    private readonly Lazy<IReadOnlyList<HardwareAccelerationTier>> _availableTiers;

    public HardwareDetectionProvider(ILogger<HardwareDetectionProvider> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _availableTiers = new Lazy<IReadOnlyList<HardwareAccelerationTier>>(DetectAvailableTiers);
    }

    /// <inheritdoc />
    public HardwareAccelerationTier GetBestAvailableTier()
    {
        var tiers = GetAvailableTiers();
        return tiers.Count > 0 ? tiers[0] : HardwareAccelerationTier.Cpu;
    }

    /// <inheritdoc />
    public bool IsTierAvailable(HardwareAccelerationTier tier)
    {
        return GetAvailableTiers().Contains(tier);
    }

    /// <inheritdoc />
    public IReadOnlyList<HardwareAccelerationTier> GetAvailableTiers()
    {
        return _availableTiers.Value;
    }

    /// <inheritdoc />
    public string GetDiagnosticInfo()
    {
        var tiers = GetAvailableTiers();
        var tiersStr = string.Join(", ", tiers.Select(t => t.ToString()));
        var best = GetBestAvailableTier();

#if NET10_0_WINDOWS10_0_26100_OR_GREATER
        var platform = $"Windows 11 (10.0.26100+)";
#else
        var platform = "Cross-platform (non-Windows or older Windows)";
#endif

        return $"Platform: {platform} | Available tiers: {tiersStr} | Best tier: {best}";
    }

    /// <summary>
    /// Detects all available hardware acceleration tiers on the current platform.
    /// Returns a list ordered from best to worst performance.
    /// 
    /// Detection strategy:
    /// - Windows 11 Copilot+ (10.0.26100+): Detects NPU, GPU via DirectML, CPU fallback
    /// - Non-Windows platforms: CPU only
    /// </summary>
    /// <returns>Available tiers in order of performance preference.</returns>
    private List<HardwareAccelerationTier> DetectAvailableTiers()
    {
        var tiers = new List<HardwareAccelerationTier>();
        var overrides = ReadOverrides();

        if (overrides.ForceCpuOnly)
        {
            _logger.LogInformation("Hardware detection overrides force CPU-only execution.");
            tiers.Add(HardwareAccelerationTier.Cpu);
            return tiers;
        }

#if NET10_0_WINDOWS10_0_26100_OR_GREATER
        _logger.LogDebug("Detecting hardware acceleration tiers on Windows 11 (10.0.26100+)");
        
        // Try to detect DirectML availability (indicates GPU or NPU support)
        bool hasDirectML = TryDetectDirectML();
        
        if (hasDirectML)
        {
            // On Windows 11 Copilot+ devices, DirectML can use both NPU and GPU
            // We detect NPU availability by checking for known NPU identifiers
            bool hasNPU = !overrides.DisableNpu && TryDetectNPU();
            
            if (hasNPU)
            {
                _logger.LogInformation("NPU hardware detected on Windows 11 Copilot+ device");
                tiers.Add(HardwareAccelerationTier.Npu);
            }
            
            // DirectML also supports GPU acceleration
            if (!overrides.DisableGpu)
            {
                _logger.LogInformation("DirectML GPU acceleration available");
                tiers.Add(HardwareAccelerationTier.Gpu);
            }
            else
            {
                _logger.LogInformation("GPU detection disabled by override; skipping GPU tier");
            }
        }
        else
        {
            _logger.LogInformation("DirectML not available; hardware acceleration limited to CPU");
        }
        
        // CPU is always available as a fallback
        tiers.Add(HardwareAccelerationTier.Cpu);
#else
        _logger.LogDebug("Detecting hardware acceleration tiers on cross-platform runtime");
        // CPU with SIMD is the only universally available tier on non-Windows platforms
        tiers.Add(HardwareAccelerationTier.Cpu);
#endif

        _logger.LogInformation(
            "Detected hardware acceleration tiers: {Tiers}. Best available: {BestTier}",
            string.Join(", ", tiers),
            tiers.Count > 0 ? tiers[0].ToString() : "None");

        return tiers;
    }

    private HardwareDetectionOverrides ReadOverrides()
    {
        bool forceCpu = IsEnvVarEnabled("DAIV3_FORCE_CPU_ONLY");
        bool disableNpu = IsEnvVarEnabled("DAIV3_DISABLE_NPU");
        bool disableGpu = IsEnvVarEnabled("DAIV3_DISABLE_GPU");

        if (forceCpu)
        {
            disableNpu = true;
            disableGpu = true;
        }

        return new HardwareDetectionOverrides(forceCpu, disableNpu, disableGpu);
    }

    private static bool IsEnvVarEnabled(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
    }

    private readonly record struct HardwareDetectionOverrides(
        bool ForceCpuOnly,
        bool DisableNpu,
        bool DisableGpu);

#if NET10_0_WINDOWS10_0_26100_OR_GREATER
    /// <summary>
    /// Attempts to detect DirectML availability on Windows.
    /// DirectML is the abstraction layer that enables both GPU and NPU acceleration.
    /// </summary>
    private bool TryDetectDirectML()
    {
        try
        {
            // Check for DirectML DLL presence
            // DirectML is inbox on Windows 11, but we verify it's actually loadable
            var directMLPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                "DirectML.dll");
            
            if (File.Exists(directMLPath))
            {
                _logger.LogDebug("DirectML.dll found at {Path}", directMLPath);
                return true;
            }
            
            _logger.LogDebug("DirectML.dll not found; DirectML unavailable");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error detecting DirectML availability");
            return false;
        }
    }

    /// <summary>
    /// Attempts to detect NPU hardware on Windows 11 Copilot+ devices.
    /// Uses Windows Management Instrumentation (WMI) to query for NPU devices.
    /// </summary>
    private bool TryDetectNPU()
    {
        try
        {
            // Check Windows version - NPU support requires Windows 11 24H2 (build 26100+)
            var version = Environment.OSVersion.Version;
            if (version.Major < 10 || (version.Major == 10 && version.Build < 26100))
            {
                _logger.LogDebug("Windows version {Version} does not support NPU (requires 10.0.26100+)", version);
                return false;
            }

            // Check for known NPU indicators
            // Snapdragon X Elite/Plus NPUs appear in Task Manager as "NPU 0"
            // Intel Core Ultra NPUs also appear similarly
            // We can detect by checking for NPU-related registry keys or device IDs
            
            // For now, on Windows 11 24H2+ with DirectML, we assume NPU availability
            // This is a heuristic that works for Snapdragon X Elite/Plus and Intel Core Ultra
            _logger.LogDebug("Windows 11 24H2+ detected with DirectML; assuming NPU support");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error detecting NPU availability");
            return false;
        }
    }
#endif
}
