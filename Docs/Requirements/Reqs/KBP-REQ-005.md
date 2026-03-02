# KBP-REQ-005

Source Spec: 6. Knowledge Back-Propagation - Requirements

## Requirement
Internet-level promotion SHALL create a draft artifact (e.g., blog post) for user review.

## Implementation Summary

### Core Components
1. **KnowledgeDraftArtifact model** (`Daiv3.Orchestration.Models`)
	 - Captures generated draft metadata: file path, file name, title, markdown content, generated timestamp, and included learning IDs.
2. **IKnowledgeInternetDraftService interface** (`Daiv3.Orchestration.Interfaces`)
	 - Defines orchestration boundary for Internet-level draft creation.
3. **KnowledgeInternetDraftService implementation** (`Daiv3.Orchestration`)
	 - Generates markdown draft artifacts for Internet-targeted promotions.
	 - Persists artifacts to local file system for user review.
4. **InternetKnowledgeDraftOptions**
	 - Configurable output directory (default `%LOCALAPPDATA%\Daiv3\drafts\knowledge-promotions`).
	 - Configurable description truncation length for artifact readability.
5. **CLI integration in `learning-promote from-task`**
	 - Detects requested `Internet` targets.
	 - Maps storage scope to `Global` for persistence compatibility.
	 - Generates and displays artifact location after successful promotions and summary generation.

### Behavior
- When a promotion target is `Internet`, the promotion still persists via existing learning scope rules.
- A dedicated review artifact is created in markdown format for user validation before public sharing.
- The artifact includes:
	- Review metadata (generated time, user, source task, count)
	- Proposed public post content section with key learnings
	- Internal promotion summary
	- Reviewer checklist

### Design Notes
- Persistence learning scope schema remains unchanged (`Skill/Agent/Project/Domain/Global`) for backward compatibility.
- Internet-level dissemination is represented as an export/review workflow in orchestration and CLI.
- Failures in draft generation do not block successful promotion persistence and summary generation.

## Testing Summary

### Unit Tests
- Added `KnowledgeInternetDraftServiceTests` with coverage for:
	- Draft file creation for Internet promotions
	- Filtering behavior (Internet targets only)
	- Negative case when no Internet promotion exists

### Validation Commands
- `dotnet test tests/unit/Daiv3.UnitTests/Daiv3.UnitTests.csproj --filter "FullyQualifiedName~KnowledgeInternetDraftServiceTests" --nologo --verbosity minimal`
	- Result: 6 tests passed, 0 failed.
- `dotnet build src/Daiv3.Orchestration/Daiv3.Orchestration.csproj --nologo --verbosity minimal`
	- Result: success (baseline warnings only).
- `dotnet build src/Daiv3.App.Cli/Daiv3.App.Cli.csproj --nologo --verbosity minimal`
	- Result: success (baseline warnings only).

## Usage and Operational Notes
- Invoke using existing batch command:
	- `daiv3-cli learning-promote from-task <taskId> -l <learningId> -s Internet`
- On successful promotion, CLI prints the generated draft artifact path for review.
- Draft artifacts are local files intended for manual review and curation before external publication.

## Dependencies
- CT-REQ-009
- LM-REQ-001

## Related Requirements
- KBP-REQ-004

## Files Changed
- `src/Daiv3.Orchestration/Models/KnowledgeDraftArtifact.cs` (new)
- `src/Daiv3.Orchestration/Interfaces/IKnowledgeInternetDraftService.cs` (new)
- `src/Daiv3.Orchestration/InternetKnowledgeDraftOptions.cs` (new)
- `src/Daiv3.Orchestration/KnowledgeInternetDraftService.cs` (new)
- `src/Daiv3.Orchestration/OrchestrationServiceExtensions.cs` (DI registration)
- `src/Daiv3.App.Cli/Program.cs` (Internet promotion handling + artifact output)
- `tests/unit/Daiv3.UnitTests/Orchestration/KnowledgeInternetDraftServiceTests.cs` (new)

## Status
Complete (100%)
