namespace Daiv3.Infrastructure.Shared.Hardware;

/// <summary>
/// Detects available hardware acceleration capabilities on the current platform.
/// 
/// Implements the Windows 11 Copilot+ first design with graceful degradation:
/// - Detects NPU availability (Snapdragon X, Intel Core Ultra, etc.)
/// - Detects GPU availability (NVIDIA DirectML, AMD DirectML via ONNX Runtime)
/// - CPU with SIMD as universal fallback
/// </summary>
public interface IHardwareDetectionProvider
{
    /// <summary>
    /// Gets the best hardware acceleration tier available on the current platform.
    /// </summary>
    /// <returns>
    /// The best available hardware tier, in order of preference:
    /// NPU (if available on Windows 11 Copilot+) > GPU (if available via DirectML) > CPU (always available)
    /// </returns>
    HardwareAccelerationTier GetBestAvailableTier();

    /// <summary>
    /// Checks if a specific hardware acceleration tier is available.
    /// </summary>
    /// <param name="tier">The tier to check.</param>
    /// <returns>True if the specified tier is available on the current platform; otherwise false.</returns>
    bool IsTierAvailable(HardwareAccelerationTier tier);

    /// <summary>
    /// Gets all available hardware acceleration tiers on the current platform, 
    /// in order from best to worst.
    /// </summary>
    /// <returns>
    /// A list of available tiers ordered by performance preference.
    /// Always includes at least CPU as a fallback.
    /// </returns>
    IReadOnlyList<HardwareAccelerationTier> GetAvailableTiers();

    /// <summary>
    /// Gets diagnostic information about available hardware and current selection.
    /// Useful for troubleshooting and performance analysis.
    /// </summary>
    /// <returns>A diagnostic summary of hardware capabilities.</returns>
    string GetDiagnosticInfo();
}
