# ES-ACC-003

Source Spec: 1. Executive Summary - Requirements

## Requirement
Adding a new skill does not require a core app rebuild.

## Implementation Summary
Implemented progressive markdown-native skill loading and runtime refresh so skill files can be added/edited without rebuilding binaries.

Key delivered behaviors:
- Markdown skill manifests supported in `SkillConfigFileLoader` (`.md` in addition to `.json/.yaml/.yml`).
- Markdown format supports your requested pattern: line 1 = name, line 2 = description, remainder = structured metadata/instructions.
- Metadata now supports richer skill descriptors: domain, language, scope level, project/sub-project/task identifiers, capabilities, restrictions, keywords, extends, override mode, inputs, output schema.
- Progressive hierarchy composition implemented: multiple definitions of same skill are merged by scope precedence (`Global -> Project -> SubProject -> Task`) with override mode (`Merge` or `Replace`).
- Runtime registry now supports removing stale skills (`ISkillRegistry.UnregisterSkill`) for update/delete scenarios.
- File watching support added via `SkillFileWatcherHostedService` for auto-reload on skill file changes.
- New CLI skill management commands added:
  - `daiv3 skill load`
  - `daiv3 skill list`
  - `daiv3 skill search`
  - `daiv3 skill tree`
  - `daiv3 skill scaffold`
- Seed `SkillCreator` skill added at `skills/global/SkillCreator.md` for prompt-driven skill creation workflows.

## Acceptance Coverage
Acceptance statement: "Adding a new skill does not require a core app rebuild."

Covered by implementation:
- New skills can be added as markdown or json files and loaded dynamically.
- Existing skills can be updated via file edits and automatically reloaded via watcher.
- Effective runtime skill composition supports multi-level inheritance/override semantics.
- Skills are searchable by multiple slices (query/scope/domain/language/capability) through catalog indexing.

## Testing Evidence
Automated tests added/updated:
- `tests/unit/Daiv3.Orchestration.Tests/SkillConfigurationTests.cs`
  - markdown parse from first two lines + metadata sections
  - directory hierarchy composition into effective skill
  - catalog capability search
- `tests/unit/Daiv3.Orchestration.Tests/ModuleAutoLoadHostedServiceTests.cs`
  - startup load of markdown skill files
- `tests/unit/Daiv3.Orchestration.Tests/SkillRegistryTests.cs`
  - `UnregisterSkill` behavior

Executed validation:
- `dotnet test tests/unit/Daiv3.Orchestration.Tests/Daiv3.Orchestration.Tests.csproj --nologo --verbosity minimal`
  - Passed: 527/527
- `dotnet test tests/unit/Daiv3.App.Cli.Tests/Daiv3.App.Cli.Tests.csproj --nologo --verbosity minimal`
  - Passed: 16/16
- `dotnet build src/Daiv3.App.Cli/Daiv3.App.Cli.csproj --nologo --verbosity minimal`
  - Build succeeded

## Usage and Operational Notes
Authoring markdown skills:
- Line 1: skill name
- Line 2: skill description
- Recommended sections:
  - `## Metadata` (`scope`, `domain`, `language`, `project`, `subproject`, `task`, `override`, etc.)
  - `## Capabilities`
  - `## Restrictions`
  - `## Inputs`
  - `## Output`
  - `## Instructions`

CLI examples:
- Load skill directory: `daiv3 skill load --path skills --recursive`
- Search skills: `daiv3 skill search --path skills --query creator --capability scaffold`
- View hierarchy: `daiv3 skill tree --path skills --name SkillCreator`
- Scaffold scoped skill: `daiv3 skill scaffold --path skills --name SkillCreator --description "..." --scope Global`

Operational constraints:
- YAML remains unimplemented (dependency approval required for full YAML parser).
- Skill watcher requires `EnableModuleAutoDiscovery=true` and `EnableSkillFileWatcher=true`.

## Dependencies
- ARCH-REQ-001
- CT-REQ-003
- KLC-REQ-001
- KM-REQ-001
- MQ-REQ-001
- LM-REQ-001
- AST-REQ-006

## Related Requirements
- ES-REQ-005
- AST-ACC-001
