# KM-REQ-001

Source Spec: 4. Knowledge Management & Indexing - Requirements

## Requirement
The system SHALL detect new or changed files in watched directories.

## Implementation Summary
**Status:** Complete  
**Component:** `Daiv3.Knowledge.DocProc`  
**Service:** `FileSystemWatcherService`

The file system watching implementation provides real-time detection of file system changes (create, modify, delete, rename) in configured directories. It includes:
- Debouncing mechanism to prevent duplicate processing of rapid changes
- Pattern-based filtering (include/exclude patterns)
- Directory exclusions (e.g., node_modules, .git, bin)
- File size limits for processing
- Recursive and non-recursive watching options
- Project-scoped configuration support

### Key Components

1. **IFileSystemWatcher** - Interface defining service contract
   - Events: `FileChanged`, `Error`
   - Methods: `StartAsync()`, `StopAsync()`, `AddWatchPath()`, `RemoveWatchPath()`, `GetWatchedPaths()`
   - Properties: `IsRunning`

2. **FileSystemWatcherService** - Production implementation
   - Uses native .NET `System.IO.FileSystemWatcher` with enhanced debouncing
   - Concurrent dictionary for managing multiple watchers and timers
   - Thread-safe event processing
   - Proper async disposal pattern

3. **FileSystemWatcherOptions** - Configuration class
   - `WatchPaths`: Dictionary of paths and recursion flags
   - `IncludePatterns`: 19 default document/code file patterns (*.pdf, *.docx, *.md, *.cs, etc.)
   - `ExcludePatterns`: 9 temp file patterns (*.tmp, *.log, *.bak, etc.)
   - `ExcludeDirectories`: 12 common build/cache directories
   - `DebounceDelayMs`: 500ms default delay
   - `MaxFileSizeBytes`: 100MB default limit
   - `AutoStart`, `ProcessExistingFilesOnStart`, `EnableVerboseLogging` flags

4. **FileSystemChangeEventArgs** - Event data class
   - `FilePath`: Full path to changed file
   - `OldFilePath`: For rename operations
   - `ChangeType`: Created, Modified, Deleted, Renamed

5. **DocumentProcessingServiceExtensions** - DI registration
   - `AddFileSystemWatcher()` method registers service with dependency injection

## Implementation Details

### Debouncing Strategy
The service implements per-file debouncing using Timer objects:
- Each file path gets its own debounce timer
- Rapid changes to the same file reset the timer
- Only the final event is raised after the debounce delay
- Delete and rename events bypass debouncing (immediate processing)

### Pattern Matching
Wildcard-based pattern matching supports:
- `*` (zero or more characters)
- `?` (single character)
- Case-insensitive matching

### Filtering Logic
Files must pass all filters to be processed:
1. File size must be ≤ MaxFileSizeBytes (when file exists)
2. Must not be in excluded directory (e.g., /node_modules/, /bin/)
3. Must not match exclude patterns (e.g., *.tmp, *.log)
4. Must match at least one include pattern (if include patterns specified)

### Project-Scoped Configuration
The watcher can be configured per-project:
- Each project can have custom watch paths
- Pattern overrides per project
- Enable/disable per project
- Paths added/removed dynamically

## Testing

### Unit Tests (34 tests, all passing)
**FileSystemWatcherServiceTests** (20 tests):
- Constructor validation (null checks, valid options)
- Path management (empty paths, relative paths, non-existent directories)
- Service lifecycle (start, stop, multiple operations)
- Event detection (file creation, modification, deletion)
- Pattern filtering (include/exclude patterns, directory exclusions)
- Debouncing behavior (rapid changes combined)
- Disposal (proper cleanup, multiple dispose calls)

**FileSystemWatcherOptionsTests** (14 tests):
- Default values validation
- Path requirements (must be absolute, non-empty)
- Debounce delay constraints (≥ 0)
- File size constraints (> 0)
- Default pattern lists (include/exclude)
- Configuration validation rules

### Integration Tests (Pending)
Planned integration tests:
- Real file system operations with actual FileSystemWatcher
- Interaction with document processing pipeline
- Project configuration loading and watching
- Performance testing with large directory trees
- Cross-platform behavior (Windows, Linux)

## Usage and Operational Notes

### Configuration in appsettings.json
```json
{
  "FileSystemWatcher": {
    "AutoStart": false,
    "DebounceDelayMs": 500,
    "MaxFileSizeBytes": 104857600,
    "ProcessExistingFilesOnStart": false,
    "EnableVerboseLogging": false,
    "WatchPaths": {
      "C:\\Users\\username\\Documents\\MyProject": true,
      "C:\\Code\\Repos": false
    },
    "IncludePatterns": [ "*.pdf", "*.docx", "*.md", "*.txt" ],
    "ExcludePatterns": [ "*.tmp", "*.log", "*.bak" ],
    "ExcludeDirectories": [ "node_modules", ".git", "bin", "obj" ]
  }
}
```

### Dependency Injection Setup
```csharp
services.AddFileSystemWatcher(options =>
{
    options.WatchPaths.Add(@"C:\MyDocuments", recursive: true);
    options.DebounceDelayMs = 500;
    options.ProcessExistingFilesOnStart = true;
});
```

### Programmatic Usage
```csharp
IFileSystemWatcher watcher = serviceProvider.GetRequiredService<IFileSystemWatcher>();

watcher.FileChanged += (sender, args) =>
{
    _logger.LogInformation("File {ChangeType}: {FilePath}", 
        args.ChangeType, args.FilePath);
    // Trigger document processing pipeline
};

watcher.Error += (sender, args) =>
{
    _logger.LogError(args.GetException(), "File system watcher error");
};

await watcher.StartAsync();

// Add paths dynamically
watcher.AddWatchPath(@"C:\AnotherFolder", recursive: false);

// Cleanup
await watcher.StopAsync();
await watcher.DisposeAsync();
```

### CLI Commands (Pending)
Future CLI commands will include:
```bash
# List watched directories
daiv3 watch list

# Add watch directory
daiv3 watch add "C:\MyDocuments" --recursive

# Remove watch directory
daiv3 watch remove "C:\MyDocuments"

# Show statistics
daiv3 watch stats
```

### Operational Constraints
- **File Size Limit:** Files exceeding MaxFileSizeBytes are skipped (default: 100MB)
- **Path Requirements:** All watch paths must be absolute and exist before watching begins
- **Platform:** Uses platform-native FileSystemWatcher (Windows, Linux, macOS)
- **Performance:** Each directory requires one FileSystemWatcher instance
- **Resource Cleanup:** Service implements IAsyncDisposable for proper cleanup
- **Thread Safety:** All event handlers and path management are thread-safe

### Monitoring and Logging
- Information level: Service start/stop, file change events, path additions/removals
- Warning level: Non-existent directories, oversized files, watcher errors
- Debug level: Detailed filtering decisions, debounce timer operations (when EnableVerboseLogging=true)

## Dependencies
- HW-REQ-003 (Windows 11 platform)
- KLC-REQ-001 (Knowledge layer components defined)
- KLC-REQ-002 (DocProc layer configuration)
- KLC-REQ-004 (Knowledge service interfaces)

## Related Requirements
- KM-REQ-002 (Text extraction) - Consumes file change events
- KM-REQ-008 (File hash computation) - Uses detected files
- KM-REQ-009 (Deletion handling) - Triggered by delete events
