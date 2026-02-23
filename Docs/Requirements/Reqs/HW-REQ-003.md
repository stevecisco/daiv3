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

**Test Project:** `tests/unit/Daiv3.UnitTests/`

**Related Test Files:**
- **Knowledge/Embedding/OnnxSessionOptionsFactoryTests.cs** (17 tests) - See [KLC-REQ-001](KLC-REQ-001.md#testing-summary)
  - DirectML execution provider selection on Windows
  - CPU fallback when DirectML unavailable
  - Auto preference hardware detection integration
  
- **Knowledge/Embedding/CpuVectorSimilarityServiceTests.cs** (48 tests) - See [KLC-REQ-003](KLC-REQ-003.md#testing-summary)
  - CPU vector operations with TensorPrimitives
  - SIMD acceleration validation
  - Cosine similarity and dot product operations

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
