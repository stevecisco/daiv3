# KLC-REQ-002

Source Spec: 12. Key .NET Libraries & Components - Requirements

## Requirement
The system SHALL use Microsoft.ML.Tokenizers for tokenization.

## Implementation Plan
- Owning component: Daiv3.Knowledge.DocProc with `ITokenizerProvider` and `ITextChunker`.
- Define configuration via `TokenizationOptions` (encoding name, chunk size, overlap, normalization flags).
- Implement tokenizer provider using Microsoft.ML.Tokenizers (Tiktoken-based) with caching and logging.
- Implement chunker using token offsets for ~400 token chunks with overlap.
- Expose DI registration via `AddDocumentProcessingServices` for orchestration/UI wiring.
- Document configuration and operational behavior below.

## Implementation Design

### Components
- **Tokenizer Provider**: Lazily builds a tokenizer using `TiktokenTokenizer.CreateForEncoding` with a configurable encoding name and optional extra special tokens.
- **Text Chunker**: Produces token-based chunks using encoded token offsets so chunk boundaries map to the original text.

### Configuration
- `TokenizationOptions.EncodingName` (default: `gpt2`)
- `TokenizationOptions.MaxTokensPerChunk` (default: 400)
- `TokenizationOptions.OverlapTokens` (default: 50)
- `TokenizationOptions.ConsiderPreTokenization` (default: true)
- `TokenizationOptions.ConsiderNormalization` (default: false)

### Error Handling
- Invalid options throw `InvalidOperationException` during validation.
- Tokenizer creation errors are logged and rethrown.

## Implementation Tasks
- [X] **Task 1**: Add tokenization options and provider with Microsoft.ML.Tokenizers (2 hours)
- [X] **Task 2**: Implement token-based chunker using token offsets (2 hours)
- [X] **Task 3**: Register DocProc services in DI (1 hour)
- [X] **Task 4**: Add unit tests for options, provider, and chunker (2 hours)

## Testing Plan
- Unit tests: options validation, tokenizer provider caching, chunker behavior and token count checks.
- Integration tests: deferred until ingestion pipeline is wired.
- Negative tests: invalid options throw during validation.
- Performance checks deferred to KM-REQ-004/KM-NFR-001.

## Testing Summary

### Unit Tests: ✅ 8/8 Passing (100%)

**Test Project:** [tests/unit/Daiv3.UnitTests/Daiv3.UnitTests.csproj](tests/unit/Daiv3.UnitTests/Daiv3.UnitTests.csproj)

**Test Files:**
- **[tests/unit/Daiv3.UnitTests/Knowledge/DocProc/TokenizerProviderTests.cs](tests/unit/Daiv3.UnitTests/Knowledge/DocProc/TokenizerProviderTests.cs)** (1 test)
  - **Test Class:** [Daiv3.UnitTests.Knowledge.DocProc.TokenizerProviderTests](tests/unit/Daiv3.UnitTests/Knowledge/DocProc/TokenizerProviderTests.cs#L8)
  - **Test Methods:**
    - [GetTokenizer_CachesInstance](tests/unit/Daiv3.UnitTests/Knowledge/DocProc/TokenizerProviderTests.cs#L11)
  - **Coverage:** Tokenizer provider initialization and caching
  
- **[tests/unit/Daiv3.UnitTests/Knowledge/DocProc/TokenizationOptionsTests.cs](tests/unit/Daiv3.UnitTests/Knowledge/DocProc/TokenizationOptionsTests.cs)** (4 tests)
  - **Test Class:** [Daiv3.UnitTests.Knowledge.DocProc.TokenizationOptionsTests](tests/unit/Daiv3.UnitTests/Knowledge/DocProc/TokenizationOptionsTests.cs#L6)
  - **Test Methods:**
    - [Validate_Defaults_NoThrow](tests/unit/Daiv3.UnitTests/Knowledge/DocProc/TokenizationOptionsTests.cs#L9)
    - [Validate_EmptyEncoding_Throws](tests/unit/Daiv3.UnitTests/Knowledge/DocProc/TokenizationOptionsTests.cs#L17)
    - [Validate_InvalidMaxTokens_Throws](tests/unit/Daiv3.UnitTests/Knowledge/DocProc/TokenizationOptionsTests.cs#L30)
    - [Validate_InvalidOverlap_Throws](tests/unit/Daiv3.UnitTests/Knowledge/DocProc/TokenizationOptionsTests.cs#L43)
  - **Coverage:** Options validation and configuration constraints
  
- **[tests/unit/Daiv3.UnitTests/Knowledge/DocProc/TextChunkerTests.cs](tests/unit/Daiv3.UnitTests/Knowledge/DocProc/TextChunkerTests.cs)** (2 tests)
  - **Test Class:** [Daiv3.UnitTests.Knowledge.DocProc.TextChunkerTests](tests/unit/Daiv3.UnitTests/Knowledge/DocProc/TextChunkerTests.cs#L9)
  - **Test Methods:**
    - [Chunk_Whitespace_ReturnsEmpty](tests/unit/Daiv3.UnitTests/Knowledge/DocProc/TextChunkerTests.cs#L12)
    - [Chunk_SplitsTextByTokens](tests/unit/Daiv3.UnitTests/Knowledge/DocProc/TextChunkerTests.cs#L30)
  - **Coverage:** Token-based chunking and overlap behavior

**Test Coverage:**
- ✅ TokenizationOptions validation and defaults
- ✅ TokenizerProvider caching and lazy initialization
- ✅ TextChunker token-based chunking
- ✅ Chunk size and overlap token count calculations
- ✅ Support for configurable encoding names (gpt2, etc.)
- ✅ Error handling for invalid configurations

### Integration Tests: ⏸️ Deferred
- Integration tests deferred until document ingestion pipeline is implemented (KM-REQ-004)
- End-to-end chunking with real documents pending pipeline integration

## Usage and Operational Notes
- Register with DI via `AddDocumentProcessingServices` and configure `TokenizationOptions`.
- Chunk sizes are token-based, not character-based; overlap is measured in tokens.
- No direct UI surface; used by knowledge ingestion and chunking workflows.
- Works offline and does not call external services.

## Dependencies
- None

## Related Requirements
- None
