using Daiv3.Persistence.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Daiv3.Persistence.Repositories;

/// <summary>
/// Repository for managing promotion metrics.
/// Stores instrumentation data for transparency (KBP-NFR-001).
/// </summary>
public class PromotionMetricRepository : RepositoryBase<PromotionMetric>
{
    public PromotionMetricRepository(IDatabaseContext databaseContext, ILogger<PromotionMetricRepository> logger)
        : base(databaseContext, logger)
    {
    }

    public override async Task<PromotionMetric?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT metric_id, metric_name, metric_value, recorded_at, period_start, period_end, context
            FROM promotion_metrics
            WHERE metric_id = $id";

        var results = await ExecuteReaderAsync(sql, MapMetric, parameters =>
        {
            parameters.Add(new SqliteParameter("$id", id));
        }, ct).ConfigureAwait(false);

        return results.FirstOrDefault();
    }

    public override async Task<IReadOnlyList<PromotionMetric>> GetAllAsync(CancellationToken ct = default)
    {
        const string sql = @"
            SELECT metric_id, metric_name, metric_value, recorded_at, period_start, period_end, context
            FROM promotion_metrics
            ORDER BY recorded_at DESC";

        return await ExecuteReaderAsync(sql, MapMetric, null, ct).ConfigureAwait(false);
    }

    public override async Task<string> AddAsync(PromotionMetric entity, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO promotion_metrics (
                metric_id, metric_name, metric_value, recorded_at, period_start, period_end, context
            ) VALUES (
                $metric_id, $metric_name, $metric_value, $recorded_at, $period_start, $period_end, $context
            )";

        await ExecuteNonQueryAsync(sql, parameters =>
        {
            parameters.Add(new SqliteParameter("$metric_id", entity.MetricId));
            parameters.Add(new SqliteParameter("$metric_name", entity.MetricName));
            parameters.Add(new SqliteParameter("$metric_value", entity.MetricValue));
            parameters.Add(new SqliteParameter("$recorded_at", entity.RecordedAt));
            parameters.Add(new SqliteParameter("$period_start", entity.PeriodStart ?? (object)DBNull.Value));
            parameters.Add(new SqliteParameter("$period_end", entity.PeriodEnd ?? (object)DBNull.Value));
            parameters.Add(new SqliteParameter("$context", entity.Context ?? (object)DBNull.Value));
        }, ct).ConfigureAwait(false);

        return entity.MetricId;
    }

    public override async Task UpdateAsync(PromotionMetric entity, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE promotion_metrics
            SET metric_name = $metric_name, metric_value = $metric_value, recorded_at = $recorded_at,
                period_start = $period_start, period_end = $period_end, context = $context
            WHERE metric_id = $metric_id";

        var rowsAffected = await ExecuteNonQueryAsync(sql, parameters =>
        {
            parameters.Add(new SqliteParameter("$metric_id", entity.MetricId));
            parameters.Add(new SqliteParameter("$metric_name", entity.MetricName));
            parameters.Add(new SqliteParameter("$metric_value", entity.MetricValue));
            parameters.Add(new SqliteParameter("$recorded_at", entity.RecordedAt));
            parameters.Add(new SqliteParameter("$period_start", entity.PeriodStart ?? (object)DBNull.Value));
            parameters.Add(new SqliteParameter("$period_end", entity.PeriodEnd ?? (object)DBNull.Value));
            parameters.Add(new SqliteParameter("$context", entity.Context ?? (object)DBNull.Value));
        }, ct).ConfigureAwait(false);

        if (rowsAffected == 0)
        {
            Logger.LogWarning("Attempted to update non-existent promotion metric {MetricId}", entity.MetricId);
        }
    }

    public override async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        const string sql = "DELETE FROM promotion_metrics WHERE metric_id = $id";

        var rowsAffected = await ExecuteNonQueryAsync(sql, parameters =>
        {
            parameters.Add(new SqliteParameter("$id", id));
        }, ct).ConfigureAwait(false);

        if (rowsAffected == 0)
        {
            Logger.LogWarning("Attempted to delete non-existent promotion metric {MetricId}", id);
        }
        else
        {
            Logger.LogInformation("Deleted promotion metric {MetricId}", id);
        }
    }

    /// <summary>
    /// Gets all metrics with a specific name, ordered by most recent first.
    /// </summary>
    public async Task<IReadOnlyList<PromotionMetric>> GetByMetricNameAsync(string metricName, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT metric_id, metric_name, metric_value, recorded_at, period_start, period_end, context
            FROM promotion_metrics
            WHERE metric_name = $metric_name
            ORDER BY recorded_at DESC";

        return await ExecuteReaderAsync(sql, MapMetric, parameters =>
        {
            parameters.Add(new SqliteParameter("$metric_name", metricName));
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets metrics within a time range.
    /// </summary>
    public async Task<IReadOnlyList<PromotionMetric>> GetByTimeRangeAsync(
        long startUnixTime,
        long endUnixTime,
        CancellationToken ct = default)
    {
        const string sql = @"
            SELECT metric_id, metric_name, metric_value, recorded_at, period_start, period_end, context
            FROM promotion_metrics
            WHERE recorded_at >= $start_time AND recorded_at <= $end_time
            ORDER BY recorded_at DESC";

        return await ExecuteReaderAsync(sql, MapMetric, parameters =>
        {
            parameters.Add(new SqliteParameter("$start_time", startUnixTime));
            parameters.Add(new SqliteParameter("$end_time", endUnixTime));
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the most recent value of a specific metric.
    /// </summary>
    public async Task<PromotionMetric?> GetLatestByMetricNameAsync(string metricName, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT metric_id, metric_name, metric_value, recorded_at, period_start, period_end, context
            FROM promotion_metrics
            WHERE metric_name = $metric_name
            ORDER BY recorded_at DESC
            LIMIT 1";

        var results = await ExecuteReaderAsync(sql, MapMetric, parameters =>
        {
            parameters.Add(new SqliteParameter("$metric_name", metricName));
        }, ct).ConfigureAwait(false);

        return results.FirstOrDefault();
    }

    private static PromotionMetric MapMetric(SqliteDataReader reader)
    {
        return new PromotionMetric
        {
            MetricId = reader.GetString(0),
            MetricName = reader.GetString(1),
            MetricValue = reader.GetDouble(2),
            RecordedAt = reader.GetInt64(3),
            PeriodStart = reader.IsDBNull(4) ? null : reader.GetInt64(4),
            PeriodEnd = reader.IsDBNull(5) ? null : reader.GetInt64(5),
            Context = reader.IsDBNull(6) ? null : reader.GetString(6)
        };
    }
}
