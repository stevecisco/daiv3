# HW-ACC-003

Source Spec: 2. Target Hardware & Runtime Environment - Requirements

## Requirement
On a CPU-only device, embedding generation completes within acceptable latency thresholds.

## Status
**In Progress** - 2026-02-23

## Implementation Plan
- Ensure the underlying feature set is implemented and wired.
- Define the verification scenario and test harness.
- Add observability to confirm behavior in logs and UI.

## Implementation Tasks
- [X] Define CPU-only latency threshold (250ms per embedding).
- [X] Add CPU-only latency integration test gated by model path.
- [ ] Execute latency test on CPU-only hardware and record results.

## Implementation Summary
- CPU fallback is selected when no accelerators are available.
- A gated integration test measures CPU embedding latency against a 250ms threshold.

## Testing Plan
- Automated test matching the acceptance scenario.
- Manual verification checklist for UI or user flows.

## Testing Summary
- Added integration test gated by `DAIV3_EMBEDDING_MODEL_PATH` for CPU latency.
- Manual execution on CPU-only hardware is still required.

## Usage and Operational Notes
- Set `DAIV3_EMBEDDING_MODEL_PATH` to run the CPU latency check.
- Auto provider selection falls back to CPU when NPU/GPU are unavailable or insufficient.
- For testing on non-CPU-only devices, set `DAIV3_FORCE_CPU_ONLY=true` before running the latency test.

## Dependencies
- KLC-REQ-001
- KLC-REQ-003

## Related Requirements
- None
