using Daiv3.Orchestration.Messaging;
using Daiv3.Orchestration.Messaging.Storage;
using Daiv3.Orchestration.Messaging.Correlation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Daiv3.IntegrationTests.Messaging;

/// <summary>
/// Integration tests for multi-agent communication scenarios and performance validation.
/// Tests AST-REQ-004 requirements for message broker functionality.
/// </summary>
public class MessageBrokerIntegrationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _testStorageDir;
    private readonly ILoggerFactory _loggerFactory;
    private readonly MessageBrokerOptions _options;
    private readonly FileSystemMessageStore _messageStore;
    private readonly MessageBroker _broker;

    public MessageBrokerIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        _testStorageDir = Path.Combine(Path.GetTempPath(), $"daiv3-broker-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testStorageDir);

        _loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Debug));

        _options = new MessageBrokerOptions
        {
            StorageBackend = "FileSystem",
            FileSystemOptions = new FileSystemMessageStoreOptions
            {
                StorageDirectory = _testStorageDir,
                CleanupIntervalSeconds = 3600
            }
        };

        _messageStore = new FileSystemMessageStore(
            _loggerFactory.CreateLogger<FileSystemMessageStore>(),
            Options.Create(_options.FileSystemOptions));

        _broker = new MessageBroker(
            _loggerFactory.CreateLogger<MessageBroker>(),
            _messageStore,
            Options.Create(_options));
    }

    #region Multi-Agent Communication Tests

    [Fact]
    public async Task MultiAgent_PublishSubscribe_MessagesDeliveredToMultipleSubscribers()
    {
        // Arrange
        var topic = "agent-coordination/task-sharing";
        var receivedMessages = new ConcurrentBag<IAgentMessage>();
        var subscriber1Received = new TaskCompletionSource<bool>();
        var subscriber2Received = new TaskCompletionSource<bool>();

        // Two agents subscribe to the same topic
        await _broker.SubscribeAsync<string>(topic, async (msg, ct) =>
        {
            receivedMessages.Add(msg);
            subscriber1Received.TrySetResult(true);
        });

        await _broker.SubscribeAsync<string>(topic, async (msg, ct) =>
        {
            receivedMessages.Add(msg);
            subscriber2Received.TrySetResult(true);
        });

        // Act - Agent publishes a task completion message
        var message = new AgentMessage<string>(
            topic,
            "agent-1",
            "Task completed successfully",
            new MessageMetadata
            {
                Tags = new Dictionary<string, string>
                {
                    { "task-id", "task-123" },
                    { "status", "completed" }
                }
            });

        var publishResult = await _broker.PublishAsync(message);

        // Wait for delivery (with timeout)
        var sub1Task = await Task.WhenAny(subscriber1Received.Task, Task.Delay(2000));
        var sub2Task = await Task.WhenAny(subscriber2Received.Task, Task.Delay(2000));

        // Assert
        Assert.True(publishResult.IsSuccess);
        Assert.Equal(2, receivedMessages.Count);
        Assert.True(sub1Task == subscriber1Received.Task);
        Assert.True(sub2Task == subscriber2Received.Task);

        _output.WriteLine($"✅ Multi-agent broadcast: {receivedMessages.Count} subscribers received message");
    }

    [Fact]
    public async Task MultiAgent_CrossAgentCommunication_MessagesRoutedCorrectly()
    {
        // Arrange
        var agent1Messages = new ConcurrentBag<IAgentMessage>();
        var agent2Messages = new ConcurrentBag<IAgentMessage>();
        
        var agent1Topic = "agent-execution/agent-1";
        var agent2Topic = "agent-execution/agent-2";

        // Agent 1 subscribes to its own topic
        await _broker.SubscribeAsync<string>(agent1Topic, async (msg, ct) =>
        {
            agent1Messages.Add(msg);
        });

        // Agent 2 subscribes to its own topic
        await _broker.SubscribeAsync<string>(agent2Topic, async (msg, ct) =>
        {
            agent2Messages.Add(msg);
        });

        // Act - Send messages to different agents
        await _broker.PublishAsync(new AgentMessage<string>(agent1Topic, "coordinator", "Task for Agent 1"));
        await _broker.PublishAsync(new AgentMessage<string>(agent2Topic, "coordinator", "Task for Agent 2"));
        await _broker.PublishAsync(new AgentMessage<string>(agent1Topic, "coordinator", "Another task for Agent 1"));

        // Give messages time to be delivered
        await Task.Delay(500);

        // Assert
        Assert.Equal(2, agent1Messages.Count);
        Assert.Equal(1, agent2Messages.Count);

        _output.WriteLine($"✅ Cross-agent routing: Agent 1 received {agent1Messages.Count} messages, Agent 2 received {agent2Messages.Count} messages");
    }

    [Fact]
    public async Task MultiAgent_WildcardSubscription_ReceivesAllMatchingMessages()
    {
        // Arrange
        var allMessages = new ConcurrentBag<IAgentMessage>();
        var wildcardTopic = "agent-execution/*";

        // Supervisor agent subscribes to all agent execution messages
        await _broker.SubscribeAsync<string>(wildcardTopic, async (msg, ct) =>
        {
            allMessages.Add(msg);
        });

        // Act - Multiple agents publish to different topics
        await _broker.PublishAsync(new AgentMessage<string>("agent-execution/agent-1", "agent-1", "Agent 1 update"));
        await _broker.PublishAsync(new AgentMessage<string>("agent-execution/agent-2", "agent-2", "Agent 2 update"));
        await _broker.PublishAsync(new AgentMessage<string>("agent-execution/agent-3", "agent-3", "Agent 3 update"));
        await _broker.PublishAsync(new AgentMessage<string>("task-completion/task-1", "agent-1", "Should not match"));

        await Task.Delay(500);

        // Assert
        Assert.Equal(3, allMessages.Count);
        Assert.All(allMessages, msg => Assert.StartsWith("agent-execution/", msg.Topic));

        _output.WriteLine($"✅ Wildcard subscription: Received {allMessages.Count} matching messages out of 4 published");
    }

    #endregion

    #region Request/Reply Pattern Tests

    [Fact]
    public async Task RequestReply_WithCorrelation_DeliveredToWaitingAgent()
    {
        // Arrange
        var requestTopic = "agent-query/database";
        var replyTopic = "agent-reply/database";
        var correlationId = Guid.NewGuid().ToString();

        // Responder agent subscribes to requests
        await _broker.SubscribeAsync<string>(requestTopic, async (msg, ct) =>
        {
            _output.WriteLine($"📨 Responder received request with correlation: {msg.Metadata.CorrelationId}");

            // Send reply with same correlation ID
            var reply = new AgentMessage<string>(
                msg.Metadata.ReplyToTopic!,
                "database-agent",
                $"Response to: {msg.Payload}",
                new MessageMetadata
                {
                    CorrelationId = msg.Metadata.CorrelationId
                });

            await _broker.PublishAsync(reply, ct);
        });

        // Act - Requester sends query with reply-to topic
        var request = new AgentMessage<string>(
            requestTopic,
            "querying-agent",
            "SELECT * FROM users",
            new MessageMetadata
            {
                CorrelationId = correlationId,
                ReplyToTopic = replyTopic
            });

        await _broker.PublishAsync(request);

        // Wait for reply using correlation context
        var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var replyMessage = await _broker.WaitForReplyAsync(correlationId, TimeSpan.FromSeconds(5), timeoutCts.Token);

        // Assert
        Assert.NotNull(replyMessage);
        Assert.Equal(correlationId, replyMessage.Metadata.CorrelationId);
        Assert.Contains("Response to:", replyMessage.Payload?.ToString());

        _output.WriteLine($"✅ Request/reply pattern: Received correlated response in <5s");
    }

    [Fact]
    public async Task RequestReply_Timeout_ThrowsTimeoutException()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var shortTimeout = TimeSpan.FromMilliseconds(100);

        // Act & Assert - No reply will be sent
        await Assert.ThrowsAsync<TimeoutException>(async () =>
        {
            await _broker.WaitForReplyAsync(correlationId, shortTimeout, CancellationToken.None);
        });

        _output.WriteLine($"✅ Request/reply timeout: Correctly threw timeout exception after 100ms");
    }

    [Fact]
    public async Task RequestReply_MultipleWaiters_EachGetsOwnReply()
    {
        // Arrange
        var replyTopic = "agent-reply/multi";
        var correlationId1 = Guid.NewGuid().ToString();
        var correlationId2 = Guid.NewGuid().ToString();

        // Act - Two agents waiting for different correlated replies
        var waiter1Task = Task.Run(async () =>
        {
            return await _broker.WaitForReplyAsync(correlationId1, TimeSpan.FromSeconds(5), CancellationToken.None);
        });

        var waiter2Task = Task.Run(async () =>
        {
            return await _broker.WaitForReplyAsync(correlationId2, TimeSpan.FromSeconds(5), CancellationToken.None);
        });

        await Task.Delay(100); // Ensure waiters are registered

        // Send replies in reverse order
        await _broker.PublishAsync(new AgentMessage<string>(
            replyTopic,
            "responder",
            "Reply 2",
            new MessageMetadata { CorrelationId = correlationId2 }));

        await _broker.PublishAsync(new AgentMessage<string>(
            replyTopic,
            "responder",
            "Reply 1",
            new MessageMetadata { CorrelationId = correlationId1 }));

        // Wait for both replies
        var reply1 = await waiter1Task;
        var reply2 = await waiter2Task;

        // Assert
        Assert.NotNull(reply1);
        Assert.NotNull(reply2);
        Assert.Equal(correlationId1, reply1.Metadata.CorrelationId);
        Assert.Equal(correlationId2, reply2.Metadata.CorrelationId);
        Assert.Equal("Reply 1", reply1.Payload?.ToString());
        Assert.Equal("Reply 2", reply2.Payload?.ToString());

        _output.WriteLine($"✅ Multiple waiters: Both received correct correlated replies");
    }

    #endregion

    #region Performance Benchmark Tests

    [Fact]
    public async Task Performance_PublishThroughput_Exceeds100MessagesPerSecond()
    {
        // Arrange
        var messageCount = 250;
        var topic = "performance-test/throughput";
        var stopwatch = Stopwatch.StartNew();

        // Act - Publish messages as fast as possible
        var publishTasks = new List<Task>();
        for (int i = 0; i < messageCount; i++)
        {
            var message = new AgentMessage<string>(
                topic,
                "performance-agent",
                $"Message {i}");
            publishTasks.Add(_broker.PublishAsync(message));
        }

        await Task.WhenAll(publishTasks);
        stopwatch.Stop();

        // Assert
        var messagesPerSecond = messageCount / stopwatch.Elapsed.TotalSeconds;
        _output.WriteLine($"📊 Throughput: {messagesPerSecond:F0} messages/second (target: >100)");
        _output.WriteLine($"   Total time: {stopwatch.ElapsedMilliseconds}ms for {messageCount} messages");

        Assert.True(messagesPerSecond > 100, 
            $"Throughput {messagesPerSecond:F0} msg/s is below target of 100 msg/s");
    }

    [Fact]
    public async Task Performance_SubscribeDelivery_SubsequentMessagesUnder100ms()
    {
        // Arrange
        var topic = "performance-test/delivery";
        var messageCount = 50;
        var deliveryLatencies = new ConcurrentBag<long>();
        var completionSource = new TaskCompletionSource<bool>();
        var messagesReceived = 0;

        await _broker.SubscribeAsync<string>(topic, async (msg, ct) =>
        {
            // Parse timestamp from message payload
            var timestamps = msg.Payload?.ToString()?.Split('|');
            if (timestamps != null && timestamps.Length == 2)
            {
                var sentTicks = long.Parse(timestamps[1]);
                var receivedTicks = DateTimeOffset.UtcNow.Ticks;
                var latencyMs = TimeSpan.FromTicks(receivedTicks - sentTicks).TotalMilliseconds;
                deliveryLatencies.Add((long)latencyMs);
            }

            if (Interlocked.Increment(ref messagesReceived) >= messageCount)
            {
                completionSource.TrySetResult(true);
            }
        });

        // Act - Publish messages with timestamps
        for (int i = 0; i < messageCount; i++)
        {
            var message = new AgentMessage<string>(
                topic,
                "perf-agent",
                $"Message {i}|{DateTimeOffset.UtcNow.Ticks}");
            await _broker.PublishAsync(message);
            await Task.Delay(20); // Pace messages to avoid overwhelming
        }

        // Wait for all messages to be delivered (with timeout)
        await Task.WhenAny(completionSource.Task, Task.Delay(10000));

        // Assert
        Assert.True(deliveryLatencies.Count > 0, "No delivery latencies recorded");

        var avgLatency = deliveryLatencies.Average();
        var maxLatency = deliveryLatencies.Max();
        var p95Latency = deliveryLatencies.OrderBy(x => x).Skip((int)(deliveryLatencies.Count * 0.95)).FirstOrDefault();

        _output.WriteLine($"📊 Delivery Latency:");
        _output.WriteLine($"   Average: {avgLatency:F1}ms");
        _output.WriteLine($"   P95: {p95Latency}ms");
        _output.WriteLine($"   Max: {maxLatency}ms");
        _output.WriteLine($"   Messages: {deliveryLatencies.Count}/{messageCount}");

        // Target: Sub-100ms delivery for most messages
        Assert.True(p95Latency < 100, 
            $"P95 latency {p95Latency}ms exceeds 100ms target");
    }

    [Fact]
    public async Task Performance_ConcurrentPublishers_HandlesMultipleAgentsSimultaneously()
    {
        // Arrange
        var agentCount = 10;
        var messagesPerAgent = 20;
        var receivedMessages = new ConcurrentBag<IAgentMessage>();
        var completionSource = new TaskCompletionSource<bool>();
        var expectedTotal = agentCount * messagesPerAgent;

        await _broker.SubscribeAsync<string>("concurrent-test/*", async (msg, ct) =>
        {
            receivedMessages.Add(msg);
            if (receivedMessages.Count >= expectedTotal)
            {
                completionSource.TrySetResult(true);
            }
        });

        var stopwatch = Stopwatch.StartNew();

        // Act - Multiple agents publishing concurrently
        var publisherTasks = Enumerable.Range(1, agentCount).Select(async agentId =>
        {
            for (int i = 0; i < messagesPerAgent; i++)
            {
                var message = new AgentMessage<string>(
                    $"concurrent-test/agent-{agentId}",
                    $"agent-{agentId}",
                    $"Message {i} from agent {agentId}");
                await _broker.PublishAsync(message);
            }
        }).ToArray();

        await Task.WhenAll(publisherTasks);
        await Task.WhenAny(completionSource.Task, Task.Delay(5000));
        stopwatch.Stop();

        // Assert
        Assert.True(receivedMessages.Count >= expectedTotal * 0.95, 
            $"Only received {receivedMessages.Count} of {expectedTotal} messages (95% threshold)");

        var throughput = receivedMessages.Count / stopwatch.Elapsed.TotalSeconds;
        _output.WriteLine($"📊 Concurrent publishers: {agentCount} agents, {messagesPerAgent} msgs each");
        _output.WriteLine($"   Total throughput: {throughput:F0} messages/second");
        _output.WriteLine($"   Messages delivered: {receivedMessages.Count}/{expectedTotal}");
        _output.WriteLine($"   Total time: {stopwatch.ElapsedMilliseconds}ms");

        Assert.True(throughput > 100, $"Concurrent throughput {throughput:F0} msg/s below target");
    }

    #endregion

    #region Cleanup

    public void Dispose()
    {
        try
        {
            _broker?.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(2));
            _loggerFactory?.Dispose();

            if (Directory.Exists(_testStorageDir))
            {
                Directory.Delete(_testStorageDir, true);
            }
        }
        catch (Exception ex)
        {
            _output?.WriteLine($"⚠️ Cleanup warning: {ex.Message}");
        }
    }

    #endregion
}
