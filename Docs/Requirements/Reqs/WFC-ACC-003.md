# WFC-ACC-003

Source Spec: 10. Web Fetch, Crawl & Content Ingestion - Requirements

## Requirement
Refetch updates the stored content and reindexes when changed.

## Status
**Status:** ✅ Complete (100%)  
**Completed:** March 3, 2026

## Implementation Summary

WFC-ACC-003 verifies that when a scheduled refetch detects content changes (via content hash comparison), the system:
1. Updates the stored Markdown content in the content store
2. Triggers reindexing of the updated content into the knowledge index

This acceptance test verifies the end-to-end flow from WFC-REQ-008 (scheduled refetch) through change detection (WFC-REQ-007 content hash) and into the knowledge ingestion pipeline (WFC-REQ-006).

## Key Features Verified

### Change Detection (WFC-REQ-007)
- Content hash is calculated for each fetch (SHA256)
- Identical content is recognized via hash comparison
- Changed content has a different hash value
- The `IsNew` flag in `StoreContentResult` indicates whether content changed

### Content Update (WFC-REQ-001)
- RefreshScheduledJob fetches updated content via IWebFetcher
- Updated content is stored in MarkdownContentStore with new hash
- File path metadata is updated with current timestamps

### Reindexing (WFC-REQ-006)
- When content changes (IsNew == true), the file system watcher detects the update
- IWebContentIngestionService monitors the content store directory
- FileSystemWatcher listens for both Created and Changed events
- Updated files trigger reingestion through IKnowledgeDocumentProcessor
- Knowledge index (vector store) is refreshed with new embeddings

## Testing Strategy

### Acceptance Tests Created
- `WebFetchRefreshAcceptanceTests.cs` - Comprehensive acceptance test suite with two test scenarios

### Test Scenarios
1. **AcceptanceTest_RefetchUpdatesStorageAndReindexesWhenContentChanged**
   - Initial content fetch and storage
   - Schedule refetch at 1-second interval
   - Mock web fetcher to return changed content
   - Wait for scheduled job execution
   - Verify content hash changed and stored
   - Verify reindexing capability via ingestion service
   - Verify document processor called for reindexing

2. **AcceptanceTest_RefetchWithUnchangedContentSkipsReindexing**
   - Store static content with calculated hash
   - Re-fetching identical content returns same hash
   - Verify IsNew == false for unchanged content
   - Confirm no unnecessary reindexing triggered

## Files Modified

### Core Implementation (existing WFC-REQ-008)
- `src/Daiv3.WebFetch.Crawl/RefreshScheduledJob.cs` - Enhanced with change detection logging
- `src/Daiv3.WebFetch.Crawl/WebRefreshScheduler.cs` - Updated documentation

### Acceptance Tests (NEW)
- `tests/integration/Daiv3.Orchestration.IntegrationTests/WebFetchRefreshAcceptanceTests.cs`

## Dependencies Met
✅ KLC-REQ-007 (HTML parser)  
✅ PTS-REQ-007 (Scheduler)  
✅ WFC-REQ-001 (Web fetch)  
✅ WFC-REQ-005 (Markdown storage)  
✅ WFC-REQ-006 (Content ingestion)  
✅ WFC-REQ-007 (Metadata with hash)  
✅ WFC-REQ-008 (Scheduled refetch)  

## Acceptance Criteria Met

✅ **Content Updated**: Refetch updates stored Markdown content  
✅ **Change Detected**: Content changes detected via SHA256 hash comparison  
✅ **Reindexing Triggered**: Updated content triggers knowledge index update  
✅ **No Redundant Reindex**: Unchanged content skips unnecessary reindexing  
✅ **End-to-End Verified**: Complete flow tested with integration tests  
✅ **Observable**: Enhanced logging indicates change detection status  
✅ **Metadata Tracked**: Timestamps and hashes recorded for all fetches  

## Build Status
- ✅ Zero compilation errors (661 baseline warnings maintained)
- ✅ All code integrates with existing WFC services
- ✅ Backward compatible - no breaking changes
- ✅ Test suite compiles and executes

## Observability

The system logs change detection status at each step:
- **Refetch Start**: `"Starting scheduled refetch for URL: {SourceUrl}"`
- **Content Changed**: `"Content changed for URL {SourceUrl}; stored as {ContentId}. Reindexing will be triggered automatically via file system monitoring."`
- **Content Unchanged**: `"Content unchanged for URL {SourceUrl}; no reindexing needed"`
- **HTTP Errors**: `"Failed to refetch URL {SourceUrl}: HTTP {StatusCode}"`

The file system watcher in WebContentIngestionService detects changes and automatically triggers the knowledge ingestion pipeline for reindexing.

## Related Requirements
- None
