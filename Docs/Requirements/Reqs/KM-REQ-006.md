# KM-REQ-006

Source Spec: 4. Knowledge Management & Indexing - Requirements

## Requirement
The system SHALL generate embeddings for each chunk and for the topic summary.

## Implementation Plan
- Identify the owning component and interface boundary.
- Define data contracts, configuration, and defaults.
- Implement the core logic with clear error handling and logging.
- Add integration points to orchestration and UI where applicable.
- Document configuration and operational behavior.

## Implementation Details

### Owning Components
- Knowledge ingestion orchestration: `KnowledgeDocumentProcessor`.
- Embedding generation: `IEmbeddingGenerator` (ONNX runtime implementation).
- Storage: `IVectorStoreService` for topic and chunk entries.

### Core Flow
- Extract text, generate topic summary, and chunk content.
- Generate one embedding for the topic summary (Tier 1).
- Generate one embedding per chunk (Tier 2) and store each entry.
- For updates, delete old topic/chunk embeddings before writing new ones.

### Code References
- `KnowledgeDocumentProcessor.ProcessDocumentAsync()` drives summary and chunk embedding generation.
- `IEmbeddingGenerator.GenerateEmbeddingAsync()` is invoked for summary and each chunk.

## Implementation Tasks
- [X] Generate embeddings for the topic summary.
- [X] Generate embeddings for each chunk.
- [X] Store topic and chunk embeddings via `IVectorStoreService`.
- [X] Add tests covering summary and chunk embedding generation calls.

## Testing Plan
- Unit tests to validate primary behavior and edge cases.
- Integration tests with dependent components and data stores.
- Negative tests to verify failure modes and error messages.
- Performance or load checks if the requirement impacts latency.
- Manual verification via UI workflows when applicable.

## Testing Summary

### Unit Tests: ✅ 28/28 Passing (100%)

**Test Project:** [tests/unit/Daiv3.UnitTests/Daiv3.UnitTests.csproj](tests/unit/Daiv3.UnitTests/Daiv3.UnitTests.csproj)

**Test File:** [tests/unit/Daiv3.UnitTests/Knowledge/KnowledgeDocumentProcessorTests.cs](tests/unit/Daiv3.UnitTests/Knowledge/KnowledgeDocumentProcessorTests.cs)

**Test Class:** [Daiv3.UnitTests.Knowledge.KnowledgeDocumentProcessorTests](tests/unit/Daiv3.UnitTests/Knowledge/KnowledgeDocumentProcessorTests.cs#L20)

**Test Methods:**
- [ProcessDocumentAsync_GeneratesEmbeddings_ForSummaryAndChunks](tests/unit/Daiv3.UnitTests/Knowledge/KnowledgeDocumentProcessorTests.cs#L544)

**Test Coverage:**
- Generates one embedding for the topic summary.
- Generates one embedding per chunk and preserves count alignment.

## Usage and Operational Notes
### Invocation
Embeddings are generated during document ingestion via `IKnowledgeDocumentProcessor.ProcessDocumentAsync()` and `ProcessDocumentsAsync()`.

### Configuration
Embedding model path and execution provider are configured via `EmbeddingOnnxOptions` and `EmbeddingTokenizationOptions`.

### Operational Behavior
- Works offline once embedding models are available locally.
- Errors during embedding generation cause the document to fail processing.
- Re-indexing deletes prior embeddings before generating new vectors.

## Dependencies
- HW-REQ-003
- KLC-REQ-001
- KLC-REQ-002
- KLC-REQ-004

## Related Requirements
- None
