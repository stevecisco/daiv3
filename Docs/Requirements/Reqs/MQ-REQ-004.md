# MQ-REQ-004

Source Spec: 5. Model Execution & Queue Management - Requirements

## Requirement
If P1 requests exist for the current model, the queue SHALL execute them before switching.

## Rationale
**Model switching in Foundry Local is expensive** (loading large model weights from disk takes multiple seconds). This requirement implements affinity-based batching to minimize these costly switches. By draining all P1 requests for the current model before switching, the queue group-batches work by model, reducing total switching overhead and keeping the user-facing queue responsive.

Without this requirement, a cache-thrashing scenario could occur: switch to model A (slow), process 1 request, switch to model B (slow), process 1 request, repeat → poor end-to-end latency and wasted disk I/O.

## Implementation Plan
- Identify the owning component and interface boundary.
- Define data contracts, configuration, and defaults.
- Implement the core logic with clear error handling and logging.
- Add integration points to orchestration and UI where applicable.
- Document configuration and operational behavior.

## Testing Plan
- Unit tests to validate primary behavior and edge cases.
- Integration tests with dependent components and data stores.
- Negative tests to verify failure modes and error messages.
- Performance or load checks if the requirement impacts latency.
- Manual verification via UI workflows when applicable.

## Usage and Operational Notes
- Describe how this capability is invoked or configured.
- List user-visible effects and any UI surfaces involved.
- Specify operational constraints (offline mode, budgets, permissions).

## Dependencies
- KLC-REQ-005
- KLC-REQ-006

## Related Requirements
- None
