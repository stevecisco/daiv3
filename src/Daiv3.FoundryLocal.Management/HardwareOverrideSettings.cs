namespace Daiv3.FoundryLocal.Management;

/// <summary>
/// Represents hardware detection configuration and overrides.
/// </summary>
public sealed class HardwareOverrideSettings
{
    public bool ForceCpuOnly { get; set; }
    public bool DisableNpu { get; set; }
    public bool DisableGpu { get; set; }

    public override string ToString()
    {
        if (ForceCpuOnly)
        {
            return "Force CPU-only execution";
        }

        var parts = new List<string>();
        if (DisableNpu) parts.Add("NPU disabled");
        if (DisableGpu) parts.Add("GPU disabled");

        return parts.Count > 0 ? string.Join(", ", parts) : "No overrides (auto-detect hardware)";
    }
}
