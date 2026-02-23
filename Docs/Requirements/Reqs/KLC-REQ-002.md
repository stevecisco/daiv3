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

## Usage and Operational Notes
- Register with DI via `AddDocumentProcessingServices` and configure `TokenizationOptions`.
- Chunk sizes are token-based, not character-based; overlap is measured in tokens.
- No direct UI surface; used by knowledge ingestion and chunking workflows.
- Works offline and does not call external services.

## Dependencies
- None

## Related Requirements
- None
