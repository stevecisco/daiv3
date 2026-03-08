# ES-REQ-007

Source Spec: 1. Executive Summary - Requirements

## Requirement
The system SHALL store structured learnings from agent activity for reuse in future tasks.

## Implementation Summary

### Status: Complete (100%)

ES-REQ-007 is implemented by integrating learning capture directly into agent execution.

### Core Changes

1. `AgentManager` now captures self-correction activity and persists it as structured learning records.
2. Self-correction metadata is tracked during execution iterations:
   - failed iteration number
   - failure reason
   - suggested correction
   - failed output
   - success iteration number and output
3. When an execution succeeds after at least one self-correction attempt, `AgentManager` creates a `SelfCorrectionTriggerContext` and calls `ILearningService.CreateSelfCorrectionLearningAsync(...)`.
4. The persisted learning includes provenance and reuse metadata:
   - `scope = Agent`
   - `source_agent = <agentId>`
   - `source_task_id = <task_id from context, else executionId>`
   - tags: `agent-execution,self-correction,es-req-007`
5. Learning persistence is best-effort and non-blocking for execution completion:
   - if learning services are unavailable or learning persistence fails, agent execution still completes
   - failures are logged with structured warning telemetry

### File Changes
- `src/Daiv3.Orchestration/AgentManager.cs`
  - Added optional `ILearningService` dependency
  - Added self-correction tracking fields in execution loop
  - Added `TryCreateSelfCorrectionLearningAsync(...)`
  - Added `BuildSelfCorrectionLearningDescription(...)`
- `tests/unit/Daiv3.Orchestration.Tests/AgentManagerTests.cs`
  - Added `ExecuteTaskAsync_WithSelfCorrection_StoresLearningFromAgentActivity`
  - Added `ExecuteTaskAsync_WithSelfCorrectionDisabled_DoesNotStoreSelfCorrectionLearning`

## Testing Summary

### Unit Tests
- Command:
  - `dotnet test tests/unit/Daiv3.Orchestration.Tests/Daiv3.Orchestration.Tests.csproj --nologo --verbosity minimal`
- Result:
  - Passed: 522
  - Failed: 0
  - Skipped: 0

### Integration Tests
- Command attempted:
  - `dotnet test tests/integration/Daiv3.Orchestration.IntegrationTests/Daiv3.Orchestration.IntegrationTests.csproj -f net10.0-windows10.0.26100 --nologo --verbosity minimal`
- Result:
  - Blocked by pre-existing compile issues in unrelated integration test files (for example `WebFetchRefreshAcceptanceTests.cs`, `OfflineWorkflowAcceptanceTests.cs`), not caused by ES-REQ-007 changes.

## Usage and Operational Notes
- This behavior is automatic during `IAgentManager.ExecuteTaskAsync(...)`.
- Learnings are only created when:
  - self-correction is enabled, and
  - criteria initially fails, and
  - a later iteration succeeds.
- If callers provide `context["task_id"]`, that value is used for `source_task_id`; otherwise, `executionId` is used.
- Created learnings are stored in the existing learning memory pipeline and become available for retrieval/injection in future tasks.

## Dependencies
- ARCH-REQ-001
- CT-REQ-003
- KLC-REQ-001
- KM-REQ-001
- MQ-REQ-001
- LM-REQ-001
- AST-REQ-006

## Related Requirements
- LM-REQ-001
- LM-REQ-003
- LM-REQ-005
