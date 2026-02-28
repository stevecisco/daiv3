using System.Globalization;

namespace Daiv3.Scheduler;

/// <summary>
/// Represents a cron expression and provides methods to calculate next execution times.
/// 
/// Supports standard 5-field cron format: minute hour day month dayOfWeek
/// - minute: 0-59
/// - hour: 0-23
/// - day: 1-31
/// - month: 1-12
/// - dayOfWeek: 0-6 (0=Sunday)
/// 
/// Special characters:
/// - * (any value)
/// - , (value list)
/// - - (range)
/// - / (step)
/// 
/// Examples:
/// - "0 0 * * *" - Daily at midnight
/// - "0 12 * * 1-5" - Weekdays at noon
/// - "*/15 * * * *" - Every 15 minutes
/// - "0 9,17 * * *" - At 9 AM and 5 PM daily
/// </summary>
public class CronExpression
{
    private readonly string _expression;
    private readonly CronField _minute;
    private readonly CronField _hour;
    private readonly CronField _day;
    private readonly CronField _month;
    private readonly CronField _dayOfWeek;

    /// <summary>
    /// Gets the original cron expression string.
    /// </summary>
    public string Expression => _expression;

    /// <summary>
    /// Parses a cron expression string.
    /// </summary>
    /// <param name="expression">The cron expression (5 fields: minute hour day month dayOfWeek).</param>
    /// <exception cref="ArgumentException">If the expression is invalid.</exception>
    public CronExpression(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            throw new ArgumentException("Cron expression cannot be null or whitespace", nameof(expression));
        }

        _expression = expression.Trim();
        var fields = _expression.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (fields.Length != 5)
        {
            throw new ArgumentException(
                $"Cron expression must have exactly 5 fields (minute hour day month dayOfWeek), got {fields.Length}",
                nameof(expression));
        }

        try
        {
            _minute = CronField.Parse(fields[0], 0, 59);
            _hour = CronField.Parse(fields[1], 0, 23);
            _day = CronField.Parse(fields[2], 1, 31);
            _month = CronField.Parse(fields[3], 1, 12);
            _dayOfWeek = CronField.Parse(fields[4], 0, 6);
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Invalid cron expression: {expression}", nameof(expression), ex);
        }
    }

    /// <summary>
    /// Calculates the next occurrence of this cron expression after the given time.
    /// </summary>
    /// <param name="afterUtc">The UTC time to search after.</param>
    /// <returns>The next occurrence in UTC, or null if no occurrence found within reasonable time (1 year).</returns>
    public DateTime? GetNextOccurrence(DateTime afterUtc)
    {
        if (afterUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Time must be in UTC", nameof(afterUtc));
        }

        // Start from the next minute
        var current = new DateTime(
            afterUtc.Year,
            afterUtc.Month,
            afterUtc.Day,
            afterUtc.Hour,
            afterUtc.Minute,
            0,
            DateTimeKind.Utc).AddMinutes(1);

        // Limit search to 1 year ahead to prevent infinite loops
        var limit = afterUtc.AddYears(1);

        while (current < limit)
        {
            if (Matches(current))
            {
                return current;
            }

            // Move to next minute
            current = current.AddMinutes(1);
        }

        return null; // No match found within 1 year
    }

    /// <summary>
    /// Checks if the given time matches this cron expression.
    /// </summary>
    /// <param name="time">The UTC time to check.</param>
    /// <returns>True if the time matches the cron expression.</returns>
    public bool Matches(DateTime time)
    {
        if (time.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Time must be in UTC", nameof(time));
        }

        return _minute.Contains(time.Minute)
               && _hour.Contains(time.Hour)
               && _day.Contains(time.Day)
               && _month.Contains(time.Month)
               && _dayOfWeek.Contains((int)time.DayOfWeek);
    }

    /// <summary>
    /// Validates a cron expression without creating an instance.
    /// </summary>
    /// <param name="expression">The expression to validate.</param>
    /// <returns>True if the expression is valid.</returns>
    public static bool TryParse(string expression, out CronExpression? cronExpression)
    {
        try
        {
            cronExpression = new CronExpression(expression);
            return true;
        }
        catch
        {
            cronExpression = null;
            return false;
        }
    }

    public override string ToString() => _expression;

    /// <summary>
    /// Represents a single field in a cron expression.
    /// </summary>
    private class CronField
    {
        private readonly HashSet<int> _values;

        private CronField(HashSet<int> values)
        {
            _values = values;
        }

        public bool Contains(int value) => _values.Contains(value);

        public static CronField Parse(string field, int min, int max)
        {
            if (string.IsNullOrWhiteSpace(field))
            {
                throw new ArgumentException("Cron field cannot be empty", nameof(field));
            }

            var values = new HashSet<int>();

            // Handle wildcard
            if (field == "*")
            {
                for (int i = min; i <= max; i++)
                {
                    values.Add(i);
                }
                return new CronField(values);
            }

            // Split by comma for lists
            var parts = field.Split(',');

            foreach (var part in parts)
            {
                // Handle step values (e.g., */15 or 0-30/5)
                if (part.Contains('/'))
                {
                    var stepParts = part.Split('/');
                    if (stepParts.Length != 2)
                    {
                        throw new ArgumentException($"Invalid step format: {part}");
                    }

                    var range = stepParts[0];
                    if (!int.TryParse(stepParts[1], out var step) || step <= 0)
                    {
                        throw new ArgumentException($"Invalid step value: {stepParts[1]}");
                    }

                    int rangeStart, rangeEnd;
                    if (range == "*")
                    {
                        rangeStart = min;
                        rangeEnd = max;
                    }
                    else if (range.Contains('-'))
                    {
                        var rangeParts = range.Split('-');
                        if (rangeParts.Length != 2
                            || !int.TryParse(rangeParts[0], out rangeStart)
                            || !int.TryParse(rangeParts[1], out rangeEnd))
                        {
                            throw new ArgumentException($"Invalid range in step: {range}");
                        }
                    }
                    else
                    {
                        if (!int.TryParse(range, out rangeStart))
                        {
                            throw new ArgumentException($"Invalid start value in step: {range}");
                        }
                        rangeEnd = max;
                    }

                    for (int i = rangeStart; i <= rangeEnd; i += step)
                    {
                        if (i >= min && i <= max)
                        {
                            values.Add(i);
                        }
                    }
                }
                // Handle ranges (e.g., 1-5)
                else if (part.Contains('-'))
                {
                    var rangeParts = part.Split('-');
                    if (rangeParts.Length != 2
                        || !int.TryParse(rangeParts[0], out var start)
                        || !int.TryParse(rangeParts[1], out var end))
                    {
                        throw new ArgumentException($"Invalid range: {part}");
                    }

                    if (start < min || start > max || end < min || end > max || start > end)
                    {
                        throw new ArgumentException($"Range out of bounds: {part} (allowed: {min}-{max})");
                    }

                    for (int i = start; i <= end; i++)
                    {
                        values.Add(i);
                    }
                }
                // Handle single values
                else
                {
                    if (!int.TryParse(part, out var value))
                    {
                        throw new ArgumentException($"Invalid value: {part}");
                    }

                    if (value < min || value > max)
                    {
                        throw new ArgumentException($"Value out of bounds: {value} (allowed: {min}-{max})");
                    }

                    values.Add(value);
                }
            }

            if (values.Count == 0)
            {
                throw new ArgumentException($"Cron field resulted in no valid values: {field}");
            }

            return new CronField(values);
        }
    }
}
