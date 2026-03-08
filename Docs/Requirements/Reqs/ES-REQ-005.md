# ES-REQ-005

Source Spec: 1. Executive Summary - Requirements

## Requirement
The system SHALL support modular skills and agents that can be added without rebuilding the core application.

## Implementation Status
**Status:** Complete  
**Date Completed:** 2026-03-08

## Architecture

### Core Design
Implemented runtime module auto-discovery in orchestration startup so both skills and agents can be supplied as external JSON configuration files and loaded at application start without rebuilding binaries.

### Components Added

1. **ConfiguredSkill** (`src/Daiv3.Orchestration/Configuration/ConfiguredSkill.cs`)
- Production `ISkill` implementation that materializes from `SkillMetadata` + config.
- Supports declarative execution behavior:
	- `response_template` with `{{param}}` interpolation.
	- `static_output` fixed response.
	- Structured default output when no custom behavior is provided.

2. **ModuleAutoLoadHostedService** (`src/Daiv3.Orchestration/ModuleAutoLoadHostedService.cs`)
- Startup hosted service (`IHostedService`) that auto-loads configured modules.
- Loads skills first, then agents, so agent skill references resolve cleanly.
- Uses robust validation and logs warnings/errors while continuing with valid entries.
- Handles existing agents idempotently by skipping duplicate creates.

3. **OrchestrationOptions Extensions** (`src/Daiv3.Orchestration/OrchestrationOptions.cs`)
- `EnableModuleAutoDiscovery` (default `true`)
- `SkillConfigAutoLoadPath` (default `config/skills`)
- `AgentConfigAutoLoadPath` (default `config/agents`)
- `ModuleAutoLoadRecursive` (default `true`)

4. **DI Integration** (`src/Daiv3.Orchestration/OrchestrationServiceExtensions.cs`)
- Registered `SkillConfigFileLoader` and `AgentConfigFileLoader` in DI.
- Registered `ModuleAutoLoadHostedService` via `IHostedService` (singleton, non-duplicating registration).

## Configuration

Add or override in host configuration (for example `appsettings.json`):

```json
{
	"OrchestrationOptions": {
		"EnableModuleAutoDiscovery": true,
		"SkillConfigAutoLoadPath": "config/skills",
		"AgentConfigAutoLoadPath": "config/agents",
		"ModuleAutoLoadRecursive": true
	}
}
```

Skills and agents are loaded from the configured paths at startup, enabling external module rollout by file deployment rather than rebuild.

## Testing Plan

### Unit Tests Added
1. `ConfiguredSkillTests.ExecuteAsync_WithResponseTemplate_InterpolatesParameters`
2. `ConfiguredSkillTests.ExecuteAsync_WithoutTemplate_ReturnsStructuredPayload`
3. `ModuleAutoLoadHostedServiceTests.StartAsync_AutoDiscoveryDisabled_DoesNotCreateAgents`
4. `ModuleAutoLoadHostedServiceTests.StartAsync_WithValidConfigFiles_LoadsSkillAndCreatesAgent`

### Validation Run
- `dotnet test tests/unit/Daiv3.Orchestration.Tests/Daiv3.Orchestration.Tests.csproj --nologo --verbosity minimal`
- Result: **520 passed, 0 failed**

## Usage and Operational Notes

1. Place skill config JSON files under `config/skills` (or configured path).
2. Place agent config JSON files under `config/agents` (or configured path).
3. Start application host (CLI/API/Worker/MAUI process that registers orchestration services).
4. Modules are discovered and loaded automatically; no source changes or rebuild required.

Operational behavior:
- Invalid configs are skipped with detailed log messages.
- Valid modules continue loading even when some entries fail validation.
- Existing agent definitions are treated idempotently during startup auto-load.

## Dependencies
- ARCH-REQ-001
- CT-REQ-003
- KLC-REQ-001
- KM-REQ-001
- MQ-REQ-001
- LM-REQ-001
- AST-REQ-006
- AST-REQ-007

## Related Requirements
- ES-ACC-003
