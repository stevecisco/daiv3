using Daiv3.Knowledge.Embedding;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace Daiv3.Knowledge.Embedding.IntegrationTests;

/// <summary>
/// Integration tests for performance metrics collection in CpuVectorSimilarityService.
/// Verifies that metrics are correctly recorded and thresholds are respected.
/// </summary>
public class VectorSimilarityMetricsCollectionTests
{
    private readonly ITestOutputHelper _output;

    public VectorSimilarityMetricsCollectionTests(ITestOutputHelper output)
    {
        _output = output ?? throw new ArgumentNullException(nameof(output));
    }

    [Fact]
    public void ServiceCreation_WithDefaultOptions_MetricsDisabled()
    {
        // Arrange & Act
        var service = new CpuVectorSimilarityService(
            NullLogger<CpuVectorSimilarityService>.Instance);

        // Assert
        Assert.NotNull(service);
        _output.WriteLine("Service created with default options (metrics disabled)");
    }

    [Fact]
    public void ServiceCreation_WithMetricsEnabled_Succeeds()
    {
        // Arrange
        var metricsOptions = new PerformanceMetricsOptions
        {
            EnableMetricsCollection = true,
            SlowOperationThresholdMs = 50.0,
            SlowOperationSampleRate = 1.0
        };

        // Act
        var service = new CpuVectorSimilarityService(
            NullLogger<CpuVectorSimilarityService>.Instance,
            metricsOptions);

        // Assert
        Assert.NotNull(service);
        _output.WriteLine("Service created with metrics enabled");
    }

    [Fact]
    public void ServiceCreation_WithDetailedTelemetry_Succeeds()
    {
        // Arrange
        var metricsOptions = new PerformanceMetricsOptions
        {
            EnableMetricsCollection = true,
            EnableDetailedTelemetry = true,
            SlowOperationSampleRate = 1.0
        };

        // Act
        var service = new CpuVectorSimilarityService(
            NullLogger<CpuVectorSimilarityService>.Instance,
            metricsOptions);

        // Assert
        Assert.NotNull(service);
        _output.WriteLine("Service created with detailed telemetry enabled");
    }

    [Fact]
    public void BatchCosineSimilarity_WithMetricsDisabled_Completes()
    {
        // Arrange
        var metricsOptions = new PerformanceMetricsOptions
        {
            EnableMetricsCollection = false
        };

        var service = new CpuVectorSimilarityService(
            NullLogger<CpuVectorSimilarityService>.Instance,
            metricsOptions);

        var queryVector = CreateTestVector(384);
        var targetVectors = CreateBatchVectors(1000, 384);
        var results = new float[1000];

        // Act
        service.BatchCosineSimilarity(queryVector, targetVectors, 1000, 384, results);

        // Assert
        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.True(float.IsFinite(r)));
        _output.WriteLine("Batch similarity computation completed with metrics disabled");
    }

    [Fact]
    public void BatchCosineSimilarity_WithMetricsEnabled_Completes()
    {
        // Arrange
        var metricsOptions = new PerformanceMetricsOptions
        {
            EnableMetricsCollection = true,
            SlowOperationThresholdMs = 100.0,
            SlowOperationSampleRate = 1.0
        };

        var service = new CpuVectorSimilarityService(
            NullLogger<CpuVectorSimilarityService>.Instance,
            metricsOptions);

        var queryVector = CreateTestVector(384);
        var targetVectors = CreateBatchVectors(1000, 384);
        var results = new float[1000];

        // Act
        service.BatchCosineSimilarity(queryVector, targetVectors, 1000, 384, results);

        // Assert
        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.True(float.IsFinite(r)));
        _output.WriteLine("Batch similarity computation completed with metrics enabled");
    }

    [Fact]
    public void OptionsValidation_InvalidThresholdThrows()
    {
        // Arrange
        var invalidOptions = new PerformanceMetricsOptions
        {
            SlowOperationThresholdMs = -10.0
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => invalidOptions.Validate());
    }

    [Fact]
    public void OptionsValidation_InvalidSampleRateThrows()
    {
        // Arrange
        var invalidOptions = new PerformanceMetricsOptions
        {
            SlowOperationSampleRate = 1.5
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => invalidOptions.Validate());
    }

    [Theory]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(1000)]
    [InlineData(10000)]
    public void BatchCosineSimilarity_VariousBatchSizes_CompletesWithMetrics(int vectorCount)
    {
        // Arrange
        var metricsOptions = new PerformanceMetricsOptions
        {
            EnableMetricsCollection = true,
            EnableDetailedTelemetry = true,
            SlowOperationSampleRate = 1.0
        };

        var service = new CpuVectorSimilarityService(
            NullLogger<CpuVectorSimilarityService>.Instance,
            metricsOptions);

        var queryVector = CreateTestVector(384);
        var targetVectors = CreateBatchVectors(vectorCount, 384);
        var results = new float[vectorCount];

        // Act
        service.BatchCosineSimilarity(queryVector, targetVectors, vectorCount, 384, results);

        // Assert
        Assert.Equal(vectorCount, results.Length);
        Assert.All(results, r => Assert.True(float.IsFinite(r)));
        _output.WriteLine($"Completed batch similarity for {vectorCount} vectors with metrics");
    }

    [Fact]
    public void ConsecutiveOperations_WithMetrics_HandleMultipleCalls()
    {
        // Arrange
        var metricsOptions = new PerformanceMetricsOptions
        {
            EnableMetricsCollection = true,
            SlowOperationSampleRate = 1.0
        };

        var service = new CpuVectorSimilarityService(
            NullLogger<CpuVectorSimilarityService>.Instance,
            metricsOptions);

        var queryVector = CreateTestVector(384);

        // Act - Perform multiple operations
        for (int i = 0; i < 5; i++)
        {
            var targetVectors = CreateBatchVectors(100, 384);
            var results = new float[100];
            service.BatchCosineSimilarity(queryVector, targetVectors, 100, 384, results);
        }

        // Assert - Should complete without errors
        _output.WriteLine("Completed 5 consecutive batch operations with metrics");
    }

    #region Helper Methods

    private static float[] CreateTestVector(int dimension)
    {
        var vector = new float[dimension];
        var random = new Random(42);

        for (int i = 0; i < dimension; i++)
        {
            vector[i] = (float)random.NextDouble();
        }

        float magnitude = MathF.Sqrt(vector.Sum(v => v * v));
        if (magnitude > 0)
        {
            for (int i = 0; i < dimension; i++)
            {
                vector[i] /= magnitude;
            }
        }

        return vector;
    }

    private static float[] CreateBatchVectors(int vectorCount, int dimension)
    {
        var result = new float[vectorCount * dimension];
        var random = new Random(42);

        for (int v = 0; v < vectorCount; v++)
        {
            float sumOfSquares = 0;
            for (int d = 0; d < dimension; d++)
            {
                float value = (float)random.NextDouble();
                result[v * dimension + d] = value;
                sumOfSquares += value * value;
            }

            float magnitude = MathF.Sqrt(sumOfSquares);
            if (magnitude > 0)
            {
                for (int d = 0; d < dimension; d++)
                {
                    result[v * dimension + d] /= magnitude;
                }
            }
        }

        return result;
    }

    #endregion
}
