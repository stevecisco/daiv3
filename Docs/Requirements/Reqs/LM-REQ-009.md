# LM-REQ-009

Source Spec: 9. Learning Memory - Requirements

## Requirement
Users SHALL be able to manually create learnings.

## Implementation Summary

### Core Components Implemented

#### 1. CLI Command: `learning create`
- **Location:** [src/Daiv3.App.Cli/Program.cs](../../../src/Daiv3.App.Cli/Program.cs)
- **Handler:** `LearningCreateCommand` method

**Command Syntax:**
```bash
learning create --title <string> --description <string> 
  [--scope <scope>] [--confidence <0.0-1.0>] [--tags <tags>]
  [--source-agent <agent>] [--source-task <taskid>]
```

**Options:**
- `--title, -t` (required): Short human-readable summary
- `--description, -d` (required): Full explanation of what was learned
- `--scope, -s` (optional): Global, Agent, Skill, Project, Domain (default: Global)
- `--confidence, -c` (optional): Confidence score 0.0-1.0 (default: 0.7)
- `--tags, -g` (optional): Comma-separated tags for filtering
- `--source-agent, -a` (optional): Identifier of agent that benefits from this learning
- `--source-task` (optional): Task ID for provenance tracking

#### 2. Input Validation
The CLI handler validates:
- **Title**: Required, non-empty
- **Description**: Required, non-empty
- **Confidence**: Must be between 0.0 and 1.0
- **Scope**: Must be one of: Global, Agent, Skill, Project, Domain

#### 3. Learning Creation Flow
1. Parse and validate CLI arguments
2. Create `ExplicitTriggerContext` with:
   - `TriggerType = "Explicit"` (distinguishes manual creation)
   - `CreatedBy = "user"` (marks as user-created, not agent-created)
   - `AgentReasoning = "Manually created by user"`
   - All provided fields from CLI options
3. Call `LearningService.CreateExplicitLearningAsync(context)`
4. Display confirmation with learning ID and metadata
5. Suggest next action: view or list learnings

#### 4. Triggering Type: ExplicitTriggerContext
- **Trigger Type**: "Explicit" (for manual/deliberate creation)
- **Confidence Default**: 0.75 (moderate, user can override)
- **CreatedBy Field**: "user" to distinguish from agent-created learnings
- **Status**: Always created as "Active" (ready for injection)

#### 5. Embedding Generation
- Embeddings are automatically generated via `LearningService` (LM-REQ-004)
- If embedding generation fails, learning still created without embedding
- Semantic search support available for learnings with embeddings

#### 6. User-Facing Output
**Success Example:**
```
✓ Learning created successfully!

Learning ID: lm-abc123...
  Title: Testing Best Practice
  Scope: Project
  Status: Active
  Confidence: 0.950
  Trigger Type: Explicit
  Created: 2026-03-01 14:22:30 UTC
  Tags: testing,quality
  Source Agent: my-skill

The learning can be injected into prompts for similar tasks.
```

**Error Example:**
```
✗ Failed to create learning: Invalid scope 'InvalidScope'
```

### Architecture Integration

**Persistence Layer:**
- Uses existing `LearningRepository` (LM-REQ-003)
- Uses existing `LearningStorageService` wrapper
- No new database schema required

**Orchestration Layer:**
- Uses existing `LearningService.CreateExplicitLearningAsync()` (LM-REQ-001)
- No new business logic - manual creation uses same flow as explicit agent creation
- Embedding generation via `IEmbeddingGenerator` (LM-REQ-004)

**Presentation Layer (CLI):**
- New command with dedicated handler in `Program.cs`
- Input validation before service calls
- User-friendly error messages

### Dependencies
- **LM-REQ-001**: Learning service for creating learnings
- **LM-REQ-003**: Persistence layer for storage
- **LM-REQ-004**: Embedding generation for semantic search
- **CLI Framework**: System.CommandLine for command parsing

## Testing Summary

### Unit Tests
- Input validation tests (title, description, scope, confidence)
- CLI command parsing and option handling
- Error message generation

### Integration Tests
Location: [tests/integration/Daiv3.Orchestration.IntegrationTests/ManualLearningCreationTests.cs](../../../tests/integration/Daiv3.Orchestration.IntegrationTests/ManualLearningCreationTests.cs)

**Test Scenarios (9 tests):**
1. **ManualCreation_WithAllFields_SavesSuccessfully** - All options provided
2. **ManualCreation_WithMinimalFields_UsesDefaults** - Only required fields, validates defaults
3. **ManualCreation_GeneratesEmbedding** - Embedding generation for semantic search
4. **ManualCreation_WithDifferentScopes_AllSupported** - All scope values (Global, Agent, Skill, Project, Domain)
5. **ManualCreation_MarkedAsExplicitTrigger** - Trigger type and CreatedBy field
6. **ManualCreation_HighConfidence_Active** - High confidence creates active learning
7. **ManualCreation_LowConfidence_StillActive** - Low confidence also creates active learning
8. **ManualCreation_MultipleSequential_AllPersisted** - Multiple learnings created successfully
9. **ManualCreation_UserCreatedByTrackedForProvenance** - Provenance tracking via CreatedBy field

**Test Status**: 9/9 passing

## Usage and Operational Notes

### How It Works
1. User identifies a pattern, technique, or best practice worth capturing
2. User runs `learning create` with descriptive title and explanation
3. System generates embedding for semantic retrieval
4. Learning stored as "Active" with optional scope and confidence
5. Learning becomes available for future task context via semantic search (LM-REQ-005)
6. User can view, edit, suppress, or promote learnings via other commands

### User-Visible Effects
- New learning appears in `learning list` output
- Learning can be found by `learning view --id`
- Learning with high confidence auto-injected into prompts for similar tasks
- Learning can be tagged for domain-specific filtering
- CreatedBy shows "user" to indicate manual creation

### Integration with Learning Memory
- **Manual Creation**: Direct via CLI `learning create` (LM-REQ-009)
- **Automatic Creation**: Via agent feedback, corrections, errors (LM-REQ-001)
- **Retrieval & Injection**: Via semantic search before agent execution (LM-REQ-005)
- **Management**: View, edit, suppress, promote, supersede (LM-REQ-007, LM-REQ-008)

### Scope Hierarchy
Learnings can be scoped to different levels of applicability:
- **Global**: Apply to all agents and tasks (broadest impact)
- **Domain**: Apply to specific problem domain or field
- **Project**: Apply within a specific project context
- **Agent**: Apply only to specific agent or skill
- **Skill**: Apply only within specific skill

### Confidence Levels
Help control how aggressively learnings are injected:
- **0.9-1.0**: Auto-inject into similar tasks (high confidence)
- **0.7-0.9**: Inject as reinforcement (normal baseline for manual creation)
- **0.5-0.7**: Inject as suggestion only (experimental)
- **<0.5**: Inject on explicit request only (low confidence)

### Operational Constraints
- **Offline Mode**: Works fully offline (uses local database)
- **Storage**: Learnings persisted in SQLite
- **Scalability**: No limits on number of learnings (vector search scales to millions)
- **Permissions**: Currently available to all CLI users (future: role-based access)

## CLI Examples

See [CLI-Command-Examples.md](../../CLI-Command-Examples.md#create-a-manual-learning) for comprehensive examples.

**Quick Reference:**
```bash
# Basic manual learning
daiv3 learning create -t "My Learning" -d "Description"

# With all options
daiv3 learning create \
  --title "Pattern Name" \
  --description "Full explanation" \
  --scope Project \
  --confidence 0.95 \
  --tags "tag1,tag2" \
  --source-agent my-skill \
  --source-task task-123
```

## Relationship to Other Requirements

- **LM-REQ-001**: Uses same learning creation pipeline (CreateExplicitLearningAsync)
- **LM-REQ-003**: Leverages SQLite persistence for storage
- **LM-REQ-004**: Embedding generation for semantic retrieval
- **LM-REQ-005**: Learnings retrieved and injected into prompts
- **LM-REQ-007**: Manual learnings can be viewed, edited, filtered
- **LM-REQ-008**: Manual learnings can be suppressed, promoted, superseded
- **CT-REQ-003**: Manual creation useful for transparency/debugging
- Combined System: Users can capture knowledge as learnings, agents can discover and apply learnings
