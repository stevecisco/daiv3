# PTS-REQ-003

Source Spec: 7. Projects, Tasks & Scheduling - Requirements

## Requirement
Projects SHALL store project-level instructions and model preferences.

## Implementation Plan
- ✅ Confirmed ownership in persistence (`Daiv3.Persistence`) via `projects.config_json` and CLI presentation boundary (`Daiv3.App.Cli`).
- ✅ Added `ProjectConfiguration` contract with `Instructions` and `ModelPreferences` (`PreferredModelId`, `FallbackModelId`) and normalization behavior.
- ✅ Implemented robust parsing and serialization for project configuration:
	- Returns safe defaults for null/empty/invalid JSON
	- Trims and drops whitespace-only values
	- Stores null in `config_json` when no configuration is supplied
- ✅ Extended CLI `projects create` with project configuration inputs:
	- `--instructions` / `-i`
	- `--preferred-model`
	- `--fallback-model`
- ✅ Updated CLI `projects list` and creation output to display stored instructions and model preferences.

## Testing Plan
- ✅ Unit tests added for parsing/serialization defaults, invalid JSON handling, and normalization behavior.
- ✅ Integration test coverage updated to verify round-trip persistence for instructions and model preferences via `ProjectRepository`.
- ✅ Required full-suite command executed after implementation updates.

## Usage and Operational Notes
- CLI create examples:
	- `./run-cli.bat projects create --name "My Project" -i "Use concise responses" --preferred-model "phi-4-mini" --fallback-model "gpt-4o-mini"`
	- `./run-cli.bat projects create -n "My Project" -r "C:\\repo\\src" -i "Focus on testability" --preferred-model "phi-4-mini"`
- CLI list output now surfaces:
	- `Instructions`
	- `Preferred Model`
	- `Fallback Model`
- Project configuration persists in `projects.config_json` using camelCase JSON.
- Empty/whitespace-only configuration values are normalized and not persisted.

## Dependencies
- KLC-REQ-010

## Related Requirements
- None

## Testing Summary

### Unit Tests: ✅ Passing

**Test Project:** [tests/unit/Daiv3.UnitTests/Daiv3.UnitTests.csproj](tests/unit/Daiv3.UnitTests/Daiv3.UnitTests.csproj)

**Test File:** [tests/unit/Daiv3.UnitTests/Persistence/ProjectConfigurationTests.cs](tests/unit/Daiv3.UnitTests/Persistence/ProjectConfigurationTests.cs)

**Added Coverage:**
- [Parse_WithNullOrWhitespace_ReturnsEmptyConfiguration](tests/unit/Daiv3.UnitTests/Persistence/ProjectConfigurationTests.cs#L8)
- [Parse_WithInvalidJson_ReturnsEmptyConfiguration](tests/unit/Daiv3.UnitTests/Persistence/ProjectConfigurationTests.cs#L20)
- [ToJsonOrNull_WithNoValues_ReturnsNull](tests/unit/Daiv3.UnitTests/Persistence/ProjectConfigurationTests.cs#L31)
- [ToJsonOrNull_WithValues_ProducesRoundTrippablePayload](tests/unit/Daiv3.UnitTests/Persistence/ProjectConfigurationTests.cs#L41)
- [ToJsonOrNull_WithWhitespaceOnlyValues_DropsEmptyFields](tests/unit/Daiv3.UnitTests/Persistence/ProjectConfigurationTests.cs#L65)

### Integration Tests: ✅ Passing

**Test Project:** [tests/integration/Daiv3.Persistence.IntegrationTests/Daiv3.Persistence.IntegrationTests.csproj](tests/integration/Daiv3.Persistence.IntegrationTests/Daiv3.Persistence.IntegrationTests.csproj)

**Test File:** [tests/integration/Daiv3.Persistence.IntegrationTests/ProjectRepositoryIntegrationTests.cs](tests/integration/Daiv3.Persistence.IntegrationTests/ProjectRepositoryIntegrationTests.cs)

**Updated Coverage:**
- [AddAndGetById_PersistsProjectCoreFields](tests/integration/Daiv3.Persistence.IntegrationTests/ProjectRepositoryIntegrationTests.cs#L60)

### Executed Validation Commands
- `dotnet test tests/unit/Daiv3.UnitTests/Daiv3.UnitTests.csproj --nologo --verbosity minimal --filter "FullyQualifiedName~ProjectConfigurationTests"`
- `dotnet test tests/integration/Daiv3.Persistence.IntegrationTests/Daiv3.Persistence.IntegrationTests.csproj --nologo --verbosity minimal --filter "FullyQualifiedName~ProjectRepositoryIntegrationTests"`
- `dotnet test Daiv3.FoundryLocal.slnx --nologo --verbosity minimal`

## Implementation Status
✅ **COMPLETE** - Projects now store project-level instructions and model preferences in `config_json`, with CLI create/list support, normalization and safe parsing behavior, and passing unit/integration/full-suite validation.
