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
- Added unit tests for hardware-aware vector similarity routing and provider preference selection.
- Existing ONNX session options tests updated to include hardware detection input.

## Usage and Operational Notes
- Embedding execution provider preference defaults to Auto and is resolved using detected hardware tiers.
- Vector similarity uses a hardware-aware router; CPU fallback remains the current implementation path.
- No UI surfaces yet; behavior is controlled by DI registration and logging.

## Dependencies
- KLC-REQ-001
- KLC-REQ-003

## Related Requirements
- None
