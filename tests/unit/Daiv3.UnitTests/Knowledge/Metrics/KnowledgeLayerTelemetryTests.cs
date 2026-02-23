using Daiv3.Knowledge.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Daiv3.Knowledge.Tests.Metrics;

/// <summary>
/// Unit tests for Knowledge Layer telemetry metrics recording and validation.
/// Verifies baseline metrics capture and guardrail enforcement for future KG integration.
/// </summary>
public class KnowledgeLayerTelemetryTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IKnowledgeLayerTelemetry _telemetry;

    public KnowledgeLayerTelemetryTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.Configure<KnowledgeLayerGuardrails>(opts =>
        {
            opts.RecordDetailedMetrics = true;
            opts.EnablePerOperationMetrics = true;
            opts.MaxTier1SearchLatencyMs = 50;
            opts.MaxSearchTotalLatencyMs = 200;
            opts.EnforceGuardrails = false; // Permissive for testing
        });
        services.AddScoped<IKnowledgeLayerTelemetry, KnowledgeLayerTelemetry>();

        _serviceProvider = services.BuildServiceProvider();
        _telemetry = _serviceProvider.GetRequiredService<IKnowledgeLayerTelemetry>();
    }

    [Fact]
    public void RecordSearchMetrics_WithValidContext_StoresMetric()
    {
        // Arrange
        var context = new SearchMetricsContext
        {
            Tier1SearchLatencyMs = 5.0,
            Tier2SearchLatencyMs = 20.0,
            TotalLatencyMs = 25.0,
            Tier1ResultCount = 10,
            Tier2ResultCount = 50
        };

        // Act
        _telemetry.RecordSearchMetrics(context);

        // Assert
        var metrics = _telemetry.GetCurrentMetrics();
        Assert.Equal(5.0, metrics.Tier1SearchLatencyMs);
        Assert.Equal(20.0, metrics.Tier2SearchLatencyMs);
        Assert.Equal(25.0, metrics.SearchTotalLatencyMs);
    }

    [Fact]
    public void RecordSearchMetrics_WithNullContext_HandlesGracefully()
    {
        // Act & Assert (should not throw)
        _telemetry.RecordSearchMetrics(null!);
        var metrics = _telemetry.GetCurrentMetrics();
        Assert.Equal(0, metrics.Tier1SearchLatencyMs);
    }

    [Fact]
    public void RecordIndexingMetrics_WithValidContext_StoresMetric()
    {
        // Arrange
        var context = new IndexingMetricsContext
        {
            DocumentId = "doc-1",
            TotalLatencyMs = 1500.0,
            EmbeddingGenerationMs = 800.0,
            ChunkGenerationMs = 300.0,
            StorageLatencyMs = 400.0,
            ChunkCount = 5,
            DocumentSizeBytes = 50000
        };

        // Act
        _telemetry.RecordIndexingMetrics(context);

        // Assert
        var metrics = _telemetry.GetCurrentMetrics();
        Assert.Equal(1500.0, metrics.DocumentIndexLatencyMs);
        Assert.Equal(800.0, metrics.EmbeddingGenerationMs);
        Assert.Equal(300.0, metrics.ChunkGenerationMs);
        Assert.Equal(1, metrics.TotalDocumentsIndexed);
        Assert.Equal(5, metrics.TotalChunksIndexed);
    }

    [Fact]
    public void GetCurrentMetrics_ReturnsAggregateMetrics()
    {
        // Arrange
        var context1 = new SearchMetricsContext { TotalLatencyMs = 20.0, Tier1SearchLatencyMs = 5.0 };
        var context2 = new SearchMetricsContext { TotalLatencyMs = 40.0, Tier1SearchLatencyMs = 10.0 };

        _telemetry.RecordSearchMetrics(context1);
        _telemetry.RecordSearchMetrics(context2);

        // Act
        var metrics = _telemetry.GetCurrentMetrics();

        // Assert
        Assert.Equal(30.0, metrics.SearchTotalLatencyMs); // Average of 20 and 40
        Assert.Equal(7.5, metrics.Tier1SearchLatencyMs);  // Average of 5 and 10
    }

    [Fact]
    public void ValidateAgainstGuardrails_WithinThresholds_ReturnsOk()
    {
        // Arrange
        var context = new SearchMetricsContext
        {
            TotalLatencyMs = 100.0,
            Tier1SearchLatencyMs = 20.0
        };
        _telemetry.RecordSearchMetrics(context);

        // Act
        var validation = _telemetry.ValidateAgainstGuardrails();

        // Assert
        Assert.False(validation.HasViolations);
        Assert.Equal(HealthStatus.Ok, validation.HealthStatus);
    }

    [Fact]
    public void ValidateAgainstGuardrails_ExceedsThreshold_DetectsViolation()
    {
        // Arrange
        var context = new SearchMetricsContext
        {
            TotalLatencyMs = 300.0, // Exceeds 200ms threshold
            Tier1SearchLatencyMs = 20.0
        };
        _telemetry.RecordSearchMetrics(context);

        // Act
        var validation = _telemetry.ValidateAgainstGuardrails();

        // Assert
        Assert.True(validation.HasViolations);
        Assert.Equal(HealthStatus.Critical, validation.HealthStatus);
        Assert.NotEmpty(validation.ViolationDetails);
    }

    [Fact]
    public void ExportMetricsAsJson_ReturnsValidJson()
    {
        // Arrange
        var context = new SearchMetricsContext
        {
            TotalLatencyMs = 50.0,
            Tier1SearchLatencyMs = 15.0
        };
        _telemetry.RecordSearchMetrics(context);

        // Act
        var json = _telemetry.ExportMetricsAsJson();

        // Assert
        Assert.NotEmpty(json);
        Assert.Contains("CurrentMetrics", json);
        Assert.Contains("SearchTotalLatencyMs", json);
    }

    [Fact]
    public void GetSearchMetricsById_WithValidId_ReturnsContext()
    {
        // Arrange
        var context = new SearchMetricsContext { TotalLatencyMs = 50.0 };
        _telemetry.RecordSearchMetrics(context);
        var operationId = context.OperationId;

        // Act
        var retrieved = _telemetry.GetSearchMetricsById(operationId);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(50.0, retrieved.TotalLatencyMs);
    }

    [Fact]
    public void GetLatestIndexingMetricsForDocument_ReturnsNewestMetrics()
    {
        // Arrange
        var context1 = new IndexingMetricsContext
        {
            DocumentId = "doc-1",
            TotalLatencyMs = 1000.0,
            StartedAt = DateTimeOffset.UtcNow.AddSeconds(-10)
        };
        var context2 = new IndexingMetricsContext
        {
            DocumentId = "doc-1",
            TotalLatencyMs = 1500.0,
            StartedAt = DateTimeOffset.UtcNow
        };

        _telemetry.RecordIndexingMetrics(context1);
        _telemetry.RecordIndexingMetrics(context2);

        // Act
        var latest = _telemetry.GetLatestIndexingMetricsForDocument("doc-1");

        // Assert
        Assert.NotNull(latest);
        Assert.Equal(1500.0, latest.TotalLatencyMs);
    }

    [Fact]
    public void ClearMetrics_RemovesAllRecordedMetrics()
    {
        // Arrange
        var context = new SearchMetricsContext { TotalLatencyMs = 50.0 };
        _telemetry.RecordSearchMetrics(context);

        // Act
        _telemetry.ClearMetrics();
        var metrics = _telemetry.GetCurrentMetrics();

        // Assert
        Assert.Equal(0, metrics.Tier1SearchLatencyMs);
        Assert.Equal(0, metrics.SearchTotalLatencyMs);
    }

    [Fact]
    public void GetSummary_ReturnsAggregateStatistics()
    {
        // Arrange
        var searchContext = new SearchMetricsContext { TotalLatencyMs = 50.0 };
        var indexContext = new IndexingMetricsContext
        {
            DocumentId = "doc-1",
            TotalLatencyMs = 1500.0,
            ChunkCount = 5
        };

        _telemetry.RecordSearchMetrics(searchContext);
        _telemetry.RecordIndexingMetrics(indexContext);

        // Act
        var summary = _telemetry.GetSummary();

        // Assert
        Assert.Equal(1, summary.SearchOperationCount);
        Assert.Equal(1, summary.IndexingOperationCount);
        Assert.Equal(50.0, summary.AverageSearchLatencyMs);
        Assert.Equal(1500.0, summary.AverageIndexingLatencyMs);
        Assert.Equal(1, summary.TotalDocumentsIndexed);
        Assert.Equal(5, summary.TotalChunksCreated);
    }

    [Fact]
    public void RecordSearchMetrics_TrimsSamplesBeyondMaxRetained()
    {
        // Arrange - create service with small sample limit
        var services = new ServiceCollection();
        services.AddLogging();
        services.Configure<KnowledgeLayerGuardrails>(opts =>
        {
            opts.MaxMetricSamplesRetained = 5;
            opts.RecordDetailedMetrics = true;
        });
        services.AddScoped<IKnowledgeLayerTelemetry, KnowledgeLayerTelemetry>();
        var provider = services.BuildServiceProvider();
        var telemetry = provider.GetRequiredService<IKnowledgeLayerTelemetry>();

        // Act - record more than max samples
        for (int i = 0; i < 10; i++)
        {
            telemetry.RecordSearchMetrics(
                new SearchMetricsContext { TotalLatencyMs = i * 10.0 });
        }

        var summary = telemetry.GetSummary();

        // Assert - should only have 5 latest samples
        Assert.Equal(5, summary.SearchOperationCount);
    }

    [Fact]
    public void RecordMetrics_WithDetailedMetricsDisabled_DoesNotStore()
    {
        // Arrange - disable detailed metrics
        var services = new ServiceCollection();
        services.AddLogging();
        services.Configure<KnowledgeLayerGuardrails>(opts =>
        {
            opts.RecordDetailedMetrics = false;
        });
        services.AddScoped<IKnowledgeLayerTelemetry, KnowledgeLayerTelemetry>();
        var provider = services.BuildServiceProvider();
        var telemetry = provider.GetRequiredService<IKnowledgeLayerTelemetry>();

        // Act
        telemetry.RecordSearchMetrics(
            new SearchMetricsContext { TotalLatencyMs = 50.0 });

        // Assert - should have no stored samples but still return 0 values
        var metrics = telemetry.GetCurrentMetrics();
        Assert.Equal(0, metrics.SearchTotalLatencyMs);
    }

    [Fact]
    public void GetCurrentMetrics_ContainsCorrectBaselineEmbeddingDimensions()
    {
        // Act
        var metrics = _telemetry.GetCurrentMetrics();

        // Assert - baseline dimensions should always be fixed
        Assert.Equal(384, metrics.EmbeddingDimensionsTier1);
        Assert.Equal(768, metrics.EmbeddingDimensionsTier2);
    }
}
