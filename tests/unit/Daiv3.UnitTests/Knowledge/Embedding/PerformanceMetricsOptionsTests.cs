using Daiv3.Knowledge.Embedding;
using Xunit;

namespace Daiv3.UnitTests.Knowledge.Embedding;

public class PerformanceMetricsOptionsTests
{
    [Fact]
    public void DefaultValues_AreSet()
    {
        // Arrange & Act
        var options = new PerformanceMetricsOptions();

        // Assert
        Assert.False(options.EnableMetricsCollection);
        Assert.Equal(100.0, options.SlowOperationThresholdMs);
        Assert.Equal(0.1, options.SlowOperationSampleRate);
        Assert.False(options.EnableDetailedTelemetry);
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        // Arrange
        var options = new PerformanceMetricsOptions();

        // Act
        options.EnableMetricsCollection = true;
        options.SlowOperationThresholdMs = 50.0;
        options.SlowOperationSampleRate = 0.5;
        options.EnableDetailedTelemetry = true;

        // Assert
        Assert.True(options.EnableMetricsCollection);
        Assert.Equal(50.0, options.SlowOperationThresholdMs);
        Assert.Equal(0.5, options.SlowOperationSampleRate);
        Assert.True(options.EnableDetailedTelemetry);
    }

    [Fact]
    public void Validate_ValidOptions_Succeeds()
    {
        // Arrange
        var options = new PerformanceMetricsOptions
        {
            EnableMetricsCollection = true,
            SlowOperationThresholdMs = 100.0,
            SlowOperationSampleRate = 0.5,
            EnableDetailedTelemetry = true
        };

        // Act & Assert - Should not throw
        options.Validate();
    }

    [Theory]
    [InlineData(-1.0)]
    [InlineData(-100.0)]
    public void Validate_NegativeThreshold_ThrowsArgumentException(double threshold)
    {
        // Arrange
        var options = new PerformanceMetricsOptions
        {
            SlowOperationThresholdMs = threshold
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => options.Validate());
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(-1.0)]
    [InlineData(2.0)]
    public void Validate_InvalidSampleRate_ThrowsArgumentException(double sampleRate)
    {
        // Arrange
        var options = new PerformanceMetricsOptions
        {
            SlowOperationSampleRate = sampleRate
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => options.Validate());
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void Validate_ValidSampleRate_Succeeds(double sampleRate)
    {
        // Arrange
        var options = new PerformanceMetricsOptions
        {
            SlowOperationSampleRate = sampleRate
        };

        // Act & Assert - Should not throw
        options.Validate();
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(1.0)]
    [InlineData(100.0)]
    [InlineData(1000.0)]
    public void Validate_ValidThreshold_Succeeds(double threshold)
    {
        // Arrange
        var options = new PerformanceMetricsOptions
        {
            SlowOperationThresholdMs = threshold
        };

        // Act & Assert - Should not throw
        options.Validate();
    }

    [Fact]
    public void Validate_ZeroThreshold_Succeeds()
    {
        // Arrange
        var options = new PerformanceMetricsOptions
        {
            SlowOperationThresholdMs = 0.0
        };

        // Act & Assert - Should not throw
        options.Validate();
    }
}
