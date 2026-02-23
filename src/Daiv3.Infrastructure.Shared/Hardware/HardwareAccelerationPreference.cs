namespace Daiv3.Infrastructure.Shared.Hardware;

/// <summary>
/// User preference for hardware acceleration tier selection.
/// Allows users to override automatic detection and force a specific tier.
/// </summary>
public enum HardwareAccelerationPreference
{
    /// <summary>
    /// Automatically select the best available hardware tier.
    /// Recommended for most users; provides optimal performance with fallback support.
    /// </summary>
    Auto = 0,

    /// <summary>
    /// Force NPU execution. Fails if NPU is not available.
    /// Only valid on Windows 11 Copilot+ devices; should not be used in production UI.
    /// </summary>
    ForceNpu = 1,

    /// <summary>
    /// Force GPU execution via DirectML. Falls back to CPU if unavailable.
    /// Useful for testing GPU behavior on systems with both GPU and NPU.
    /// </summary>
    ForceGpu = 2,

    /// <summary>
    /// Force CPU-only execution with SIMD optimizations.
    /// Used for testing fallback behavior and performance analysis.
    /// </summary>
    ForceCpu = 3,
}
