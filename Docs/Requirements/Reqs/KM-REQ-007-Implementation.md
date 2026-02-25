# KM-REQ-007 Implementation Details

**Requirement:** The system SHALL store embeddings and metadata in SQLite.

**Status:** ✅ COMPLETE

## Overview

The system successfully stores embeddings and metadata in SQLite with full CRUD operations, proper error handling, and comprehensive testing. Topic (Tier 1) and chunk (Tier 2) embeddings are persisted in binary format with efficient retrieval operations.

## Implemented Components

### Core Services

**IVectorStoreService Interface**
- Location: `src/Daiv3.Knowledge/IVectorStoreService.cs`
- Manages storage/retrieval of embeddings in SQLite
- Handles Tier 1 (topic) and Tier 2 (chunk) indices

**VectorStoreService Implementation**
- Location: `src/Daiv3.Knowledge/VectorStoreService.cs`
- Converts float[] embeddings to bytes for storage
- Implements all CRUD operations with logging
- Proper null validation and error handling

### Repository Layer

**TopicIndexRepository**
- Location: `src/Daiv3.Persistence/Repositories/TopicIndexRepository.cs`
- Manages Tier 1 (one per document) embeddings
- Queries by document ID, source path, or all

**ChunkIndexRepository**
- Location: `src/Daiv3.Persistence/Repositories/ChunkIndexRepository.cs`
- Manages Tier 2 (many per document) embeddings
- Hierarchical queries by document ID with order tracking

### Data Entities

**TopicIndex**
- DocId, SummaryText, EmbeddingBlob, EmbeddingDimensions
- SourcePath, FileHash (denormalized for lookups)
- IngestedAt, MetadataJson (for extensibility)

**ChunkIndex**
- ChunkId, DocId (foreign key), ChunkText
- EmbeddingBlob, EmbeddingDimensions, ChunkOrder
- TopicTags (optional), CreatedAt

### Database Schema

**topic_index Table**
- doc_id (TEXT PRIMARY KEY, references documents.doc_id)
- summary_text, embedding_blob (BLOB), embedding_dimensions
- source_path, file_hash (denormalized)
- Index on: source_path, ingested_at

**chunk_index Table**
- chunk_id (TEXT PRIMARY KEY)
- doc_id (TEXT NOT NULL, foreign key to documents)
- chunk_text, embedding_blob (BLOB), embedding_dimensions
- chunk_order (sequence within document)
- Index on: doc_id, (doc_id, chunk_order), created_at

All embeddings for a document are deleted via CASCADE DELETE when document is removed.

## Integration Points

**Document Processing Pipeline** (`KnowledgeDocumentProcessor`)
1. Extract text from document
2. Generate topic summary → Store via VectorStoreService
3. Chunk document (~400 tokens)
4. Generate embedding for each chunk → Store via VectorStoreService
5. Track file hash for future changes

**DI Registration** (`KnowledgeServiceExtensions`)
```csharp
services.AddScoped<TopicIndexRepository>();
services.AddScoped<ChunkIndexRepository>();
services.AddScoped<IVectorStoreService, VectorStoreService>();
```

## Testing

### Unit Tests: ✅ 28/28 Passing
File: `tests/unit/Daiv3.UnitTests/Knowledge/VectorStoreServiceTests.cs`

Covers:
- Store/retrieve operations
- Null validation
- Embedding conversion
- Deletion cascades

### Integration Tests: ✅ 7/7 Passing
File: `tests/integration/Daiv3.Knowledge.IntegrationTests/VectorStoreServiceIntegrationTests.cs`

Test Methods:
1. `StoreTopicIndex_StoresAndRetrievesFromDatabase` - Round-trip persistence
2. `StoreChunk_StoresAndRetrievesFromDatabase` - Chunk persistence
3. `GetChunksByDocument_RetrievesAllChunksForDocument` - Batch retrieval
4. `DeleteTopicAndChunks_RemovesAllRelatedData` - Cascade delete
5. `TopicIndexExistsAsync_ReturnsTrueWhenExists` - Existence check
6. `TopicIndexExistsAsync_ReturnsFalseWhenNotExists` - Non-existence check
7. `StoreMultipleDocuments_EachHasIndependentChunks` - Document isolation

Coverage includes BLOB operations, foreign key constraints, unique constraints, and null handling.

## Technical Specifications

**Embedding Binary Format:**
- Float32 (4 bytes per value)
- 384D embedding = 1,536 bytes
- 768D embedding = 3,072 bytes

**Error Handling:**
- ArgumentNullException for null parameters
- SqliteException propagated for constraint violations
- Comprehensive logging (INFO/DEBUG/ERROR)

**Performance:**
- Topic index retrieval: O(1) by doc_id
- Chunk retrieval: O(m) where m = chunks per document
- Bulk operations: O(n) for n documents

## Operational Behavior

**Offline Mode:**
- All embeddings stored locally
- No external vector DB required
- Change detection via file hashing

**User-Visible Effects:**
- Indexed documents shown in dashboard
- Search results include matched documents and chunks
- No configuration required (automatic)

## Dependencies Met

✅ HW-REQ-003 - ONNX embedding execution
✅ KLC-REQ-001 - DirectML access
✅ KLC-REQ-002 - Tokenization
✅ KLC-REQ-004 - SQLite persistence

## Related Requirements

- **KM-REQ-006** - Embedding generation (generates embeddings stored here)
- **KM-REQ-010/011** - Two-tier search (uses embeddings stored here)
- **KM-DATA-001** - Database schema (defines storage tables)
