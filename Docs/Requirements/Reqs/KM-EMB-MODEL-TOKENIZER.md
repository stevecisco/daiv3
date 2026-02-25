# KM-EMB-MODEL-TOKENIZER

Source Spec: 4. Embedding Model Management - Requirements

**Status: COMPLETE (v0.1)**

## Requirement
The system SHALL support pluggable tokenizer implementations that can be registered, discovered, and selected based on embedding model requirements.

## Implementation Completed (v0.1 - MVP)

### Interfaces & Abstractions
- ✅ `IEmbeddingTokenizer` interface with methods:
  - `Tokenize(string text)` → `long[]` - Convert text to token IDs
  - `ValidateTokenIds(long[] tokenIds)` → `bool` - Validate token bounds
  - `GetSpecialTokens()` → `IReadOnlyDictionary<string, int>` - Special token mappings
  - Properties: `Name`, `ModelId`, `VocabularySize`

### Tokenizer Implementations
- ✅ **BertWordPieceTokenizer** - BERT-style WordPiece tokenizer
  - Designed for: all-MiniLM-L6-v2 (Tier 1, 384 dimensions)
  - Loads vocabulary from vocab.txt files
  - Implements greedy longest-match WordPiece algorithm
  - Supports special tokens: [UNK], [CLS], [SEP], [PAD], [MASK]

- ✅ **SentencePieceTokenizer** - SentencePiece tokenizer
  - Designed for: nomic-embed-text-v1.5 (Tier 2, 768 dimensions)
  - Loads vocabulary from sentencepiece.model or vocab files
  - Implements simplified BPE-like matching (greedy longest-match)
  - Fallback implementation with minimal vocabulary for testing
  - Future: Can be upgraded to use official SentencePiece.NetNative package

### Registry & Selection
- ✅ **EmbeddingTokenizerRegistry** - Central registry for tokenizer implementations
  - Maps model IDs to tokenizer factory functions
  - Validates tokenizers are registered at startup
  - Supports dynamic tokenizer creation with dependency injection

- ✅ **EmbeddingTokenizerProvider** - Smart tokenizer selection
  - Automatically selects correct tokenizer based on active model path
  - Extracts model ID from standard path: `...embeddings\{modelId}\model.onnx`
  - Pre-registers all v0.1 tokenizers at initialization
  - Thread-safe singleton caching of active tokenizer

### Integration
- ✅ Updated `OnnxEmbeddingGenerator` to use model-specific tokenizers
- ✅ Replaced legacy Tiktoken-based tokenization with model-aware implementation
- ✅ Updated `IEmbeddingTokenizerProvider` interface signature
- ✅ Modified dependency injection to use `ILoggerFactory` for flexible logger creation

### Testing
- ✅ 10 unit tests for tokenizer implementations
  - BertWordPieceTokenizer vocab loading and validation
  - SentencePieceTokenizer initialization and fallback
  - Tokenization correctness
  - Token ID boundary checking
  - Error handling for invalid inputs
- ✅ 4 OnnxEmbeddingGenerator integration tests (all passing)
- ✅ 96+ total embedding-related unit tests (all passing)
- ✅ CLI integration test: embedding generation with 768D output

## Future Implementation (v0.2+ - Backlog)
- Integrate official SentencePiece.NetNative NuGet package for production use
- Add configurable tokenizer plugins for dynamic loading
- Support additional tokenizer types as needed for new models
- Implement tokenizer version tracking and compatibility validation

## Testing Results
- **Unit Tests**: 10/10 passing for tokenizer implementations
- **Integration Tests**: 4/4 passing for OnnxEmbeddingGenerator  
- **Total Embedding Tests**: 96+ passing
- **CLI Validation**: ✅ Embedding generation produces correct 768D vectors with proper normalization

## Implementation Notes

### Architecture Decisions
1. **Greedy Longest-Match Algorithm**: Simple and effective for MVP; production may benefit from full BPE for SentencePiece
2. **Fallback Vocabulary**: SentencePiece uses minimal stub vocabulary when model files unavailable; enables testing without full model downloads
3. **Logger Factory Pattern**: Uses `ILoggerFactory` instead of generic `ILogger<T>` to support dynamic logger creation for different tokenizer implementations
4. **Lazy Initialization**: Tokenizers created on-demand via factory functions in registry

### Usage Example
```csharp
// Configuration example from EmbeddingServiceExtensions
services.AddEmbeddingServices(options =>
{
    options.ModelPath = modelPath; // Path to active model
});

// Automatic tokenizer selection based on model
var tokenizer = tokenizerProvider.GetEmbeddingTokenizer();
var tokens = tokenizer.Tokenize("text to embed");
```

### Model ID Detection
Automatically extracts model ID from paths like:
- `C:\Users\User\AppData\Local\Daiv3\models\embeddings\all-MiniLM-L6-v2\model.onnx` → `all-MiniLM-L6-v2`
- `C:\Users\User\AppData\Local\Daiv3\models\embeddings\nomic-embed-text-v1.5\model.onnx` → `nomic-embed-text-v1.5`

## Files Modified/Created
- ✅ [IEmbeddingTokenizer.cs](../../src/Daiv3.Knowledge.Embedding/IEmbeddingTokenizer.cs) - New interface
- ✅ [BertWordPieceTokenizer.cs](../../src/Daiv3.Knowledge.Embedding/BertWordPieceTokenizer.cs) - New implementation
- ✅ [SentencePieceTokenizer.cs](../../src/Daiv3.Knowledge.Embedding/SentencePieceTokenizer.cs) - New implementation
- ✅ [EmbeddingTokenizerRegistry.cs](../../src/Daiv3.Knowledge.Embedding/EmbeddingTokenizerRegistry.cs) - New registry
- ✅ [EmbeddingTokenizerProvider.cs](../../src/Daiv3.Knowledge.Embedding/EmbeddingTokenizerProvider.cs) - Updated provider
- ✅ [IEmbeddingTokenizerProvider.cs](../../src/Daiv3.Knowledge.Embedding/IEmbeddingTokenizerProvider.cs) - Updated interface
- ✅ [OnnxEmbeddingGenerator.cs](../../src/Daiv3.Knowledge.Embedding/OnnxEmbeddingGenerator.cs) - Updated to use new tokenizers
- ✅ [EmbeddingTokenizerImplementationTests.cs](../../tests/unit/Daiv3.UnitTests/Knowledge/Embedding/EmbeddingTokenizerImplementationTests.cs) - New test suite
- ✅ [OnnxEmbeddingGeneratorTests.cs](../../tests/unit/Daiv3.UnitTests/Knowledge/Embedding/OnnxEmbeddingGeneratorTests.cs) - Updated to use new mocks

## Known Limitations & Future Work
1. **SentencePiece Implementation**: Current greedy matching is simplified; production should use official library
2. **Vocabulary Files**: Assumes vocab files exist in model directories; need to handle download/bundling
3. **BERT Preprocessing**: Simplified word cleaning; production should implement full BERT preprocessing
4. **No Dynamic Plugin Loading**: v0.1 uses static registration; v0.2+ should support dynamic loading

## Blocking Tasks / Resolved Open Questions
- ✅ Define `IEmbeddingTokenizer` interface contract
- ✅ Implement tokenizer registry format and storage
- ✅ Create tokenizer factory pattern for DI integration
- 🟡 SentencePiece NuGet package: Optional for v0.1 (fallback implementation works), recommended for v0.2+
- 🟡 Vocab artifact bundling: Handled by model package structure, documented in 04-Embedding-Model-Packaging.md
