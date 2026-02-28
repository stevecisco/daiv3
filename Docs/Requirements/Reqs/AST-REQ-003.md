# AST-REQ-003

Source Spec: 8. Agents, Skills & Tools - Requirements

## Requirement
The system SHALL allow dynamic creation of agents for new task types.

## Implementation Summary

Status: ✅ Complete (February 28, 2026)

### Core Design
- Extended `IAgentManager` with `GetOrCreateAgentForTaskTypeAsync` and `DynamicAgentCreationOptions`.
- Implemented dynamic task-type creation in `AgentManager` with:
	- Task-type normalization/sanitization for deterministic naming.
	- Reuse-first strategy (in-memory map, then repository lookup by generated name).
	- Auto-created metadata config keys (`task_type`, `creation_mode=dynamic`).
	- Race-condition recovery when concurrent requests attempt same dynamic agent creation.
- Added `OrchestrationOptions` defaults for dynamic creation:
	- `EnableDynamicAgentCreation`
	- `DynamicAgentNamePrefix`
	- `DynamicAgentPurposeTemplate`
	- `DynamicAgentDefaultSkills`
	- `DynamicAgentSkillsByTaskType`

### Orchestration Integration
- Updated `TaskOrchestrator` to request an execution agent per resolved task type using dynamic get-or-create semantics.
- Task execution now logs and reports which agent handled each task type.

### CLI Surface
- Added explicit CLI command for operational control and validation:
	- `agent create-for-task --task-type <type> [--name] [--purpose] [--skills ...]`

## Testing Summary

### Unit Tests: ✅ 80/80 Passing

**Test Project:** [tests/unit/Daiv3.UnitTests/Daiv3.UnitTests.csproj](../../../tests/unit/Daiv3.UnitTests/Daiv3.UnitTests.csproj)

**Updated Test Files:**
- [tests/unit/Daiv3.UnitTests/Orchestration/AgentManagerTests.cs](../../../tests/unit/Daiv3.UnitTests/Orchestration/AgentManagerTests.cs)
- [tests/unit/Daiv3.UnitTests/Orchestration/TaskOrchestratorTests.cs](../../../tests/unit/Daiv3.UnitTests/Orchestration/TaskOrchestratorTests.cs)

**AST-REQ-003-focused unit scenarios:**
- Dynamic agent created for previously unseen task type.
- Existing dynamic agent reused for repeated task type.
- Task-type skill mapping merged with dynamic defaults.
- Dynamic creation disabled path returns expected failure.
- Orchestrator execution path invokes task-type dynamic agent resolution.

### Integration Tests (AST-REQ-003 target): ✅ 2/2 Passing

**Test Project:** [tests/integration/Daiv3.Orchestration.IntegrationTests/Daiv3.Orchestration.IntegrationTests.csproj](../../../tests/integration/Daiv3.Orchestration.IntegrationTests/Daiv3.Orchestration.IntegrationTests.csproj)

**Test File:**
- [tests/integration/Daiv3.Orchestration.IntegrationTests/DynamicAgentCreationIntegrationTests.cs](../../../tests/integration/Daiv3.Orchestration.IntegrationTests/DynamicAgentCreationIntegrationTests.cs)

**AST-REQ-003 integration scenario:**
- Dynamic agent created in one DI scope and reused in a second scope via persisted repository state.

### Full Suite Validation
- `dotnet test Daiv3.FoundryLocal.slnx --nologo --verbosity minimal`
- Result: ✅ `1995 total, 0 failed, 1985 passed, 10 skipped`

## Usage and Operational Notes
- Dynamic creation is enabled by default and occurs automatically during task orchestration.
- Dynamic agents are deterministic per task type (normalized task key), reducing duplicate agent sprawl.
- Dynamic creation can be disabled with `OrchestrationOptions.EnableDynamicAgentCreation = false`.
- Operators can pre-create/resolve mappings explicitly via CLI:
	- `./run-cli.bat agent create-for-task --task-type "search"`
- Skills can be controlled globally (`DynamicAgentDefaultSkills`) or per task type (`DynamicAgentSkillsByTaskType`).

## Dependencies
- KLC-REQ-008

## Related Requirements
- None
