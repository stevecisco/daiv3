# KM-REQ-004

Source Spec: 4. Knowledge Management & Indexing - Requirements

## Requirement
The system SHALL chunk documents into ~400 token segments with ~50 token overlap.

## Status
**Complete** - 2026-02-23

## Implementation Summary
The document chunking capability is implemented in the `Daiv3.Knowledge.DocProc` module via the `ITextChunker` interface and `TextChunker` implementation. This requirement depends on Microsoft.ML.Tokenizers for accurate token counting and is integrated into the full document ingestion pipeline.

## Implementation Plan
- ✅ Identify the owning component and interface boundary
- ✅ Define data contracts, configuration, and defaults
- ✅ Implement the core logic with clear error handling and logging
- ✅ Add integration points to orchestration and UI where applicable
- ✅ Document configuration and operational behavior

## Implementation Design

### Components
- **Interface:** `ITextChunker` - Splits text into token-based chunks
- **Implementation:** `TextChunker` - Stateless chunker using token offsets to preserve chunk boundaries
- **Data Contract:** `TextChunk` record with text, start offset, length, and token count
- **Configuration:** `TokenizationOptions` (from KLC-REQ-002)

### Configuration
Chunking uses `TokenizationOptions` configured with:
- `MaxTokensPerChunk` (default: 400) - Target chunk size in tokens
- `OverlapTokens` (default: 50) - Token overlap between consecutive chunks
- `EncodingName` (default: `gpt2`) - Tokenizer encoding to use
- `ConsiderPreTokenization` (default: true) - Whether to pre-tokenize text
- `ConsiderNormalization` (default: false) - Whether to apply text normalization

### Algorithm
1. Encode input text to tokens using Microsoft.ML.Tokenizers
2. Iterate through tokens, creating chunks of up to `MaxTokensPerChunk` size
3. Maintain overlap by starting subsequent chunks `(MaxTokensPerChunk - OverlapTokens)` tokens after the previous start
4. Map token indices to character offsets to extract original text chunks
5. Return list of `TextChunk` objects with preserved text and metadata

### Error Handling
- Empty/whitespace input returns empty chunk list
- Invalid options throw `InvalidOperationException` during validation
- Tokenizer errors are logged and rethrown
- Invalid token offsets are logged and cause chunking to stop

## Testing Plan

### Unit Tests
- ✅ Primary behavior: whitespace input returns empty
- ✅ Token-based splitting with configured chunk size and overlap
- ✅ Chunk boundary preservation using token offsets
- ✅ Token count verification matches expected bounds

### Integration Tests
- ✅ Full ingestion pipeline: file → extraction → chunking → storage
- ✅ Chunk persistence in SQLite with document association
- ✅ Multiple document processing with progress tracking
- ✅ Document update and deletion with chunk cleanup

### Negative Tests
- ✅ Invalid paths and missing files
- ✅ Empty files handled gracefully
- ✅ Unsupported file formats rejected appropriately

## Testing Summary

**Unit Tests: ✅ 2/2 Passing**

**Test Project:** [tests/unit/Daiv3.UnitTests/Daiv3.UnitTests.csproj](tests/unit/Daiv3.UnitTests/Daiv3.UnitTests.csproj)

**Test File:** [tests/unit/Daiv3.UnitTests/Knowledge/DocProc/TextChunkerTests.cs](tests/unit/Daiv3.UnitTests/Knowledge/DocProc/TextChunkerTests.cs)

**Test Class:** [TextChunkerTests](tests/unit/Daiv3.UnitTests/Knowledge/DocProc/TextChunkerTests.cs)

**Test Methods:**
- [Chunk_Whitespace_ReturnsEmpty](tests/unit/Daiv3.UnitTests/Knowledge/DocProc/TextChunkerTests.cs#L15) ✅
- [Chunk_SplitsTextByTokens](tests/unit/Daiv3.UnitTests/Knowledge/DocProc/TextChunkerTests.cs#L28) ✅

**Integration Tests: ✅ Covered by KnowledgeDocumentProcessor**

**Test Project:** [tests/integration/Daiv3.Knowledge.IntegrationTests/Daiv3.Knowledge.IntegrationTests.csproj](tests/integration/Daiv3.Knowledge.IntegrationTests/Daiv3.Knowledge.IntegrationTests.csproj)

## Usage and Operational Notes

### Invocation
Document chunking is automatically invoked during document ingestion via `IKnowledgeDocumentProcessor.ProcessDocumentAsync()`. The processor:
1. Extracts text from the document (KM-REQ-002)
2. Chunks the extracted text using `ITextChunker`
3. Generates embeddings for each chunk
4. Stores chunks and embeddings in SQLite

### Configuration
Configure chunking behavior via dependency injection with `TokenizationOptions`:
- `MaxTokensPerChunk` (default: 400)
- `OverlapTokens` (default: 50)
- `EncodingName` (default: "gpt2")

### Operational Behavior
- **Memory:** O(n) where n = number of tokens in document
- **Performance:** Token encoding dominates; chunking is a linear pass
- **Chunk overlap:** Ensures context continuity across boundaries
- **Offline mode:** Works offline with no external dependencies
- **Deterministic:** Same input always produces same chunks

### User-Visible Effects
- Documents appear in knowledge base after ingestion
- Search results may include multiple chunks from same document
- Document updates re-index all chunks
- Chunk boundaries respect token boundaries

## Dependencies
- Microsoft.ML.Tokenizers (via KLC-REQ-002)
- HW-REQ-003 (hardware acceleration)
- KLC-REQ-001 (ONNX Runtime)
- KLC-REQ-002 (tokenization)
- KLC-REQ-004 (SQLite persistence)

## Related Requirements
- KM-REQ-003 (HTML to Markdown conversion affects input)
- KM-REQ-005 (Topic summary generation)
- KM-REQ-006 (Embeddings for chunks)
- KM-NFR-001 (Performance targets)
