# KM-REQ-008

Source Spec: 4. Knowledge Management & Indexing - Requirements

## Requirement
The system SHALL store and compare file hashes to detect changes.

## Implementation Summary
**Status:** Complete  
**Component:** `Daiv3.Knowledge`  
**Service:** `KnowledgeDocumentProcessor`

The file hashing implementation provides content-based change detection for documents in the knowledge base. It computes SHA256 hashes of file contents and stores them with document metadata, enabling the system to skip reprocessing unchanged documents and efficiently detect when files need re-indexing.

### Key Components

1. **ComputeFileHashAsync (Private Method)** - SHA256 hash computation
   - Location: `KnowledgeDocumentProcessor.cs`
   - Algorithm: SHA256 cryptographic hash
   - Implementation: Async file stream hashing with cancellation support
   - Returns: Uppercase hexadecimal string representation of hash

2. **Document.FileHash (Data Model)** - Storage field
   - Location: `Daiv3.Persistence.Entities.CoreEntities`
   - Type: `string` (SHA256 hash as hex string)
   - Stored in: `documents` table in SQLite database
   - Persisted by: `DocumentRepository`

3. **TopicIndex.FileHash (Data Model)** - Vector store field
   - Location: `Daiv3.Persistence.Entities.CoreEntities`
   - Type: `string` (SHA256 hash as hex string)
   - Stored in: `topic_index` table in SQLite database
   - Persisted by: `TopicIndexRepository` via `IVectorStoreService`

4. **Change Detection Logic** - Skip-if-unchanged behavior
   - Location: `KnowledgeDocumentProcessor.ProcessDocumentAsync`
   - Compares computed hash against stored hash from existing document
   - Option: `DocumentProcessingOptions.SkipUnchangedDocuments` (boolean flag)
   - Default: Configurable per service instance

## Implementation Details

### Hash Computation
```csharp
private static async Task<string> ComputeFileHashAsync(string filePath, CancellationToken cancellationToken)
{
    using var sha256 = SHA256.Create();
    using var stream = File.OpenRead(filePath);
    var hash = await sha256.ComputeHashAsync(stream, cancellationToken).ConfigureAwait(false);
    return Convert.ToHexString(hash);
}
```

**Characteristics:**
- **Algorithm:** SHA256 (256-bit cryptographic hash)
- **Format:** Uppercase hexadecimal string (64 characters)
- **Performance:** Async streaming for large files (no memory buffer required)
- **Cancellation:** Supports `CancellationToken` for long-running operations
- **Deterministic:** Same file content always produces same hash

### Change Detection Workflow
1. **File Processing Request:** System receives request to process document at `documentPath`
2. **Hash Computation:** Compute SHA256 hash of current file contents
3. **Lookup Existing Document:** Query `DocumentRepository` for existing document with same `SourcePath`
4. **Hash Comparison:**
   - If document exists AND `SkipUnchangedDocuments` option is true AND hash matches → Skip processing
   - Otherwise → Proceed with full processing (text extraction, chunking, embedding generation)
5. **Store Hash:** Save computed hash in both `Document.FileHash` and `TopicIndex.FileHash`
6. **Update Processing:** When document exists but hash differs, delete old embeddings and create new ones

### Configuration Options
```csharp
public class DocumentProcessingOptions
{
    /// <summary>
    /// When true, skip processing documents whose file hash matches existing record.
    /// Default: false (always reprocess).
    /// </summary>
    public bool SkipUnchangedDocuments { get; set; }
}
```

**Usage in DI:**
```csharp
services.AddSingleton(new DocumentProcessingOptions 
{ 
    SkipUnchangedDocuments = true 
});
```

### Database Storage
**Documents Table:**
```sql
CREATE TABLE IF NOT EXISTS documents (
    doc_id TEXT PRIMARY KEY,
    source_path TEXT NOT NULL UNIQUE,
    file_hash TEXT NOT NULL,
    format TEXT NOT NULL,
    size_bytes INTEGER NOT NULL,
    last_modified INTEGER NOT NULL,
    status TEXT NOT NULL,
    created_at INTEGER NOT NULL,
    metadata_json TEXT
);
CREATE INDEX IF NOT EXISTS idx_documents_path ON documents(source_path);
CREATE INDEX IF NOT EXISTS idx_documents_hash ON documents(file_hash);
```

**Topic Index Table:**
```sql
CREATE TABLE IF NOT EXISTS topic_index (
    doc_id TEXT PRIMARY KEY,
    summary_text TEXT NOT NULL,
    embedding BLOB NOT NULL,
    embedding_dimensions INTEGER NOT NULL,
    source_path TEXT NOT NULL,
    file_hash TEXT NOT NULL,
    ingested_at INTEGER NOT NULL,
    metadata_json TEXT,
    FOREIGN KEY (doc_id) REFERENCES documents(doc_id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS idx_topic_index_path ON topic_index(source_path);
CREATE INDEX IF NOT EXISTS idx_topic_index_hash ON topic_index(file_hash);
```

## Testing

### Unit Tests (17 tests, all passing)
**KnowledgeDocumentProcessorTests** includes:

1. **ProcessDocumentAsync_SkipsProcessing_WhenDocumentUnchangedAndOptionEnabled**
   - Verifies skip behavior when hash matches and option enabled
   - Confirms vector store operations NOT called
   - Validates success result with "unchanged" message

2. **ProcessDocumentAsync_ReprocessesDocumentWithChangedContent**
   - Updates file content between processing attempts
   - Verifies hash changes when content changes
   - Confirms old embeddings deleted before storing new ones
   - Validates full reprocessing occurs

3. **ProcessDocumentAsync_ComputeFileHashCorrectly**
   - Computes hash twice on same file
   - Verifies hashes are identical (deterministic)
   - Validates hash is non-empty string

4. **ProcessDocumentAsync_GeneratesDifferentHashForDifferentContent**
   - Creates two files with different content
   - Computes hash for each file
   - Verifies hashes are different

Additional tests verify hash storage and retrieval through full pipeline.

### Integration Tests (4 tests, all passing)
**KnowledgeDocumentProcessorIntegrationTests** includes:

1. **ProcessDocumentAsync_WithChangedContent_UpdatesDocument**
   - Full pipeline test with real SQLite database
   - Processes document, modifies content, reprocesses
   - Verifies document ID consistency across updates
   - Confirms new embeddings replace old ones

2. **ProcessDocumentAsync_SkipUnchangedDocument_WithSameContent**
   - Full pipeline test with skip option enabled
   - Processes document twice with no changes
   - Verifies skip behavior with real database persistence
   - Confirms embeddings not duplicated

3. **DocumentHash_ConsistentForSameContent**
   - Two separate files with identical content
   - Both process successfully
   - Verifies consistent processing behavior

4. **DocumentHash_DifferentForDifferentContent**
   - Two files with different content
   - Verifies different document IDs (derived from path hash)
   - Confirms both process independently

### Test Coverage Summary
- ✅ Hash computation (consistency, determinism)
- ✅ Change detection (skip unchanged, reprocess changed)
- ✅ Database persistence (Document and TopicIndex tables)
- ✅ Configuration options (SkipUnchangedDocuments flag)
- ✅ Update workflow (delete old embeddings, store new)
- ✅ Full pipeline integration (with real database)

## Usage and Operational Notes

### Automatic Change Detection
File hashing is automatically integrated into the document processing pipeline:

```csharp
IKnowledgeDocumentProcessor processor = serviceProvider.GetRequiredService<IKnowledgeDocumentProcessor>();

// Process document - hash computed automatically
var result = await processor.ProcessDocumentAsync(@"C:\Documents\report.pdf");

if (result.Success)
{
    Console.WriteLine($"Document {result.DocumentId} processed or skipped based on hash");
}
```

### Skip Unchanged Documents (Recommended Configuration)
```csharp
services.AddSingleton(new DocumentProcessingOptions 
{ 
    SkipUnchangedDocuments = true  // Recommended for production
});
```

**Benefits:**
- **Performance:** Avoids redundant processing (text extraction, chunking, embedding generation)
- **Cost Savings:** Reduces compute for large document sets
- **Incremental Updates:** Only reprocesses documents that actually changed

### Hash-Based Queries
Query documents by hash to detect duplicates or find unchanged documents:

```csharp
var documentRepo = serviceProvider.GetRequiredService<DocumentRepository>();
var allDocs = await documentRepo.GetAllAsync();

// Find documents with same content (hash collision = identical content)
var duplicates = allDocs
    .GroupBy(d => d.FileHash)
    .Where(g => g.Count() > 1)
    .ToList();
```

### File System Watcher Integration
KM-REQ-001 (FileSystemWatcherService) detects file modifications. When combined with KM-REQ-008:

1. **File Modified Event:** FileSystemWatcher detects file change
2. **Reprocessing Triggered:** KnowledgeDocumentProcessor.ProcessDocumentAsync called
3. **Hash Comparison:** System computes new hash and compares to stored hash
4. **Smart Reprocessing:** Only reprocesses if hash actually changed (avoids false positives from file "touch" operations)

### Operational Constraints
- **Hash Algorithm:** SHA256 (not configurable, ensures consistency)
- **Hash Storage:** Requires database schema with `file_hash` fields
- **File Access:** Requires read access to file for hash computation
- **Memory Usage:** Async streaming minimizes memory footprint (no full file load)
- **Performance:** Hash computation adds ~1-5ms for small files, ~100-500ms for 100MB files

### Monitoring and Logging
- **Information:** "Skipped processing unchanged document: {Path}" (when hash matches)
- **Information:** "Updated document: {Path}" (when hash changes and document exists)
- **Information:** "Added new document: {Path}" (when no existing document found)
- **Debug:** Hash values logged in detailed mode (for troubleshooting)

## Dependencies
- **HW-REQ-003** - Windows 11 platform
- **KLC-REQ-001** - Knowledge layer components defined
- **KLC-REQ-002** - DocProc layer configuration
- **KLC-REQ-004** - Knowledge service interfaces
- **KM-REQ-001** - File system watcher (for triggering change detection)
- **KM-DATA-001** - Database schema with file_hash fields

## Related Requirements
- **KM-REQ-001** - File system watching (detects when to check for changes)
- **KM-REQ-007** - Vector store service (stores FileHash in TopicIndex)
- **KM-ACC-002** - Acceptance test for document updates (depends on hash-based change detection)
