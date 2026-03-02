# AST-NFR-002

Source Spec: 8. Agents, Skills & Tools - Requirements

## Requirement
Skill execution SHOULD be sandboxed where feasible.

## Status
- Complete
- Progress: 100%
- Date: 2026-03-01

## Implementation Summary
- Added configurable skill sandboxing in orchestration with 4 modes: `None`, `PermissionChecks`, `ResourceLimits`, `ProcessIsolation` (reserved for future implementation).
- Added permission policy enforcement with allow/deny lists and wildcard matching (`*`, `Area.*`).
- Added resource monitoring for memory/CPU with automatic cancellation when limits are exceeded.
- Integrated sandbox execution into `SkillExecutor` and surfaced optional resource metrics in `SkillExecutionResult`.
- Added DI registrations for sandbox configuration and permission validator.

## Files Added
- `src/Daiv3.Orchestration/SkillSandboxConfiguration.cs`
- `src/Daiv3.Orchestration/SkillPermissionValidator.cs`
- `src/Daiv3.Orchestration/SkillResourceMonitor.cs`
- `tests/unit/Daiv3.UnitTests/Orchestration/SkillPermissionValidatorTests.cs`
- `tests/unit/Daiv3.UnitTests/Orchestration/SkillResourceMonitorTests.cs`
- `tests/unit/Daiv3.UnitTests/Orchestration/SkillExecutorSandboxingTests.cs`
- `tests/integration/Daiv3.Orchestration.IntegrationTests/SkillSandboxIntegrationTests.cs`

## Files Updated
- `src/Daiv3.Orchestration/SkillExecutor.cs`
- `src/Daiv3.Orchestration/Interfaces/ISkillExecutor.cs`
- `src/Daiv3.Orchestration/OrchestrationServiceExtensions.cs`

## Operational Notes
- Backward compatibility is preserved by default with `AllowUntrustedSkills = true`.
- Hardened environments can disable untrusted skills and tighten allow/deny lists through `SkillSandboxConfiguration`.
- `ResourceLimits` mode records runtime resource snapshots and returns them in `SkillExecutionResult.ResourceMetrics`.

## Validation
- Targeted unit execution passed for new sandboxing coverage:
  - `SkillPermissionValidatorTests`
  - `SkillResourceMonitorTests`
  - `SkillExecutorSandboxingTests`
- Integration test project currently has pre-existing compile errors unrelated to this requirement in `AgentExecutionObservabilityIntegrationTests.cs` (missing `IDatabaseContextFactory`/`IDatabaseContext` types), which block running integration tests in that project.

## Dependencies
- AST-REQ-006

## Related Requirements
- None
