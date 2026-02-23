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

**Test Project:** `tests/unit/Daiv3.UnitTests/`
**Test File:** `Persistence/SchemaScriptsTests.cs`

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

**Test Project:** `tests/integration/Daiv3.Persistence.IntegrationTests/`
**Test Files:** `DatabaseContextIntegrationTests.cs`

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
