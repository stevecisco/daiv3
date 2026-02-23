# HW-ACC-002

Source Spec: 2. Target Hardware & Runtime Environment - Requirements

## Requirement
On a GPU-only device, embedding generation completes without errors.

## Status
**Blocked** - 2026-02-23

## Implementation Plan
- Implement embedding generation service (IEmbeddingService)
- Create integration tests that disable NPU via environment variable
- Measure and verify performance on actual hardware with simulated GPU-only config

## Implementation Tasks
- [X] Ensure provider selection falls back to GPU when NPU is unavailable
- [X] Validate provider preference selection for GPU-only tier in unit tests
- [X] Implement hardware override via DAIV3_DISABLE_NPU environment variable
- [ ] Implement IEmbeddingService that generates embeddings via ONNX Runtime
- [ ] Create integration test that runs with DAIV3_DISABLE_NPU=1 and measures latency
- [ ] Verify embedding output is valid and GPU backend was used

## Implementation Summary
- Hardware detection and ONNX session factory support GPU fallback
- DAIV3_DISABLE_NPU environment variable simulates GPU-only device on NPU hardware
- **BLOCKED**: No embedding generation service exists yet to test end-to-end

## Testing Plan
- Integration test that:
  1. Sets DAIV3_DISABLE_NPU=1 to simulate GPU-only device
  2. Loads real ONNX embedding model
  3. Generates embeddings for test text
  4. Verifies output is valid float array with correct dimensions
  5. Measures and logs latency
  6. Verifies DirectML provider was used (GPU, not NPU) via logs

## Testing Summary
- Unit tests for hardware tier selection with DAIV3_DISABLE_NPU: ✅ PASSING
- Unit tests for ONNX session options: ✅ PASSING
- Integration tests for actual embedding generation: ❌ NOT IMPLEMENTED (service doesn't exist)

## Usage and Operational Notes
- Set DAIV3_DISABLE_NPU=1 to simulate GPU-only hardware on NPU device
- No embedding service exists yet to test acceptance criteria

## Dependencies
- KLC-REQ-001
- KLC-REQ-003

## Related Requirements
- None
