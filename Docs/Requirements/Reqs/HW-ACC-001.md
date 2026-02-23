# HW-ACC-001

Source Spec: 2. Target Hardware & Runtime Environment - Requirements

## Requirement
On an NPU device, embedding generation uses the NPU by default.

## Status
**In Progress** - 2026-02-23

## Implementation Plan
- Ensure the underlying feature set is implemented and wired.
- Define the verification scenario and test harness.
- Add observability to confirm behavior in logs and UI.

## Implementation Tasks
- [X] Ensure hardware detection prefers NPU tier when available.
- [X] Validate provider preference selection for NPU tier in automated tests.
- [ ] Manual verification on NPU hardware.

## Implementation Summary
- Auto provider selection prefers DirectML when NPU is detected via hardware tiers.
- Structured logging indicates NPU preference when available.

## Testing Plan
- Automated test matching the acceptance scenario.
- Manual verification checklist for UI or user flows.

## Testing Summary
- Unit tests validate Auto preference resolves to DirectML when NPU tier is detected.
- Manual verification on real NPU hardware is still required.

## Usage and Operational Notes
- Auto provider selection is used by default; NPU preference is logged when detected.
- No UI surfaces yet; verification is through logs and hardware diagnostics.

## Dependencies
- KLC-REQ-001
- KLC-REQ-003

## Related Requirements
- None
