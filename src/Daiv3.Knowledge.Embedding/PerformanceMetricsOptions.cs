namespace Daiv3.Knowledge.Embedding;

/// <summary>
/// Configuration options for performance metrics collection.
/// Allows tuning of metrics collection behavior and thresholds.
/// </summary>
public sealed class PerformanceMetricsOptions
{
    /// <summary>
    /// Enable or disable metrics collection.
    /// Default: false (metrics collection disabled for production performance)
    /// </summary>
    public bool EnableMetricsCollection { get; set; } = false;

    /// <summary>
    /// Threshold (milliseconds) above which operations are logged as warnings.
    /// Default: 100ms (operations longer than 100ms are anomalies)
    /// </summary>
    public double SlowOperationThresholdMs { get; set; } = 100.0;

    /// <summary>
    /// Sample rate for metrics logging (0.0 to 1.0).
    /// 0.0 = no logging, 0.5 = log 50% of operations, 1.0 = log all.
    /// Default: 0.1 (log 10% of slow operations)
    /// </summary>
    public double SlowOperationSampleRate { get; set; } = 0.1;

    /// <summary>
    /// Enable detailed telemetry events for downstream analysis systems.
    /// Default: false
    /// </summary>
    public bool EnableDetailedTelemetry { get; set; } = false;

    /// <summary>
    /// Validate options and ensure sensible configuration.
    /// </summary>
    public void Validate()
    {
        if (SlowOperationThresholdMs < 0)
        {
            throw new ArgumentException(
                $"SlowOperationThresholdMs must be non-negative. Got {SlowOperationThresholdMs}",
                nameof(SlowOperationThresholdMs));
        }

        if (SlowOperationSampleRate < 0.0 || SlowOperationSampleRate > 1.0)
        {
            throw new ArgumentException(
                $"SlowOperationSampleRate must be between 0.0 and 1.0. Got {SlowOperationSampleRate}",
                nameof(SlowOperationSampleRate));
        }
    }
}
