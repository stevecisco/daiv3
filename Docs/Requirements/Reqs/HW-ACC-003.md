# HW-ACC-003

Source Spec: 2. Target Hardware & Runtime Environment - Requirements

## Requirement
On a CPU-only device, embedding generation completes within acceptable latency thresholds.

## Status
**Blocked** - 2026-02-23

## Implementation Plan
- Implement embedding generation service (IEmbeddingService)
- Create integration tests that force CPU via environment variable
- Measure latency and verify it meets <250ms threshold

## Implementation Tasks
- [X] Define CPU-only latency threshold (250ms per embedding)
- [X] Ensure provider selection falls back to CPU when forced
- [X] Implement hardware override via DAIV3_FORCE_CPU_ONLY environment variable
- [ ] Implement IEmbeddingService that generates embeddings via ONNX Runtime
- [ ] Create integration test that runs with DAIV3_FORCE_CPU_ONLY=true
- [ ] Measure latency and verify <250ms threshold
- [ ] Verify embedding output is valid and CPU backend was used

## Implementation Summary
- Hardware detection and ONNX session factory support CPU fallback
- DAIV3_FORCE_CPU_ONLY environment variable simulates CPU-only device
- **BLOCKED**: No embedding generation service exists yet to test end-to-end

## Testing Plan
- Integration test that:
  1. Sets DAIV3_FORCE_CPU_ONLY=true to force CPU execution
  2. Loads real ONNX embedding model
  3. Generates embeddings for test text
  4. Verifies output is valid float array with correct dimensions
  5. Measures latency and asserts <250ms
  6. Verifies CPU provider was used via logs

## Testing Summary
- Unit tests for hardware tier selection with DAIV3_FORCE_CPU_ONLY: ✅ PASSING
- Unit tests for ONNX session options: ✅ PASSING
- Integration tests for actual embedding generation: ❌ NOT IMPLEMENTED (service doesn't exist)

## Usage and Operational Notes
- Set DAIV3_FORCE_CPU_ONLY=true to simulate CPU-only hardware on NPU/GPU device
- No embedding service exists yet to test acceptance criteria
- Latency threshold: <250ms per embedding on CPU

## Dependencies
- KLC-REQ-001
- KLC-REQ-003

## Related Requirements
- None
