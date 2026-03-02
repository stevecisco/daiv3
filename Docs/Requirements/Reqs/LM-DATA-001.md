# LM-DATA-001

Source Spec: 9. Learning Memory - Requirements

## Requirement
Learning records SHALL store provenance and timestamps.

## Implementation Summary

### Overview
Complete implementation of learning memory database schema with comprehensive provenance tracking and timestamp management per LM-DATA-001. The implementation provides a robust foundation for storing, retrieving, and managing AI learning records with full traceability.

### Database Schema
- **Table:** `learnings` (created via Migration 004)
- **Migration:** Migration004_LearningMemory
- **Schema Version:** 4

### Core Fields

**Identity & Content:**
- `learning_id` (TEXT PRIMARY KEY) - Unique identifier
- `title` (TEXT NOT NULL) - Short human-readable summary
- `description` (TEXT NOT NULL) - Full explanation of the learning

**Classification:**
- `trigger_type` (TEXT NOT NULL, CHECK constraint) - UserFeedback, SelfCorrection, CompilationError, ToolFailure, KnowledgeConflict, Explicit
- `scope` (TEXT NOT NULL, CHECK constraint) - Global, Agent, Skill, Project, Domain
- `status` (TEXT NOT NULL, CHECK constraint) - Active, Suppressed, Superseded, Archived

**Provenance Fields (LM-DATA-001):**
- `source_agent` (TEXT, nullable) - Agent or skill that generated the learning
- `source_task_id` (TEXT, nullable) - Task or session for traceability
- `created_by` (TEXT NOT NULL) - Agent ID or 'user' for manually created learnings

**Timestamp Fields (LM-DATA-001):**
- `created_at` (INTEGER NOT NULL) - Creation timestamp (Unix epoch)
- `updated_at` (INTEGER NOT NULL) - Last modification timestamp (Unix epoch)

**Semantic Retrieval:**
- `embedding_blob` (BLOB, nullable) - Vector embedding for semantic search
- `embedding_dimensions` (INTEGER, nullable) - Embedding vector dimensionality (e.g., 384, 768)

**Metadata:**
- `tags` (TEXT, nullable) - Comma-separated tags for filtering
- `confidence` (REAL NOT NULL, CHECK 0.0-1.0) - Confidence score for automatic injection
- `times_applied` (INTEGER NOT NULL, DEFAULT 0) - Retrieval count for analytics

### Performance Indexes
- **Status:** `idx_learnings_status` - Filter active/suppressed/archived learnings
- **Scope:** `idx_learnings_scope` - Filter by application scope
- **Trigger Type:** `idx_learnings_trigger_type` - Filter by creation trigger
- **Provenance:** `idx_learnings_source_agent`, `idx_learnings_source_task_id` - Traceability queries
- **Quality:** `idx_learnings_confidence` (DESC), `idx_learnings_times_applied` (DESC) - Ranking
- **Temporal:** `idx_learnings_created_at` (DESC) - Time-based queries
- **Creator:** `idx_learnings_created_by` - Filter by creator
- **Composite:** `idx_learnings_status_scope` - Most common query pattern (active learnings by scope)

### Data Integrity Constraints
- **CHECK Constraints:**
  - `trigger_type` must be one of 6 valid values
  - `scope` must be one of 5 valid values
  - `status` must be one of 4 valid values
  - `confidence` must be between 0.0 and 1.0
- **Primary Key:** `learning_id` (unique identifier)
- **Nullability:** Provenance fields (`source_agent`, `source_task_id`) allow NULL for manual learnings

### Data Access Layer

**Entity:** `Learning` class in `CoreEntities.cs`
- Full property-based model
- Nullable types for optional fields
- XML documentation for all properties

**Repository:** `LearningRepository` in `Repositories/LearningRepository.cs`
- Extends `RepositoryBase<Learning>`
- CRUD operations: GetByIdAsync, GetAllAsync, AddAsync, UpdateAsync, DeleteAsync (soft delete)
- Specialized queries:
  - `GetActiveAsync()` - Active learnings only
  - `GetByStatusAsync(status)` - Filter by status
  - `GetByScopeAsync(scope)` - Filter by scope
  - `GetBySourceAgentAsync(sourceAgent)` - Provenance tracking
  - `GetBySourceTaskAsync(sourceTaskId)` - Provenance tracking
  - `GetWithEmbeddingsAsync()` - Learnings ready for semantic search
  - `IncrementTimesAppliedAsync(learningId)` - Usage tracking

### Migration Strategy
- **Version:** 4
- **Idempotent:** Uses `CREATE TABLE IF NOT EXISTS`
- **Backward Compatible:** No changes to existing tables
- **Automatic:** Applied via `DatabaseContext.InitializeAsync()`

## Testing Summary

### Unit Tests: ✅ Passing (2 tests)
**File:** `tests/unit/Daiv3.UnitTests/Persistence/LearningRepositoryValidationTests.cs`
- Constructor validation (null checks for dependencies)

### Integration Tests: ✅ Passing (15 tests)
**File:** `tests/integration/Daiv3.Persistence.IntegrationTests/LearningRepositoryIntegrationTests.cs`

**Core CRUD Operations:**
1. ✅ AddAndGetById_PersistsLearningWithProvenanceAndTimestamps
2. ✅ AddAndGetById_PersistsLearningWithEmbedding
3. ✅ Update_ChangesLearningFieldsAndUpdateTimestamp
4. ✅ Delete_SoftDeletesByArchivingLearning

**Filtering & Queries:**
5. ✅ GetActiveAsync_ReturnsOnlyActiveLearnings
6. ✅ GetByStatusAsync_FiltersLearningsByStatus
7. ✅ GetByScopeAsync_FiltersLearningsByScope
8. ✅ GetBySourceAgentAsync_ReturnsLearningsFromSpecificAgent
9. ✅ GetBySourceTaskAsync_ReturnsLearningsFromSpecificTask

**Special Operations:**
10. ✅ IncrementTimesAppliedAsync_IncrementsCounterAndUpdatesTimestamp
11. ✅ GetWithEmbeddingsAsync_ReturnsOnlyActiveLearningsWithEmbeddings

**Schema Constraints:**
12. ✅ Schema_EnforcesCheckConstraintsOnTriggerType
13. ✅ Schema_EnforcesCheckConstraintsOnScope
14. ✅ Schema_EnforcesCheckConstraintsOnConfidence

**Provenance Support:**
15. ✅ ProvenanceFields_AllowNullValuesForManualLearnings

**Test Coverage:**
- Provenance field persistence and retrieval
- Timestamp tracking on creation and updates
- Soft delete behavior (archives instead of hard delete)
- CHECK constraint validation at database level
- Nullable provenance fields for manual learnings
- Embedding storage and retrieval
- Usage counter incrementation
- All specialized query methods

## Usage and Operational Notes

### How to Create a Learning
```csharp
var learning = new Learning
{
    LearningId = Guid.NewGuid().ToString(),
    Title = "Always dispose FileStream",
    Description = "File streams must be wrapped in using blocks to prevent locks.",
    TriggerType = "CompilationError",
    Scope = "Global",
    SourceAgent = "agent-123",      // Provenance
    SourceTaskId = "task-abc-456",  // Provenance
    Confidence = 0.95,
    Status = "Active",
    CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),  // Timestamp
    UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),  // Timestamp
    CreatedBy = "agent-123"         // Provenance
};

await learningRepository.AddAsync(learning);
```

### Provenance Tracking
All learnings include:
- **SourceAgent:** Which agent or skill created the learning
- **SourceTaskId:** Which task or session triggered the learning
- **CreatedBy:** Agent ID or 'user' for manual entries

These fields enable full traceability: "This learning came from agent X during task Y."

### Timestamp Management
- **CreatedAt:** Set once on creation, never changed
- **UpdatedAt:** Updated on any modification (edit, status change, increment)
- Both stored as Unix epoch (timezone-independent)

### Manual vs. Automatic Learnings
- **Automatic:** Created by agents, includes `source_agent` and `source_task_id`
- **Manual:** Created by users, provenance fields are NULL, `created_by = "user"`

### User-Visible Effects
- Learning records are fully observable via transparency dashboard (future: CT-REQ-003)
- Users can filter learnings by agent, task, scope, status, and time range
- All provenance fields are exposed for auditing and debugging

### Operational Constraints
- Soft deletes: Deleted learnings are archived, not removed
- Embeddings generated asynchronously after creation (LM-REQ-004)
- Confidence scores determine automatic vs. suggestion injection
- Times_applied counter tracked automatically during retrieval

## Dependencies
- ✅ **KLC-REQ-004:** SQLite persistence (Complete)
- ✅ **KM-REQ-013:** ONNX embedding generator (Complete - for future LM-REQ-004)
- ⏳ **CT-REQ-003:** Transparency dashboard (Not Started - for UI visibility)

## Related Requirements
- **LM-REQ-001:** Create learning when triggered (depends on LM-DATA-001)
- **LM-REQ-002:** Learning structure fields (implements schema per LM-DATA-001)
- **LM-REQ-003:** Store learnings in SQLite (implements persistence per LM-DATA-001)
- **LM-REQ-004:** Generate embeddings (will populate embedding_blob field)

## Files Changed
**Core Implementation:**
- `src/Daiv3.Persistence/SchemaScripts.cs` - Added Migration004_LearningMemory
- `src/Daiv3.Persistence/DatabaseContext.cs` - Registered Migration 4
- `src/Daiv3.Persistence/Entities/CoreEntities.cs` - Added Learning entity
- `src/Daiv3.Persistence/Repositories/LearningRepository.cs` - New repository (320 lines)

**Tests:**
- `tests/unit/Daiv3.UnitTests/Persistence/LearningRepositoryValidationTests.cs` - New (2 tests)
- `tests/integration/Daiv3.Persistence.IntegrationTests/LearningRepositoryIntegrationTests.cs` - New (15 tests)

**Documentation:**
- `Docs/Requirements/Reqs/LM-DATA-001.md` - This document
- `Docs/Requirements/Master-Implementation-Tracker.md` - Marked Complete

## Status
**Complete (100%)** - All acceptance criteria met:
- ✅ Learning records store provenance fields (source_agent, source_task_id, created_by)
- ✅ Learning records store timestamp fields (created_at, updated_at)
- ✅ Schema migration created and tested
- ✅ Entity and repository implemented with full CRUD
- ✅ 2 unit tests passing (constructor validation)
- ✅ 15 integration tests passing (schema, CRUD, queries, constraints)
- ✅ Zero compilation errors or warnings
- ✅ Backward compatible with existing database schema
