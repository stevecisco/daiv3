using System.Collections.Concurrent;
using Daiv3.Orchestration.Messaging.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Daiv3.Orchestration.Messaging.Watchers;

/// <summary>
/// File system-based message watcher using polling.
/// Periodically checks the file system message store for new messages and delivers them to subscribers.
/// </summary>
public sealed class FileSystemMessageWatcher : IMessageWatcher
{
    private readonly ILogger<FileSystemMessageWatcher> _logger;
    private readonly IMessageStore _messageStore;
    private readonly MessageBrokerOptions _options;

    /// <summary>
    /// Maps topics to their watch registrations and handlers.
    /// </summary>
    private readonly ConcurrentDictionary<string, WatchRegistration> _watchedTopics = new();

    /// <summary>
    /// Dictionary to track the last processed timestamp for each topic.
    /// Used to avoid reprocessing messages.
    /// </summary>
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastProcessedTimestamp = new();

    /// <summary>
    /// Set of already-processed message IDs to avoid duplicate delivery.
    /// </summary>
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> _processedMessages = new();

    /// <summary>
    /// Cancellation token for coordinating shutdown.
    /// </summary>
    private CancellationTokenSource _shutdownCts = new();

    public FileSystemMessageWatcher(
        ILogger<FileSystemMessageWatcher> logger,
        IMessageStore messageStore,
        IOptions<MessageBrokerOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _messageStore = messageStore ?? throw new ArgumentNullException(nameof(messageStore));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

        _logger.LogInformation(
            "FileSystem message watcher initialized for storage directory {Dir}",
            _options.FileSystemOptions.StorageDirectory);
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

        _logger.LogInformation("Started watching topic {Topic}", topic);

        // Start background polling task for this topic
        _ = PollAndDeliverAsync(topic, ct);

        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task StopWatchingAsync(string topic)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);

        if (_watchedTopics.TryRemove(topic, out var registration))
        {
            _logger.LogInformation("Stopped watching topic {Topic}", topic);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<(bool IsHealthy, string DiagnosticMessage)> GetHealthAsync()
    {
        try
        {
            var storageDir = _options.FileSystemOptions.StorageDirectory;
            if (!Directory.Exists(storageDir))
            {
                return (false, $"Storage directory not found: {storageDir}");
            }

            var msgDirCount = Directory.GetDirectories(storageDir).Length;
            return (true, $"FileSystem watcher healthy. Monitoring {_watchedTopics.Count} topics, {msgDirCount} message directories");
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
        _logger.LogInformation("Shutting down FileSystem message watcher");

        _shutdownCts.Cancel();
        _shutdownCts.Dispose();

        _watchedTopics.Clear();
        _processedMessages.Clear();

        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <summary>
    /// Polls for new messages in the topic directory and delivers them.
    /// </summary>
    private async Task PollAndDeliverAsync(string topic, CancellationToken ct)
    {
        var topicPath = GetTopicPath(topic);

        try
        {
            // Ensure topic directory exists
            Directory.CreateDirectory(topicPath);

            while (!ct.IsCancellationRequested && !_shutdownCts.Token.IsCancellationRequested)
            {
                try
                {
                    // Get all JSON files in the topic directory
                    var files = Directory.GetFiles(topicPath, "*.json");

                    foreach (var filePath in files)
                    {
                        if (ct.IsCancellationRequested)
                            break;

                        try
                        {
                            var fileName = Path.GetFileNameWithoutExtension(filePath);
                            if (!Guid.TryParse(fileName, out var messageId))
                                continue;

                            // Skip if already processed recently
                            if (_processedMessages.ContainsKey(messageId))
                                continue;

                            // Load and deliver the message
                            var message = await _messageStore.LoadMessageAsync(messageId, ct).ConfigureAwait(false);
                            if (message != null && message.Status == MessageStatus.Pending)
                            {
                                if (_watchedTopics.TryGetValue(topic, out var registration) && registration.Handler is not null)
                                {
                                    await registration.Handler(message).ConfigureAwait(false);
                                    _processedMessages.TryAdd(messageId, DateTimeOffset.UtcNow);

                                    _logger.LogDebug(
                                        "Delivered message {MessageId} from topic {Topic}",
                                        messageId, topic);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex,
                                "Failed to process message file {FilePath}",
                                filePath);
                        }
                    }

                    // Cleanup old processed message entries (older than 1 hour)
                    CleanupOldProcessedMessages();

                    // Poll interval
                    await Task.Delay(500, ct).ConfigureAwait(false);
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
                    await Task.Delay(1000, ct).ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "FileSystem watcher failed for topic {Topic}",
                topic);
        }
    }

    /// <summary>
    /// Gets the file system path for storing messages of a topic.
    /// </summary>
    private string GetTopicPath(string topic)
    {
        var storageDir = _options.FileSystemOptions.StorageDirectory;
        return Path.Combine(storageDir, topic);
    }

    /// <summary>
    /// Removes old entries from the processed messages cache.
    /// </summary>
    private void CleanupOldProcessedMessages()
    {
        var cutoff = DateTimeOffset.UtcNow.AddHours(-1);
        var keysToRemove = _processedMessages
            .Where(kvp => kvp.Value < cutoff)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            _processedMessages.TryRemove(key, out _);
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
