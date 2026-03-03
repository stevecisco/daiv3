# KM-REQ-010

Source Spec: 4. Knowledge Management & Indexing - Requirements

## Requirement
The system SHALL maintain a Tier 1 topic index with one vector per document.

## Implementation Summary
**Status:** Complete  
**Primary Components:** `Daiv3.Knowledge`, `Daiv3.Persistence`

Tier 1 indexing is implemented and enforced with a single topic vector per document (`doc_id`):
- `topic_index.doc_id` is the primary key (schema-level uniqueness)
- `KnowledgeDocumentProcessor` generates one summary embedding per document and stores it through `IVectorStoreService.StoreTopicIndexAsync(...)`
- `VectorStoreService.StoreTopicIndexAsync(...)` now performs idempotent write behavior:
  - inserts for new documents
  - updates existing row for existing `doc_id`
- `TopicIndexRepository.GetCountAsync()` correctly handles SQLite `COUNT(*)` scalar types (`long`/`int`) to provide accurate Tier 1 counts and statistics

## Implementation Details

### One-vector-per-document enforcement
1. **Schema constraint:** `topic_index` uses `doc_id TEXT PRIMARY KEY`.
2. **Service behavior:** `VectorStoreService` checks for existing topic row and performs add/update accordingly.
3. **Pipeline behavior:** `KnowledgeDocumentProcessor` stores exactly one topic embedding (summary embedding) per processed document.

### Updated files
- `src/Daiv3.Knowledge/VectorStoreService.cs`
- `src/Daiv3.Persistence/Repositories/TopicIndexRepository.cs`
- `tests/unit/Daiv3.UnitTests/Knowledge/VectorStoreServiceTests.cs`
- `tests/integration/Daiv3.Knowledge.IntegrationTests/VectorStoreServiceIntegrationTests.cs`

## Testing

### Unit tests
`VectorStoreServiceTests` verifies:
- topic index insert for new document
- topic index update for existing document (`StoreTopicIndexAsync_WhenDocumentExists_UpdatesExistingTopicIndex`)
- existing validation/serialization behavior

### Integration tests
`VectorStoreServiceIntegrationTests` verifies:
- repeated topic indexing for same `doc_id` keeps Tier 1 at one row
- second write updates summary content
- count/statistics path returns correct values

### Validation commands executed
- `dotnet test tests/unit/Daiv3.UnitTests/Daiv3.UnitTests.csproj --nologo --verbosity minimal --filter "FullyQualifiedName~VectorStoreServiceTests"`
- `dotnet test tests/integration/Daiv3.Knowledge.IntegrationTests/Daiv3.Knowledge.IntegrationTests.csproj --nologo --verbosity minimal --filter "FullyQualifiedName~StoreTopicIndex_WhenCalledTwiceForSameDocument_MaintainsSingleTier1Vector" -p:TestTfmsInParallel=false`

Results:
- Unit: 66 passed, 0 failed
- Integration (targeted): 2 passed, 0 failed

## Usage and Operational Notes
- No additional runtime configuration is required for KM-REQ-010.
- Tier 1 is updated automatically by the existing document processing pipeline.
- Reprocessing the same document updates the existing Tier 1 vector instead of creating duplicates.

## Dependencies
- KM-REQ-007 (vector storage/repository infrastructure)
- KM-REQ-006 (summary embedding generation)
- HW-REQ-003
- KLC-REQ-001
- KLC-REQ-002
- KLC-REQ-004

## Related Requirements
- KM-REQ-011 (Tier 2 chunk index)
- KM-REQ-012 (two-tier query strategy)
- KM-REQ-017 (batch cosine similarity)
- KM-REQ-018 (Tier 1 memory loading)

## Acceptance Criteria
✅ Tier 1 maintains one vector per document (`doc_id`)  
✅ Repeat index writes for same document update existing row  
✅ Topic summary vector remains the canonical Tier 1 representation  
✅ Unit and integration tests validate the invariant
