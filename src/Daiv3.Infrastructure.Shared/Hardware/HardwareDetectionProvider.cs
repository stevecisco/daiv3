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
    /// Default (cross-platform) behavior:
    /// - Windows 11 Copilot+ (10.0.26100+): CPU as fallback (NPU/GPU detection happens at library level via TFM)
    /// - Non-Windows platforms: CPU only
    /// - Generic Windows: CPU only (GPU detection handled separately by embedding library)
    /// </summary>
    /// <returns>Available tiers in order of performance preference.</returns>
    private List<HardwareAccelerationTier> DetectAvailableTiers()
    {
        var tiers = new List<HardwareAccelerationTier>();

#if NET10_0_WINDOWS10_0_26100_OR_GREATER
        _logger.LogDebug("Detecting hardware acceleration tiers on Windows 11 (10.0.26100+)");
        
        // Note: Actual NPU/GPU detection happens in specialized libraries (e.g., Daiv3.Knowledge.Embedding)
        // via ONNX Runtime's GetAvailableProviders() and TFM multi-targeting.
        // This provider reports what is detectable at the infrastructure level.
        // CPU is always available as a fallback.
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
}
