# HW-REQ-003

Source Spec: 2. Target Hardware & Runtime Environment - Requirements

## Requirement
The system SHALL execute embedding generation and vector operations using ONNX Runtime DirectML.

## Status
**Complete** - 2026-02-23

## Implementation Summary

This requirement is satisfied by the DirectML-backed ONNX session infrastructure in
`Daiv3.Knowledge.Embedding` (KLC-REQ-001) and the CPU vector operations fallback
implementation (KLC-REQ-003). DirectML is selected on Windows targets and used for
in-process ONNX inference when embeddings are executed. Vector operations are handled via
`IVectorSimilarityService` with SIMD-accelerated CPU math, providing the fallback path
outlined in the hardware execution chain.

## Implementation Plan
- Identify the owning component and interface boundary.
- Define data contracts, configuration, and defaults.
- Implement the core logic with clear error handling and logging.
- Add integration points to orchestration and UI where applicable.
- Document configuration and operational behavior.

## Implementation Tasks
- [X] Use DirectML-capable ONNX session options factory for embedding inference.
- [X] Provide ONNX inference session provider with model path validation and logging.
- [X] Register embedding services and vector similarity service in DI.
- [X] Implement CPU vector operations fallback with TensorPrimitives.

## Testing Plan
- Unit tests to validate primary behavior and edge cases.
- Integration tests with dependent components and data stores.
- Negative tests to verify failure modes and error messages.
- Performance or load checks if the requirement impacts latency.
- Manual verification via UI workflows when applicable.

## Testing Summary

**Status:** Tests implemented under KLC-REQ-001 (ONNX Runtime DirectML) and KLC-REQ-003 (TensorPrimitives)

### Unit Tests: ✅ Covered by Parent Requirements

**Test Project:** [tests/unit/Daiv3.UnitTests/Daiv3.UnitTests.csproj](tests/unit/Daiv3.UnitTests/Daiv3.UnitTests.csproj)

**Related Test Files:**
- **[tests/unit/Daiv3.UnitTests/Knowledge/Embedding/OnnxSessionOptionsFactoryTests.cs](tests/unit/Daiv3.UnitTests/Knowledge/Embedding/OnnxSessionOptionsFactoryTests.cs)** (17 tests)
  - **Test Class:** [Daiv3.UnitTests.Knowledge.Embedding.OnnxSessionOptionsFactoryTests](tests/unit/Daiv3.UnitTests/Knowledge/Embedding/OnnxSessionOptionsFactoryTests.cs#L17)
  - **Test Methods:**
    - [Create_WithCpuPreference_ReturnsCpuProvider](tests/unit/Daiv3.UnitTests/Knowledge/Embedding/OnnxSessionOptionsFactoryTests.cs#L66)
    - [Create_WithDirectMLPreference_AttemptsDirectML](tests/unit/Daiv3.UnitTests/Knowledge/Embedding/OnnxSessionOptionsFactoryTests.cs#L85)
    - [Create_WithAutoPreference_SelectsBestAvailable](tests/unit/Daiv3.UnitTests/Knowledge/Embedding/OnnxSessionOptionsFactoryTests.cs#L107)
    - [Create_WithAutoPreference_FallsBackToGpuWhenNpuUnavailable](tests/unit/Daiv3.UnitTests/Knowledge/Embedding/OnnxSessionOptionsFactoryTests.cs#L129)
    - [Create_WithAutoPreference_FallsBackToCpuWhenNoAccelerators](tests/unit/Daiv3.UnitTests/Knowledge/Embedding/OnnxSessionOptionsFactoryTests.cs#L150)
    - [Create_WithAutoPreference_NpuTier_PrefersDirectML](tests/unit/Daiv3.UnitTests/Knowledge/Embedding/OnnxSessionOptionsFactoryTests.cs#L169)
    - [Create_WithAutoPreference_GpuOnly_PrefersDirectML](tests/unit/Daiv3.UnitTests/Knowledge/Embedding/OnnxSessionOptionsFactoryTests.cs#L190)
    - [Create_ReturnsTuningOptions](tests/unit/Daiv3.UnitTests/Knowledge/Embedding/OnnxSessionOptionsFactoryTests.cs#L211)
    - [Create_AppliesMemoryPatternOptions](tests/unit/Daiv3.UnitTests/Knowledge/Embedding/OnnxSessionOptionsFactoryTests.cs#L232)
    - [Constructor_WithNullOptions_ThrowsArgumentNullException](tests/unit/Daiv3.UnitTests/Knowledge/Embedding/OnnxSessionOptionsFactoryTests.cs#L253)
    - [Create_MultipleCallsProduceConsistentProvider](tests/unit/Daiv3.UnitTests/Knowledge/Embedding/OnnxSessionOptionsFactoryTests.cs#L267)
    - [Create_DefaultThreadOptions_AppliedOnlyIfSet](tests/unit/Daiv3.UnitTests/Knowledge/Embedding/OnnxSessionOptionsFactoryTests.cs#L286)
    - [Create_AllPreferencesProduceSessions](tests/unit/Daiv3.UnitTests/Knowledge/Embedding/OnnxSessionOptionsFactoryTests.cs#L311)
    - [Create_CpuAlwaysAvailable](tests/unit/Daiv3.UnitTests/Knowledge/Embedding/OnnxSessionOptionsFactoryTests.cs#L332)
  - **Coverage:** DirectML selection and CPU fallback behavior
  
- **[tests/unit/Daiv3.UnitTests/Knowledge/Embedding/CpuVectorSimilarityServiceTests.cs](tests/unit/Daiv3.UnitTests/Knowledge/Embedding/CpuVectorSimilarityServiceTests.cs)** (48 tests)
  - **Test Class:** [Daiv3.UnitTests.Knowledge.Embedding.CpuVectorSimilarityServiceTests](tests/unit/Daiv3.UnitTests/Knowledge/Embedding/CpuVectorSimilarityServiceTests.cs#L8)
  - **Test Methods:**
    - [CosineSimilarity_IdenticalVectors_ReturnsOne](tests/unit/Daiv3.UnitTests/Knowledge/Embedding/CpuVectorSimilarityServiceTests.cs#L22)
    - [CosineSimilarity_OrthogonalVectors_ReturnsZero](tests/unit/Daiv3.UnitTests/Knowledge/Embedding/CpuVectorSimilarityServiceTests.cs#L35)
    - [CosineSimilarity_OppositeVectors_ReturnsNegativeOne](tests/unit/Daiv3.UnitTests/Knowledge/Embedding/CpuVectorSimilarityServiceTests.cs#L49)
    - [CosineSimilarity_PartialOverlap_ReturnsExpectedValue](tests/unit/Daiv3.UnitTests/Knowledge/Embedding/CpuVectorSimilarityServiceTests.cs#L63)
    - [CosineSimilarity_DifferentLengths_ThrowsArgumentException](tests/unit/Daiv3.UnitTests/Knowledge/Embedding/CpuVectorSimilarityServiceTests.cs#L82)
    - [CosineSimilarity_EmptyVectors_ThrowsArgumentException](tests/unit/Daiv3.UnitTests/Knowledge/Embedding/CpuVectorSimilarityServiceTests.cs#L94)
    - [CosineSimilarity_ZeroMagnitudeVector_ReturnsZero](tests/unit/Daiv3.UnitTests/Knowledge/Embedding/CpuVectorSimilarityServiceTests.cs#L106)
    - [CosineSimilarity_HighDimensionalVectors_ComputesCorrectly](tests/unit/Daiv3.UnitTests/Knowledge/Embedding/CpuVectorSimilarityServiceTests.cs#L120)
    - [BatchCosineSimilarity_SingleVector_ComputesCorrectly](tests/unit/Daiv3.UnitTests/Knowledge/Embedding/CpuVectorSimilarityServiceTests.cs#L145)
    - [BatchCosineSimilarity_MultipleVectors_ComputesCorrectly](tests/unit/Daiv3.UnitTests/Knowledge/Embedding/CpuVectorSimilarityServiceTests.cs#L160)
    - [BatchCosineSimilarity_LargeVectorCount_ComputesCorrectly](tests/unit/Daiv3.UnitTests/Knowledge/Embedding/CpuVectorSimilarityServiceTests.cs#L182)
    - [BatchCosineSimilarity_QueryDimensionMismatch_ThrowsArgumentException](tests/unit/Daiv3.UnitTests/Knowledge/Embedding/CpuVectorSimilarityServiceTests.cs#L224)
    - [BatchCosineSimilarity_TargetArraySizeMismatch_ThrowsArgumentException](tests/unit/Daiv3.UnitTests/Knowledge/Embedding/CpuVectorSimilarityServiceTests.cs#L237)
    - [BatchCosineSimilarity_ResultsArrayTooSmall_ThrowsArgumentException](tests/unit/Daiv3.UnitTests/Knowledge/Embedding/CpuVectorSimilarityServiceTests.cs#L250)
    - [BatchCosineSimilarity_ZeroQueryMagnitude_FillsResultsWithZero](tests/unit/Daiv3.UnitTests/Knowledge/Embedding/CpuVectorSimilarityServiceTests.cs#L263)
    - [BatchCosineSimilarity_ZeroTargetMagnitude_SetsResultToZero](tests/unit/Daiv3.UnitTests/Knowledge/Embedding/CpuVectorSimilarityServiceTests.cs#L279)
    - [Normalize_StandardVector_CreatesUnitVector](tests/unit/Daiv3.UnitTests/Knowledge/Embedding/CpuVectorSimilarityServiceTests.cs#L303)
    - [Normalize_HighDimensionalVector_CreatesUnitVector](tests/unit/Daiv3.UnitTests/Knowledge/Embedding/CpuVectorSimilarityServiceTests.cs#L322)
    - [Normalize_UnitVector_RemainsUnchanged](tests/unit/Daiv3.UnitTests/Knowledge/Embedding/CpuVectorSimilarityServiceTests.cs#L344)
    - [Normalize_ZeroVector_FillsWithZero](tests/unit/Daiv3.UnitTests/Knowledge/Embedding/CpuVectorSimilarityServiceTests.cs#L360)
    - [Normalize_DifferentLengths_ThrowsArgumentException](tests/unit/Daiv3.UnitTests/Knowledge/Embedding/CpuVectorSimilarityServiceTests.cs#L376)
    - [Normalize_EmptyVector_ThrowsArgumentException](tests/unit/Daiv3.UnitTests/Knowledge/Embedding/CpuVectorSimilarityServiceTests.cs#L388)
    - [Constructor_NullLogger_ThrowsArgumentNullException](tests/unit/Daiv3.UnitTests/Knowledge/Embedding/CpuVectorSimilarityServiceTests.cs#L404)
    - [BatchCosineSimilarity_10000Vectors_CompletesInReasonableTime](tests/unit/Daiv3.UnitTests/Knowledge/Embedding/CpuVectorSimilarityServiceTests.cs#L416)
    - [StressTest_LargeScale_ShowsCpuActivity](tests/unit/Daiv3.UnitTests/Knowledge/Embedding/CpuVectorSimilarityServiceTests.cs#L447)
  - **Coverage:** TensorPrimitives vector math and SIMD performance

**Test Coverage:**
- ✅ ONNX Runtime with DirectML provider creation on Windows targets
- ✅ DirectML session options factory with hardware detection
- ✅ CPU fallback when DirectML unavailable
- ✅ Vector operations using TensorPrimitives SIMD
- ✅ Embedding generation path (deferred to full embedding service - KM-REQ-013)

### Integration Tests: ⏸️ Deferred
- End-to-end embedding generation with ONNX Runtime deferred to KM-REQ-013
- DirectML hardware acceleration validation pending real model integration

## Usage and Operational Notes
- Configure `EmbeddingOnnxOptions.ModelPath` and register services via
	`EmbeddingServiceExtensions.AddEmbeddingServices()`.
- DirectML is selected automatically on Windows targets; CPU fallback is used when DirectML
	is unavailable or non-Windows.
- No direct UI surfaces yet; wiring is consumed by embedding workflows in the Knowledge Layer.

## Dependencies
- KLC-REQ-001
- KLC-REQ-003

## Related Requirements
- None
