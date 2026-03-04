using System.Collections.Concurrent;
using Daiv3.Orchestration.Messaging.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Daiv3.Orchestration.Messaging.Watchers;

/// <summary>
/// Polling-based message watcher for Azure Blob Storage backend.
/// Periodically checks for new messages and delivers them to subscribers.
/// </summary>
public sealed class PollingMessageWatcher : IMessageWatcher
{
    private readonly ILogger<PollingMessageWatcher> _logger;
    private readonly IMessageStore _messageStore;
    private readonly MessageBrokerOptions _options;

    /// <summary>
    /// Maps topics to their watch registrations and handlers.
    /// </summary>
    private readonly ConcurrentDictionary<string, WatchRegistration> _watchedTopics = new();

    /// <summary>
    /// Tracks the last processed timestamp for each topic to avoid duplicate delivery.
    /// </summary>
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastProcessedTimestamp = new();

    /// <summary>
    /// Cancellation token for coordinating shutdown.
    /// </summary>
    private CancellationTokenSource _shutdownCts = new();

    public PollingMessageWatcher(
        ILogger<PollingMessageWatcher> logger,
        IMessageStore messageStore,
        IOptions<MessageBrokerOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _messageStore = messageStore ?? throw new ArgumentNullException(nameof(messageStore));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

        _logger.LogInformation(
            "Polling message watcher initialized with interval {IntervalMs}ms",
            _options.AzureBlobOptions.PollingIntervalMs);
    }

    /// <inheritdoc />
    public async Task StartWatchingAsync(string topic, Func<IAgentMessage, Task> handler, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ArgumentNullException.ThrowIfNull(handler);

        if (_watchedTopics.ContainsKey(topic))
        {
            _logger.LogWarning("Topic {Topic} is already being watched", topic);
            return;
        }

        var registration = new WatchRegistration
        {
            Topic = topic,
            Handler = handler,
            CancellationToken = ct
        };

        if (!_watchedTopics.TryAdd(topic, registration))
        {
            throw new InvalidOperationException($"Failed to register watcher for topic {topic}");
        }

        _lastProcessedTimestamp.TryAdd(topic, DateTimeOffset.UtcNow);

        _logger.LogInformation("Started watching topic {Topic} with polling interval {IntervalMs}ms",
            topic, _options.AzureBlobOptions.PollingIntervalMs);

        // Start background polling task for this topic
        _ = PollAndDeliverAsync(topic, ct);

        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task StopWatchingAsync(string topic)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);

        _watchedTopics.TryRemove(topic, out _);
        _lastProcessedTimestamp.TryRemove(topic, out _);

        _logger.LogInformation("Stopped watching topic {Topic}", topic);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<(bool IsHealthy, string DiagnosticMessage)> GetHealthAsync()
    {
        try
        {
            var (storeHealthy, storeDiagnostics) = await _messageStore.ValidateHealthAsync().ConfigureAwait(false);
            return (storeHealthy, $"Polling watcher healthy. Monitoring {_watchedTopics.Count} topics. Store: {storeDiagnostics}");
        }
        catch (Exception ex)
        {
            return (false, $"Health check failed: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public int ActiveWatchCount => _watchedTopics.Count;

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _logger.LogInformation("Shutting down polling message watcher");

        _shutdownCts.Cancel();
        _shutdownCts.Dispose();

        _watchedTopics.Clear();
        _lastProcessedTimestamp.Clear();

        await Task.CompletedTask.ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Polls for new messages on the topic and delivers them.
    /// </summary>
    private async Task PollAndDeliverAsync(string topic, CancellationToken ct)
    {
        var pollingInterval = TimeSpan.FromMilliseconds(_options.AzureBlobOptions.PollingIntervalMs);

        try
        {
            while (!ct.IsCancellationRequested && !_shutdownCts.Token.IsCancellationRequested)
            {
                try
                {
                    // Query pending messages for this topic
                    var query = new PendingMessageQuery
                    {
                        TopicPattern = topic,
                        MaxAgeHours = null,
                        PageSize = 100,
                        SortByPriority = true
                    };

                    var messages = await _messageStore.QueryMessagesAsync(query, ct).ConfigureAwait(false);

                    // Deliver messages that are newer than last processed
                    if (_watchedTopics.TryGetValue(topic, out var registration) && registration.Handler is not null)
                    {
                        var lastProcessed = _lastProcessedTimestamp.GetOrAdd(topic, DateTimeOffset.UtcNow);

                        foreach (var message in messages.Where(m => m.Metadata.PublishedAt > lastProcessed))
                        {
                            try
                            {
                                await registration.Handler(message).ConfigureAwait(false);
                                _lastProcessedTimestamp[topic] = message.Metadata.PublishedAt;

                                _logger.LogDebug(
                                    "Delivered message {MessageId} from topic {Topic}",
                                    message.MessageId, topic);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex,
                                    "Handler failed for message {MessageId}",
                                    message.MessageId);
                            }
                        }
                    }

                    // Wait for next polling cycle
                    await Task.Delay(pollingInterval, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Error polling topic {Topic}",
                        topic);

                    // Back off on error
                    await Task.Delay(TimeSpan.FromSeconds(1), ct).ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Polling watcher failed for topic {Topic}",
                topic);
        }
    }

    /// <summary>
    /// Internal watch registration.
    /// </summary>
    private class WatchRegistration
    {
        public string? Topic { get; init; }
        public Func<IAgentMessage, Task>? Handler { get; init; }
        public CancellationToken CancellationToken { get; init; }
    }
}
