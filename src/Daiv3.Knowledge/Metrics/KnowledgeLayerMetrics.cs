namespace Daiv3.Knowledge.Metrics;

/// <summary>
/// Metrics for Knowledge Layer performance monitoring.
/// Establishes baseline performance thresholds for extensibility verification.
/// Used to verify that Knowledge Graph integration doesn't degrade performance.
/// </summary>
public class KnowledgeLayerMetrics
{
    // Search Performance Metrics
    /// <summary>Latency for Tier 1 (topic) search in milliseconds.</summary>
    public double Tier1SearchLatencyMs { get; set; }

    /// <summary>Latency for Tier 2 (chunk) search in milliseconds.</summary>
    public double Tier2SearchLatencyMs { get; set; }

    /// <summary>Total end-to-end search latency in milliseconds.</summary>
    public double SearchTotalLatencyMs { get; set; }

    // Indexing Performance Metrics
    /// <summary>Latency for complete document indexing in milliseconds.</summary>
    public double DocumentIndexLatencyMs { get; set; }

    /// <summary>Latency for embedding generation in milliseconds.</summary>
    public double EmbeddingGenerationMs { get; set; }

    /// <summary>Latency for chunk generation in milliseconds.</summary>
    public double ChunkGenerationMs { get; set; }

    // Memory Usage Metrics
    /// <summary>Memory used by Tier 1 in-memory cache in bytes.</summary>
    public long Tier1MemoryBytesUsed { get; set; }

    /// <summary>Calculated bytes per vector (useful for capacity planning).</summary>
    public long Tier1MemoryBytesPerVector { get; set; }

    /// <summary>Number of Tier 1 vectors currently loaded in memory.</summary>
    public int Tier1VectorsLoaded { get; set; }

    // Index Integrity Metrics
    /// <summary>Total number of documents indexed.</summary>
    public int TotalDocumentsIndexed { get; set; }

    /// <summary>Total number of chunks indexed.</summary>
    public int TotalChunksIndexed { get; set; }

    /// <summary>Embedding dimension for Tier 1 (baseline: 384).</summary>
    public int EmbeddingDimensionsTier1 { get; set; }

    /// <summary>Embedding dimension for Tier 2 (baseline: 768).</summary>
    public int EmbeddingDimensionsTier2 { get; set; }

    // Guardrail Status
    /// <summary>Whether Tier 1 search exceeded the configured threshold.</summary>
    public bool Tier1SearchExceededThreshold { get; set; }

    /// <summary>Whether Tier 2 search exceeded the configured threshold.</summary>
    public bool Tier2SearchExceededThreshold { get; set; }

    /// <summary>Whether memory usage exceeded the configured threshold.</summary>
    public bool MemoryUsageExceededThreshold { get; set; }

    // Timestamp for when metrics were captured
    /// <summary>UTC timestamp when metrics were captured.</summary>
    public DateTimeOffset CapturedAt { get; set; } = DateTimeOffset.UtcNow;
}
