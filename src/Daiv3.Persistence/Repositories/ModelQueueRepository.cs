using System.Text.Json;
using Daiv3.ModelExecution.Interfaces;
using Daiv3.ModelExecution.Models;
using Daiv3.Persistence.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Daiv3.Persistence.Repositories;

/// <summary>
/// Repository for persisting model queue state.
/// </summary>
/// <remarks>
/// Implements MQ-REQ-013: Persists online tasks when offline for later retry.
/// </remarks>
public class ModelQueueRepository : RepositoryBase<ModelQueueEntry>, IModelQueueRepository
{
    public ModelQueueRepository(IDatabaseContext databaseContext, ILogger<ModelQueueRepository> logger)
        : base(databaseContext, logger)
    {
    }

    public async Task SavePendingRequestAsync(
        ExecutionRequest request,
        ExecutionPriority priority,
        string modelId,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);

        var payloadJson = SerializeRequest(request);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        const string sql = @"
            INSERT OR REPLACE INTO model_queue 
            (request_id, model_id, priority, status, payload_json, created_at)
            VALUES ($request_id, $model_id, $priority, $status, $payload_json, $created_at)";

        await ExecuteNonQueryAsync(sql, parameters =>
        {
            parameters.Add(new SqliteParameter("$request_id", request.Id.ToString()));
            parameters.Add(new SqliteParameter("$model_id", modelId));
            parameters.Add(new SqliteParameter("$priority", (int)priority));
            parameters.Add(new SqliteParameter("$status", "pending"));
            parameters.Add(new SqliteParameter("$payload_json", payloadJson));
            parameters.Add(new SqliteParameter("$created_at", now));
        }, ct).ConfigureAwait(false);

        Logger.LogInformation(
            "Saved pending request {RequestId} for model {ModelId} with priority {Priority}",
            request.Id, modelId, priority);
    }

    public async Task<List<(ExecutionRequest Request, ExecutionPriority Priority, string ModelId)>> GetPendingRequestsAsync(
        CancellationToken ct = default)
    {
        const string sql = @"
            SELECT request_id, model_id, priority, payload_json
            FROM model_queue
            WHERE status = 'pending'
            ORDER BY priority DESC, created_at ASC";

        var results = new List<(ExecutionRequest Request, ExecutionPriority Priority, string ModelId)>();

        await using var connection = await DatabaseContext.GetConnectionAsync(ct).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var requestId = reader.GetString(0);
            var modelId = reader.GetString(1);
            var priority = (ExecutionPriority)reader.GetInt32(2);
            var payloadJson = reader.GetString(3);
            var request = DeserializeRequest(payloadJson, requestId);

            results.Add((Request: request, Priority: priority, ModelId: modelId));
        }

        Logger.LogDebug("Retrieved {Count} pending requests", results.Count);
        return results;
    }

    public async Task UpdateRequestStatusAsync(
        Guid requestId,
        ExecutionStatus status,
        string? errorMessage = null,
        CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var statusString = status.ToString().ToLowerInvariant();

        // Map ExecutionStatus to database status values
        statusString = status switch
        {
            ExecutionStatus.Pending => "pending",
            ExecutionStatus.Queued => "queued",
            ExecutionStatus.Processing => "running",
            ExecutionStatus.Completed => "complete",
            ExecutionStatus.Failed => "error",
            ExecutionStatus.Cancelled => "cancelled",
            _ => "pending"
        };

        string sql;
        if (status == ExecutionStatus.Processing)
        {
            sql = @"
                UPDATE model_queue
                SET status = $status, started_at = $timestamp, error_message = NULL
                WHERE request_id = $request_id";
        }
        else if (status == ExecutionStatus.Completed || status == ExecutionStatus.Failed || status == ExecutionStatus.Cancelled)
        {
            sql = @"
                UPDATE model_queue
                SET status = $status, completed_at = $timestamp, error_message = $error_message
                WHERE request_id = $request_id";
        }
        else
        {
            sql = @"
                UPDATE model_queue
                SET status = $status, error_message = $error_message
                WHERE request_id = $request_id";
        }

        var rowsAffected = await ExecuteNonQueryAsync(sql, parameters =>
        {
            parameters.Add(new SqliteParameter("$request_id", requestId.ToString()));
            parameters.Add(new SqliteParameter("$status", statusString));
            if (sql.Contains("$timestamp"))
                parameters.Add(new SqliteParameter("$timestamp", now));
            if (sql.Contains("$error_message"))
                parameters.Add(new SqliteParameter("$error_message", (object?)errorMessage ?? DBNull.Value));
        }, ct).ConfigureAwait(false);

        if (rowsAffected > 0)
        {
            Logger.LogInformation(
                "Updated request {RequestId} status to {Status}",
                requestId, status);
        }
        else
        {
            Logger.LogWarning("Request {RequestId} not found for status update", requestId);
        }
    }

    public async Task DeleteRequestAsync(Guid requestId, CancellationToken ct = default)
    {
        const string sql = "DELETE FROM model_queue WHERE request_id = $request_id";

        var rowsAffected = await ExecuteNonQueryAsync(sql, parameters =>
        {
            parameters.Add(new SqliteParameter("$request_id", requestId.ToString()));
        }, ct).ConfigureAwait(false);

        if (rowsAffected > 0)
        {
            Logger.LogInformation("Deleted request {RequestId} from queue", requestId);
        }
        else
        {
            Logger.LogWarning("Request {RequestId} not found for deletion", requestId);
        }
    }

    public async Task<int> GetPendingCountAsync(CancellationToken ct = default)
    {
        const string sql = "SELECT COUNT(*) FROM model_queue WHERE status = 'pending'";

        var count = await ExecuteScalarAsync<long?>(sql, null, ct).ConfigureAwait(false);
        return (int)(count ?? 0);
    }

    // Required abstract method implementations from RepositoryBase
    public override async Task<ModelQueueEntry?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT request_id, model_id, priority, status, payload_json, created_at, started_at, completed_at, error_message
            FROM model_queue
            WHERE request_id = $id";

        var results = await ExecuteReaderAsync(sql, MapModelQueueEntry, parameters =>
        {
            parameters.Add(new SqliteParameter("$id", id));
        }, ct).ConfigureAwait(false);

        return results.FirstOrDefault();
    }

    public override async Task<IReadOnlyList<ModelQueueEntry>> GetAllAsync(CancellationToken ct = default)
    {
        const string sql = @"
            SELECT request_id, model_id, priority, status, payload_json, created_at, started_at, completed_at, error_message
            FROM model_queue
            ORDER BY priority DESC, created_at ASC";

        return await ExecuteReaderAsync(sql, MapModelQueueEntry, null, ct).ConfigureAwait(false);
    }

    public override async Task<string> AddAsync(ModelQueueEntry entity, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        const string sql = @"
            INSERT INTO model_queue 
            (request_id, model_id, priority, status, payload_json, created_at, started_at, completed_at, error_message)
            VALUES ($request_id, $model_id, $priority, $status, $payload_json, $created_at, $started_at, $completed_at, $error_message)";

        await ExecuteNonQueryAsync(sql, parameters =>
        {
            parameters.Add(new SqliteParameter("$request_id", entity.RequestId));
            parameters.Add(new SqliteParameter("$model_id", entity.ModelId));
            parameters.Add(new SqliteParameter("$priority", entity.Priority));
            parameters.Add(new SqliteParameter("$status", entity.Status));
            parameters.Add(new SqliteParameter("$payload_json", entity.PayloadJson));
            parameters.Add(new SqliteParameter("$created_at", entity.CreatedAt));
            parameters.Add(new SqliteParameter("$started_at", (object?)entity.StartedAt ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$completed_at", (object?)entity.CompletedAt ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$error_message", (object?)entity.ErrorMessage ?? DBNull.Value));
        }, ct).ConfigureAwait(false);

        Logger.LogInformation("Added model queue entry {RequestId}", entity.RequestId);
        return entity.RequestId;
    }

    public override async Task UpdateAsync(ModelQueueEntry entity, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        const string sql = @"
            UPDATE model_queue
            SET model_id = $model_id,
                priority = $priority,
                status = $status,
                payload_json = $payload_json,
                started_at = $started_at,
                completed_at = $completed_at,
                error_message = $error_message
            WHERE request_id = $request_id";

        var rowsAffected = await ExecuteNonQueryAsync(sql, parameters =>
        {
            parameters.Add(new SqliteParameter("$request_id", entity.RequestId));
            parameters.Add(new SqliteParameter("$model_id", entity.ModelId));
            parameters.Add(new SqliteParameter("$priority", entity.Priority));
            parameters.Add(new SqliteParameter("$status", entity.Status));
            parameters.Add(new SqliteParameter("$payload_json", entity.PayloadJson));
            parameters.Add(new SqliteParameter("$started_at", (object?)entity.StartedAt ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$completed_at", (object?)entity.CompletedAt ?? DBNull.Value));
            parameters.Add(new SqliteParameter("$error_message", (object?)entity.ErrorMessage ?? DBNull.Value));
        }, ct).ConfigureAwait(false);

        if (rowsAffected == 0)
        {
            Logger.LogWarning("Model queue entry {RequestId} not found for update", entity.RequestId);
        }
        else
        {
            Logger.LogInformation("Updated model queue entry {RequestId}", entity.RequestId);
        }
    }

    public override async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        await DeleteRequestAsync(Guid.Parse(id), ct).ConfigureAwait(false);
    }

    // Helper methods
    private static ModelQueueEntry MapModelQueueEntry(SqliteDataReader reader)
    {
        return new ModelQueueEntry
        {
            RequestId = reader.GetString(0),
            ModelId = reader.GetString(1),
            Priority = reader.GetInt32(2),
            Status = reader.GetString(3),
            PayloadJson = reader.GetString(4),
            CreatedAt = reader.GetInt64(5),
            StartedAt = reader.IsDBNull(6) ? null : reader.GetInt64(6),
            CompletedAt = reader.IsDBNull(7) ? null : reader.GetInt64(7),
            ErrorMessage = reader.IsDBNull(8) ? null : reader.GetString(8)
        };
    }

    private static string SerializeRequest(ExecutionRequest request)
    {
        return JsonSerializer.Serialize(request, new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    private static ExecutionRequest DeserializeRequest(string json, string requestIdFallback)
    {
        try
        {
            var request = JsonSerializer.Deserialize<ExecutionRequest>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (request == null)
            {
                throw new InvalidOperationException("Deserialization returned null");
            }

            // Ensure ID is set if missing
            if (request.Id == Guid.Empty && Guid.TryParse(requestIdFallback, out var guidId))
            {
                request.Id = guidId;
            }

            return request;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to deserialize ExecutionRequest: {ex.Message}", ex);
        }
    }
}
