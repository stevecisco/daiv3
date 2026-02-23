# 2. Target Hardware & Runtime Environment - Requirements

## Overview
This document specifies requirements derived from Section 2 of the design document. It defines supported hardware, OS, and execution provider priorities.

## Goals
- Run on Copilot+ PCs with NPUs across ARM64 and x64.
- Provide GPU and CPU fallback without code changes.
- Use ONNX Runtime DirectML to select execution providers.

## Functional Requirements
- HW-REQ-001: The system SHALL support Windows 11 Copilot+ devices with NPUs. (Covered by HW-CON-001)
- HW-REQ-002: The system SHALL support ARM64 (Snapdragon X Elite) and x64 (Intel Core Ultra NPU) architectures.
- HW-REQ-003: The system SHALL execute embedding generation and vector operations using ONNX Runtime DirectML.
- HW-REQ-004: The system SHALL prefer NPU execution for embeddings and batch vector operations when available.
- HW-REQ-005: The system SHALL fall back to GPU when NPU is not available or insufficient.
- HW-REQ-006: The system SHALL fall back to CPU when NPU and GPU are unavailable.

## Non-Functional Requirements
- HW-NFR-001: Execution provider selection SHOULD be automatic without user intervention.
- HW-NFR-002: Performance SHOULD remain usable under CPU fallback with SIMD acceleration.

## Constraints
- HW-CON-001: The runtime target is Windows 11 Copilot+.
- HW-CON-002: .NET 10 is the baseline runtime.

## Dependencies
- Microsoft.ML.OnnxRuntime.DirectML.
- .NET TensorPrimitives for CPU SIMD operations.

## Acceptance Criteria
- HW-ACC-001: On an NPU device, embedding generation uses the NPU by default.
- HW-ACC-002: On a GPU-only device, embedding generation completes without errors.
- HW-ACC-003: On a CPU-only device, embedding generation completes within acceptable latency thresholds.

## Out of Scope
- Linux or macOS support for v0.1.
- Non-Windows mobile device support.

## Risks and Open Questions
- Determine acceptable CPU-only performance thresholds for v0.1.
- Validate DirectML provider availability across target hardware.
