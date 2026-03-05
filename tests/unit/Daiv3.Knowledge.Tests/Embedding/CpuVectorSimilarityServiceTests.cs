using Daiv3.Knowledge.Embedding;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Daiv3.Knowledge.Tests.Embedding;

public class CpuVectorSimilarityServiceTests
{
    private readonly Mock<ILogger<CpuVectorSimilarityService>> _loggerMock;
    private readonly CpuVectorSimilarityService _service;

    public CpuVectorSimilarityServiceTests()
    {
        _loggerMock = new Mock<ILogger<CpuVectorSimilarityService>>();
        _service = new CpuVectorSimilarityService(_loggerMock.Object);
    }

    #region CosineSimilarity Tests

    [Fact]
    public void CosineSimilarity_IdenticalVectors_ReturnsOne()
    {
        // Arrange
        float[] vector = [1.0f, 2.0f, 3.0f, 4.0f];

        // Act
        float result = _service.CosineSimilarity(vector, vector);

        // Assert
        Assert.Equal(1.0f, result, precision: 6);
    }

    [Fact]
    public void CosineSimilarity_OrthogonalVectors_ReturnsZero()
    {
        // Arrange
        float[] vector1 = [1.0f, 0.0f];
        float[] vector2 = [0.0f, 1.0f];

        // Act
        float result = _service.CosineSimilarity(vector1, vector2);

        // Assert
        Assert.Equal(0.0f, result, precision: 6);
    }

    [Fact]
    public void CosineSimilarity_OppositeVectors_ReturnsNegativeOne()
    {
        // Arrange
        float[] vector1 = [1.0f, 2.0f, 3.0f];
        float[] vector2 = [-1.0f, -2.0f, -3.0f];

        // Act
        float result = _service.CosineSimilarity(vector1, vector2);

        // Assert
        Assert.Equal(-1.0f, result, precision: 6);
    }

    [Fact]
    public void CosineSimilarity_PartialOverlap_ReturnsExpectedValue()
    {
        // Arrange
        float[] vector1 = [1.0f, 0.0f, 0.0f];
        float[] vector2 = [1.0f, 1.0f, 0.0f];

        // Expected: dot(v1, v2) / (||v1|| * ||v2||)
        // = 1.0 / (1.0 * sqrt(2))
        // = 1.0 / 1.414...
        // ≈ 0.707

        // Act
        float result = _service.CosineSimilarity(vector1, vector2);

        // Assert
        Assert.Equal(0.707106f, result, precision: 5);
    }

    [Fact]
    public void CosineSimilarity_DifferentLengths_ThrowsArgumentException()
    {
        // Arrange
        float[] vector1 = [1.0f, 2.0f];
        float[] vector2 = [1.0f, 2.0f, 3.0f];

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            _service.CosineSimilarity(vector1, vector2));
    }

    [Fact]
    public void CosineSimilarity_EmptyVectors_ThrowsArgumentException()
    {
        // Arrange
        float[] vector1 = [];
        float[] vector2 = [];

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            _service.CosineSimilarity(vector1, vector2));
    }

    [Fact]
    public void CosineSimilarity_ZeroMagnitudeVector_ReturnsZero()
    {
        // Arrange
        float[] vector1 = [1.0f, 2.0f, 3.0f];
        float[] vector2 = [0.0f, 0.0f, 0.0f];

        // Act
        float result = _service.CosineSimilarity(vector1, vector2);

        // Assert
        Assert.Equal(0.0f, result);
    }

    [Fact]
    public void CosineSimilarity_HighDimensionalVectors_ComputesCorrectly()
    {
        // Arrange - 384 dimensions (typical for embeddings)
        float[] vector1 = new float[384];
        float[] vector2 = new float[384];

        for (int i = 0; i < 384; i++)
        {
            vector1[i] = i / 384.0f;
            vector2[i] = (384 - i) / 384.0f;
        }

        // Act
        float result = _service.CosineSimilarity(vector1, vector2);

        // Assert - Should be a negative value since vectors are somewhat opposite
        Assert.True(result < 0.5f);
        Assert.True(result > -1.0f);
    }

    #endregion

    #region BatchCosineSimilarity Tests

    [Fact]
    public void BatchCosineSimilarity_SingleVector_ComputesCorrectly()
    {
        // Arrange
        float[] queryVector = [1.0f, 2.0f, 3.0f];
        float[] targetVectors = [1.0f, 2.0f, 3.0f]; // Same as query
        float[] results = new float[1];

        // Act
        _service.BatchCosineSimilarity(queryVector, targetVectors, 1, 3, results);

        // Assert
        Assert.Equal(1.0f, results[0], precision: 6);
    }

    [Fact]
    public void BatchCosineSimilarity_MultipleVectors_ComputesCorrectly()
    {
        // Arrange
        float[] queryVector = [1.0f, 0.0f, 0.0f];
        float[] targetVectors =
        [
            1.0f, 0.0f, 0.0f,  // Same as query - should be 1.0
            0.0f, 1.0f, 0.0f,  // Orthogonal - should be 0.0
            -1.0f, 0.0f, 0.0f  // Opposite - should be -1.0
        ];
        float[] results = new float[3];

        // Act
        _service.BatchCosineSimilarity(queryVector, targetVectors, 3, 3, results);

        // Assert
        Assert.Equal(1.0f, results[0], precision: 6);
        Assert.Equal(0.0f, results[1], precision: 6);
        Assert.Equal(-1.0f, results[2], precision: 6);
    }

    [Fact]
    public void BatchCosineSimilarity_LargeVectorCount_ComputesCorrectly()
    {
        // Arrange - Simulate Tier 1 search with 1000 documents
        int vectorCount = 1000;
        int dimensions = 384;

        float[] queryVector = new float[dimensions];
        for (int i = 0; i < dimensions; i++)
            queryVector[i] = i / (float)dimensions;

        float[] targetVectors = new float[vectorCount * dimensions];
        for (int v = 0; v < vectorCount; v++)
        {
            for (int d = 0; d < dimensions; d++)
            {
                // Make vectors progressively different from query
                targetVectors[v * dimensions + d] = (d + v) / (float)dimensions;
            }
        }

        float[] results = new float[vectorCount];

        // Act
        _service.BatchCosineSimilarity(queryVector, targetVectors, vectorCount, dimensions, results);

        // Assert
        Assert.Equal(vectorCount, results.Length);

        // First vector should be most similar
        Assert.True(results[0] > 0.9f);

        // Last vector should be least similar
        Assert.True(results[vectorCount - 1] < results[0]);

        // All results should be in valid range
        foreach (var score in results)
        {
            Assert.True(score >= -1.0f && score <= 1.0f);
        }
    }

    [Fact]
    public void BatchCosineSimilarity_QueryDimensionMismatch_ThrowsArgumentException()
    {
        // Arrange
        float[] queryVector = [1.0f, 2.0f]; // 2 dimensions
        float[] targetVectors = [1.0f, 2.0f, 3.0f];
        float[] results = new float[1];

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            _service.BatchCosineSimilarity(queryVector, targetVectors, 1, 3, results));
    }

    [Fact]
    public void BatchCosineSimilarity_TargetArraySizeMismatch_ThrowsArgumentException()
    {
        // Arrange
        float[] queryVector = [1.0f, 2.0f, 3.0f];
        float[] targetVectors = [1.0f, 2.0f]; // Too small
        float[] results = new float[1];

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            _service.BatchCosineSimilarity(queryVector, targetVectors, 1, 3, results));
    }

    [Fact]
    public void BatchCosineSimilarity_ResultsArrayTooSmall_ThrowsArgumentException()
    {
        // Arrange
        float[] queryVector = [1.0f, 2.0f, 3.0f];
        float[] targetVectors = [1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f];
        float[] results = new float[1]; // Should be 2

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            _service.BatchCosineSimilarity(queryVector, targetVectors, 2, 3, results));
    }

    [Fact]
    public void BatchCosineSimilarity_ZeroQueryMagnitude_FillsResultsWithZero()
    {
        // Arrange
        float[] queryVector = [0.0f, 0.0f, 0.0f];
        float[] targetVectors = [1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f];
        float[] results = new float[2];

        // Act
        _service.BatchCosineSimilarity(queryVector, targetVectors, 2, 3, results);

        // Assert
        Assert.Equal(0.0f, results[0]);
        Assert.Equal(0.0f, results[1]);
    }

    [Fact]
    public void BatchCosineSimilarity_ZeroTargetMagnitude_SetsResultToZero()
    {
        // Arrange
        float[] queryVector = [1.0f, 2.0f, 3.0f];
        float[] targetVectors =
        [
            1.0f, 2.0f, 3.0f,  // Valid vector
            0.0f, 0.0f, 0.0f   // Zero magnitude
        ];
        float[] results = new float[2];

        // Act
        _service.BatchCosineSimilarity(queryVector, targetVectors, 2, 3, results);

        // Assert
        Assert.Equal(1.0f, results[0], precision: 6);
        Assert.Equal(0.0f, results[1]);
    }

    #endregion

    #region Normalize Tests

    [Fact]
    public void Normalize_StandardVector_CreatesUnitVector()
    {
        // Arrange
        float[] vector = [3.0f, 4.0f]; // Magnitude = 5
        float[] normalized = new float[2];

        // Act
        _service.Normalize(vector, normalized);

        // Assert
        Assert.Equal(0.6f, normalized[0], precision: 6); // 3/5
        Assert.Equal(0.8f, normalized[1], precision: 6); // 4/5

        // Verify unit length
        float magnitude = MathF.Sqrt(normalized[0] * normalized[0] + normalized[1] * normalized[1]);
        Assert.Equal(1.0f, magnitude, precision: 6);
    }

    [Fact]
    public void Normalize_HighDimensionalVector_CreatesUnitVector()
    {
        // Arrange
        float[] vector = new float[384];
        for (int i = 0; i < 384; i++)
            vector[i] = i / 100.0f;

        float[] normalized = new float[384];

        // Act
        _service.Normalize(vector, normalized);

        // Assert - Verify unit length
        float sumOfSquares = 0f;
        for (int i = 0; i < 384; i++)
            sumOfSquares += normalized[i] * normalized[i];

        float magnitude = MathF.Sqrt(sumOfSquares);
        Assert.Equal(1.0f, magnitude, precision: 5);
    }

    [Fact]
    public void Normalize_UnitVector_RemainsUnchanged()
    {
        // Arrange
        float[] vector = [1.0f, 0.0f, 0.0f];
        float[] normalized = new float[3];

        // Act
        _service.Normalize(vector, normalized);

        // Assert
        Assert.Equal(1.0f, normalized[0], precision: 6);
        Assert.Equal(0.0f, normalized[1], precision: 6);
        Assert.Equal(0.0f, normalized[2], precision: 6);
    }

    [Fact]
    public void Normalize_ZeroVector_FillsWithZero()
    {
        // Arrange
        float[] vector = [0.0f, 0.0f, 0.0f];
        float[] normalized = new float[3];

        // Act
        _service.Normalize(vector, normalized);

        // Assert
        Assert.Equal(0.0f, normalized[0]);
        Assert.Equal(0.0f, normalized[1]);
        Assert.Equal(0.0f, normalized[2]);
    }

    [Fact]
    public void Normalize_DifferentLengths_ThrowsArgumentException()
    {
        // Arrange
        float[] vector = [1.0f, 2.0f];
        float[] normalized = new float[3];

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            _service.Normalize(vector, normalized));
    }

    [Fact]
    public void Normalize_EmptyVector_ThrowsArgumentException()
    {
        // Arrange
        float[] vector = [];
        float[] normalized = [];

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            _service.Normalize(vector, normalized));
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new CpuVectorSimilarityService(null!));
    }

    #endregion

    #region Performance Characteristics Tests

    [Fact]
    public void BatchCosineSimilarity_10000Vectors_CompletesInReasonableTime()
    {
        // Arrange - Simulate Tier 1 search target: <10ms for 10,000 vectors
        int vectorCount = 10000;
        int dimensions = 384;

        float[] queryVector = new float[dimensions];
        Array.Fill(queryVector, 0.1f);

        float[] targetVectors = new float[vectorCount * dimensions];
        Array.Fill(targetVectors, 0.1f);

        float[] results = new float[vectorCount];

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        _service.BatchCosineSimilarity(queryVector, targetVectors, vectorCount, dimensions, results);
        stopwatch.Stop();

        // Assert
        Assert.Equal(vectorCount, results.Length);

        // Log performance (not a hard requirement for unit test, but useful info)
        // Target is <10ms on CPU for ~10,000 vectors (per KM-NFR-001)
        // Note: This may vary widely based on CPU, so we don't fail on performance here
        Assert.True(stopwatch.ElapsedMilliseconds < 1000,
            $"Batch operation took {stopwatch.ElapsedMilliseconds}ms, expected under 1000ms");
    }

    [Fact] // NOTE: This is a stress test - comment out this line and uncomment below to skip in CI
    // [Fact(Skip = "Stress test - only run manually to observe CPU activity")]
    public void StressTest_LargeScale_ShowsCpuActivity()
    {
        // Arrange - Large-scale test to show CPU activity
        // This simulates searching against 100,000 documents with high-dimensional embeddings
        int vectorCount = 100000;  // 100K documents
        int dimensions = 768;      // Tier 2 dimensions
        int iterations = 5;        // Multiple search queries

        float[] queryVector = new float[dimensions];
        float[] targetVectors = new float[vectorCount * dimensions];
        float[] results = new float[vectorCount];

        var random = new Random(42);
        for (int i = 0; i < dimensions; i++)
            queryVector[i] = (float)random.NextDouble();

        for (int i = 0; i < targetVectors.Length; i++)
            targetVectors[i] = (float)random.NextDouble();

        // Act - Run multiple iterations to show sustained CPU activity
        var totalStopwatch = System.Diagnostics.Stopwatch.StartNew();

        for (int iter = 0; iter < iterations; iter++)
        {
            var iterStopwatch = System.Diagnostics.Stopwatch.StartNew();
            _service.BatchCosineSimilarity(queryVector, targetVectors, vectorCount, dimensions, results);
            iterStopwatch.Stop();

            // Find top results
            var topScores = results.OrderByDescending(x => x).Take(10).ToList();
        }

        totalStopwatch.Stop();

        // Assert - Should complete but will take a few seconds
        Assert.Equal(vectorCount, results.Length);

        // This should take several seconds and show CPU activity in Task Manager
        // On a modern CPU with SIMD: ~500-2000ms per iteration expected
        Assert.True(totalStopwatch.ElapsedMilliseconds > 100,
            "Stress test completed too quickly - increase workload");
        Assert.True(totalStopwatch.ElapsedMilliseconds < 30000,
            $"Stress test took too long: {totalStopwatch.ElapsedMilliseconds}ms - may indicate performance issue");
    }

    #endregion
}
