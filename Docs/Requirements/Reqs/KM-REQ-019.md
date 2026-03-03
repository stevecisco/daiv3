# KM-REQ-019

Source Spec: 4. Knowledge Management & Indexing - Requirements

## Requirement
Chunk embeddings SHOULD be loaded on demand.

## Implementation Summary

### Architecture
KM-REQ-019 is implemented in `TwoTierIndexService.SearchAsync()` with lazy Tier 2 retrieval:

1. Tier 1 runs first against in-memory topic embeddings.
2. Tier 2 then loads chunk embeddings **only** for top Tier 1 candidates.
3. Chunk embeddings are not preloaded at startup and are fetched per request from SQLite.

### Core Implementation

#### 1. On-demand chunk loading in search path
**Location:** `src/Daiv3.Knowledge/TwoTierIndexService.cs`

- Tier 2 now uses a named candidate limit constant (`Tier2CandidateDocumentLimit = 3`).
- `GetChunksByDocumentAsync(docId)` is called only for those top candidates.
- Chunk embeddings are flattened and scored per candidate document, then discarded after request completion.

#### 2. Blocker fix: dimension mismatch safety
**Location:** `src/Daiv3.Knowledge/TwoTierIndexService.cs`

Resolved a Tier 2 failure mode where query dimensions could differ from chunk dimensions:

- Before scoring chunk vectors, service now validates `queryEmbedding.Length == chunkDimensions`.
- When mismatched, document-level Tier 2 scoring is skipped with structured warning log.
- Search continues without exception, preserving Tier 1 results and compatible Tier 2 results.

This unblocks runtime failures in mixed-dimension scenarios while preserving lazy-loading behavior.

## Testing Plan

### Unit Tests
**Location:** `tests/unit/Daiv3.UnitTests/Knowledge/TwoTierIndexServiceTests.cs`

Added/validated targeted coverage:

- `SearchAsync_LoadsChunksOnlyForTopThreeTier1Candidates`
	- Verifies `IVectorStoreService.GetChunksByDocumentAsync(...)` is called exactly 3 times.
	- Confirms chunks are not fetched for lower-ranked Tier 1 documents.
- `SearchAsync_WithMismatchedChunkDimensions_SkipsTier2ResultsWithoutThrowing`
	- Verifies mismatch is handled safely and Tier 2 returns empty instead of throwing.

Validation command:

- `runTests` on `tests/unit/Daiv3.UnitTests/Knowledge/TwoTierIndexServiceTests.cs`
	- Result: 26 passed, 0 failed.

### Build Validation
- `dotnet build Daiv3.FoundryLocal.slnx --nologo --verbosity minimal`
	- Result: 0 errors, baseline warning families only.

## Usage and Operational Notes

- No startup configuration required; behavior is intrinsic to two-tier search.
- Tier 1 cache remains startup-loaded (KM-REQ-018); Tier 2 chunk vectors remain demand-loaded (KM-REQ-019).
- For mismatched dimensions, Tier 2 logs a warning and skips incompatible candidate documents.

## Dependencies
- KM-REQ-011 (Tier 2 chunk storage)
- KM-REQ-012 (two-tier search flow)
- KM-REQ-018 (Tier 1 in-memory startup cache)

## Related Requirements
- KM-NFR-001 (Tier 1 latency target)
- KM-NFR-002 (future scale path to ANN/HNSW)

## Implementation Status
✅ **COMPLETE**

- On-demand Tier 2 chunk loading: implemented and validated.
- Outstanding blocked issue (Tier 2 dimension mismatch exception): resolved.
- Unit tests: passing with new requirement-focused coverage.
- Build: successful with no net-new errors.
