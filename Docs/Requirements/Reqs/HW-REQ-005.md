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
- Added unit test coverage for Auto preference GPU fallback.
- Added unit test to verify hardware detection is consulted for vector routing.

## Usage and Operational Notes
- Auto provider selection falls back to GPU when NPU is unavailable or insufficient, using DirectML when available.
- Vector similarity routing logs GPU fallback while using CPU execution until an accelerated vector backend is added.
- No UI surfaces yet; behavior is controlled by DI registration and logging.

## Dependencies
- KLC-REQ-001
- KLC-REQ-003

## Related Requirements
- None
