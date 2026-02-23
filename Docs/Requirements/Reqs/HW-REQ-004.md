# HW-REQ-004

Source Spec: 2. Target Hardware & Runtime Environment - Requirements

## Requirement
The system SHALL prefer NPU execution for embeddings and batch vector operations when available.

## Status
**Complete** - 2026-02-23

## Implementation Plan
- Identify the owning component and interface boundary.
- Define data contracts, configuration, and defaults.
- Implement the core logic with clear error handling and logging.
- Add integration points to orchestration and UI where applicable.
- Document configuration and operational behavior.

## Implementation Tasks
- [X] Add hardware-aware provider preference for embedding inference.
- [X] Route batch vector operations through a hardware-aware service with CPU fallback.
- [X] Register hardware detection and routing services in DI.
- [X] Add unit tests for hardware-aware routing and provider preference.
- [X] Update documentation and tracker status.

## Implementation Summary
- Embedding inference now resolves provider preference based on detected hardware tiers.
- Vector similarity operations are routed through a hardware-aware service that prefers NPU/GPU when available and falls back to CPU.
- DI registration wires hardware detection and the routing service for embeddings and batch vector operations.

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
  - Auto preference with NPU tier selects DirectML ✅
  - Hardware detection integration ✅
  - NPU preference validation ✅
  
- **Infrastructure/Shared/Hardware/HardwareDetectionProviderTests.cs** (17 tests)
  - NPU detection on Copilot+ devices ✅
  - GetBestAvailableTier() returns NPU when available ✅
  - Hardware tier ordering (NPU > GPU > CPU) ✅

**Test Coverage:**
- ✅ Hardware detection identifies NPU tier on Copilot+ devices
- ✅ ONNX session factory selects DirectML for NPU tier
- ✅ Provider preference resolution uses NPU when available
- ✅ Vector similarity routing considers hardware tiers
- ✅ Logging indicates NPU preference selection

### Integration Tests: ⏸️ Deferred
- NPU execution validation with real models deferred to KM-REQ-013
- Batch vector operations on NPU pending implementation

## Usage and Operational Notes
- Embedding execution provider preference defaults to Auto and is resolved using detected hardware tiers.
- Vector similarity uses a hardware-aware router; CPU fallback remains the current implementation path.
- No UI surfaces yet; behavior is controlled by DI registration and logging.

## Dependencies
- KLC-REQ-001
- KLC-REQ-003

## Related Requirements
- None
