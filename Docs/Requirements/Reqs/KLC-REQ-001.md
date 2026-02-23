# KLC-REQ-001

Source Spec: 12. Key .NET Libraries & Components - Requirements

## Requirement
The system SHALL use Microsoft.ML.OnnxRuntime.DirectML for in-process inference and embedding generation.

## Implementation Plan
- Owning component: Daiv3.Knowledge.Embedding with `IOnnxInferenceSessionProvider` and `IOnnxSessionOptionsFactory`.
- Define configuration via `EmbeddingOnnxOptions` (model path, provider preference, thread settings).
- Implement DirectML session creation on Windows with CPU fallback and structured logging.
- Expose DI registration via `AddEmbeddingServices` for orchestration/UI wiring.
- Document configuration and operational behavior below.

## Implementation Tasks
- [X] **Task 1**: Add ONNX session options factory with DirectML preference (2 hours)
- [X] **Task 2**: Implement inference session provider with model path validation (2 hours)
- [X] **Task 3**: Register embedding services and options in DI (1 hour)
- [X] **Task 4**: Add unit tests for options and session creation (2 hours)
- [X] **Task 5**: Add integration test for DirectML preference handling (1 hour)

## Testing Plan
- Unit tests: options validation, environment expansion, CPU preference selection, missing model path error.
- Integration test: DirectML-preferred session options creation (fallback to CPU allowed).
- Negative tests: missing model path throws before session initialization.
- Performance checks deferred to KM-REQ-013/HW-REQ-003 when real models are wired.

## Testing Summary

### Unit Tests: ✅ 20/20 Passing (100%)

**Test Project:** [tests/unit/Daiv3.UnitTests/Daiv3.UnitTests.csproj](tests/unit/Daiv3.UnitTests/Daiv3.UnitTests.csproj)

**Test Files:**
- **[tests/unit/Daiv3.UnitTests/Knowledge/Embedding/EmbeddingOnnxOptionsTests.cs](tests/unit/Daiv3.UnitTests/Knowledge/Embedding/EmbeddingOnnxOptionsTests.cs)** (2 tests)
  - **Test Class:** [Daiv3.UnitTests.Knowledge.Embedding.EmbeddingOnnxOptionsTests](tests/unit/Daiv3.UnitTests/Knowledge/Embedding/EmbeddingOnnxOptionsTests.cs#L6)
  - **Test Methods:**
    - [Validate_ThrowsWhenModelPathMissing](tests/unit/Daiv3.UnitTests/Knowledge/Embedding/EmbeddingOnnxOptionsTests.cs#L9)
    - [GetExpandedModelPath_ExpandsEnvironmentVariables](tests/unit/Daiv3.UnitTests/Knowledge/Embedding/EmbeddingOnnxOptionsTests.cs#L19)
  - **Coverage:** Options validation and environment variable expansion
  
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
  - **Coverage:** Auto provider selection, DirectML/CPU fallback, threading, memory options
  
- **[tests/unit/Daiv3.UnitTests/Knowledge/Embedding/OnnxInferenceSessionProviderTests.cs](tests/unit/Daiv3.UnitTests/Knowledge/Embedding/OnnxInferenceSessionProviderTests.cs)** (1 test)
  - **Test Class:** [Daiv3.UnitTests.Knowledge.Embedding.OnnxInferenceSessionProviderTests](tests/unit/Daiv3.UnitTests/Knowledge/Embedding/OnnxInferenceSessionProviderTests.cs#L9)
  - **Test Methods:**
    - [GetSessionAsync_ThrowsWhenModelMissing](tests/unit/Daiv3.UnitTests/Knowledge/Embedding/OnnxInferenceSessionProviderTests.cs#L12)
  - **Coverage:** Session provider validation

**Test Coverage:**
- ✅ EmbeddingOnnxOptions validation and configuration
- ✅ OnnxSessionOptionsFactory hardware-aware provider selection
- ✅ DirectML preference on Windows with CPU fallback
- ✅ CPU-only preference selection
- ✅ Threading and memory options application
- ✅ Model path validation and expansion

### Integration Tests: ⏸️ Deferred
- Integration tests deferred until real embedding models are integrated (KM-REQ-013)
- End-to-end inference testing pending model availability

## Usage and Operational Notes
- Register with DI via `EmbeddingServiceExtensions.AddEmbeddingServices` and configure `EmbeddingOnnxOptions.ModelPath`.
- Default provider preference is Auto: DirectML on Windows, CPU fallback on other targets or DirectML init failure.
- Model path supports environment variable expansion (e.g., `%TEMP%\model.onnx`).
- No UI surfaces yet; service is used by higher-level embedding workflows.

## Dependencies
- None

## Related Requirements
- None
