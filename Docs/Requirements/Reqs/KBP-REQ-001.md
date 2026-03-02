# KBP-REQ-001

Source Spec: 6. Knowledge Back-Propagation - Requirements

## Requirement
The system SHALL support promotion levels: Context, Sub-task, Task, Sub-topic, Topic, Project, Organization (future), Internet (export).

## Implementation Summary
Implemented a dedicated knowledge back-propagation promotion-level contract in orchestration, separate from persistence learning scopes, so the full KBP hierarchy is represented and usable by upcoming UX/approval workflows.

### Core Components
- Added `KnowledgePromotionLevel` enum with the full KBP hierarchy in order:
	- Context
	- SubTask
	- Task
	- SubTopic
	- Topic
	- Project
	- Organization
	- Internet
- Added `IKnowledgePromotionService` interface to provide:
	- Supported level enumeration
	- Enabled level enumeration
	- Alias-aware parsing (`sub-task`, `subtopic`, `org`, etc.)
	- Enablement checks per level
- Added `KnowledgePromotionService` implementation:
	- Treats all required levels as supported
	- Marks `Organization` as disabled for now (future scope) while still modeled
	- Keeps `Internet` enabled to support export workflows
- Registered service in DI via `AddOrchestrationServices()`.

### Design Notes
- This requirement is implemented as a promotion-level capability model and service boundary.
- Existing learning lifecycle scope values in persistence remain unchanged for compatibility with LM requirements and migration constraints.
- KBP-REQ-002/003/005 can consume this service for target selection, confirmation flows, and internet-export behavior.

## Testing Summary
- Added unit test suite: `KnowledgePromotionServiceTests`.
- Coverage includes:
	- Full hierarchy order validation
	- Enabled levels behavior (`Organization` excluded, `Internet` included)
	- Alias parsing for canonical and shorthand values
	- Invalid/empty input handling
	- Per-level enablement checks
- Command executed:
	- `dotnet test tests/unit/Daiv3.UnitTests/Daiv3.UnitTests.csproj --filter "FullyQualifiedName~KnowledgePromotionServiceTests" --nologo --verbosity minimal`
- Result:
	- 24 test executions passed (12 tests across 2 target frameworks), 0 failed.

## Usage and Operational Notes
- Inject `IKnowledgePromotionService` into orchestration or UI flows needing promotion level metadata.
- Use `GetSupportedLevels()` for full hierarchy display and `GetEnabledLevels()` for currently actionable targets.
- Use `TryParseLevel(...)` for CLI/UI input normalization.
- `Organization` is intentionally represented but not currently enabled (future feature gate).

## Dependencies
- CT-REQ-009
- LM-REQ-001

## Related Requirements
- None

## Status
Complete (100%)
