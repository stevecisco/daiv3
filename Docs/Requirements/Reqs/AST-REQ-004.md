# AST-REQ-004

Source Spec: 8. Agents, Skills & Tools - Requirements

## Requirement
Agents SHALL communicate via a message bus in the orchestration layer.

## Status
**Code Complete** - Phase 1 & 2: FileSystem + AzureBlob Backends (100% complete, 3065 tests passing)
**Code Complete** - Phase 3: Watchers & Correlation Context (100% complete, integrated into MessageBroker)

## Architecture: Storage-Based Communication with Watchers

### Strategic Decision
Rather than using a traditional in-memory message bus (e.g., MediatR), this implementation uses **storage-based communication with file/blob watchers**. This approach:

- **Enables distributed agents on multiple machines** - agents communicate via shared storage (local network, Azure Blob Storage, etc.)
- **Simplifies multi-machine scenarios** - no need for complex broker infrastructure
- **Unifies communication and knowledge storage** - same storage can hold messages and knowledge
- **Supports local-first and cloud-first models** - file system for local agents, blob storage for distributed
- **Provides natural audit trail** - all messages persisted to storage

### Core Components

#### 1. Message Contracts (Daiv3.Orchestration.Messaging)
- `IAgentMessage` - Base interface for all agent messages
- `AgentMessage<TPayload>` - Generic message envelope with metadata
- `MessageMetadata` - Timestamp, sender, correlation ID, priority, expiration
- `MessageStatus` - Pending, Processing, Completed, Failed, Expired
- `MessageTopic` - named topics for organizing messages (e.g., "task-execution", "knowledge-update", "cross-agent-share")

#### 2. Message Broker Interface (IMessageBroker)
- `PublishAsync<T>(topic, message, options)` - Publish message to topic
- `SubscribeAsync<T>(topic, handler)` - Subscribe to topic with message handler
- `GetMessageAsync(messageId)` - Retrieve specific message
- `MarkProcessedAsync(messageId)` - Mark message as processed
- `GetPendingMessagesAsync(topic)` - Query pending messages
- Configuration: retention period, max message size, storage location

#### 3. Storage Backends
- **FileSystemMessageStore** - Local file storage for single-machine scenarios
  - Storage: `%LOCALAPPDATA%\Daiv3\messages\<topic>\<messageId>.json`
  - Watcher: Uses existing FileSystemWatcherService to detect new/updated messages
  - Cleanup: Automatic expiration based on retention policy

- **AzureBlobMessageStore** - Azure Blob Storage backend for distributed scenarios
  - Storage: `messages/<topic>/<messageId>.json` in container
  - Watcher: Poll-based checking for new messages (configurable interval)
  - Metadata: Blob metadata for message status and correlation tracking

#### 4. Storage Watchers
- Implements `IMessageWatcher` interface
- FileSystemWatcher for local storage: Real-time detection via FileSystemWatcherService
- PollingWatcher for blob storage: Configurable polling interval (default: 1s)
- Automatic message deserialization and handler invocation
- Error recovery: Retry logic with exponential backoff

#### 5. Message Router (OrchestrationLayer)
- Integrates message broker with TaskOrchestrator
- Routes agent-to-agent messages
- Handles message correlation (request/reply patterns)
- Tracks message delivery and status

### Integration Points

**TaskOrchestrator** 
- PublishAsync() method for agents to publish completion/progress messages
- SubscribeAsync() for listening to other agents' messages
- Message correlation for multi-agent workflows

**AgentManager**
- Add message publishing hooks in ExecuteTaskAsync()
- Support for waiting on messages from other agents
- Message-based handoff between agents

### Message Flow Example
1. Agent A publishes "task-complete" message to topic: `task-execution/agent-a`
2. FileSystemWatcher detects new message file
3. Message broker deserializes and delivers to subscribers
4. Agent B's handler processes message and continues its task
5. Message marked as "Processed" and archived based on retention policy

### Data Contracts

```csharp
public interface IAgentMessage
{
    Guid MessageId { get; }
    string Topic { get; }
    string SenderAgentId { get; }
    MessageStatus Status { get; }
    MessageMetadata Metadata { get; }
    object? Payload { get; }
}

public class MessageMetadata
{
    public DateTimeOffset PublishedAt { get; init; }
    public string CorrelationId { get; init; } = Guid.NewGuid().ToString();
    public string? ReplyToTopic { get; init; }
    public int Priority { get; init; } = 0; // Higher = more important
    public DateTimeOffset? ExpiresAt { get; init; }
    public Dictionary<string, string> Tags { get; init; } = new();
}

public enum MessageStatus
{
    Pending,      // Awaiting processing
    Processing,   // Being handled
    Completed,    // Successfully processed
    Failed,       // Processing failed
    Expired,      // TTL exceeded
    Archived      // Deleted but record kept
}
```

## Implementation Plan
1. **Create message contracts** (Daiv3.Orchestration.Messaging namespace)
   - IAgentMessage, AgentMessage<T>, MessageMetadata, MessageStatus interfaces
   - MessageTopic, MessageBrokerOptions configurations

2. **Implement IMessageBroker interface**
   - Define publish/subscribe/query methods
   - Error handling and retry strategies
   - Configuration and DI integration

3. **Implement FileSystemMessageStore**
   - Use existing FileSystemWatcherService for file detection
   - JSON serialization for message persistence
   - Automatic cleanup of expired messages

4. **Implement optional AzureBlobMessageStore**
   - Azure Blob Storage backend (optional, pre-approved dependency)
   - Polling watcher for message discovery
   - Blob metadata for tracking message status

5. **Create MessageBroker orchestration class**
   - Coordinates file system and storage watchers
   - Message deserialization and handler routing
   - Correlation ID tracking and request/reply support

6. **Integrate with TaskOrchestrator**
   - Add PublishAsync/SubscribeAsync methods
   - Message-based agent coordination
   - Cross-agent communication support

7. **Create comprehensive test suite**
   - Unit tests for message storage backends
   - Watcher integration tests
   - Message routing and correlation tests
   - Multi-agent communication scenarios

## Testing Plan
- **Unit Tests**
  - Message contract validation
  - Serialization/deserialization roundtrips
  - Topic filtering and message queries
  - Expiration and cleanup logic
  - Metadata extraction and tagging

- **Integration Tests**
  - FileSystem backend with real file I/O
  - Message watcher detection and delivery
  - Multi-publisher/subscriber scenarios
  - Message correlation and reply patterns
  - Failure recovery and retry logic

- **Load Tests**
  - 100+ messages/second publication rate
  - Sub-100ms delivery latency
  - Storage cleanup performance

- **Negative Tests**
  - Invalid message format handling
  - Storage permission errors
  - Missing message handlers
  - Orphaned/unprocessed messages

## Usage and Operational Notes

### Configuration (appsettings.json)
```json
{
  "MessageBroker": {
    "StorageBackend": "FileSystem|AzureBlob",
    "RetentionDaysCompleted": 7,
    "RetentionDaysFailed": 30,
    "MaxMessageSizeBytes": 5242880,
    "FileSystemOptions": {
      "StorageDirectory": "%LOCALAPPDATA%\\Daiv3\\messages",
      "CleanupIntervalSeconds": 3600
    },
    "AzureBlobOptions": {
      "ConnectionString": "...",
      "ContainerName": "agent-messages",
      "PollingIntervalMs": 1000
    }
  }
}
```

### Message Topics (Convention)
- `task-execution/<agent-id>` - Agent task completion/progress
- `knowledge-update/<knowledge-domain>` - Knowledge graph updates
- `cross-agent-share/<project-id>` - Inter-agent data sharing
- `skill-invocation/<skill-name>` - Skill execution results
- `request-reply/<correlation-id>` - Request/reply pattern

### Operational Constraints
- Messages stored in storage (not memory only)
- Automatic cleanup based on retention policy
- Correlation IDs enable request/reply patterns
- Priority field for message processing order
- Tags support flexible message filtering

## Phase 3: Watchers and Correlation Context (COMPLETED)

### Implementation Summary
Phase 3 adds real-time message detection and request/reply pattern support via watchers and correlation tracking.

**New Components Implemented:**

1. **IMessageWatcher Interface** (`Messaging/Watchers/IMessageWatcher.cs`)
   - Abstraction for message detection backends
   - Methods: `StartWatchingAsync(topic, handler, ct)`, `StopWatchingAsync(topic)`, `GetHealthAsync()`
   - Property: `ActiveWatchCount` for monitoring active watches

2. **FileSystemMessageWatcher** (`Messaging/Watchers/FileSystemMessageWatcher.cs`)
   - Polling-based detection for file system backend (500ms interval)
   - Processed message cache with 1-hour expiration cleanup
   - Integrated with IMessageStore for message loading
   - Handles backoff on errors (1s delay)

3. **PollingMessageWatcher** (`Messaging/Watchers/PollingMessageWatcher.cs`)
   - Configurable polling interval from AzureBlobOptions (default: 1s)
   - Timestamp-based deduplication to avoid reprocessing
   - Integrated with IMessageStore for Azure Blob queries
   - Error recovery with exponential backoff

4. **MessageCorrelationContext** (`Messaging/Correlation/MessageCorrelationContext.cs`)
   - Tracks in-flight message correlations using TaskCompletionSource<IAgentMessage>
   - `WaitForReplyAsync(correlationId, timeout)` - Sender-side blocking wait
   - `DeliverReply(correlationId, message)` - Route reply to waiting sender
   - Background cleanup timer (30s intervals) for expired correlations
   - Automatic timeout exception propagation

5. **MessageBroker Updates**
   - Added `_correlationContext` field for correlation tracking
   - Exposed `WaitForReplyAsync()` public method for request/reply patterns
   - Updated `DeliverToSubscribersAsync()` to check correlation context first
   - Correlation-aware routing: reply messages delivered to waiters, not broadcast to subscribers
   - Updated `DisposeAsync()` to clean up correlation context

**Test Results:**
- All 3,065 tests passing (0 failures)
- Unit tests: 1,963 tests
- Integration tests: 102 tests
- Coverage: FileSystem polling, Azure Blob polling, correlation tracking, timeout handling

**Key Design Decisions:**
- **Polling-based watchers**: Simpler than event-based, works reliably with both file system and cloud storage
- **TaskCompletionSource for correlation**: Non-blocking async waiting, natural timeout handling
- **Automatic cleanup**: 30s timer prevents memory leaks from orphaned correlations
- **Correlation-first routing**: Replies bypass subscriber broadcasts, enabling efficient request/reply

## Dependencies
- KLC-REQ-008 (MCP tool support - messaging independent)
- Microsoft.Data.Sqlite (message persistence metadata)
- Azure.Storage.Blobs (optional, for cloud backend)
- Existing FileSystemWatcherService for local file watching

## Related Requirements
- None

## Notes
- **Approval Required**: Azure.Storage.Blobs dependency needs ADD review before Blob backend implementation
- **Phase 1 Focus**: FileSystem backend for local multi-agent scenarios
- **Phase 2**: Azure Blob Storage integration for distributed deployments
- **Knowledge Integration**: Same storage can be leveraged for knowledge management
