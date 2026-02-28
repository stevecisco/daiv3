# PTS-REQ-004

Source Spec: 7. Projects, Tasks & Scheduling - Requirements

## Requirement
The system SHALL support tasks with title, description, status, priority, dependencies, and schedule.

## Implementation Plan
- Identify the owning component and interface boundary.
- Define data contracts, configuration, and defaults.
- Implement the core logic with clear error handling and logging.
- Add integration points to orchestration and UI where applicable.
- Document configuration and operational behavior.

## Testing Plan
- Unit tests to validate primary behavior and edge cases.
- Integration tests with dependent components and data stores.
- Negative tests to verify failure modes and error messages.
- Performance or load checks if the requirement impacts latency.
- Manual verification via UI workflows when applicable.

## Usage and Operational Notes
- Describe how this capability is invoked or configured.
- List user-visible effects and any UI surfaces involved.
- Specify operational constraints (offline mode, budgets, permissions).

## Dependencies
- KLC-REQ-010

## Related Requirements
- None
---

## ✅ IMPLEMENTATION COMPLETE

**Status:** Complete (100%)  
**Commit:** [57d8a15](https://github.com/example/daiv3/commit/57d8a15)  
**Date:** February 28, 2026

### Deliverables

**Data Layer (Already Implemented - PTS-DATA-001):**
- `ProjectTask` entity in `Daiv3.Persistence.Entities.CoreEntities` with all required fields
- `TaskRepository` in `Daiv3.Persistence.Repositories.TaskRepository` with full CRUD operations
- Task schema migration with support for:
  - `title`, `description`, `status`, `priority` - core task properties
  - `dependencies_json` - JSON-serialized task dependency IDs
  - `scheduled_at`, `next_run_at`, `last_run_at` - task scheduling timestamps
  - `completed_at` - task completion timestamp
  - `result_json` - task execution results

**CLI Commands (310 lines added to Program.cs):**

1. **tasks list** - List all tasks with optional filtering
   - Options: `--project-id/-p` (filter by project), `--status/-s` (filter by status)
   - Output: Formatted table with ID, Title, Description, Project, Status, Priority, Dependencies, Timestamps
   - Example: `tasks list --project-id abc123 --status pending`

2. **tasks create** - Create a new task
   - Options:
     - `--title/-t` (required): Task title
     - `--description/-d` (optional): Task description
     - `--project-id/-p` (optional): Project ID to associate task with
     - `--priority` (optional, default: 5): Priority level 0-9
     - `--dependency/--dep` (repeatable): Task dependencies (alternate form: `--dependency <id>`)
   - Output: Confirmation with assigned task ID and all properties
   - Example: `tasks create --title "Implement API" --description "REST endpoints" --priority 8 --dependency task1 --dependency task2`

3. **tasks update** - Update a task
   - Options:
     - `--id` (required): Task ID to update
     - `--status/-s` (optional): New status (sets completed_at if status is "complete"/"completed")
     - `--priority` (optional): New priority level 0-9
   - Output: Confirmation with updated task properties
   - Example: `tasks update --id abc123 --status in-progress --priority 7`

**Testing Coverage:**
- **Unit Tests:** TaskRepositoryValidationTests (2 tests) - Constructor validation
- **Integration Tests:** TaskRepositoryIntegrationTests (3 tests)
  - `AddAndGetById_PersistsTaskWithDependencyMetadata` - Full round-trip persistence
  - `ExistingTaskWithoutDependencies_CanBeUpdatedWithDependencyMetadata` - Legacy compatibility
  - `DeletingProject_PreservesTaskAndDependencyMetadata` - Referential integrity
- **Full Test Suite:** All 696 unit + 737 windows unit tests pass, 58 integration tests pass
- **Build Status:** Zero errors, solution builds cleanly

### Key Features Implemented

✅ **Task Properties:**
- Title (required), Description (optional)
- Status tracking (stored as string: pending, queued, in-progress, complete, failed, blocked)
- Priority (0-9 numeric scale)
- Dependencies (JSON array of task IDs)
- Schedule fields (scheduled_at, next_run_at, last_run_at, completed_at)

✅ **Repository Operations:**
- `AddAsync()` - Create task with full validation
- `UpdateAsync()` - Modify task properties with logging
- `DeleteAsync()` - Remove task from database
- `GetByIdAsync()` - Retrieve single task by ID
- `GetAllAsync()` - List all tasks (ordered by priority desc, created_at asc)
- `GetByProjectIdAsync()` - Query tasks for specific project
- `GetByStatusAsync()` - Filter tasks by status

✅ **CLI Integration:**
- Full command infrastructure for task management
- Input validation (title required, priority 0-9 validation)
- Dependency serialization to JSON format
- Status transitions with completed timestamp tracking
- User-friendly output formatting with timestamps

✅ **Error Handling:**
- Null validation on repository methods
- Graceful missing task handling in update command
- Invalid priority range validation
- Clear error messages for failed operations

✅ **Integration with Prior Requirements:**
- Compatible with PTS-DATA-001 schema
- Works with ProjectRepository for project-task associations
- Ready for integration with KLC-REQ-010 scheduler
- Supports PTS-REQ-005 dependency resolution

### Design Decisions

1. **Dependencies Storage:** JSON array format allows flexible dependency lists without schema changes
2. **Status as String:** Allows extensible status values beyond enum constraints
3. **Timestamp Fields:** Unix seconds (long) for consistent time representation across platform
4. **CLI-first Implementation:** Validates task management workflow before MAUI integration
5. **Priority Numeric Range:** 0-9 scale allows future gradient-based scheduling decisions

### Acceptance Criteria Met

- ✅ Tasks support all required properties: title, description, status, priority, dependencies, schedule fields
- ✅ Tasks can be created via CLI with configurable properties
- ✅ Tasks can be listed with optional filtering
- ✅ Tasks can be updated to change status and priority
- ✅ Full persistence integration with SQLite
- ✅ Dependency information preserved in JSON format
- ✅ All tests passing (1,433 total suite pass)