using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Daiv3.Knowledge.Metrics;

/// <summary>
/// Implementation of telemetry service for Knowledge Layer metrics.
/// Records search and indexing operations, tracks against guardrails.
/// </summary>
public class KnowledgeLayerTelemetry : IKnowledgeLayerTelemetry
{
    private readonly ILogger<KnowledgeLayerTelemetry> _logger;
    private readonly KnowledgeLayerGuardrails _guardrails;

    private readonly List<SearchMetricsContext> _searchMetrics = new();
    private readonly List<IndexingMetricsContext> _indexingMetrics = new();
    private readonly object _lockObject = new();

    public KnowledgeLayerTelemetry(
        ILogger<KnowledgeLayerTelemetry> logger,
        IOptions<KnowledgeLayerGuardrails> guardrailsOptions)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _guardrails = guardrailsOptions?.Value ?? new KnowledgeLayerGuardrails();
    }

    public void RecordSearchMetrics(SearchMetricsContext context)
    {
        if (context == null)
        {
            _logger.LogWarning("Attempted to record null search metrics context");
            return;
        }

        lock (_lockObject)
        {
            if (_guardrails.RecordDetailedMetrics)
            {
                _searchMetrics.Add(context);

                // Trim old entries if we exceed max retained samples
                if (_searchMetrics.Count > _guardrails.MaxMetricSamplesRetained)
                {
                    var itemsToRemove = _searchMetrics.Count - _guardrails.MaxMetricSamplesRetained;
                    _searchMetrics.RemoveRange(0, itemsToRemove);
                }
            }

            // Check guardrails
            if (_guardrails.EnablePerOperationMetrics)
            {
                CheckSearchGuardrails(context);
            }
        }
    }

    public void RecordIndexingMetrics(IndexingMetricsContext context)
    {
        if (context == null)
        {
            _logger.LogWarning("Attempted to record null indexing metrics context");
            return;
        }

        lock (_lockObject)
        {
            if (_guardrails.RecordDetailedMetrics)
            {
                _indexingMetrics.Add(context);

                // Trim old entries if we exceed max retained samples
                if (_indexingMetrics.Count > _guardrails.MaxMetricSamplesRetained)
                {
                    var itemsToRemove = _indexingMetrics.Count - _guardrails.MaxMetricSamplesRetained;
                    _indexingMetrics.RemoveRange(0, itemsToRemove);
                }
            }

            // Check guardrails
            if (_guardrails.EnablePerOperationMetrics)
            {
                CheckIndexingGuardrails(context);
            }
        }
    }

    public KnowledgeLayerMetrics GetCurrentMetrics()
    {
        lock (_lockObject)
        {
            var metrics = new KnowledgeLayerMetrics();

            if (_searchMetrics.Any())
            {
                var tier1WithValues = _searchMetrics
                    .Where(s => s.Tier1SearchLatencyMs.HasValue)
                    .ToList();
                if (tier1WithValues.Any())
                {
                    metrics.Tier1SearchLatencyMs = tier1WithValues.Average(s => s.Tier1SearchLatencyMs!.Value);
                }

                var tier2WithValues = _searchMetrics
                    .Where(s => s.Tier2SearchLatencyMs.HasValue)
                    .ToList();
                if (tier2WithValues.Any())
                {
                    metrics.Tier2SearchLatencyMs = tier2WithValues.Average(s => s.Tier2SearchLatencyMs!.Value);
                }

                metrics.SearchTotalLatencyMs = _searchMetrics.Average(s => s.TotalLatencyMs);
            }

            if (_indexingMetrics.Any())
            {
                metrics.DocumentIndexLatencyMs = _indexingMetrics.Average(i => i.TotalLatencyMs);

                var embeddingWithValues = _indexingMetrics
                    .Where(i => i.EmbeddingGenerationMs.HasValue)
                    .ToList();
                if (embeddingWithValues.Any())
                {
                    metrics.EmbeddingGenerationMs = embeddingWithValues.Average(i => i.EmbeddingGenerationMs!.Value);
                }

                var chunkWithValues = _indexingMetrics
                    .Where(i => i.ChunkGenerationMs.HasValue)
                    .ToList();
                if (chunkWithValues.Any())
                {
                    metrics.ChunkGenerationMs = chunkWithValues.Average(i => i.ChunkGenerationMs!.Value);
                }

                metrics.TotalDocumentsIndexed = _indexingMetrics
                    .Select(i => i.DocumentId)
                    .Distinct()
                    .Count();

                metrics.TotalChunksIndexed = _indexingMetrics.Sum(i => i.ChunkCount);
            }

            // Set baseline embedding dimensions (these are fixed per model)
            metrics.EmbeddingDimensionsTier1 = 384;  // default for nomic-embed-text or similar
            metrics.EmbeddingDimensionsTier2 = 768;  // default for all-MiniLM-L6-v2 or similar

            return metrics;
        }
    }

    public KnowledgeLayerMetricsValidationResult ValidateAgainstGuardrails()
    {
        lock (_lockObject)
        {
            var result = new KnowledgeLayerMetricsValidationResult();
            var metrics = GetCurrentMetrics();

            // Check Tier 1 search latency
            if (metrics.Tier1SearchLatencyMs > 0 &&
                metrics.Tier1SearchLatencyMs > _guardrails.MaxTier1SearchLatencyMs)
            {
                result.ViolationDetails.Add(
                    $"Tier 1 search latency {metrics.Tier1SearchLatencyMs:F2}ms exceeds " +
                    $"threshold {_guardrails.MaxTier1SearchLatencyMs}ms");
                result.HasViolations = true;
            }
            else
            {
                result.PassingMetrics.Add($"Tier 1 search latency: {metrics.Tier1SearchLatencyMs:F2}ms");
            }

            // Check total search latency
            if (metrics.SearchTotalLatencyMs > 0 &&
                metrics.SearchTotalLatencyMs > _guardrails.MaxSearchTotalLatencyMs)
            {
                result.ViolationDetails.Add(
                    $"Total search latency {metrics.SearchTotalLatencyMs:F2}ms exceeds " +
                    $"threshold {_guardrails.MaxSearchTotalLatencyMs}ms");
                result.HasViolations = true;
            }
            else
            {
                result.PassingMetrics.Add($"Total search latency: {metrics.SearchTotalLatencyMs:F2}ms");
            }

            // Check indexing latency
            if (metrics.DocumentIndexLatencyMs > 0 &&
                metrics.DocumentIndexLatencyMs > _guardrails.MaxIndexLatencyMs)
            {
                result.ViolationDetails.Add(
                    $"Document indexing latency {metrics.DocumentIndexLatencyMs:F2}ms exceeds " +
                    $"threshold {_guardrails.MaxIndexLatencyMs}ms");
                result.HasViolations = true;
            }
            else
            {
                result.PassingMetrics.Add($"Document indexing latency: {metrics.DocumentIndexLatencyMs:F2}ms");
            }

            // Check memory usage
            if (metrics.Tier1MemoryBytesUsed > _guardrails.MaxTier1MemoryBytes)
            {
                result.ViolationDetails.Add(
                    $"Tier 1 memory usage {metrics.Tier1MemoryBytesUsed / (1024 * 1024)}MB exceeds " +
                    $"threshold {_guardrails.MaxTier1MemoryBytes / (1024 * 1024)}MB");
                result.HasViolations = true;
            }
            else
            {
                result.PassingMetrics.Add($"Tier 1 memory usage: {metrics.Tier1MemoryBytesUsed / 1024}KB");
            }

            // Set health status
            if (result.HasViolations)
            {
                result.HealthStatus = HealthStatus.Critical;
                _logger.LogError("Knowledge Layer guardrails violated: {Violations}",
                    string.Join("; ", result.ViolationDetails));
            }
            else if (result.PassingMetrics.Any())
            {
                result.HealthStatus = HealthStatus.Ok;
            }

            return result;
        }
    }

    public string ExportMetricsAsJson()
    {
        lock (_lockObject)
        {
            var exportData = new
            {
                Timestamp = DateTimeOffset.UtcNow,
                CurrentMetrics = GetCurrentMetrics(),
                Summary = GetSummary(),
                Validation = ValidateAgainstGuardrails(),
                RecentSearchOperations = _searchMetrics.TakeLast(100).ToList(),
                RecentIndexingOperations = _indexingMetrics.TakeLast(100).ToList()
            };

            return JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    public SearchMetricsContext? GetSearchMetricsById(string operationId)
    {
        if (string.IsNullOrEmpty(operationId))
        {
            return null;
        }

        lock (_lockObject)
        {
            return _searchMetrics.FirstOrDefault(s => s.OperationId == operationId);
        }
    }

    public IndexingMetricsContext? GetLatestIndexingMetricsForDocument(string documentId)
    {
        if (string.IsNullOrEmpty(documentId))
        {
            return null;
        }

        lock (_lockObject)
        {
            return _indexingMetrics
                .Where(i => i.DocumentId == documentId)
                .OrderByDescending(i => i.StartedAt)
                .FirstOrDefault();
        }
    }

    public void ClearMetrics()
    {
        lock (_lockObject)
        {
            _searchMetrics.Clear();
            _indexingMetrics.Clear();
            _logger.LogInformation("Knowledge Layer metrics cleared");
        }
    }

    public TelemetrySummary GetSummary()
    {
        lock (_lockObject)
        {
            var summary = new TelemetrySummary
            {
                SearchOperationCount = _searchMetrics.Count,
                IndexingOperationCount = _indexingMetrics.Count,
                AverageSearchLatencyMs = _searchMetrics.Any()
                    ? _searchMetrics.Average(s => s.TotalLatencyMs)
                    : 0,
                AverageIndexingLatencyMs = _indexingMetrics.Any()
                    ? _indexingMetrics.Average(i => i.TotalLatencyMs)
                    : 0,
                TotalDocumentsIndexed = _indexingMetrics
                    .Select(i => i.DocumentId)
                    .Distinct()
                    .Count(),
                TotalChunksCreated = _indexingMetrics.Sum(i => i.ChunkCount),
                RecentViolationCount = ValidateAgainstGuardrails().HasViolations ? 1 : 0
            };

            return summary;
        }
    }

    private void CheckSearchGuardrails(SearchMetricsContext context)
    {
        var violations = new List<string>();

        if (context.Tier1SearchLatencyMs.HasValue &&
            context.Tier1SearchLatencyMs > _guardrails.MaxTier1SearchLatencyMs)
        {
            violations.Add($"Tier 1 search: {context.Tier1SearchLatencyMs:F2}ms");
        }

        if (context.TotalLatencyMs > _guardrails.MaxSearchTotalLatencyMs)
        {
            violations.Add($"Total search: {context.TotalLatencyMs:F2}ms");
        }

        if (violations.Any())
        {
            var message = $"Search guardrails exceeded: {string.Join(", ", violations)}";
            if (_guardrails.EnforceGuardrails)
            {
                _logger.LogError(message);
                // Could throw here if strict enforcement desired
            }
            else
            {
                _logger.LogWarning(message);
            }
        }
    }

    private void CheckIndexingGuardrails(IndexingMetricsContext context)
    {
        if (context.TotalLatencyMs > _guardrails.MaxIndexLatencyMs)
        {
            var message = $"Indexing guardrail exceeded for {context.DocumentId}: " +
                         $"{context.TotalLatencyMs:F2}ms > {_guardrails.MaxIndexLatencyMs}ms";
            if (_guardrails.EnforceGuardrails)
            {
                _logger.LogError(message);
                // Could throw here if strict enforcement desired
            }
            else
            {
                _logger.LogWarning(message);
            }
        }
    }
}
