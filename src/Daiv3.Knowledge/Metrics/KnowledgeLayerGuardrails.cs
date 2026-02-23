namespace Daiv3.Knowledge.Metrics;

/// <summary>
/// Configuration for Knowledge Layer performance guardrails.
/// These thresholds ensure that Knowledge Graph integration doesn't degrade performance.
/// </summary>
public class KnowledgeLayerGuardrails
{
    /// <summary>
    /// Maximum acceptable latency for Tier 1 search (milliseconds).
    /// Baseline target: <10ms for ~10,000 vectors at 384 dimensions on CPU fallback.
    /// </summary>
    public double MaxTier1SearchLatencyMs { get; set; } = 50;

    /// <summary>
    /// Maximum acceptable latency for complete two-tier search (milliseconds).
    /// Includes Tier 1 + Tier 2 search time.
    /// </summary>
    public double MaxSearchTotalLatencyMs { get; set; } = 200;

    /// <summary>
    /// Maximum acceptable latency for single document indexing (milliseconds).
    /// Includes embedding generation, chunking, and storage.
    /// </summary>
    public double MaxIndexLatencyMs { get; set; } = 10000;

    /// <summary>
    /// Maximum memory allowed for Tier 1 in-memory cache (bytes).
    /// Default: 100 MB. Adjust based on available system memory.
    /// </summary>
    public long MaxTier1MemoryBytes { get; set; } = 100 * 1024 * 1024;

    /// <summary>
    /// When true, violations of guardrails cause exceptions.
    /// When false, violations are logged as warnings only.
    /// Default: false (permissive mode for baseline measurement).
    /// </summary>
    public bool EnforceGuardrails { get; set; } = false;

    /// <summary>
    /// When true, detailed metrics are recorded for all operations.
    /// When false, only aggregate metrics are tracked.
    /// Default: true (needed for KG integration verification).
    /// </summary>
    public bool RecordDetailedMetrics { get; set; } = true;

    /// <summary>
    /// Enable collection of per-operation metrics (search, index, etc).
    /// Useful for performance analysis but adds overhead.
    /// Default: true (needed for integration verification).
    /// </summary>
    public bool EnablePerOperationMetrics { get; set; } = true;

    /// <summary>
    /// Maximum number of metric samples to retain in memory.
    /// Oldest samples are discarded when limit is reached.
    /// Default: 10,000 samples (~1-10 MB depending on detail level).
    /// </summary>
    public int MaxMetricSamplesRetained { get; set; } = 10000;
}
