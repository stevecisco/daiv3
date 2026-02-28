# PTS-REQ-002

Source Spec: 7. Projects, Tasks & Scheduling - Requirements

## Requirement
Projects SHALL have root paths for scoped document indexing.

## Implementation Plan
- ✅ Confirmed ownership in persistence (`Daiv3.Persistence`) and CLI presentation boundary (`Daiv3.App.Cli`).
- ✅ Introduced `ProjectRootPaths` contract in persistence to normalize, serialize, and parse project root paths.
- ✅ Implemented compatibility behavior for existing stored values:
	- Supports new JSON array storage format (preferred)
	- Supports legacy single-path and delimited text values on read
- ✅ Updated CLI `projects create` flow to support explicit root path selection:
	- Added repeatable `--root-path` / `-r` option
	- Normalizes and de-duplicates paths
	- Validates each root path exists
	- Defaults to current working directory only when no root path is supplied
- ✅ Updated CLI `projects list` output to display configured root paths for each project.
- ✅ Added repository validation so project writes require non-empty `root_paths`.

## Testing Plan
- ✅ Unit tests added for root path serialization, normalization, and legacy parsing compatibility.
- ✅ Integration tests updated to validate persisted root path round-tripping in SQLite.
- ✅ Negative integration test added to verify empty `root_paths` is rejected.
- ✅ Full solution regression test run completed to confirm no cross-requirement regressions.

## Usage and Operational Notes
- Use CLI commands:
	- `./run-cli.bat projects create --name "My Project" --description "Project description" --root-path "C:\\repo\\src"`
	- `./run-cli.bat projects create --name "My Project" -r "C:\\repo\\src" -r "C:\\repo\\docs"`
	- `./run-cli.bat projects list`
- Root paths are persisted as normalized absolute paths in a JSON array string in `projects.root_paths`.
- Existing records with legacy single-string values remain readable and are surfaced correctly in CLI.
- If no root path is provided at create time, the current working directory is used.
- Provided root paths must exist on disk; invalid/non-existent paths fail command execution.

## Dependencies
- KLC-REQ-010

## Related Requirements
- None

## Testing Summary

### Unit Tests: ✅ Passing

**Test Project:** [tests/unit/Daiv3.UnitTests/Daiv3.UnitTests.csproj](tests/unit/Daiv3.UnitTests/Daiv3.UnitTests.csproj)

**Test File:** [tests/unit/Daiv3.UnitTests/Persistence/ProjectRootPathsTests.cs](tests/unit/Daiv3.UnitTests/Persistence/ProjectRootPathsTests.cs)

**Added Coverage:**
- [Serialize_WithEmptyPaths_ThrowsArgumentException](tests/unit/Daiv3.UnitTests/Persistence/ProjectRootPathsTests.cs#L8)
- [Serialize_AndParse_WithMultiplePaths_ReturnsNormalizedDistinctPaths](tests/unit/Daiv3.UnitTests/Persistence/ProjectRootPathsTests.cs#L14)
- [Parse_WithLegacySinglePath_ReturnsSingleNormalizedPath](tests/unit/Daiv3.UnitTests/Persistence/ProjectRootPathsTests.cs#L27)
- [Parse_WithLegacyDelimitedPaths_ReturnsDistinctNormalizedPaths](tests/unit/Daiv3.UnitTests/Persistence/ProjectRootPathsTests.cs#L38)

### Integration Tests: ✅ Passing

**Test Project:** [tests/integration/Daiv3.Persistence.IntegrationTests/Daiv3.Persistence.IntegrationTests.csproj](tests/integration/Daiv3.Persistence.IntegrationTests/Daiv3.Persistence.IntegrationTests.csproj)

**Test File:** [tests/integration/Daiv3.Persistence.IntegrationTests/ProjectRepositoryIntegrationTests.cs](tests/integration/Daiv3.Persistence.IntegrationTests/ProjectRepositoryIntegrationTests.cs)

**Added Coverage:**
- [AddAndGetById_PersistsProjectCoreFields](tests/integration/Daiv3.Persistence.IntegrationTests/ProjectRepositoryIntegrationTests.cs#L60)
- [AddAsync_WithEmptyRootPaths_ThrowsArgumentException](tests/integration/Daiv3.Persistence.IntegrationTests/ProjectRepositoryIntegrationTests.cs#L176)

### Executed Validation Commands
- `dotnet test tests/unit/Daiv3.UnitTests/Daiv3.UnitTests.csproj --nologo --verbosity minimal --filter "FullyQualifiedName~ProjectRootPathsTests"`
- `dotnet test tests/integration/Daiv3.Persistence.IntegrationTests/Daiv3.Persistence.IntegrationTests.csproj --nologo --verbosity minimal --filter "FullyQualifiedName~ProjectRepositoryIntegrationTests"`
- `dotnet build Daiv3.FoundryLocal.slnx --nologo --verbosity minimal`
- `dotnet test Daiv3.FoundryLocal.slnx --nologo --verbosity minimal`

## Implementation Status
✅ **COMPLETE** - Projects now support explicit, validated root paths for scoped document indexing with persistence, CLI input/output support, backward-compatible parsing, and passing unit/integration/full-suite validation.
