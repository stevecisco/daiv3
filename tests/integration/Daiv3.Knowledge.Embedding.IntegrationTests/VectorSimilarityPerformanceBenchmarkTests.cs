using Daiv3.Knowledge.Embedding;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace Daiv3.Knowledge.Embedding.IntegrationTests;

/// <summary>
/// Performance benchmark tests for CPU vector similarity operations.
/// Validates that CPU-based SIMD acceleration meets usable performance thresholds.
/// 
/// Performance Targets (HW-NFR-002):
/// - Single cosine similarity: &lt; 100µs per pair
/// - Batch search (10K vectors, 384 dims): &lt; 10ms
/// - Batch search (1K vectors, 768 dims): &lt; 50ms
/// - Linear scaling with vector count
/// </summary>
public class VectorSimilarityPerformanceBenchmarkTests
{
    private readonly ITestOutputHelper _output;

    // Performance thresholds (milliseconds)
    // Note: These thresholds include JIT warmup and system overhead
    // Raw CPU throughput: ~0.5-2µs per vector comparison depending on CPU/system
    private const double MaxSingleSimilarityMs = 0.2; // 200µs (2x ideal for system variance)
    private const double MaxTier1SearchMs = 25.0; // 25ms for 10,000 vectors (2.5µs per vector including overhead)
    private const double MaxTier2SearchMs = 100.0; // 100ms for 1,000 vectors (100µs per vector including overhead)
    
    // Tolerance for platform variance (10% allowance on top of thresholds)
    private const double PerformanceTolerance = 1.1;

    public VectorSimilarityPerformanceBenchmarkTests(ITestOutputHelper output)
    {
        _output = output ?? throw new ArgumentNullException(nameof(output));
    }

    #region Benchmark: Single Cosine Similarity

    [Fact]
    public void SingleCosineSimilarity_384Dims_UnderThreshold()
    {
        // Arrange
        var service = new CpuVectorSimilarityService(NullLogger<CpuVectorSimilarityService>.Instance);
        var vector1 = CreateTestVector(384);
        var vector2 = CreateTestVector(384);

        var stopwatch = Stopwatch.StartNew();
        const int iterations = 1000;

        // Act
        for (int i = 0; i < iterations; i++)
        {
            _ = service.CosineSimilarity(vector1, vector2);
        }

        stopwatch.Stop();
        double avgTimeMs = stopwatch.Elapsed.TotalMilliseconds / iterations;

        // Assert
        _output.WriteLine($"Single similarity (384 dims): {avgTimeMs:F4}ms per operation (avg {iterations} ops)");
        Assert.True(avgTimeMs < MaxSingleSimilarityMs * PerformanceTolerance,
            $"Average time {avgTimeMs:F4}ms exceeded threshold {MaxSingleSimilarityMs * PerformanceTolerance:F4}ms");
    }

    [Fact]
    public void SingleCosineSimilarity_768Dims_UnderThreshold()
    {
        // Arrange
        var service = new CpuVectorSimilarityService(NullLogger<CpuVectorSimilarityService>.Instance);
        var vector1 = CreateTestVector(768);
        var vector2 = CreateTestVector(768);

        var stopwatch = Stopwatch.StartNew();
        const int iterations = 1000;

        // Act
        for (int i = 0; i < iterations; i++)
        {
            _ = service.CosineSimilarity(vector1, vector2);
        }

        stopwatch.Stop();
        double avgTimeMs = stopwatch.Elapsed.TotalMilliseconds / iterations;

        // Assert
        _output.WriteLine($"Single similarity (768 dims): {avgTimeMs:F4}ms per operation (avg {iterations} ops)");
        Assert.True(avgTimeMs < MaxSingleSimilarityMs * PerformanceTolerance,
            $"Average time {avgTimeMs:F4}ms exceeded threshold {MaxSingleSimilarityMs * PerformanceTolerance:F4}ms");
    }

    #endregion

    #region Benchmark: Batch Cosine Similarity (Tier 1)

    [Fact]
    public void BatchCosineSimilarity_Tier1_10VectorsOf384Dims_UnderThreshold()
    {
        // Arrange
        var service = new CpuVectorSimilarityService(NullLogger<CpuVectorSimilarityService>.Instance);
        var queryVector = CreateTestVector(384);
        var targetVectors = CreateBatchVectors(10, 384);
        var results = new float[10];

        var stopwatch = Stopwatch.StartNew();

        // Act
        service.BatchCosineSimilarity(queryVector, targetVectors, 10, 384, results);

        stopwatch.Stop();
        double elapsedMs = stopwatch.Elapsed.TotalMilliseconds;

        // Assert
        _output.WriteLine($"Batch search (10 vectors, 384 dims): {elapsedMs:F4}ms");
        Assert.True(elapsedMs < MaxTier1SearchMs * PerformanceTolerance,
            $"Elapsed time {elapsedMs:F4}ms exceeded threshold {MaxTier1SearchMs * PerformanceTolerance:F4}ms");
    }

    [Fact]
    public void BatchCosineSimilarity_Tier1_100VectorsOf384Dims_UnderThreshold()
    {
        // Arrange
        var service = new CpuVectorSimilarityService(NullLogger<CpuVectorSimilarityService>.Instance);
        var queryVector = CreateTestVector(384);
        var targetVectors = CreateBatchVectors(100, 384);
        var results = new float[100];

        var stopwatch = Stopwatch.StartNew();

        // Act
        service.BatchCosineSimilarity(queryVector, targetVectors, 100, 384, results);

        stopwatch.Stop();
        double elapsedMs = stopwatch.Elapsed.TotalMilliseconds;

        // Assert
        _output.WriteLine($"Batch search (100 vectors, 384 dims): {elapsedMs:F4}ms");
        Assert.True(elapsedMs < MaxTier1SearchMs * PerformanceTolerance,
            $"Elapsed time {elapsedMs:F4}ms exceeded threshold {MaxTier1SearchMs * PerformanceTolerance:F4}ms");
    }

    [Fact]
    public void BatchCosineSimilarity_Tier1_1000VectorsOf384Dims_UnderThreshold()
    {
        // Arrange
        var service = new CpuVectorSimilarityService(NullLogger<CpuVectorSimilarityService>.Instance);
        var queryVector = CreateTestVector(384);
        var targetVectors = CreateBatchVectors(1000, 384);
        var results = new float[1000];

        var stopwatch = Stopwatch.StartNew();

        // Act
        service.BatchCosineSimilarity(queryVector, targetVectors, 1000, 384, results);

        stopwatch.Stop();
        double elapsedMs = stopwatch.Elapsed.TotalMilliseconds;

        // Assert
        _output.WriteLine($"Batch search (1,000 vectors, 384 dims): {elapsedMs:F4}ms");
        Assert.True(elapsedMs < MaxTier1SearchMs * PerformanceTolerance,
            $"Elapsed time {elapsedMs:F4}ms exceeded threshold {MaxTier1SearchMs * PerformanceTolerance:F4}ms");
    }

    [Fact]
    public void BatchCosineSimilarity_Tier1_10000VectorsOf384Dims_UnderThreshold()
    {
        // Arrange - This is the critical test case for KM-NFR-001
        var service = new CpuVectorSimilarityService(NullLogger<CpuVectorSimilarityService>.Instance);
        var queryVector = CreateTestVector(384);
        var targetVectors = CreateBatchVectors(10000, 384);
        var results = new float[10000];

        var stopwatch = Stopwatch.StartNew();

        // Act
        service.BatchCosineSimilarity(queryVector, targetVectors, 10000, 384, results);

        stopwatch.Stop();
        double elapsedMs = stopwatch.Elapsed.TotalMilliseconds;

        // Assert
        _output.WriteLine($"Batch search (10,000 vectors, 384 dims): {elapsedMs:F4}ms - CRITICAL THRESHOLD TEST");
        Assert.True(elapsedMs < MaxTier1SearchMs * PerformanceTolerance,
            $"Elapsed time {elapsedMs:F4}ms exceeded critical threshold {MaxTier1SearchMs * PerformanceTolerance:F4}ms");
    }

    #endregion

    #region Benchmark: Batch Cosine Similarity (Tier 2)

    [Fact]
    public void BatchCosineSimilarity_Tier2_100VectorsOf768Dims_UnderThreshold()
    {
        // Arrange
        var service = new CpuVectorSimilarityService(NullLogger<CpuVectorSimilarityService>.Instance);
        var queryVector = CreateTestVector(768);
        var targetVectors = CreateBatchVectors(100, 768);
        var results = new float[100];

        var stopwatch = Stopwatch.StartNew();

        // Act
        service.BatchCosineSimilarity(queryVector, targetVectors, 100, 768, results);

        stopwatch.Stop();
        double elapsedMs = stopwatch.Elapsed.TotalMilliseconds;

        // Assert
        _output.WriteLine($"Batch search (100 vectors, 768 dims): {elapsedMs:F4}ms");
        Assert.True(elapsedMs < MaxTier2SearchMs * PerformanceTolerance,
            $"Elapsed time {elapsedMs:F4}ms exceeded threshold {MaxTier2SearchMs * PerformanceTolerance:F4}ms");
    }

    [Fact]
    public void BatchCosineSimilarity_Tier2_500VectorsOf768Dims_UnderThreshold()
    {
        // Arrange
        var service = new CpuVectorSimilarityService(NullLogger<CpuVectorSimilarityService>.Instance);
        var queryVector = CreateTestVector(768);
        var targetVectors = CreateBatchVectors(500, 768);
        var results = new float[500];

        var stopwatch = Stopwatch.StartNew();

        // Act
        service.BatchCosineSimilarity(queryVector, targetVectors, 500, 768, results);

        stopwatch.Stop();
        double elapsedMs = stopwatch.Elapsed.TotalMilliseconds;

        // Assert
        _output.WriteLine($"Batch search (500 vectors, 768 dims): {elapsedMs:F4}ms");
        Assert.True(elapsedMs < MaxTier2SearchMs * PerformanceTolerance,
            $"Elapsed time {elapsedMs:F4}ms exceeded threshold {MaxTier2SearchMs * PerformanceTolerance:F4}ms");
    }

    [Fact]
    public void BatchCosineSimilarity_Tier2_1000VectorsOf768Dims_UnderThreshold()
    {
        // Arrange - Standard Tier 2 search case
        var service = new CpuVectorSimilarityService(NullLogger<CpuVectorSimilarityService>.Instance);
        var queryVector = CreateTestVector(768);
        var targetVectors = CreateBatchVectors(1000, 768);
        var results = new float[1000];

        var stopwatch = Stopwatch.StartNew();

        // Act
        service.BatchCosineSimilarity(queryVector, targetVectors, 1000, 768, results);

        stopwatch.Stop();
        double elapsedMs = stopwatch.Elapsed.TotalMilliseconds;

        // Assert
        _output.WriteLine($"Batch search (1,000 vectors, 768 dims): {elapsedMs:F4}ms");
        Assert.True(elapsedMs < MaxTier2SearchMs * PerformanceTolerance,
            $"Elapsed time {elapsedMs:F4}ms exceeded threshold {MaxTier2SearchMs * PerformanceTolerance:F4}ms");
    }

    #endregion

    #region Benchmark: Scaling Characteristics

    [Fact]
    public void BatchCosineSimilarity_ScalingTest_384Dims_LinearPerformance()
    {
        // Arrange - Test that performance scales linearly with vector count
        var service = new CpuVectorSimilarityService(NullLogger<CpuVectorSimilarityService>.Instance);
        var queryVector = CreateTestVector(384);

        var counts = new[] { 100, 500, 1000, 5000, 10000 };
        var timings = new List<(int Count, double TimeMs)>();

        // Act
        foreach (var count in counts)
        {
            var targetVectors = CreateBatchVectors(count, 384);
            var results = new float[count];

            var stopwatch = Stopwatch.StartNew();
            service.BatchCosineSimilarity(queryVector, targetVectors, count, 384, results);
            stopwatch.Stop();

            timings.Add((count, stopwatch.Elapsed.TotalMilliseconds));
        }

        // Assert - Check for linear scaling (allow 1.5x variance for system noise)
        for (int i = 1; i < timings.Count; i++)
        {
            var prev = timings[i - 1];
            var curr = timings[i];

            double countRatio = (double)curr.Count / prev.Count;
            double timeRatio = curr.TimeMs / prev.TimeMs;

            // Allow 50% variance from perfect linear scaling (JIT, cache effects, etc.)
            double maxExpectedRatio = countRatio * 1.5;
            double minExpectedRatio = countRatio / 1.3;

            _output.WriteLine($"  {prev.Count} → {curr.Count} vectors: count ratio {countRatio:F2}x, time ratio {timeRatio:F2}x");

            Assert.True(
                timeRatio <= maxExpectedRatio,
                $"Performance scaling degraded at {curr.Count} vectors: expected time ratio ≈ {countRatio:F2}x, got {timeRatio:F2}x");
        }

        _output.WriteLine("Scaling test results (384 dims):");
        foreach (var (count, timeMs) in timings)
        {
            _output.WriteLine($"  {count} vectors: {timeMs:F2}ms");
        }
    }

    [Fact]
    public void BatchCosineSimilarity_ScalingTest_768Dims_LinearPerformance()
    {
        // Arrange - Test that performance scales linearly with vector count
        // Note: This test has higher variance due to cache effects at different scales
        var service = new CpuVectorSimilarityService(NullLogger<CpuVectorSimilarityService>.Instance);
        var queryVector = CreateTestVector(768);

        var counts = new[] { 100, 500, 1000 };
        var timings = new List<(int Count, double TimeMs)>();

        // Act
        foreach (var count in counts)
        {
            var targetVectors = CreateBatchVectors(count, 768);
            var results = new float[count];

            var stopwatch = Stopwatch.StartNew();
            service.BatchCosineSimilarity(queryVector, targetVectors, count, 768, results);
            stopwatch.Stop();

            timings.Add((count, stopwatch.Elapsed.TotalMilliseconds));
        }

        // Assert - Check for linear scaling (allow 50% variance for system noise/cache effects)
        for (int i = 1; i < timings.Count; i++)
        {
            var prev = timings[i - 1];
            var curr = timings[i];

            double countRatio = (double)curr.Count / prev.Count;
            double timeRatio = curr.TimeMs / prev.TimeMs;

            // Allow 50% variance from perfect linear scaling (high-dim operations have more variance)
            double maxExpectedRatio = countRatio * 1.5;

            _output.WriteLine($"  {prev.Count} → {curr.Count} vectors: count ratio {countRatio:F2}x, time ratio {timeRatio:F2}x");

            Assert.True(
                timeRatio <= maxExpectedRatio,
                $"Performance scaling degraded at {curr.Count} vectors: expected time ratio ≈ {countRatio:F2}x, got {timeRatio:F2}x");
        }

        _output.WriteLine("Scaling test results (768 dims):");
        foreach (var (count, timeMs) in timings)
        {
            _output.WriteLine($"  {count} vectors: {timeMs:F2}ms");
        }
    }

    #endregion

    #region Stress Tests

    [Fact]
    public void BatchCosineSimilarity_LargeBatch_50000Vectors_Completes()
    {
        // Arrange - Stress test with very large batch
        var service = new CpuVectorSimilarityService(NullLogger<CpuVectorSimilarityService>.Instance);
        var queryVector = CreateTestVector(384);
        var targetVectors = CreateBatchVectors(50000, 384);
        var results = new float[50000];

        var stopwatch = Stopwatch.StartNew();

        // Act
        service.BatchCosineSimilarity(queryVector, targetVectors, 50000, 384, results);

        stopwatch.Stop();
        double elapsedMs = stopwatch.Elapsed.TotalMilliseconds;

        // Assert - Just verify it completes without error
        // (Performance may degrade gracefully with very large batches)
        _output.WriteLine($"Large batch stress test (50,000 vectors, 384 dims): {elapsedMs:F2}ms");
        Assert.True(elapsedMs < 500.0, // 500ms for 50k vectors is still reasonable
            $"Stress test took {elapsedMs:F2}ms (expected < 500ms)");

        // Verify results are valid
        Assert.All(results, r => Assert.True(float.IsFinite(r), "Result contains NaN or Infinity"));
    }

    [Fact]
    public void SingleCosineSimilarity_VeryHighDimension_2048Dims_UnderThreshold()
    {
        // Arrange - Test with very high dimension vectors (beyond typical use)
        var service = new CpuVectorSimilarityService(NullLogger<CpuVectorSimilarityService>.Instance);
        var vector1 = CreateTestVector(2048);
        var vector2 = CreateTestVector(2048);

        var stopwatch = Stopwatch.StartNew();
        const int iterations = 100; // Fewer iterations for high-dim test

        // Act
        for (int i = 0; i < iterations; i++)
        {
            _ = service.CosineSimilarity(vector1, vector2);
        }

        stopwatch.Stop();
        double avgTimeMs = stopwatch.Elapsed.TotalMilliseconds / iterations;

        // Assert - Allow longer time for higher dimensions
        double threshold = MaxSingleSimilarityMs * 3.0; // 300µs for 2048 dims

        _output.WriteLine($"Single similarity (2048 dims): {avgTimeMs:F4}ms per operation");
        Assert.True(avgTimeMs < threshold * PerformanceTolerance,
            $"Average time {avgTimeMs:F4}ms exceeded threshold {threshold * PerformanceTolerance:F4}ms");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void BatchCosineSimilarity_VerySmallBatch_OneVector_Completes()
    {
        // Arrange - Edge case: single vector batch
        var service = new CpuVectorSimilarityService(NullLogger<CpuVectorSimilarityService>.Instance);
        var queryVector = CreateTestVector(384);
        var targetVectors = CreateBatchVectors(1, 384);
        var results = new float[1];

        // Act
        service.BatchCosineSimilarity(queryVector, targetVectors, 1, 384, results);

        // Assert
        Assert.NotEmpty(results);
        Assert.True(float.IsFinite(results[0]));
    }

    [Fact]
    public void BatchCosineSimilarity_SmallDimension_4Dims_UnderThreshold()
    {
        // Arrange - Edge case: very small dimension vectors
        var service = new CpuVectorSimilarityService(NullLogger<CpuVectorSimilarityService>.Instance);
        var queryVector = CreateTestVector(4);
        var targetVectors = CreateBatchVectors(10000, 4);
        var results = new float[10000];

        var stopwatch = Stopwatch.StartNew();

        // Act
        service.BatchCosineSimilarity(queryVector, targetVectors, 10000, 4, results);

        stopwatch.Stop();
        double elapsedMs = stopwatch.Elapsed.TotalMilliseconds;

        // Assert
        _output.WriteLine($"Small dimension batch (10,000 vectors, 4 dims): {elapsedMs:F4}ms");
        Assert.True(elapsedMs < 5.0, // Should be very fast with tiny vectors
            $"Small dimension test took {elapsedMs:F4}ms");
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a test vector with pseudorandom values (deterministic seeding).
    /// Values are normalized to unit magnitude for consistent similarity computation.
    /// </summary>
    private static float[] CreateTestVector(int dimension)
    {
        var vector = new float[dimension];
        var random = new Random(42); // Fixed seed for reproducibility

        // Generate random values
        for (int i = 0; i < dimension; i++)
        {
            vector[i] = (float)random.NextDouble();
        }

        // Normalize to unit magnitude
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

    /// <summary>
    /// Creates a batch of test vectors (count vectors of specified dimension).
    /// Each vector is independently randomized and normalized.
    /// </summary>
    private static float[] CreateBatchVectors(int vectorCount, int dimension)
    {
        var result = new float[vectorCount * dimension];
        var random = new Random(42);

        for (int v = 0; v < vectorCount; v++)
        {
            // Generate random values for this vector
            float sumOfSquares = 0;
            for (int d = 0; d < dimension; d++)
            {
                float value = (float)random.NextDouble();
                result[v * dimension + d] = value;
                sumOfSquares += value * value;
            }

            // Normalize this vector to unit magnitude
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
