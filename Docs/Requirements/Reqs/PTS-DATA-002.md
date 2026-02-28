# PTS-DATA-002

Source Spec: 7. Projects, Tasks & Scheduling - Requirements

## Requirement
Scheduled tasks SHALL record next-run and last-run timestamps.

## Implementation Plan
- ✅ Added migration `Migration002_TaskSchedulingTimestamps` to evolve existing databases with `tasks.next_run_at` and `tasks.last_run_at`.
- ✅ Updated `ProjectTask` entity to include `NextRunAt` and `LastRunAt` timestamp fields.
- ✅ Updated `TaskRepository` SQL projections, inserts, updates, and row mapping to persist/retrieve next-run and last-run timestamps.
- ✅ Added indexes `idx_tasks_next_run_at` and `idx_tasks_last_run_at` for scheduling metadata lookup efficiency.

## Testing Plan
- ✅ Schema migration unit tests validate migration SQL and indexes for `next_run_at`/`last_run_at`.
- ✅ Round-trip integration persistence test validates timestamp storage and retrieval.
- ✅ Backward compatibility integration test validates legacy task rows can be read (null timestamps) and later updated with scheduling timestamps.

## Usage and Operational Notes
- No API surface changes were required in callers beyond existing `TaskRepository` usage.
- New scheduling metadata is persisted in SQLite `tasks` table columns:
	- `next_run_at` (nullable INTEGER, Unix seconds)
	- `last_run_at` (nullable INTEGER, Unix seconds)
- This requirement is persistence-only; no UI behavior changes were introduced.

## Dependencies
- KLC-REQ-010

## Related Requirements
- None

## Testing Summary

### Unit Tests: ✅ Passing

**Test Project:** [tests/unit/Daiv3.UnitTests/Daiv3.UnitTests.csproj](tests/unit/Daiv3.UnitTests/Daiv3.UnitTests.csproj)

**Test File:** [tests/unit/Daiv3.UnitTests/Persistence/SchemaScriptsTests.cs](tests/unit/Daiv3.UnitTests/Persistence/SchemaScriptsTests.cs)

**Added Coverage:**
- [Migration002_IsNotNullOrEmpty](tests/unit/Daiv3.UnitTests/Persistence/SchemaScriptsTests.cs#L122)
- [Migration002_AddsTaskNextRunAndLastRunColumns](tests/unit/Daiv3.UnitTests/Persistence/SchemaScriptsTests.cs#L132)
- [Migration002_ContainsTaskTimestampIndexes](tests/unit/Daiv3.UnitTests/Persistence/SchemaScriptsTests.cs#L142)

### Integration Tests: ✅ Passing

**Test Project:** [tests/integration/Daiv3.Persistence.IntegrationTests/Daiv3.Persistence.IntegrationTests.csproj](tests/integration/Daiv3.Persistence.IntegrationTests/Daiv3.Persistence.IntegrationTests.csproj)

**Test Files:**
- [tests/integration/Daiv3.Persistence.IntegrationTests/TaskRepositoryIntegrationTests.cs](tests/integration/Daiv3.Persistence.IntegrationTests/TaskRepositoryIntegrationTests.cs)
- [tests/integration/Daiv3.Persistence.IntegrationTests/DatabaseContextIntegrationTests.cs](tests/integration/Daiv3.Persistence.IntegrationTests/DatabaseContextIntegrationTests.cs)

**Updated Coverage:**
- [AddAndGetById_PersistsTaskWithDependencyMetadata](tests/integration/Daiv3.Persistence.IntegrationTests/TaskRepositoryIntegrationTests.cs#L62) (now asserts `NextRunAt`/`LastRunAt`)
- [ExistingTaskWithoutDependencies_CanBeUpdatedWithDependencyMetadata](tests/integration/Daiv3.Persistence.IntegrationTests/TaskRepositoryIntegrationTests.cs#L117) (now validates null legacy timestamps and update path)
- [MigrateToLatest_SetsSchemaVersion](tests/integration/Daiv3.Persistence.IntegrationTests/DatabaseContextIntegrationTests.cs#L133) (schema version now `2`)

**Executed Validation Commands:**
- `dotnet test tests/unit/Daiv3.UnitTests/Daiv3.UnitTests.csproj --filter FullyQualifiedName~SchemaScriptsTests`
- `dotnet test tests/integration/Daiv3.Persistence.IntegrationTests/Daiv3.Persistence.IntegrationTests.csproj --filter FullyQualifiedName~TaskRepositoryIntegrationTests`
- `dotnet test tests/integration/Daiv3.Persistence.IntegrationTests/Daiv3.Persistence.IntegrationTests.csproj --filter FullyQualifiedName~DatabaseContextIntegrationTests`
- `dotnet build Daiv3.FoundryLocal.slnx`

## Implementation Status
✅ **COMPLETE** - Scheduled task next-run and last-run timestamps are now stored and retrievable via schema migration + repository support with unit and integration test coverage.
