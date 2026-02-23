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

**Test Project:** `tests/unit/Daiv3.UnitTests/`

**Test Files:**
- **Knowledge/Embedding/OnnxSessionOptionsFactoryTests.cs** (17 tests)
  - Auto preference with CPU-only tier selects CPU provider ✅
  - CPU fallback when DirectML unavailable ✅
  - Hardware detection integration ✅
  
- **Infrastructure/Shared/Hardware/HardwareDetectionProviderTests.cs** (17 tests)
  - CPU tier always available ✅
  - CPU-only detection ✅
  - Force CPU-only override testing ✅
  
- **Knowledge/Embedding/CpuVectorSimilarityServiceTests.cs** (48 tests)
  - CPU vector operations with TensorPrimitives SIMD ✅
  - All vector similarity metrics on CPU ✅
  - Performance validation ✅

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
