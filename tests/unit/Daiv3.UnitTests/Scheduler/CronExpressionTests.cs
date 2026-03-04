using Daiv3.Scheduler;
using Xunit;

namespace Daiv3.UnitTests.Scheduler;

/// <summary>
/// Unit tests for CronExpression class, which parses and evaluates cron expressions.
/// </summary>
public class CronExpressionTests
{
    [Theory]
    [InlineData("0 0 * * *")]       // Daily at midnight
    [InlineData("0 12 * * 1-5")]    // Weekdays at noon
    [InlineData("*/15 * * * *")]    // Every 15 minutes
    [InlineData("0 9,17 * * *")]    // At 9 AM and 5 PM daily
    [InlineData("30 8-18 * * *")]   // Every hour from 8 AM to 6 PM at 30 minutes past
    [InlineData("0 0 1 * *")]       // First day of every month at midnight
    [InlineData("0 0 * * 0")]       // Every Sunday at midnight
    public void Constructor_WithValidExpression_Succeeds(string expression)
    {
        // Act
        var cron = new CronExpression(expression);

        // Assert
        Assert.NotNull(cron);
        Assert.Equal(expression, cron.Expression);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("0 0 * *")]         // Only 4 fields
    [InlineData("0 0 * * * *")]     // 6 fields (not supported)
    [InlineData("60 0 * * *")]      // Invalid minute (> 59)
    [InlineData("0 25 * * *")]      // Invalid hour (> 23)
    [InlineData("0 0 32 * *")]      // Invalid day (> 31)
    [InlineData("0 0 * 13 *")]      // Invalid month (> 12)
    [InlineData("0 0 * * 7")]       // Invalid day of week (> 6)
    public void Constructor_WithInvalidExpression_ThrowsArgumentException(string? expression)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new CronExpression(expression!));
    }

    [Fact]
    public void GetNextOccurrence_DailyAtMidnight_ReturnsNextMidnight()
    {
        // Arrange
        var cron = new CronExpression("0 0 * * *");
        var now = new DateTime(2026, 2, 28, 10, 30, 0, DateTimeKind.Utc);

        // Act
        var next = cron.GetNextOccurrence(now);

        // Assert
        Assert.NotNull(next);
        Assert.Equal(new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), next.Value);
    }

    [Fact]
    public void GetNextOccurrence_Every15Minutes_ReturnsNext15MinuteMark()
    {
        // Arrange
        var cron = new CronExpression("*/15 * * * *");
        var now = new DateTime(2026, 2, 28, 10, 7, 0, DateTimeKind.Utc);

        // Act
        var next = cron.GetNextOccurrence(now);

        // Assert
        Assert.NotNull(next);
        Assert.Equal(new DateTime(2026, 2, 28, 10, 15, 0, DateTimeKind.Utc), next.Value);
    }

    [Fact]
    public void GetNextOccurrence_WeekdaysAtNoon_ReturnsNextWeekdayNoon()
    {
        // Arrange
        var cron = new CronExpression("0 12 * * 1-5"); // Monday-Friday at noon
        var saturday = new DateTime(2026, 2, 28, 10, 0, 0, DateTimeKind.Utc); // Saturday

        // Act
        var next = cron.GetNextOccurrence(saturday);

        // Assert
        Assert.NotNull(next);
        // Next weekday should be Monday March 2
        Assert.Equal(DayOfWeek.Monday, next.Value.DayOfWeek);
        Assert.Equal(12, next.Value.Hour);
        Assert.Equal(0, next.Value.Minute);
    }

    [Fact]
    public void GetNextOccurrence_MultipleHours_ReturnsNextMatchingHour()
    {
        // Arrange
        var cron = new CronExpression("0 9,17 * * *"); // 9 AM and 5 PM
        var now = new DateTime(2026, 2, 28, 10, 0, 0, DateTimeKind.Utc);

        // Act
        var next = cron.GetNextOccurrence(now);

        // Assert
        Assert.NotNull(next);
        Assert.Equal(new DateTime(2026, 2, 28, 17, 0, 0, DateTimeKind.Utc), next.Value);
    }

    [Fact]
    public void GetNextOccurrence_FirstDayOfMonth_ReturnsFirstOfNextMonth()
    {
        // Arrange
        var cron = new CronExpression("0 0 1 * *"); // First day of month at midnight
        var now = new DateTime(2026, 2, 15, 10, 0, 0, DateTimeKind.Utc);

        // Act
        var next = cron.GetNextOccurrence(now);

        // Assert
        Assert.NotNull(next);
        Assert.Equal(new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), next.Value);
    }

    [Fact]
    public void Matches_WithMatchingTime_ReturnsTrue()
    {
        // Arrange
        var cron = new CronExpression("30 14 * * *"); // Every day at 2:30 PM
        var matchingTime = new DateTime(2026, 2, 28, 14, 30, 0, DateTimeKind.Utc);

        // Act
        var matches = cron.Matches(matchingTime);

        // Assert
        Assert.True(matches);
    }

    [Fact]
    public void Matches_WithNonMatchingTime_ReturnsFalse()
    {
        // Arrange
        var cron = new CronExpression("30 14 * * *"); // Every day at 2:30 PM
        var nonMatchingTime = new DateTime(2026, 2, 28, 14, 31, 0, DateTimeKind.Utc);

        // Act
        var matches = cron.Matches(nonMatchingTime);

        // Assert
        Assert.False(matches);
    }

    [Fact]
    public void Matches_WeekdayPattern_CorrectlyMatchesWeekdays()
    {
        // Arrange
        var cron = new CronExpression("0 12 * * 1-5"); // Weekdays at noon
        var monday = new DateTime(2026, 3, 2, 12, 0, 0, DateTimeKind.Utc); // Monday
        var saturday = new DateTime(2026, 2, 28, 12, 0, 0, DateTimeKind.Utc); // Saturday

        // Act
        var mondayMatches = cron.Matches(monday);
        var saturdayMatches = cron.Matches(saturday);

        // Assert
        Assert.True(mondayMatches);
        Assert.False(saturdayMatches);
    }

    [Fact]
    public void TryParse_WithValidExpression_ReturnsTrue()
    {
        // Arrange
        var expression = "0 0 * * *";

        // Act
        var success = CronExpression.TryParse(expression, out var cron);

        // Assert
        Assert.True(success);
        Assert.NotNull(cron);
        Assert.Equal(expression, cron.Expression);
    }

    [Fact]
    public void TryParse_WithInvalidExpression_ReturnsFalse()
    {
        // Arrange
        var expression = "invalid cron";

        // Act
        var success = CronExpression.TryParse(expression, out var cron);

        // Assert
        Assert.False(success);
        Assert.Null(cron);
    }

    [Fact]
    public void GetNextOccurrence_WithNonUtcTime_ThrowsArgumentException()
    {
        // Arrange
        var cron = new CronExpression("0 0 * * *");
        var localTime = DateTime.Now; // Local time, not UTC

        // Act & Assert
        Assert.Throws<ArgumentException>(() => cron.GetNextOccurrence(localTime));
    }

    [Fact]
    public void Matches_WithNonUtcTime_ThrowsArgumentException()
    {
        // Arrange
        var cron = new CronExpression("0 0 * * *");
        var localTime = DateTime.Now; // Local time, not UTC

        // Act & Assert
        Assert.Throws<ArgumentException>(() => cron.Matches(localTime));
    }

    [Theory]
    [InlineData("0-30/5 * * * *")]  // Every 5 minutes from 0 to 30
    [InlineData("0 8-17/2 * * *")]  // Every 2 hours from 8 AM to 5 PM
    public void Constructor_WithStepRanges_Succeeds(string expression)
    {
        // Act
        var cron = new CronExpression(expression);

        // Assert
        Assert.NotNull(cron);
        Assert.Equal(expression, cron.Expression);
    }

    [Fact]
    public void GetNextOccurrence_WithStepPattern_ReturnsCorrectNext()
    {
        // Arrange
        var cron = new CronExpression("0,30 * * * *"); // Every hour at 0 and 30 minutes
        var now = new DateTime(2026, 2, 28, 10, 15, 0, DateTimeKind.Utc);

        // Act
        var next = cron.GetNextOccurrence(now);

        // Assert
        Assert.NotNull(next);
        Assert.Equal(new DateTime(2026, 2, 28, 10, 30, 0, DateTimeKind.Utc), next.Value);
    }
}
