using System.Collections.Concurrent;

namespace Daiv3.Orchestration.Messaging.Correlation;

/// <summary>
/// Manages in-flight message correlations for request/reply patterns.
/// Tracks pending replies and handles timeout-based cleanup.
/// </summary>
public class MessageCorrelationContext : IDisposable
{
    /// <summary>
    /// Maps correlation IDs to their pending reply completions.
    /// </summary>
    private readonly ConcurrentDictionary<string, CorrelationEntry> _pendingReplies = new();

    /// <summary>
    /// Background task that cleans up expired correlations.
    /// </summary>
    private readonly Timer? _cleanupTimer;

    public MessageCorrelationContext()
    {
        // Start cleanup timer to remove expired correlations every 30 seconds
        _cleanupTimer = new Timer(
            _ => CleanupExpiredCorrelations(),
            null,
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// Registers a pending correlation and returns a task that completes when the reply arrives.
    /// </summary>
    /// <param name="correlationId">The correlation ID to wait for.</param>
    /// <param name="timeout">Maximum time to wait for a reply.</param>
    /// <returns>A task that completes when a reply message is received.</returns>
    public Task<IAgentMessage> WaitForReplyAsync(string correlationId, TimeSpan timeout)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        var entry = new CorrelationEntry
        {
            CorrelationId = correlationId,
            ExpiresAt = DateTimeOffset.UtcNow.Add(timeout),
            CompletionSource = new TaskCompletionSource<IAgentMessage>()
        };

        if (!_pendingReplies.TryAdd(correlationId, entry))
        {
            throw new InvalidOperationException($"Correlation {correlationId} already pending");
        }

        // Set timeout to fail the task if reply doesn't arrive in time
        var _ = Task.Delay(timeout).ContinueWith(_ =>
        {
            if (_pendingReplies.TryRemove(correlationId, out var pendingEntry))
            {
                pendingEntry.CompletionSource?.TrySetException(
                    new TimeoutException($"No reply received for correlation {correlationId} within {timeout.TotalSeconds}s"));
            }
        });

        return entry.CompletionSource.Task;
    }

    /// <summary>
    /// Delivers a reply message to a waiting correlation.
    /// </summary>
    /// <param name="correlationId">The correlation ID of the reply.</param>
    /// <param name="replyMessage">The reply message.</param>
    /// <returns>True if a matching correlation was found and the reply was delivered, false otherwise.</returns>
    public bool DeliverReply(string correlationId, IAgentMessage replyMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        ArgumentNullException.ThrowIfNull(replyMessage);

        if (_pendingReplies.TryRemove(correlationId, out var entry))
        {
            entry.CompletionSource?.TrySetResult(replyMessage);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if a correlation is pending.
    /// </summary>
    /// <param name="correlationId">The correlation ID to check.</param>
    /// <returns>True if the correlation is pending, false otherwise.</returns>
    public bool IsPending(string correlationId)
    {
        return _pendingReplies.ContainsKey(correlationId);
    }

    /// <summary>
    /// Gets the number of pending correlations.
    /// </summary>
    public int PendingCount => _pendingReplies.Count;

    /// <summary>
    /// Removes expired correlations.
    /// </summary>
    private void CleanupExpiredCorrelations()
    {
        var now = DateTimeOffset.UtcNow;
        var keysToRemove = _pendingReplies
            .Where(kvp => kvp.Value.ExpiresAt < now)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            if (_pendingReplies.TryRemove(key, out var entry))
            {
                entry.CompletionSource?.TrySetException(
                    new TimeoutException($"Correlation {key} expired"));
            }
        }
    }

    public void Dispose()
    {
        _cleanupTimer?.Dispose();
        _pendingReplies.Clear();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Internal record for tracking a pending correlation.
    /// </summary>
    private class CorrelationEntry
    {
        public string? CorrelationId { get; init; }
        public DateTimeOffset ExpiresAt { get; init; }
        public TaskCompletionSource<IAgentMessage>? CompletionSource { get; init; }
    }
}
