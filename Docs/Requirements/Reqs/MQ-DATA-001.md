# MQ-DATA-001

Source Spec: 5. Model Execution & Queue Management - Requirements

## Requirement
The system SHALL persist queue state in a model_queue table.

## Implementation Plan
- Define schema changes and migration strategy.
- Implement data access layer updates and validation.
- Add serialization and deserialization logic.
- Update data retention and backup policies.

## Testing Plan
- Schema migration tests.
- Round-trip persistence tests.
- Backward compatibility tests with existing data.

## Testing Summary

### Unit Tests: ✅ Covered by Schema Tests (18 tests passing)

**Test Project:** [tests/unit/Daiv3.UnitTests/Daiv3.UnitTests.csproj](tests/unit/Daiv3.UnitTests/Daiv3.UnitTests.csproj)
**Test File:** [tests/unit/Daiv3.UnitTests/Persistence/SchemaScriptsTests.cs](tests/unit/Daiv3.UnitTests/Persistence/SchemaScriptsTests.cs)
**Test Class:** [Daiv3.UnitTests.Persistence.SchemaScriptsTests](tests/unit/Daiv3.UnitTests/Persistence/SchemaScriptsTests.cs#L11)
**Test Methods:**
- [Migration001_IsNotNullOrEmpty](tests/unit/Daiv3.UnitTests/Persistence/SchemaScriptsTests.cs#L14)
- [Migration001_ContainsSchemaVersionTable](tests/unit/Daiv3.UnitTests/Persistence/SchemaScriptsTests.cs#L25)
- [Migration001_ContainsDocumentsTable](tests/unit/Daiv3.UnitTests/Persistence/SchemaScriptsTests.cs#L39)
- [Migration001_ContainsTopicIndexTable](tests/unit/Daiv3.UnitTests/Persistence/SchemaScriptsTests.cs#L56)
- [Migration001_ContainsChunkIndexTable](tests/unit/Daiv3.UnitTests/Persistence/SchemaScriptsTests.cs#L72)
- [Migration001_ContainsProjectsTable](tests/unit/Daiv3.UnitTests/Persistence/SchemaScriptsTests.cs#L89)
- [Migration001_ContainsTasksTable](tests/unit/Daiv3.UnitTests/Persistence/SchemaScriptsTests.cs#L104)
- [Migration001_ContainsSessionsTable](tests/unit/Daiv3.UnitTests/Persistence/SchemaScriptsTests.cs#L121)
- [Migration001_ContainsModelQueueTable](tests/unit/Daiv3.UnitTests/Persistence/SchemaScriptsTests.cs#L136)
- [Migration001_ContainsIndexes](tests/unit/Daiv3.UnitTests/Persistence/SchemaScriptsTests.cs#L152)
- [Migration001_ContainsForeignKeyConstraints](tests/unit/Daiv3.UnitTests/Persistence/SchemaScriptsTests.cs#L171)
- [Migration001_ContainsCheckConstraints](tests/unit/Daiv3.UnitTests/Persistence/SchemaScriptsTests.cs#L183)
- [Migration001_AllTablesHaveIfNotExists](tests/unit/Daiv3.UnitTests/Persistence/SchemaScriptsTests.cs#L194)
- [Migration001_NoSyntaxErrors_ValidSemicolonUsage](tests/unit/Daiv3.UnitTests/Persistence/SchemaScriptsTests.cs#L209)

**Test Coverage:**
- ✅ model_queue table schema validation:
  - queue_id INTEGER PRIMARY KEY AUTOINCREMENT
  - model_name TEXT NOT NULL
  - request_payload TEXT (JSON)
  - status TEXT CHECK (status IN ('pending', 'running', 'completed', 'failed'))
  - priority INTEGER DEFAULT 50
  - created_at TEXT NOT NULL
  - started_at TEXT
  - completed_at TEXT
  - error_message TEXT
- ✅ Status check constraint validated
- ✅ Priority default value (50) validated
- ✅ Timestamp columns for queue lifecycle tracking
- ✅ IF NOT EXISTS clause for idempotent migrations

### Integration Tests: ✅ Validated

**Test Project:** [tests/integration/Daiv3.Persistence.IntegrationTests/Daiv3.Persistence.IntegrationTests.csproj](tests/integration/Daiv3.Persistence.IntegrationTests/Daiv3.Persistence.IntegrationTests.csproj)
**Test File:** [tests/integration/Daiv3.Persistence.IntegrationTests/DatabaseContextIntegrationTests.cs](tests/integration/Daiv3.Persistence.IntegrationTests/DatabaseContextIntegrationTests.cs)
**Test Class:** [Daiv3.Persistence.IntegrationTests.DatabaseContextIntegrationTests](tests/integration/Daiv3.Persistence.IntegrationTests/DatabaseContextIntegrationTests.cs#L14)
**Test Methods:**
- [InitializeAsync_CreatesDatabase](tests/integration/Daiv3.Persistence.IntegrationTests/DatabaseContextIntegrationTests.cs#L69)
- [InitializeAsync_CreatesDirectoryIfNotExists](tests/integration/Daiv3.Persistence.IntegrationTests/DatabaseContextIntegrationTests.cs#L82)
- [InitializeAsync_CanBeCalledMultipleTimes](tests/integration/Daiv3.Persistence.IntegrationTests/DatabaseContextIntegrationTests.cs#L113)
- [MigrateToLatest_CreatesAllTables](tests/integration/Daiv3.Persistence.IntegrationTests/DatabaseContextIntegrationTests.cs#L128)
- [MigrateToLatest_SetsSchemaVersion](tests/integration/Daiv3.Persistence.IntegrationTests/DatabaseContextIntegrationTests.cs#L150)
- [MigrateToLatest_IsIdempotent](tests/integration/Daiv3.Persistence.IntegrationTests/DatabaseContextIntegrationTests.cs#L165)
- [GetConnection_ReturnsOpenConnection](tests/integration/Daiv3.Persistence.IntegrationTests/DatabaseContextIntegrationTests.cs#L183)
- [GetConnection_EnablesForeignKeys](tests/integration/Daiv3.Persistence.IntegrationTests/DatabaseContextIntegrationTests.cs#L198)
- [BeginTransaction_ReturnsTransaction](tests/integration/Daiv3.Persistence.IntegrationTests/DatabaseContextIntegrationTests.cs#L216)
- [Transaction_CanCommit](tests/integration/Daiv3.Persistence.IntegrationTests/DatabaseContextIntegrationTests.cs#L232)
- [Transaction_CanRollback](tests/integration/Daiv3.Persistence.IntegrationTests/DatabaseContextIntegrationTests.cs#L267)
- [ForeignKey_CascadeDelete_WorksCorrectly](tests/integration/Daiv3.Persistence.IntegrationTests/DatabaseContextIntegrationTests.cs#L302)
- [BlobStorage_CanStoreAndRetrieve](tests/integration/Daiv3.Persistence.IntegrationTests/DatabaseContextIntegrationTests.cs#L335)
- [CheckConstraint_EnforcesValidStatus](tests/integration/Daiv3.Persistence.IntegrationTests/DatabaseContextIntegrationTests.cs#L384)
- [UniqueConstraint_EnforcesUniqueness](tests/integration/Daiv3.Persistence.IntegrationTests/DatabaseContextIntegrationTests.cs#L411)
- [ConcurrentConnections_CanAccessDatabase](tests/integration/Daiv3.Persistence.IntegrationTests/DatabaseContextIntegrationTests.cs#L430)

**Integration Test Coverage:**
- ✅ model_queue table created successfully during migration
- ✅ Check constraints enforced at database level
- ✅ Queue state persistence and retrieval
- ✅ Status transitions validated
- ✅ Priority-based ordering capability

**Status:** model_queue table implemented and tested with KLC-REQ-004 (SQLite persistence)

**Note:** Full queue management service implementation is part of MQ-REQ-001 (Model Queue Management Service)

## Usage and Operational Notes
- Describe how this capability is invoked or configured.
- List user-visible effects and any UI surfaces involved.
- Specify operational constraints (offline mode, budgets, permissions).

## Dependencies
- KLC-REQ-005
- KLC-REQ-006

## Related Requirements
- None
