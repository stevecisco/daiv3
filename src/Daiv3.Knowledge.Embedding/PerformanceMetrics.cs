namespace Daiv3.Knowledge.Embedding;

/// <summary>
/// Contains performance metrics for vector similarity operations.
/// Used for monitoring, diagnostics, and performance trend analysis.
/// </summary>
public sealed class PerformanceMetrics
{
    /// <summary>Elapsed time in milliseconds for the operation.</summary>
    public double ElapsedMs { get; set; }

    /// <summary>Number of vectors processed in the operation.</summary>
    public int VectorCount { get; set; }

    /// <summary>Dimension of vectors processed.</summary>
    public int Dimension { get; set; }

    /// <summary>Time per vector comparison in microseconds.</summary>
    public double TimePerVectorMicroSeconds => ElapsedMs > 0
        ? (ElapsedMs * 1000) / VectorCount
        : 0;

    /// <summary>Vectors processed per second (throughput).</summary>
    public double VectorsPerSecond => ElapsedMs > 0
        ? (VectorCount / ElapsedMs) * 1000
        : 0;

    /// <summary>
    /// Determines if this operation exceeded expected performance threshold.
    /// Returns null if metrics are inconclusive.
    /// </summary>
    public bool IsSlowOperation(double thresholdMs)
    {
        return ElapsedMs > thresholdMs;
    }

    /// <summary>String representation for logging.</summary>
    public override string ToString()
    {
        return $"PerformanceMetrics(vectors={VectorCount}, dims={Dimension}, " +
               $"elapsed={ElapsedMs:F2}ms, throughput={VectorsPerSecond:F0} vec/sec, " +
               $"time_per_vec={TimePerVectorMicroSeconds:F2}µs)";
    }
}
