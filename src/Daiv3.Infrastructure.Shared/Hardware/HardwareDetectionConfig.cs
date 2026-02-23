namespace Daiv3.Infrastructure.Shared.Hardware;

/// <summary>
/// Hardware detection configuration for testing and diagnostics.
/// </summary>
public sealed class HardwareDetectionConfig
{
    /// <summary>
    /// Gets or sets whether to force CPU-only execution (disables NPU and GPU detection).
    /// </summary>
    public bool ForceCpuOnly { get; set; }

    /// <summary>
    /// Gets or sets whether to disable NPU detection.
    /// </summary>
    public bool DisableNpu { get; set; }

    /// <summary>
    /// Gets or sets whether to disable GPU detection.
    /// </summary>
    public bool DisableGpu { get; set; }

    /// <summary>
    /// Reads current configuration from environment variables.
    /// </summary>
    public static HardwareDetectionConfig ReadFromEnvironment()
    {
        return new HardwareDetectionConfig
        {
            ForceCpuOnly = IsEnvVarEnabled("DAIV3_FORCE_CPU_ONLY"),
            DisableNpu = IsEnvVarEnabled("DAIV3_DISABLE_NPU"),
            DisableGpu = IsEnvVarEnabled("DAIV3_DISABLE_GPU")
        };
    }

    /// <summary>
    /// Applies this configuration to environment variables.
    /// </summary>
    public void WriteToEnvironment()
    {
        SetEnvVar("DAIV3_FORCE_CPU_ONLY", ForceCpuOnly);
        SetEnvVar("DAIV3_DISABLE_NPU", DisableNpu);
        SetEnvVar("DAIV3_DISABLE_GPU", DisableGpu);
    }

    /// <summary>
    /// Clears all hardware detection override environment variables.
    /// </summary>
    public static void ClearEnvironment()
    {
        Environment.SetEnvironmentVariable("DAIV3_FORCE_CPU_ONLY", null);
        Environment.SetEnvironmentVariable("DAIV3_DISABLE_NPU", null);
        Environment.SetEnvironmentVariable("DAIV3_DISABLE_GPU", null);
    }

    private static bool IsEnvVarEnabled(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
    }

    private static void SetEnvVar(string name, bool enabled)
    {
        Environment.SetEnvironmentVariable(name, enabled ? "true" : null);
    }
}
