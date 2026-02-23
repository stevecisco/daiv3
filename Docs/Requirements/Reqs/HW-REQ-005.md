# HW-REQ-005

Source Spec: 2. Target Hardware & Runtime Environment - Requirements

## Requirement
The system SHALL fall back to GPU when NPU is not available or insufficient.

## Status
**Complete** - 2026-02-23

## Implementation Plan
- Identify the owning component and interface boundary.
- Define data contracts, configuration, and defaults.
- Implement the core logic with clear error handling and logging.
- Add integration points to orchestration and UI where applicable.
- Document configuration and operational behavior.

## Implementation Tasks
- [X] Ensure hardware detection orders NPU, GPU, CPU for preference resolution.
- [X] Log explicit GPU fallback when NPU is unavailable or insufficient.
- [X] Validate embedding provider preference resolves to DirectML with GPU fallback.
- [X] Add unit tests for GPU fallback paths.
- [X] Update documentation and tracker status.

## Implementation Summary
- Embedding provider preference now logs explicit GPU fallback when NPU is unavailable or insufficient.
- Vector similarity routing logs GPU fallback while retaining CPU fallback for execution.
- Hardware-aware preference resolution uses available tiers to select GPU when NPU is not present.

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
  - Auto preference with GPU tier selects DirectML ✅
  - GPU-only fallback when NPU unavailable ✅
  - Hardware detection integration ✅
  
- **Infrastructure/Shared/Hardware/HardwareDetectionProviderTests.cs** (17 tests)
  - GPU detection via DirectML ✅
  - Hardware tier ordering with GPU fallback ✅
  - NPU disable override testing ✅

**Test Coverage:**
- ✅ Hardware detection identifies GPU tier when NPU unavailable
- ✅ ONNX session factory selects DirectML for GPU tier
- ✅ Auto preference falls back to GPU when NPU insufficient
- ✅ Logging indicates GPU fallback decision
- ✅ Vector similarity routing considers GPU tier
- ✅ Environment override DAIV3_DISABLE_NPU forces GPU tier

### Integration Tests: ⏸️ Deferred
- GPU execution validation with real models deferred to KM-REQ-013
- Batch vector operations on GPU pending implementation

## Usage and Operational Notes
- Auto provider selection falls back to GPU when NPU is unavailable or insufficient, using DirectML when available.
- Vector similarity routing logs GPU fallback while using CPU execution until an accelerated vector backend is added.
- No UI surfaces yet; behavior is controlled by DI registration and logging.

## Dependencies
- KLC-REQ-001
- KLC-REQ-003

## Related Requirements
- None
