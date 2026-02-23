namespace Daiv3.Knowledge.Metrics;

/// <summary>
/// Context for recording search operation metrics.
/// </summary>
public class SearchMetricsContext
{
    /// <summary>Unique identifier for this search operation.</summary>
    public string OperationId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Latency for Tier 1 (topic) search in milliseconds.</summary>
    public double? Tier1SearchLatencyMs { get; set; }

    /// <summary>Latency for Tier 2 (chunk) search in milliseconds.</summary>
    public double? Tier2SearchLatencyMs { get; set; }

    /// <summary>Total end-to-end search latency in milliseconds.</summary>
    public double TotalLatencyMs { get; set; }

    /// <summary>Number of documents returned from Tier 1.</summary>
    public int Tier1ResultCount { get; set; }

    /// <summary>Number of chunks returned from Tier 2.</summary>
    public int Tier2ResultCount { get; set; }

    /// <summary>Query embedding dimension (should match Tier 1 model).</summary>
    public int QueryEmbeddingDimensions { get; set; }

    /// <summary>Timestamp when search started.</summary>
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Optional error message if search failed.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Whether search succeeded.</summary>
    public bool IsSuccess => ErrorMessage == null;
}

/// <summary>
/// Context for recording document indexing metrics.
/// </summary>
public class IndexingMetricsContext
{
    /// <summary>Unique identifier for this indexing operation.</summary>
    public string OperationId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Document ID being indexed.</summary>
    public string DocumentId { get; set; } = string.Empty;

    /// <summary>Latency for complete document indexing in milliseconds.</summary>
    public double TotalLatencyMs { get; set; }

    /// <summary>Latency for embedding generation in milliseconds.</summary>
    public double? EmbeddingGenerationMs { get; set; }

    /// <summary>Latency for chunk generation in milliseconds.</summary>
    public double? ChunkGenerationMs { get; set; }

    /// <summary>Latency for storing to database in milliseconds.</summary>
    public double? StorageLatencyMs { get; set; }

    /// <summary>Number of chunks generated from document.</summary>
    public int ChunkCount { get; set; }

    /// <summary>Size of document in bytes.</summary>
    public long DocumentSizeBytes { get; set; }

    /// <summary>File hash used for change detection.</summary>
    public string FileHash { get; set; } = string.Empty;

    /// <summary>Whether document was already indexed (change detection).</summary>
    public bool WasAlreadyIndexed { get; set; }

    /// <summary>Timestamp when indexing started.</summary>
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Optional error message if indexing failed.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Whether indexing succeeded.</summary>
    public bool IsSuccess => ErrorMessage == null;
}

/// <summary>
/// Validation result for Knowledge Layer metrics against guardrails.
/// </summary>
public class KnowledgeLayerMetricsValidationResult
{
    /// <summary>Whether any guardrail violations were detected.</summary>
    public bool HasViolations { get; set; }

    /// <summary>Details of each violation.</summary>
    public List<string> ViolationDetails { get; set; } = new();

    /// <summary>Details of metrics that are within acceptable ranges.</summary>
    public List<string> PassingMetrics { get; set; } = new();

    /// <summary>Overall health status (OK, WARNING, CRITICAL).</summary>
    public HealthStatus HealthStatus { get; set; } = HealthStatus.Ok;

    /// <summary>Timestamp of validation.</summary>
    public DateTimeOffset ValidatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Health status categories for Knowledge Layer.
/// </summary>
public enum HealthStatus
{
    /// <summary>All metrics within acceptable bounds.</summary>
    Ok,

    /// <summary>Some metrics approaching thresholds but still acceptable.</summary>
    Warning,

    /// <summary>One or more metrics have exceeded thresholds.</summary>
    Critical
}
