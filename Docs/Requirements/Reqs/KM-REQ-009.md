# KM-REQ-009

Source Spec: 4. Knowledge Management & Indexing - Requirements

## Requirement
The system SHALL delete index entries when source files are removed.

## Implementation Summary
**Status:** Complete  
**Component:** `Daiv3.Knowledge`  
**Service:** `KnowledgeFileOrchestrationService`

The file deletion handling implementation provides automatic cleanup of index entries when source files are removed from watched directories. It includes:
- Orchestration service that connects file system monitoring to document processing
- Event-driven architecture for real-time deletion detection
- Handles file deletion, rename (delete old + create new), and modification events
- Proper error handling and statistics tracking
- Comprehensive test coverage (unit and integration)

### Key Components

1. **IKnowledgeFileOrchestrationService** - Interface defining orchestration contract
   - Methods: `StartAsync()`, `StopAsync()`, `GetStatistics()`
   - Properties: `IsRunning`
   - Lifecycle: `IAsyncDisposable` for proper cleanup

2. **KnowledgeFileOrchestrationService** - Production implementation
   - Subscribes to `IFileSystemWatcher.FileChanged` events
   - Routes events to appropriate document processor methods
   - Handles Created/Modified → `IKnowledgeDocumentProcessor.ProcessDocumentAsync()`
   - Handles Deleted → `IKnowledgeDocumentProcessor.RemoveDocumentAsync()`
   - Handles Renamed → Delete old + Process new
   - Thread-safe async event processing
   - Statistics tracking (files processed, deleted, errors)

3. **KnowledgeFileOrchestrationStatistics** - Statistics data class
   - `FilesProcessed`: Count of successfully processed files
   - `FilesDeleted`: Count of successfully deleted documents
   - `ProcessingErrors`: Count of processing failures
   - `DeletionErrors`: Count of deletion failures

4. **Integration with Existing Components**
   - Uses `IFileSystemWatcher` (KM-REQ-001) for file system monitoring
   - Uses `IKnowledgeDocumentProcessor.RemoveDocumentAsync()` for index cleanup
   - Registered in DI container via `KnowledgeServiceExtensions.AddKnowledgeLayer()`

## Implementation Details

### Event Processing Flow
1. FileSystemWatcher detects file deletion
2. FileChanged event raised with `FileChangeType.Deleted`
3. KnowledgeFileOrchestrationService receives event
4. Orchestration service calls `RemoveDocumentAsync(filePath)`
5. Document processor:
   - Looks up document by source path
   - Calls `IVectorStoreService.DeleteTopicAndChunksAsync(docId)` to remove embeddings
   - Calls `IDocumentRepository.DeleteAsync(docId)` to remove document record
6. Statistics updated (FilesDeleted or DeletionErrors)

### Error Handling
- All exceptions caught and logged at ERROR level
- Failed operations increment error counters
- Service continues processing other events
- No cascading failures

### Concurrency Control
- SemaphoreSlim ensures sequential event processing
- Prevents race conditions during file operations
- Each event processed to completion before next

### Lifecycle Management
- Service starts/stops independently
- Subscribes/unsubscribes from FileSystemWatcher events
- Proper async disposal pattern
- Waits for in-progress operations during shutdown

## Testing

### Unit Tests (21 tests, all passing)
**KnowledgeFileOrchestrationServiceTests**:
- Constructor validation (null checks for dependencies)
- Service lifecycle (start, stop, multiple operations)
- File creation event handling
- File modification event handling
- File deletion event handling
- File rename event handling (delete old + create new)
- Error handling (processing errors, deletion errors, exceptions)
- Statistics accuracy
- Disposal behavior
- State management (IsRunning)

### Integration Tests (9 tests, all passing)
**KnowledgeFileOrchestrationIntegrationTests**:
- Real file system operations with temp directories
- File creation triggers document processing
- File modification triggers reprocessing
- File deletion triggers document removal
- File rename triggers delete + process
- Multiple files processed independently
- Filtered file extensions (only matching patterns)
- Processing errors tracked in statistics
- Service stop prevents event processing

## Usage and Operational Notes

### Dependency Injection Setup
The orchestration service is automatically registered when adding the Knowledge Layer:
```csharp
services.AddKnowledgeLayer();

// Service is registered as Singleton
// IKnowledgeFileOrchestrationService -> KnowledgeFileOrchestrationService
```

### Programmatic Usage
```csharp
var orchestrationService = serviceProvider.GetRequiredService<IKnowledgeFileOrchestrationService>();

// Start monitoring and processing
await orchestrationService.StartAsync();

// Check statistics
var stats = orchestrationService.GetStatistics();
Console.WriteLine($"Files processed: {stats.FilesProcessed}");
Console.WriteLine($"Files deleted: {stats.FilesDeleted}");
Console.WriteLine($"Errors: {stats.ProcessingErrors + stats.DeletionErrors}");

// Stop monitoring
await orchestrationService.StopAsync();

// Cleanup
await orchestrationService.DisposeAsync();
```

### Integration with Application Lifecycle
```csharp
// In Program.cs or Startup
var orchestrationService = app.Services.GetRequiredService<IKnowledgeFileOrchestrationService>();
await orchestrationService.StartAsync();

// Service will now automatically:
// - Process new/modified files via document processor
// - Delete index entries when files are removed
// - Handle file renames (delete old + process new)
// - Track statistics and errors
```

### CLI Commands (Future Enhancement)
Planned CLI commands for monitoring and control:
```bash
# Start/stop orchestration
daiv3 knowledge-orchestration start
daiv3 knowledge-orchestration stop

# View statistics
daiv3 knowledge-orchestration stats

# View status
daiv3 knowledge-orchestration status
```

### Operational Constraints
- **File System Watcher:** Relies on KM-REQ-001 (FileSystemWatcherService)
- **Document Processor:** Requires IKnowledgeDocumentProcessor for deletion operations
- **Concurrency:** Sequential event processing to avoid race conditions
- **Error Recovery:** Failed operations logged but do not stop service
- **Resource Cleanup:** Automatically unsubscribes and disposes on shutdown

### Monitoring and Logging
- **Information level:** Service start/stop, file processing, deletion operations
- **Warning level:** Processing failures (document not found, etc.)
- **Error level:** Exceptions during processing or deletion
- **Statistics:** Available via `GetStatistics()` for operational metrics

## Dependencies
- KM-REQ-001 (FileSystemWatcherService - file system monitoring)
- KM-REQ-007 (IKnowledgeDocumentProcessor.RemoveDocumentAsync - deletion logic)
- HW-REQ-003 (Windows 11 platform)
- KLC-REQ-001 (Knowledge layer components)
- KLC-REQ-002 (DocProc layer configuration)
- KLC-REQ-004 (Knowledge service interfaces)

## Related Requirements
- KM-REQ-001 (File system watching - provides change detection)
- KM-REQ-007 (Document processing - provides deletion method)
- KM-REQ-008 (File hashing - enables change detection for updates)
- KM-ACC-003 (Acceptance: Deleting a document removes entries)

## Acceptance Criteria
✅ File deletion automatically triggers index entry removal  
✅ Document, topic, and chunk entries all deleted  
✅ File rename handled as delete old + process new  
✅ Errors logged and tracked but don't stop service  
✅ Statistics available for monitoring  
✅ Service lifecycle properly managed (start/stop/dispose)  
✅ 100% test coverage (21 unit + 9 integration tests passing)
