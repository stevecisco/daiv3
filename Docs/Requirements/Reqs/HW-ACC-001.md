# HW-ACC-001

Source Spec: 2. Target Hardware & Runtime Environment - Requirements

## Requirement
On an NPU device, embedding generation uses the NPU by default.

## Status
**Blocked** - 2026-02-23

## Implementation Plan
- Implement embedding generation service (IEmbeddingService)
- Create integration tests that use environment variables to simulate hardware configs
- Measure and verify performance on actual hardware

## Implementation Tasks
- [X] Ensure hardware detection prefers NPU tier when available
- [X] Validate provider preference selection for NPU tier in unit tests
- [X] Implement hardware override via DAIV3_DISABLE_NPU, DAIV3_DISABLE_GPU, DAIV3_FORCE_CPU_ONLY
- [ ] Implement IEmbeddingService that generates embeddings via ONNX Runtime
- [ ] Create integration test that runs with default config (NPU enabled) and measures latency
- [ ] Verify embedding output is valid and NPU backend was used

## Implementation Summary
- Hardware detection and ONNX session factory support NPU preference
- Environment variable overrides work (DAIV3_DISABLE_NPU, DAIV3_DISABLE_GPU, DAIV3_FORCE_CPU_ONLY)
- **BLOCKED**: No embedding generation service exists yet to test end-to-end

## Testing Plan
- Integration test that:
  1. Runs on actual NPU hardware (no environment overrides)
  2. Loads real ONNX embedding model
  3. Generates embeddings for test text
  4. Verifies output is valid float array with correct dimensions
  5. Measures and logs latency
  6. Verifies DirectML provider was used via logs

## Testing Summary
- Unit tests for hardware tier selection: ✅ PASSING
- Unit tests for ONNX session options: ✅ PASSING
- Integration tests for actual embedding generation: ❌ NOT IMPLEMENTED (service doesn't exist)

## Usage and Operational Notes
- Hardware detection respects environment variable overrides
- No embedding service exists yet to test acceptance criteria

## Dependencies
- KLC-REQ-001
- KLC-REQ-003

## Related Requirements
- None
