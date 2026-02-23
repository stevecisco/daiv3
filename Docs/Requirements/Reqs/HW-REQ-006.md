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
- Added unit test coverage for Auto preference CPU fallback.

## Usage and Operational Notes
- Auto provider selection falls back to CPU when NPU/GPU are unavailable or insufficient.
- Vector similarity routing logs CPU fallback and uses CPU execution.
- No UI surfaces yet; behavior is controlled by DI registration and logging.

## Dependencies
- KLC-REQ-001
- KLC-REQ-003

## Related Requirements
- None
