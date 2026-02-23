using Daiv3.Knowledge.Embedding;
using Xunit;

namespace Daiv3.UnitTests.Knowledge.Embedding;

public class PerformanceMetricsTests
{
    [Fact]
    public void Constructor_InitializesProperties()
    {
        // Arrange & Act
        var metrics = new PerformanceMetrics
        {
            ElapsedMs = 10.0,
            VectorCount = 1000,
            Dimension = 384
        };

        // Assert
        Assert.Equal(10.0, metrics.ElapsedMs);
        Assert.Equal(1000, metrics.VectorCount);
        Assert.Equal(384, metrics.Dimension);
    }

    [Fact]
    public void TimePerVectorMicroSeconds_CalculatesCorrectly()
    {
        // Arrange
        var metrics = new PerformanceMetrics
        {
            ElapsedMs = 10.0, // 10 ms
            VectorCount = 10000,
            Dimension = 384
        };

        // Act
        double timePerVector = metrics.TimePerVectorMicroSeconds;

        // Assert
        // 10ms * 1000µs/ms / 10000 vectors = 1µs per vector
        Assert.Equal(1.0, timePerVector, precision: 4);
    }

    [Fact]
    public void VectorsPerSecond_CalculatesCorrectly()
    {
        // Arrange
        var metrics = new PerformanceMetrics
        {
            ElapsedMs = 10.0,
            VectorCount = 10000,
            Dimension = 384
        };

        // Act
        double throughput = metrics.VectorsPerSecond;

        // Assert
        // (10000 vectors / 10ms) * 1000 = 1,000,000 vectors/second
        Assert.Equal(1000000.0, throughput, precision: 0);
    }

    [Fact]
    public void IsSlowOperation_BelowThreshold_ReturnsFalse()
    {
        // Arrange
        var metrics = new PerformanceMetrics
        {
            ElapsedMs = 5.0,
            VectorCount = 1000,
            Dimension = 384
        };

        // Act
        bool isSlow = metrics.IsSlowOperation(10.0);

        // Assert
        Assert.False(isSlow);
    }

    [Fact]
    public void IsSlowOperation_AboveThreshold_ReturnsTrue()
    {
        // Arrange
        var metrics = new PerformanceMetrics
        {
            ElapsedMs = 15.0,
            VectorCount = 1000,
            Dimension = 384
        };

        // Act
        bool isSlow = metrics.IsSlowOperation(10.0);

        // Assert
        Assert.True(isSlow);
    }

    [Fact]
    public void IsSlowOperation_AtThreshold_ReturnsFalse()
    {
        // Arrange
        var metrics = new PerformanceMetrics
        {
            ElapsedMs = 10.0,
            VectorCount = 1000,
            Dimension = 384
        };

        // Act
        bool isSlow = metrics.IsSlowOperation(10.0);

        // Assert
        Assert.False(isSlow); // Exactly at threshold is NOT considered slow (must exceed)
    }

    [Fact]
    public void ToString_ProducesFormattedString()
    {
        // Arrange
        var metrics = new PerformanceMetrics
        {
            ElapsedMs = 10.0,
            VectorCount = 10000,
            Dimension = 384
        };

        // Act
        string result = metrics.ToString();

        // Assert
        Assert.Contains("vectors=10000", result);
        Assert.Contains("dims=384", result);
        Assert.Contains("elapsed=10.00ms", result);
        Assert.Contains("throughput=", result);
        Assert.Contains("time_per_vec=", result);
    }

    [Theory]
    [InlineData(0, 1000, 384)]
    [InlineData(1.0, 0, 384)]
    [InlineData(1.0, 1000, 0)]
    public void TimePerVectorMicroSeconds_WithZeroValues_HandlesGracefully(
        double elapsedMs, int vectorCount, int dimension)
    {
        // Arrange
        var metrics = new PerformanceMetrics
        {
            ElapsedMs = elapsedMs,
            VectorCount = vectorCount,
            Dimension = dimension
        };

        // Act & Assert - Should not throw
        double timePerVector = metrics.TimePerVectorMicroSeconds;
        Assert.True(timePerVector >= 0 || double.IsNaN(timePerVector) || double.IsInfinity(timePerVector));
    }
}
