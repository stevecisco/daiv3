# KBP-DATA-001

Source Spec: 6. Knowledge Back-Propagation - Requirements

## Requirement
Promotions SHALL reference source task/session IDs.

## Implementation Summary

### Overview
Complete implementation of promotion history tracking for knowledge back-propagation (KBP-DATA-001 and KBP-DATA-002 implemented together). The system now persistently tracks all learning scope promotions with full provenance including source task/session IDs, timestamps, and audit information.

### Core Components Implemented

#### 1. Database Schema (Migration005_LearningPromotions)
- **Table:** `promotions` with comprehensive audit trail
- **Migration:** Migration005_LearningPromotions
- **Schema Version:** 5

**Table Structure:**
```sql
CREATE TABLE IF NOT EXISTS promotions (
    promotion_id TEXT PRIMARY KEY,
    learning_id TEXT NOT NULL,
    from_scope TEXT NOT NULL CHECK(from_scope IN ('Global', 'Agent', 'Skill', 'Project', 'Domain')),
    to_scope TEXT NOT NULL CHECK(to_scope IN ('Global', 'Agent', 'Skill', 'Project', 'Domain')),
    promoted_at INTEGER NOT NULL,
    promoted_by TEXT NOT NULL,
    source_task_id TEXT,
    source_agent TEXT,
    notes TEXT,
    FOREIGN KEY (learning_id) REFERENCES learnings(learning_id) ON DELETE CASCADE
);
```

**Indexes:**
- `idx_promotions_learning_id` - For querying promotion history by learning
- `idx_promotions_promoted_at` - For time-based queries (DESC)
- `idx_promotions_source_task_id` - **KBP-DATA-001:** For querying promotions by source task
- `idx_promotions_promoted_by` - For user/agent audit trails
- `idx_promotions_to_scope` - For querying promotions by target scope
- `idx_promotions_learning_promoted_at` - Composite index for efficient history queries

#### 2. Promotion Entity (`Promotion`)
**File:** `src/Daiv3.Persistence/Entities/CoreEntities.cs`

**Properties:**
- `PromotionId` - Primary key (UUID)
- `LearningId` - Foreign key to learnings table
- `FromScope` - Source scope (e.g., 'Skill', 'Agent')
- `ToScope` - **KBP-DATA-002:** Target scope (e.g., 'Agent', 'Project', 'Global')
- `PromotedAt` - **KBP-DATA-002:** Unix timestamp of promotion
- `PromotedBy` - User or agent who performed the promotion
- `SourceTaskId` - **KBP-DATA-001:** Optional task/session ID that triggered the promotion
- `SourceAgent` - Optional agent that triggered the promotion
- `Notes` - Optional human-readable notes

#### 3. PromotionRepository
**File:** `src/Daiv3.Persistence/Repositories/PromotionRepository.cs`

**Key Methods:**
- `AddAsync(Promotion)` - Records new promotion
- `GetByIdAsync(string)` - Retrieves promotion by ID
- `GetByLearningIdAsync(string)` - **Gets full promotion history for a learning** (ordered by most recent first)
- `GetBySourceTaskIdAsync(string)` - **KBP-DATA-001:** Query promotions by source task
- `GetByToScopeAsync(string)` - **KBP-DATA-002:** Query promotions by target scope
- `GetByPromotedByAsync(string)` - Query promotions by user/agent
- `GetByTimeRangeAsync(long, long)` - **KBP-DATA-002:** Query promotions by time range
- `UpdateAsync(Promotion)` - Updates promotion record
- `DeleteAsync(string)` - Removes promotion record

#### 4. LearningStorageService Enhancement
**File:** `src/Daiv3.Persistence/LearningService.cs`

**Updated `PromoteLearningAsync` Method:**
```csharp
public async Task<string?> PromoteLearningAsync(
    string learningId,
    string promotedBy = "user",
    string? sourceTaskId = null,
    string? sourceAgent = null,
    string? notes = null,
    CancellationToken ct = default)
```

**New Features:**
- Accepts optional `sourceTaskId` parameter (**KBP-DATA-001**)
- Accepts optional `sourceAgent` parameter
- Accepts optional `notes` parameter
- Automatically records promotion in `promotions` table
- Maintains backward compatibility (all new parameters are optional)

**Promotion Recording Flow:**
1. Validates learning exists
2. Calculates new scope based on hierarchy
3. Updates learning scope in `learnings` table
4. **Records promotion history** in `promotions` table with:
   - Unique promotion ID
   - Learning ID, from/to scopes
   - Timestamp (KBP-DATA-002)
   - Promoted by user/agent
   - Source task/session (KBP-DATA-001)
   - Optional notes
5. Fires observability event

## Testing

### Unit Tests (15/15 passing)
**File:** `tests/unit/Daiv3.UnitTests/Persistence/PromotionRepositoryTests.cs`

**Test Coverage:**
- Basic CRUD operations (add, get by ID, update, delete)
- Promotion history queries (by learning ID, returns all promotions)
- Source task/session tracking (**KBP-DATA-001**)
- Target scope and timestamp tracking (**KBP-DATA-002**)
- User/agent audit trail queries
- Time range queries
- Nullable field handling (optional fields work correctly)

### Integration Tests (6/6 passing)
**File:** `tests/integration/Daiv3.Persistence.IntegrationTests/LearningManagementWorkflowTests.cs`

**Test Scenarios:**
- `PromoteLearning_RecordsPromotionHistory` - **KBP-DATA-001/002:** Verifies promotion recording with full provenance
- `PromoteLearningMultipleTimes_RecordsFullHistory` - Verifies complete promotion chain tracking
- `GetBySourceTaskId_ReturnsPromotionsFromTask` - **KBP-DATA-001:** Verifies task-based queries
- `GetByToScope_ReturnsPromotionsToTargetScope` - **KBP-DATA-002:** Verifies scope-based queries
- `PromotionHistory_PersistsAcrossSessions` - Verifies database persistence
- `PromotionWithOptionalFields_WorksCorrectly` - Verifies backward compatibility

### Build Status
- **Compilation:** Zero new errors
- **Warnings:** No new warnings introduced

## Implementation Plan
- ✅ Defined schema changes and migration strategy (Migration005)
- ✅ Implemented data access layer updates (PromotionRepository)
- ✅ Implemented validation logic (CHECK constraints, foreign keys)
- ✅ Added serialization/deserialization (PromotionRepository mapping)
- ✅ Updated service registrations (PersistenceServiceExtensions)
- ✅ Data retention follows same policies as learnings (CASCADE on learning deletion)

## Usage and Operational Notes

### Programmatic Usage
```csharp
// Promote learning with full provenance
var newScope = await learningService.PromoteLearningAsync(
    learningId: "learning-123",
    promotedBy: "user-alice",
    sourceTaskId: "task-abc-def",  // KBP-DATA-001: Track which task triggered promotion
    sourceAgent: "automation-agent-001",
    notes: "High confidence and frequent usage"
);

// Query promotion history for a learning
var promotionRepo = serviceProvider.GetRequiredService<PromotionRepository>();
var history = await promotionRepo.GetByLearningIdAsync("learning-123");
// Returns list ordered by most recent first

// Query promotions from a specific task (KBP-DATA-001)
var taskPromotions = await promotionRepo.GetBySourceTaskIdAsync("task-abc-def");

// Query promotions to Global scope (KBP-DATA-002)
var globalPromotions = await promotionRepo.GetByToScopeAsync("Global");
```

### CLI Usage
The existing `learning promote` command automatically records promotion history:
```bash
daiv3-cli learning promote <learning-id>
```

Promotion history is now persistently tracked with default values:
- `promotedBy`: "user"
- `sourceTaskId`: null (manual CLI promotion)
- `sourceAgent`: null

### Observability
- Promotion operations are logged at `Information` level
- Each promotion generates structured log with learning ID, from/to scopes
- `ILearningObserver.OnLearningPromotedAsync()` event still fires for metrics

### Data Retention
- Promotions are linked to learnings via `FOREIGN KEY`
- **CASCADE DELETE:** When a learning is deleted/archived, its promotion history is also deleted
- This ensures referential integrity and prevents orphaned records

### Operational Constraints
- **Offline Mode:** Promotions are recorded synchronously during learning promotion
- **Performance:** Indexed queries on `source_task_id`, `to_scope`, and `learning_id` for efficiency
- **Budgets:** Minimal overhead - single INSERT per promotion
- **Permissions:** No additional permissions needed (same as learning management)

## Dependencies
- ✅ LM-REQ-001 - Learning entity model (complete)
- ✅ LM-REQ-003 - Learning storage (complete)
- ✅ LM-REQ-008 - Learning promotion operations (complete)
- ⚠️ CT-REQ-009 - Dashboard for promotion transparency (not started, but promotion data ready)

## Related Requirements
- **KBP-DATA-002** - Implemented together (target scope and timestamps)
- **KBP-REQ-001** - Promotion levels (enabled by this tracking)
- **KBP-NFR-002** - Provenance storage (fully implemented)
- **CT-REQ-009** - Future dashboard can query promotion history

## Status
**Complete (100%)**

**Files Changed:**
- `src/Daiv3.Persistence/SchemaScripts.cs` - Added Migration005
- `src/Daiv3.Persistence/DatabaseContext.cs` - Registered Migration005
- `src/Daiv3.Persistence/Entities/CoreEntities.cs` - Added Promotion entity
- `src/Daiv3.Persistence/Repositories/PromotionRepository.cs` - New repository (9 methods)
- `src/Daiv3.Persistence/LearningService.cs` - Enhanced PromoteLearningAsync with provenance
- `src/Daiv3.Persistence/PersistenceServiceExtensions.cs` - Registered PromotionRepository
- `tests/unit/Daiv3.UnitTests/Persistence/PromotionRepositoryTests.cs` - 15 unit tests
- `tests/integration/Daiv3.Persistence.IntegrationTests/LearningManagementWorkflowTests.cs` - 6 integration tests

**Commit:** (pending)

