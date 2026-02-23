# HW-REQ-006

Source Spec: 2. Target Hardware & Runtime Environment - Requirements

## Requirement
The system SHALL fall back to CPU when NPU and GPU are unavailable.

## Status
**Complete** - 2026-02-23

## Implementation Plan
- Identify the owning component and interface boundary.
- Define data contracts, configuration, and defaults.
- Implement the core logic with clear error handling and logging.
- Add integration points to orchestration and UI where applicable.
- Document configuration and operational behavior.

## Implementation Tasks
- [X] Ensure hardware detection resolves CPU when no accelerators are available.
- [X] Log explicit CPU fallback when NPU/GPU are unavailable.
- [X] Add unit tests for CPU fallback paths.
- [X] Update documentation and tracker status.

## Implementation Summary
- Embedding provider selection logs explicit CPU fallback when no NPU/GPU is available.
- Vector similarity routing logs CPU fallback when accelerators are unavailable.
- Auto provider selection resolves to CPU when only CPU tier is detected.

## Testing Plan
- Unit tests to validate primary behavior and edge cases.
- Integration tests with dependent components and data stores.
- Negative tests to verify failure modes and error messages.
- Performance or load checks if the requirement impacts latency.
- Manual verification via UI workflows when applicable.

## Testing Summary

### Unit Tests: ✅ Covered

**Test Project:** [tests/unit/Daiv3.UnitTests/Daiv3.UnitTests.csproj](tests/unit/Daiv3.UnitTests/Daiv3.UnitTests.csproj)

**Test Files:**
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
  - **Coverage:** CPU fallback selection and Auto preference behavior
  
- **[tests/unit/Daiv3.UnitTests/Infrastructure/Shared/Hardware/HardwareDetectionProviderTests.cs](tests/unit/Daiv3.UnitTests/Infrastructure/Shared/Hardware/HardwareDetectionProviderTests.cs)** (17 tests)
  - **Test Class:** [Daiv3.UnitTests.Infrastructure.Shared.Hardware.HardwareDetectionProviderTests](tests/unit/Daiv3.UnitTests/Infrastructure/Shared/Hardware/HardwareDetectionProviderTests.cs#L13)
  - **Test Methods:**
    - [Constructor_WithNullLogger_ThrowsArgumentNullException](tests/unit/Daiv3.UnitTests/Infrastructure/Shared/Hardware/HardwareDetectionProviderTests.cs#L16)
    - [GetAvailableTiers_AlwaysIncludeCpu](tests/unit/Daiv3.UnitTests/Infrastructure/Shared/Hardware/HardwareDetectionProviderTests.cs#L26)
    - [GetAvailableTiers_OnCopilotPlusPC_DetectsMultipleTiers](tests/unit/Daiv3.UnitTests/Infrastructure/Shared/Hardware/HardwareDetectionProviderTests.cs#L41)
    - [GetAvailableTiers_OnSnapdragonXElite_DetectsNPU](tests/unit/Daiv3.UnitTests/Infrastructure/Shared/Hardware/HardwareDetectionProviderTests.cs#L70)
    - [GetAvailableTiers_ReturnsSortedByPerference](tests/unit/Daiv3.UnitTests/Infrastructure/Shared/Hardware/HardwareDetectionProviderTests.cs#L111)
    - [IsTierAvailable_Cpu_ReturnsTrue](tests/unit/Daiv3.UnitTests/Infrastructure/Shared/Hardware/HardwareDetectionProviderTests.cs#L132)
    - [IsTierAvailable_None_ReturnsFalse](tests/unit/Daiv3.UnitTests/Infrastructure/Shared/Hardware/HardwareDetectionProviderTests.cs#L145)
    - [GetBestAvailableTier_ReturnsCpu_WhenCpuIsOnly](tests/unit/Daiv3.UnitTests/Infrastructure/Shared/Hardware/HardwareDetectionProviderTests.cs#L158)
    - [GetDiagnosticInfo_ReturnsNonEmptyString](tests/unit/Daiv3.UnitTests/Infrastructure/Shared/Hardware/HardwareDetectionProviderTests.cs#L175)
    - [GetDiagnosticInfo_IncludesBestTier](tests/unit/Daiv3.UnitTests/Infrastructure/Shared/Hardware/HardwareDetectionProviderTests.cs#L192)
    - [Service_CanBeRegisteredViaExtension](tests/unit/Daiv3.UnitTests/Infrastructure/Shared/Hardware/HardwareDetectionProviderTests.cs#L206)
    - [Service_RegisteredAsSingleton](tests/unit/Daiv3.UnitTests/Infrastructure/Shared/Hardware/HardwareDetectionProviderTests.cs#L223)
    - [GetAvailableTiers_MultipleCallsReturnConsistentResults](tests/unit/Daiv3.UnitTests/Infrastructure/Shared/Hardware/HardwareDetectionProviderTests.cs#L242)
    - [GetAvailableTiers_ForceCpuOnly_OverridesHardwareDetection](tests/unit/Daiv3.UnitTests/Infrastructure/Shared/Hardware/HardwareDetectionProviderTests.cs#L262)
    - [GetAvailableTiers_DisableNpuAndGpu_LeavesCpuOnly](tests/unit/Daiv3.UnitTests/Infrastructure/Shared/Hardware/HardwareDetectionProviderTests.cs#L289)
    - [IsTierAvailable_ConsistentWithGetAvailableTiers](tests/unit/Daiv3.UnitTests/Infrastructure/Shared/Hardware/HardwareDetectionProviderTests.cs#L321)
    - [HardwareDetectionDemo_Runs](tests/unit/Daiv3.UnitTests/Infrastructure/Shared/Hardware/HardwareDetectionProviderTests.cs#L342)
  - **Coverage:** CPU tier detection and overrides
  
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
  - **Coverage:** CPU vector math with TensorPrimitives and performance thresholds

**Test Coverage:**
- ✅ Hardware detection always includes CPU tier as fallback
- ✅ ONNX session factory selects CPU provider when no accelerators available
- ✅ Auto preference falls back to CPU when NPU/GPU unavailable
- ✅ Vector similarity executes on CPU with SIMD optimizations
- ✅ Logging indicates CPU fallback decision
- ✅ Environment override DAIV3_FORCE_CPU_ONLY forces CPU tier
- ✅ CPU execution completes without errors

### Integration Tests: ⏸️ Deferred
- CPU execution latency validation with real models deferred to KM-REQ-013
- Performance benchmarking for CPU-only execution pending

## Usage and Operational Notes
- Auto provider selection falls back to CPU when NPU/GPU are unavailable or insufficient.
- Vector similarity routing logs CPU fallback and uses CPU execution.
- No UI surfaces yet; behavior is controlled by DI registration and logging.

## Dependencies
- KLC-REQ-001
- KLC-REQ-003

## Related Requirements
- None
