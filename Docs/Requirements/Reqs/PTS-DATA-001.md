# PTS-DATA-001

Source Spec: 7. Projects, Tasks & Scheduling - Requirements

## Requirement
The database SHALL store projects and tasks with dependency metadata.

## Implementation Plan
- ✅ Verified schema includes `projects` and `tasks` tables with dependency metadata column (`dependencies_json`).
- ✅ Added `ProjectTask` persistence entity with dependency metadata support.
- ✅ Implemented `TaskRepository` CRUD and task lookup methods (`GetByProjectIdAsync`, `GetByStatusAsync`).
- ✅ Registered `TaskRepository` in persistence DI via `AddPersistence(...)`.
- ✅ Added backward compatibility coverage for legacy task rows without dependency metadata.

## Testing Plan
- ✅ Schema migration test validates dependency metadata column exists.
- ✅ Round-trip persistence test verifies project/task storage and dependency metadata retrieval.
- ✅ Backward compatibility test verifies legacy task rows with null dependency metadata can be read and updated.

## Usage and Operational Notes
- Persistence registration remains unchanged for consumers: call `services.AddPersistence(...)` and request `TaskRepository` from DI.
- Dependency metadata is stored in `tasks.dependencies_json` as JSON text and preserved when parent projects are deleted (`ON DELETE SET NULL` only clears `project_id`).
- This is a persistence-layer requirement; no new UI surfaces or runtime configuration flags were introduced.

## Dependencies
- KLC-REQ-004

## Related Requirements
- PTS-REQ-001
- PTS-REQ-004
- PTS-DATA-002

## Testing Summary

### Unit Tests: ✅ 3/3 Passing (100%)

**Test Project:** [tests/unit/Daiv3.UnitTests/Daiv3.UnitTests.csproj](tests/unit/Daiv3.UnitTests/Daiv3.UnitTests.csproj)

**Test Files:**
- **[tests/unit/Daiv3.UnitTests/Persistence/SchemaScriptsTests.cs](tests/unit/Daiv3.UnitTests/Persistence/SchemaScriptsTests.cs)**
	- **Test Method:** [Migration001_ContainsTasksTable](tests/unit/Daiv3.UnitTests/Persistence/SchemaScriptsTests.cs#L104)
- **[tests/unit/Daiv3.UnitTests/Persistence/TaskRepositoryValidationTests.cs](tests/unit/Daiv3.UnitTests/Persistence/TaskRepositoryValidationTests.cs)**
	- **Test Class:** [Daiv3.UnitTests.Persistence.TaskRepositoryValidationTests](tests/unit/Daiv3.UnitTests/Persistence/TaskRepositoryValidationTests.cs#L9)
	- **Test Methods:**
		- [Constructor_WithNullDatabaseContext_ThrowsArgumentNullException](tests/unit/Daiv3.UnitTests/Persistence/TaskRepositoryValidationTests.cs#L12)
		- [Constructor_WithNullLogger_ThrowsArgumentNullException](tests/unit/Daiv3.UnitTests/Persistence/TaskRepositoryValidationTests.cs#L20)

### Integration Tests: ✅ 3/3 Passing (100%)

**Test Project:** [tests/integration/Daiv3.Persistence.IntegrationTests/Daiv3.Persistence.IntegrationTests.csproj](tests/integration/Daiv3.Persistence.IntegrationTests/Daiv3.Persistence.IntegrationTests.csproj)

**Test File:** [tests/integration/Daiv3.Persistence.IntegrationTests/TaskRepositoryIntegrationTests.cs](tests/integration/Daiv3.Persistence.IntegrationTests/TaskRepositoryIntegrationTests.cs)

**Test Class:** [Daiv3.IntegrationTests.Persistence.TaskRepositoryIntegrationTests](tests/integration/Daiv3.Persistence.IntegrationTests/TaskRepositoryIntegrationTests.cs#L12)

**Test Methods:**
- [AddAndGetById_PersistsTaskWithDependencyMetadata](tests/integration/Daiv3.Persistence.IntegrationTests/TaskRepositoryIntegrationTests.cs#L62)
- [ExistingTaskWithoutDependencies_CanBeUpdatedWithDependencyMetadata](tests/integration/Daiv3.Persistence.IntegrationTests/TaskRepositoryIntegrationTests.cs#L114)
- [DeletingProject_PreservesTaskAndDependencyMetadata](tests/integration/Daiv3.Persistence.IntegrationTests/TaskRepositoryIntegrationTests.cs#L151)

**Executed Validation Commands:**
- `dotnet test tests/integration/Daiv3.Persistence.IntegrationTests/Daiv3.Persistence.IntegrationTests.csproj --verbosity minimal`
- `dotnet build src/Daiv3.Persistence/Daiv3.Persistence.csproj`
- `dotnet build tests/unit/Daiv3.UnitTests/Daiv3.UnitTests.csproj`
- `dotnet build tests/integration/Daiv3.Persistence.IntegrationTests/Daiv3.Persistence.IntegrationTests.csproj`

## Implementation Status
✅ **COMPLETE** - Projects and tasks (including dependency metadata) are persisted through schema + repository support with dedicated unit and integration test coverage.
