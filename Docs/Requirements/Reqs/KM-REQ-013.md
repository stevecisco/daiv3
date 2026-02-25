# KM-REQ-013

Source Spec: 4. Knowledge Management & Indexing - Requirements

## Requirement
Embeddings SHALL be generated using ONNX Runtime in-process.

## Implementation Plan
- Implement an ONNX embedding generator with tokenizer support and pooling.
- Add a model runner that executes ONNX Runtime in-process and selects outputs.
- Wire embeddings into document processing to replace placeholder vectors.
- Register embedding services and configuration in the Knowledge Layer.
- Document configuration and operational behavior.

## Implementation Tasks
- [X] Add ONNX embedding generator and model runner
- [X] Add embedding tokenization options and provider
- [X] Wire embedding generation into KnowledgeDocumentProcessor
- [X] Register embedding services in KnowledgeServiceExtensions
- [X] Add unit tests for embedding generation logic

## Testing Plan
- Unit tests to validate primary behavior and edge cases.
- Integration tests with dependent components and data stores.
- Negative tests to verify failure modes and error messages.
- Performance or load checks if the requirement impacts latency.
- Manual verification via UI workflows when applicable.

## Usage and Operational Notes
- Configure `EmbeddingOnnxOptions.ModelPath` to the ONNX embedding model.
- Set `InputIdsTensorName`, `AttentionMaskTensorName`, `OutputTensorName`, and optional `TokenTypeIdsTensorName` to match the model signature.
- Configure `EmbeddingTokenizationOptions` (encoding and `MaxTokens`) for the model tokenizer.
- Embeddings are generated in-process using ONNX Runtime with DirectML when available and CPU fallback.

## Testing Summary

### Unit Tests: ✅ 4/4 Passing (100%)

**Test Project:** [tests/unit/Daiv3.UnitTests/Daiv3.UnitTests.csproj](tests/unit/Daiv3.UnitTests/Daiv3.UnitTests.csproj)

**Test File:** [tests/unit/Daiv3.UnitTests/Knowledge/Embedding/OnnxEmbeddingGeneratorTests.cs](tests/unit/Daiv3.UnitTests/Knowledge/Embedding/OnnxEmbeddingGeneratorTests.cs)

**Test Class:** [Daiv3.UnitTests.Knowledge.Embedding.OnnxEmbeddingGeneratorTests](tests/unit/Daiv3.UnitTests/Knowledge/Embedding/OnnxEmbeddingGeneratorTests.cs#L10)

**Test Methods:**
- [GenerateEmbeddingAsync_ThrowsWhenTextMissing](tests/unit/Daiv3.UnitTests/Knowledge/Embedding/OnnxEmbeddingGeneratorTests.cs#L13)
- [GenerateEmbeddingAsync_VectorOutput_ReturnsOutput](tests/unit/Daiv3.UnitTests/Knowledge/Embedding/OnnxEmbeddingGeneratorTests.cs#L21)
- [GenerateEmbeddingAsync_TokenOutput_UsesMeanPooling](tests/unit/Daiv3.UnitTests/Knowledge/Embedding/OnnxEmbeddingGeneratorTests.cs#L33)
- [GenerateEmbeddingAsync_NormalizesEmbedding](tests/unit/Daiv3.UnitTests/Knowledge/Embedding/OnnxEmbeddingGeneratorTests.cs#L45)

**Test Coverage:**
- Validates text input and tokenization behavior
- Handles 2D vector output and 3D token output pooling
- Normalizes embeddings when configured

### Integration Tests: ⏸️ Deferred
- End-to-end ONNX model inference tests pending model asset integration

## Dependencies
- HW-REQ-003
- KLC-REQ-001
- KLC-REQ-002
- KLC-REQ-004

## Related Requirements
- None
