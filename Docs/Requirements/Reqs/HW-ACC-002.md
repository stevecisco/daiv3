# HW-ACC-002

Source Spec: 2. Target Hardware & Runtime Environment - Requirements

## Requirement
On a GPU-only device, embedding generation completes without errors.

## Status
**In Progress** - 2026-02-23

## Implementation Plan
- Ensure the underlying feature set is implemented and wired.
- Define the verification scenario and test harness.
- Add observability to confirm behavior in logs and UI.

## Implementation Tasks
- [X] Ensure provider selection falls back to GPU when NPU is unavailable.
- [X] Validate provider preference selection for GPU-only tier in automated tests.
- [ ] Manual verification on GPU-only hardware.

## Implementation Summary
- Auto provider selection prefers DirectML when GPU tier is detected without NPU.
- Structured logging indicates GPU fallback when NPU is unavailable or insufficient.

## Testing Plan
- Automated test matching the acceptance scenario.
- Manual verification checklist for UI or user flows.

## Testing Summary
- Unit tests validate Auto preference resolves to DirectML for GPU-only tier with CPU fallback permitted.
- Manual verification on real GPU-only hardware is still required.

## Usage and Operational Notes
- Auto provider selection falls back to GPU (DirectML) when NPU is unavailable or insufficient.
- No UI surfaces yet; verification is through logs and hardware diagnostics.

## Dependencies
- KLC-REQ-001
- KLC-REQ-003

## Related Requirements
- None
