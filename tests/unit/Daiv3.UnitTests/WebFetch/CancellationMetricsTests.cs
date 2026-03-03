using Xunit;
using Daiv3.WebFetch.Crawl;

namespace Daiv3.UnitTests.WebFetch;

/// <summary>
/// Tests for cancellation metrics tracking in web fetch operations.
/// </summary>
public class CancellationMetricsTests
{
    [Fact]
    public void RecordCancellation_WithValidInputs_IncrementsTotalCancellations()
    {
        // Arrange
        var metrics = new CancellationMetrics();

        // Act
        metrics.RecordCancellation("Fetch", "https://example.com", "UserRequested", 100);

        // Assert
        var snapshot = metrics.GetSnapshot();
        Assert.Equal(1, snapshot.TotalCancellations);
    }

    [Fact]
    public void RecordCancellation_WithValidInputs_RecordsOperationType()
    {
        // Arrange
        var metrics = new CancellationMetrics();

        // Act
        metrics.RecordCancellation("Fetch", "https://example.com", "UserRequested", 100);
        metrics.RecordCancellation("Crawl", "https://example.com", "Timeout", 200);

        // Assert
        var snapshot = metrics.GetSnapshot();
        Assert.Equal(2, snapshot.TotalCancellations);
        Assert.Equal(2, snapshot.CancellationsByOperationType.Count);
        Assert.Equal(1, snapshot.CancellationsByOperationType["Fetch"]);
        Assert.Equal(1, snapshot.CancellationsByOperationType["Crawl"]);
    }

    [Fact]
    public void RecordCancellation_WithUserRequested_IncrementsUserRequestedCounter()
    {
        // Arrange
        var metrics = new CancellationMetrics();

        // Act
        metrics.RecordCancellation("Fetch", "https://example.com", "UserRequested", 150);

        // Assert
        var snapshot = metrics.GetSnapshot();
        Assert.Equal(1, snapshot.UserRequestedCancellations);
        Assert.Equal(0, snapshot.TimeoutCancellations);
        Assert.Equal(0, snapshot.ResourceExhaustedCancellations);
    }

    [Fact]
    public void RecordCancellation_WithTimeout_IncrementsTimeoutCounter()
    {
        // Arrange
        var metrics = new CancellationMetrics();

        // Act
        metrics.RecordCancellation("Fetch", "https://example.com", "Timeout", 5000);

        // Assert
        var snapshot = metrics.GetSnapshot();
        Assert.Equal(0, snapshot.UserRequestedCancellations);
        Assert.Equal(1, snapshot.TimeoutCancellations);
        Assert.Equal(0, snapshot.ResourceExhaustedCancellations);
    }

    [Fact]
    public void RecordCancellation_WithResourceExhausted_IncrementsResourceExhaustedCounter()
    {
        // Arrange
        var metrics = new CancellationMetrics();

        // Act
        metrics.RecordCancellation("Fetch", "https://example.com", "ResourceExhausted", 500);

        // Assert
        var snapshot = metrics.GetSnapshot();
        Assert.Equal(0, snapshot.UserRequestedCancellations);
        Assert.Equal(0, snapshot.TimeoutCancellations);
        Assert.Equal(1, snapshot.ResourceExhaustedCancellations);
    }

    [Fact]
    public void RecordCancellation_TracksLatency()
    {
        // Arrange
        var metrics = new CancellationMetrics();

        // Act
        metrics.RecordCancellation("Fetch", "https://example.com", "UserRequested", 100);
        metrics.RecordCancellation("Fetch", "https://other.com", "Timeout", 500);
        metrics.RecordCancellation("Crawl", "https://third.com", "UserRequested", 250);

        // Assert
        var snapshot = metrics.GetSnapshot();
        Assert.Equal(3, snapshot.TotalCancellations);
        Assert.Equal(100, snapshot.FastestCancellationMs);
        Assert.Equal(500, snapshot.SlowestCancellationMs);
        Assert.Equal((100 + 500 + 250) / 3.0, snapshot.AverageCancellationLatencyMs);
    }

    [Fact]
    public void RecordCancellation_WithNullOperationType_ThrowsArgumentException()
    {
        // Arrange
        var metrics = new CancellationMetrics();

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            metrics.RecordCancellation(null!, "https://example.com", "UserRequested", 100));
    }

    [Fact]
    public void RecordCancellation_WithEmptyOperationType_ThrowsArgumentException()
    {
        // Arrange
        var metrics = new CancellationMetrics();

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            metrics.RecordCancellation("", "https://example.com", "UserRequested", 100));
    }

    [Fact]
    public void RecordCancellation_WithNegativeLatency_ThrowsArgumentException()
    {
        // Arrange
        var metrics = new CancellationMetrics();

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            metrics.RecordCancellation("Fetch", "https://example.com", "UserRequested", -1));
    }

    [Fact]
    public void GetSnapshot_WithNoCancellations_ReturnsZeroValues()
    {
        // Arrange
        var metrics = new CancellationMetrics();

        // Act
        var snapshot = metrics.GetSnapshot();

        // Assert
        Assert.Equal(0, snapshot.TotalCancellations);
        Assert.Equal(0, snapshot.UserRequestedCancellations);
        Assert.Equal(0, snapshot.TimeoutCancellations);
        Assert.Equal(0, snapshot.ResourceExhaustedCancellations);
        Assert.Equal(0, snapshot.AverageCancellationLatencyMs);
        Assert.Null(snapshot.FastestCancellationMs);
        Assert.Null(snapshot.SlowestCancellationMs);
        Assert.Empty(snapshot.CancellationsByOperationType);
    }

    [Fact]
    public void Reset_ClearsAllMetrics()
    {
        // Arrange
        var metrics = new CancellationMetrics();
        metrics.RecordCancellation("Fetch", "https://example.com", "UserRequested", 100);
        metrics.RecordCancellation("Crawl", "https://example.com", "Timeout", 200);

        // Act
        metrics.Reset();

        // Assert
        var snapshot = metrics.GetSnapshot();
        Assert.Equal(0, snapshot.TotalCancellations);
        Assert.Equal(0, snapshot.UserRequestedCancellations);
        Assert.Equal(0, snapshot.TimeoutCancellations);
        Assert.Empty(snapshot.CancellationsByOperationType);
    }

    [Fact]
    public async Task RecordCancellation_IsThreadSafe()
    {
        // Arrange
        var metrics = new CancellationMetrics();
        var tasks = new List<Task>();
        var iterations = 100;

        // Act
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < iterations; j++)
                {
                    metrics.RecordCancellation("Fetch", $"https://example{j}.com", "UserRequested", j);
                }
            }));
        }

        await Task.WhenAll(tasks.ToArray());

        // Assert
        var snapshot = metrics.GetSnapshot();
        Assert.Equal(10 * iterations, snapshot.TotalCancellations);
    }

    [Fact]
    public void GetSnapshot_ContainsAllRequiredMetrics()
    {
        // Arrange
        var metrics = new CancellationMetrics();
        metrics.RecordCancellation("Fetch", "https://example.com", "UserRequested", 100);
        metrics.RecordCancellation("Fetch", "https://example.com", "Timeout", 500);

        // Act
        var snapshot = metrics.GetSnapshot();

        // Assert
        Assert.NotNull(snapshot);
        Assert.True(snapshot.TotalCancellations > 0);
        Assert.True(snapshot.SuccessfulCancellations > 0);
        Assert.NotNull(snapshot.CancellationsByOperationType);
    }

    [Fact]
    public void RecordCancellation_MultipleOperationTypes_AggregatesAccurately()
    {
        // Arrange
        var metrics = new CancellationMetrics();

        // Act
        for (int i = 0; i < 5; i++)
            metrics.RecordCancellation("Fetch", $"https://example{i}.com", "UserRequested", 100 + i);

        for (int i = 0; i < 3; i++)
            metrics.RecordCancellation("Crawl", $"https://example{i}.com", "Timeout", 200 + i);

        for (int i = 0; i < 2; i++)
            metrics.RecordCancellation("Parse", $"https://example{i}.com", "ResourceExhausted", 300 + i);

        // Assert
        var snapshot = metrics.GetSnapshot();
        Assert.Equal(10, snapshot.TotalCancellations);
        Assert.Equal(5, snapshot.CancellationsByOperationType["Fetch"]);
        Assert.Equal(3, snapshot.CancellationsByOperationType["Crawl"]);
        Assert.Equal(2, snapshot.CancellationsByOperationType["Parse"]);
        Assert.Equal(5, snapshot.UserRequestedCancellations);
        Assert.Equal(3, snapshot.TimeoutCancellations);
        Assert.Equal(2, snapshot.ResourceExhaustedCancellations);
    }
}
