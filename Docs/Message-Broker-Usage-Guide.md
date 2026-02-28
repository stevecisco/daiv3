# Message Broker Phase 2 - Usage Guide and Examples

## Overview

Phase 2 of AST-REQ-004 extends the message broker with Azure Blob Storage backend support for distributed agent scenarios. This guide demonstrates how to use both FileSystem and AzureBlob backends.

## Configuration

### FileSystem Backend (Local/Single Machine)

```json
{
  "MessageBroker": {
    "StorageBackend": "FileSystem",
    "RetentionDaysCompleted": 7,
    "RetentionDaysFailed": 30,
    "MaxMessageSizeBytes": 5242880,
    "FileSystemOptions": {
      "StorageDirectory": "%LOCALAPPDATA%\\Daiv3\\messages",
      "CleanupIntervalSeconds": 3600
    }
  }
}
```

### Azure Blob Storage Backend (Distributed/Cloud)

```json
{
  "MessageBroker": {
    "StorageBackend": "AzureBlob",
    "RetentionDaysCompleted": 7,
    "RetentionDaysFailed": 30,
    "MaxMessageSizeBytes": 5242880,
    "AzureBlobOptions": {
      "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=...;EndpointSuffix=core.windows.net",
      "ContainerName": "agent-messages",
      "PollingIntervalMs": 1000
    }
  }
}
```

**Connection String Options:**
- **Connection String** (recommended for dev/test): Include AccountName and AccountKey
- **Managed Identity** (recommended for production): Leave ConnectionString empty and use Azure.Identity.DefaultAzureCredential

## Dependency Injection Registration

### With Configuration

```csharp
// In Program.cs or startup configuration
var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

services.AddMessageBroker(configuration);
```

### With Default Options

```csharp
// Uses default FileSystem backend
services.AddMessageBroker();
```

## Core APIs

### Publishing Messages

```csharp
// Inject IMessageBroker
public class MyService
{
    private readonly IMessageBroker _messageBroker;

    public MyService(IMessageBroker messageBroker)
    {
        _messageBroker = messageBroker;
    }

    public async Task PublishTaskCompletionAsync()
    {
        var message = new AgentMessage<TaskResult>(
            topic: "task-execution/agent-123",
            senderAgentId: "agent-123",
            payload: new TaskResult { Success = true, Output = "Task completed" }
        );

        await _messageBroker.PublishAsync(message);
    }
}
```

### Subscribing to Messages

```csharp
// Subscribe to a specific topic
var subscriptionId = await _messageBroker.SubscribeAsync<TaskResult>(
    topic: "task-execution/*",
    handler: async message =>
    {
        Console.WriteLine($"Received message from {message.SenderAgentId}");
        var result = (TaskResult)message.Payload!;
        await ProcessResultAsync(result);
    }
);

// Later: unsubscribe
await _messageBroker.UnsubscribeAsync(subscriptionId);
```

### Querying Pending Messages

```csharp
// Get pending messages with filters
var query = new PendingMessageQuery
{
    TopicPattern = "task-execution/*",
    SenderAgentId = "agent-123",
    MaxAgeHours = 24,
    PageSize = 100,
    SortByPriority = true
};

var messages = await _messageBroker.GetPendingMessagesAsync(query);

foreach (var message in messages)
{
    Console.WriteLine($"Message {message.MessageId}: {message.Topic}");
}
```

### Message Lifecycle Management

```csharp
// Mark message as processed
await _messageBroker.MarkProcessedAsync(messageId);

// Mark message as failed with error reason
await _messageBroker.MarkFailedAsync(messageId, "Connection timeout");

// Get a specific message
var message = await _messageBroker.GetMessageAsync(messageId);

// Get message count for a topic
var count = await _messageBroker.GetMessageCountAsync("task-execution/*");

// Cleanup expired messages
var deletedCount = await _messageBroker.CleanupExpiredMessagesAsync();

// Check broker health
var (isHealthy, diagnostics) = await _messageBroker.GetHealthAsync();
Console.WriteLine($"Health: {isHealthy}, Details: {diagnostics}");
```

## Message Topics Convention

Standard topic patterns used throughout the system:

| Topic Pattern | Purpose | Example |
|---|---|---|
| `task-execution/{agentId}` | Agent task completion/progress | `task-execution/agent-a1` |
| `knowledge-update/{domain}` | Knowledge graph updates | `knowledge-update/documents` |
| `agent-execution/{agentId}` | Agent execution status | `agent-execution/research-agent` |
| `cross-agent-share/{projectId}` | Inter-agent data sharing | `cross-agent-share/project-x1` |
| `skill-invocation/{skillName}` | Skill execution results | `skill-invocation/web-search` |

Wildcard patterns:
- `task-execution/*` - All task completion messages
- `*` - All messages (all topics)

## Advanced: Message Metadata

Every message carries metadata for correlation and lifecycle tracking:

```csharp
var message = new AgentMessage<string>(
    topic: "task-execution/agent-1",
    senderAgentId: "agent-1",
    payload: "Task complete",
    metadata: new MessageMetadata
    {
        CorrelationId = correlationId, // Link related messages
        ReplyToTopic = "results/agent-2", // For request/reply patterns
        Priority = 10, // 0-100, higher = more urgent
        ExpiresAt = DateTimeOffset.UtcNow.AddHours(1), // Auto-cleanup after expiration
        Tags = new Dictionary<string, string>
        {
            { "task-type", "analysis" },
            { "status", "completed" }
        }
    }
);

await _messageBroker.PublishAsync(message);
```

## Request/Reply Pattern

```csharp
// Agent A sends request and waits for reply
var correlationId = Guid.NewGuid().ToString();
var request = new AgentMessage<AnalysisRequest>(
    topic: "service/request",
    senderAgentId: "agent-a",
    payload: new AnalysisRequest { Data = "analyze this" },
    metadata: new MessageMetadata
    {
        CorrelationId = correlationId,
        ReplyToTopic = "service/reply/agent-a"
    }
);

await _messageBroker.PublishAsync(request);

// Agent B receives request and sends reply
var subscriptionId = await _messageBroker.SubscribeAsync<AnalysisRequest>(
    "service/request",
    async message =>
    {
        var result = PerformAnalysis(message.Payload);
        
        var reply = new AgentMessage<AnalysisResult>(
            topic: message.Metadata.ReplyToTopic!,
            senderAgentId: "agent-b",
            payload: result,
            metadata: new MessageMetadata 
            { 
                CorrelationId = message.Metadata.CorrelationId 
            }
        );
        
        await _messageBroker.PublishAsync(reply);
    }
);

// Agent A waits for reply
var reply = await WaitForReplyAsync(correlationId, timeout: TimeSpan.FromSeconds(30));
```

## Storage Backend Comparison

### FileSystem Backend (Phase 1)

**Pros:**
- No network latency
- No Azure credentials required
- Great for development and testing
- Full control over message storage location
- Message files are human-readable JSON

**Cons:**
- Single-machine only
- No built-in replication
- Storage limited to local disk space
- Requires file system permissions

**Use Cases:**
- Development and testing
- Single-machine deployments
- On-premises installations
- High-throughput local communication

### Azure Blob Storage Backend (Phase 2)

**Pros:**
- Globally scalable
- Support for millions of messages
- Cloud-native architecture
- Can be combined with knowledge storage
- Managed identity support (no credentials in code)
- Versioning and audit trails (via blob snapshots)

**Cons:**
- Network latency (typically 10-100ms)
- Azure subscription required
- Polling overhead for message detection
- Storage costs (~$0.018/GB-month)

**Use Cases:**
- Distributed agent systems
- Cloud deployments (Azure VMs, ACI)
- Multi-region scenarios
- Enterprise deployments with audit requirements

## Testing Message Broker

### Unit Testing with Mocks

```csharp
[Fact]
public async Task MyService_PublishesMessageOnCompletion()
{
    // Arrange
    var mockBroker = new Mock<IMessageBroker>();
    var service = new MyService(mockBroker.Object);

    // Act
    await service.CompleteTaskAsync();

    // Assert
    mockBroker.Verify(
        x => x.PublishAsync(
            It.Is<AgentMessage<TaskResult>>(m => m.SenderAgentId == "my-agent"),
            It.IsAny<CancellationToken>()),
        Times.Once);
}
```

### Integration Testing with FileSystem

```csharp
[Fact]
public async Task MessageBroker_FileSystem_RoundTrip()
{
    // Arrange
    var testStorageDir = Path.Combine(Path.GetTempPath(), $"msg-test-{Guid.NewGuid()}");
    Directory.CreateDirectory(testStorageDir);
    
    var options = new FileSystemMessageStoreOptions { StorageDirectory = testStorageDir };
    var store = new FileSystemMessageStore(
        LoggerFactory.Create(b => b.AddConsole()).CreateLogger<FileSystemMessageStore>(),
        Options.Create(options));
    
    // Act
    var message = new AgentMessage<string>("test-topic", "agent-1", "payload");
    await store.SaveMessageAsync(message);
    var loaded = await store.LoadMessageAsync(message.MessageId);

    // Assert
    Assert.NotNull(loaded);
    Assert.Equal(message.MessageId, loaded.MessageId);
    
    // Cleanup
    Directory.Delete(testStorageDir, recursive: true);
}
```

## Monitoring and Debugging

### Check Message Count

```csharp
var taskExecutionCount = await _messageBroker.GetMessageCountAsync("task-execution/*");
var agentExecutionCount = await _messageBroker.GetMessageCountAsync("agent-execution/*");

Console.WriteLine($"Pending task messages: {taskExecutionCount}");
Console.WriteLine($"Pending agent messages: {agentExecutionCount}");
```

### Health Check

```csharp
var (isHealthy, diagnostics) = await _messageBroker.GetHealthAsync();

if (!isHealthy)
{
    Console.WriteLine($"Message broker unhealthy: {diagnostics}");
    // Log alert, notify monitoring system, etc.
}
```

### Cleanup Old Messages

```csharp
// Run cleanup (normally scheduled via background service)
var deletedCount = await _messageBroker.CleanupExpiredMessagesAsync();
Console.WriteLine($"Cleaned up {deletedCount} expired/old messages");
```

## Performance Considerations

### FileSystem Backend
- **Throughput**: 100-1,000 messages/sec (SSD dependent)
- **Latency**: Sub-millisecond
- **Scalability**: Limited by disk I/O and available disk space

### Azure Blob Backend
- **Throughput**: Limited by Azure account scalability (20,000 requests/sec target)
- **Latency**: 10-100ms (network dependent)
- **Scalability**: Azure Storage limits (~500TB per account)

### Optimization Tips
1. **Use topic patterns** to filter queries (reduces full scans)
2. **Set appropriate retention policies** to keep storage clean
3. **Monitor message counts** and archive old messages
4. **Use priority tags** for important messages
5. **Batch message publishing** when possible

## Troubleshooting

### FileSystem: Messages Not Found
- Check `StorageDirectory` exists and is writable
- Verify topic directory structure: `{StorageDirectory}/{Topic}/{MessageId:N}.json`
- Check file permissions

### Azure Blob: Connection Timeout
- Verify connection string is correct
- Check network connectivity to Azure
- Confirm storage account exists and container is created
- Check Azure identity/SAS token permissions

### Messages Not Being Delivered
- Verify subscription topic pattern (wildcards supported)
- Check message status (must be `Pending`)
- Verify message hasn't expired
- Check message size (max 5MB by default)

## Migration Path

**From Phase 1 (FileSystem) to Phase 2 (AzureBlob):**

1. **Deploy with Azure Blob configuration**
   - New messages go to Azure Blob
   - Set retention policy to keep FileSystem messages a few days

2. **Archive FileSystem messages** (optional)
   - Export old messages from FileSystem for audit trail
   - Delete FileSystem messages after retention period

3. **Monitor dual storage** during transition
   - Verify Azure Blob messages are being published and received
   - Check performance and costs

4. **Switch to 100% Azure Blob**
   - Update configuration to remove FileSystem options
   - Clear old FileSystem storage directory

See [Deployment Guide](../Deployment-Guide.md) for details.

## References

- [AST-REQ-004 Specification](../Requirements/Reqs/AST-REQ-004.md)
- [Architecture Decision: Azure Blob Messaging](../Requirements/Architecture/decisions/ADD-20260228-azure-blob-messaging.md)
- [Message Broker API Reference](IMessageBroker.md)
- [Azure Storage Documentation](https://learn.microsoft.com/en-us/azure/storage/)
