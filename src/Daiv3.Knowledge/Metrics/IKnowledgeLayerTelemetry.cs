namespace Daiv3.Knowledge.Metrics;

/// <summary>
/// Telemetry service for recording and validating Knowledge Layer metrics against guardrails.
/// Provides baseline data for Knowledge Graph integration verification.
/// </summary>
public interface IKnowledgeLayerTelemetry
{
    /// <summary>
    /// Records metrics from a search operation.
    /// </summary>
    /// <param name="context">Search metrics context with timing and result information.</param>
    void RecordSearchMetrics(SearchMetricsContext context);

    /// <summary>
    /// Records metrics from a document indexing operation.
    /// </summary>
    /// <param name="context">Indexing metrics context with timing and document information.</param>
    void RecordIndexingMetrics(IndexingMetricsContext context);

    /// <summary>
    /// Gets the current aggregate metrics snapshot.
    /// </summary>
    /// <returns>Current metrics across all operations recorded so far.</returns>
    KnowledgeLayerMetrics GetCurrentMetrics();

    /// <summary>
    /// Validates current metrics against configured guardrails.
    /// </summary>
    /// <returns>Validation result with violations and health status.</returns>
    KnowledgeLayerMetricsValidationResult ValidateAgainstGuardrails();

    /// <summary>
    /// Exports all recorded metrics in JSON format for external analysis.
    /// </summary>
    /// <returns>JSON string containing detailed metrics.</returns>
    string ExportMetricsAsJson();

    /// <summary>
    /// Gets detailed metrics for a specific operation by ID.
    /// </summary>
    /// <param name="operationId">The operation ID (from SearchMetricsContext or IndexingMetricsContext).</param>
    /// <returns>Detailed metrics or null if operation not found.</returns>
    SearchMetricsContext? GetSearchMetricsById(string operationId);

    /// <summary>
    /// Gets detailed metrics for a specific indexing operation by document ID.
    /// </summary>
    /// <param name="documentId">The document ID.</param>
    /// <returns>Most recent indexing metrics for document or null if not found.</returns>
    IndexingMetricsContext? GetLatestIndexingMetricsForDocument(string documentId);

    /// <summary>
    /// Clears all recorded metrics (useful for starting fresh baseline measurements).
    /// </summary>
    void ClearMetrics();

    /// <summary>
    /// Gets summary statistics about recorded operations.
    /// </summary>
    TelemetrySummary GetSummary();
}

/// <summary>
/// Summary statistics for Knowledge Layer telemetry.
/// </summary>
public class TelemetrySummary
{
    /// <summary>Total number of search operations recorded.</summary>
    public int SearchOperationCount { get; set; }

    /// <summary>Total number of indexing operations recorded.</summary>
    public int IndexingOperationCount { get; set; }

    /// <summary>Average search latency in milliseconds.</summary>
    public double AverageSearchLatencyMs { get; set; }

    /// <summary>Average indexing latency in milliseconds.</summary>
    public double AverageIndexingLatencyMs { get; set; }

    /// <summary>Total documents processed.</summary>
    public int TotalDocumentsIndexed { get; set; }

    /// <summary>Total chunks created.</summary>
    public int TotalChunksCreated { get; set; }

    /// <summary>Current memory usage for Tier 1 cache.</summary>
    public long CurrentTier1MemoryBytes { get; set; }

    /// <summary>Number of recent violations detected.</summary>
    public int RecentViolationCount { get; set; }

    /// <summary>Timestamp of summary generation.</summary>
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;
}
