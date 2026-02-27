namespace Daiv3.ModelExecution;

/// <summary>
/// Configuration options for model lifecycle management.
/// </summary>
public class ModelLifecycleOptions
{
    /// <summary>
    /// Maximum time to wait for a model to load (in milliseconds).
    /// Default: 60,000 ms (1 minute).
    /// </summary>
    /// <remarks>
    /// Model loading can take significant time due to:
    /// - Downloading from cache (if not already present)
    /// - Initializing execution providers (DirectML, GPU setup)
    /// - Loading model weights into memory
    /// </remarks>
    public int LoadTimeoutMs { get; set; } = 60_000;

    /// <summary>
    /// Maximum time to wait for a model switch (unload + load) in milliseconds.
    /// Default: 90,000 ms (1.5 minutes).
    /// </summary>
    /// <remarks>
    /// Model switching takes longer than loading because it includes unload + load operations.
    /// </remarks>
    public int SwitchTimeoutMs { get; set; } = 90_000;

    /// <summary>
    /// Whether to log detailed timing metrics for model operations.
    /// Default: true.
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = true;

    /// <summary>
    /// Whether to automatically unload model after a period of inactivity.
    /// Default: false (models stay loaded).
    /// </summary>
    /// <remarks>
    /// When enabled with IdleUnloadDelayMs, unused models will be automatically unloaded
    /// to free memory for other system operations.
    /// </remarks>
    public bool EnableIdleUnloading { get; set; } = false;

    /// <summary>
    /// Time in milliseconds after which an idle model is unloaded.
    /// Default: 300,000 ms (5 minutes).
    /// </summary>
    /// <remarks>
    /// Only used if EnableIdleUnloading is true.
    /// </remarks>
    public int IdleUnloadDelayMs { get; set; } = 300_000;
}
