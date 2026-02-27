# MQ-REQ-001

Source Spec: 5. Model Execution & Queue Management - Requirements

## Requirement
The system SHALL enforce the constraint that only one Foundry Local model is loaded at a time.

## Rationale
**This is a Foundry Local SDK limitation, not a design choice.** The Foundry Local runtime cannot load multiple large language models simultaneously in a single process. This constraint drives two key architectural decisions:

1. **Intelligent Queue System** (MQ-REQ-003 through MQ-REQ-007) - Minimize costly model switches by batching requests with affinity for the currently-loaded model, keeping background work queued until switches are necessary.

2. **Priority-Based Preemption** - Allow P0 (user-facing) requests to interrupt queued work and trigger model switches, ensuring responsiveness without sacrificing background efficiency.

Attempting to work around this constraint by continuously loading/unloading models would result in poor performance and high latency. The queue system converts this limitation into a **managed resource constraint**.

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
