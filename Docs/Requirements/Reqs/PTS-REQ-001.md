# PTS-REQ-001

Source Spec: 7. Projects, Tasks & Scheduling - Requirements

## Requirement
The system SHALL support projects with name, description, status, and timestamps.

## Implementation Plan
- ✅ Confirmed ownership in persistence (`ProjectRepository`) and CLI presentation boundary (`Daiv3.App.Cli`).
- ✅ Implemented project persistence integration for CLI project commands:
	- `projects create` now persists project `name`, `description`, `status`, `created_at`, and `updated_at`.
	- `projects list` now reads persisted projects and displays required fields.
- ✅ Applied defaults/validation for core project lifecycle in CLI flow:
	- Default status: `active`
	- `created_at`/`updated_at`: set to UTC Unix seconds at create time
	- Root path defaulted to current working directory for persisted records
- ✅ Added repository-focused test coverage for project field persistence and retrieval.

## Testing Plan
- ✅ Unit tests added for `ProjectRepository` constructor validation guards.
- ✅ Integration tests added for persisted project entity behavior:
	- Create/read for required fields
	- Update lifecycle behavior (`status`, `updated_at`)
	- Query by `status` and `name`
- ✅ Full-suite regression validation executed to ensure no cross-requirement breakage.

## Usage and Operational Notes
- Use CLI commands:
	- `./run-cli.bat projects create --name "<name>" --description "<description>"`
	- `./run-cli.bat projects list`
- Persisted project status for this requirement uses schema-supported values and defaults to `active` at creation.
- Timestamps are stored as UTC Unix seconds in SQLite and shown in UTC in CLI output.
- This implementation is local-first and offline-safe; no online provider dependency is required.

## Dependencies
- KLC-REQ-010

## Related Requirements
- None

## Testing Summary

### Unit Tests: ✅ Passing

**Test Project:** [tests/unit/Daiv3.UnitTests/Daiv3.UnitTests.csproj](tests/unit/Daiv3.UnitTests/Daiv3.UnitTests.csproj)

**Test File:** [tests/unit/Daiv3.UnitTests/Persistence/ProjectRepositoryValidationTests.cs](tests/unit/Daiv3.UnitTests/Persistence/ProjectRepositoryValidationTests.cs)

**Added Coverage:**
- [Constructor_WithNullDatabaseContext_ThrowsArgumentNullException](tests/unit/Daiv3.UnitTests/Persistence/ProjectRepositoryValidationTests.cs#L11)
- [Constructor_WithNullLogger_ThrowsArgumentNullException](tests/unit/Daiv3.UnitTests/Persistence/ProjectRepositoryValidationTests.cs#L19)

### Integration Tests: ✅ Passing

**Test Project:** [tests/integration/Daiv3.Persistence.IntegrationTests/Daiv3.Persistence.IntegrationTests.csproj](tests/integration/Daiv3.Persistence.IntegrationTests/Daiv3.Persistence.IntegrationTests.csproj)

**Test File:** [tests/integration/Daiv3.Persistence.IntegrationTests/ProjectRepositoryIntegrationTests.cs](tests/integration/Daiv3.Persistence.IntegrationTests/ProjectRepositoryIntegrationTests.cs)

**Added Coverage:**
- [AddAndGetById_PersistsProjectCoreFields](tests/integration/Daiv3.Persistence.IntegrationTests/ProjectRepositoryIntegrationTests.cs#L58)
- [Update_ChangesDescriptionStatusAndUpdatedTimestamp](tests/integration/Daiv3.Persistence.IntegrationTests/ProjectRepositoryIntegrationTests.cs#L92)
- [GetByStatusAndGetByName_ReturnExpectedProject](tests/integration/Daiv3.Persistence.IntegrationTests/ProjectRepositoryIntegrationTests.cs#L130)

### Executed Validation Commands
- `dotnet test tests/unit/Daiv3.UnitTests/Daiv3.UnitTests.csproj --nologo --verbosity minimal --filter "FullyQualifiedName~ProjectRepositoryValidationTests"`
- `dotnet test tests/integration/Daiv3.Persistence.IntegrationTests/Daiv3.Persistence.IntegrationTests.csproj --nologo --verbosity minimal --filter "FullyQualifiedName~ProjectRepositoryIntegrationTests"`
- `dotnet build Daiv3.FoundryLocal.slnx --nologo --verbosity minimal`
- `dotnet test Daiv3.FoundryLocal.slnx --nologo --verbosity minimal`

## Implementation Status
✅ **COMPLETE** - Projects are now supported end-to-end in persistence-backed CLI flows with explicit `name`, `description`, `status`, `created_at`, and `updated_at` handling plus unit/integration/full-suite validation.
