# KM-EMB-MODEL-TOKENIZER

Source Spec: 4. Embedding Model Management - Requirements

## Requirement
The system SHALL support pluggable tokenizer implementations that can be registered, discovered, and selected based on embedding model requirements.

## Implementation Plan (v0.1 - MVP)
- Define `IEmbeddingTokenizer` interface with methods: Tokenize, ValidateTokenIds, GetVocabularySize.
- Implement SentencePieceTokenizer for nomic-embed-text-v1.5 (use SentencePiece NuGet package).
- Create a tokenizer registry that maps model ids to tokenizer implementations.
- Validate at startup that selected model's tokenizer plugin is registered and available.
- For v0.1, only SentencePieceTokenizer is available.

## Future Implementation (v0.2+ - Backlog)
- Add BertTokenizer for all-MiniLM-L6-v2 and similar BERT-based models.
- Add WordPieceTokenizer for other models as needed.
- Support dynamic tokenizer discovery/loading from plugin packages.

## Testing Plan (v0.1)
- Unit tests for IEmbeddingTokenizer interface contract.
- Unit tests for SentencePieceTokenizer with nomic-embed-text vocabulary.
- Integration tests for tokenizer initialization and validation.
- Negative tests for invalid token IDs and vocab mismatches.

## Usage and Operational Notes
- Tokenizer MUST be validated at embedding model selection time.
- Tokenizer MUST produce token IDs within model vocabulary bounds.
- Tokenizer initialization MUST fail fast with clear error if vocab/model mismatch detected.
- Registry MUST log tokenizer version and source (e.g., "SentencePiece v0.1.99").

## Dependencies
- SentencePiece NuGet package (pending ADD approval)
- KM-EMB-MODEL-001 (model registry to lookup tokenizer plugin name)
- KM-EMB-MODEL-003 (model selection validates tokenizer availability)

## Related Requirements
- KM-REQ-013 (embedding generation uses selected tokenizer)
- KM-REQ-014 (model support depends on tokenizer availability)

## Blocking Tasks / Open Questions
- Confirm SentencePiece NuGet package availability and license.
- Define tokenizer registry format and storage location.
- Define how tokenizer vocab artifacts (e.g., sentencepiece.model file) are bundled with model package.
