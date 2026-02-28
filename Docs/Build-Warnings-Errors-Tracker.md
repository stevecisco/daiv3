# Build Warnings & Errors Tracker

Purpose: Track warning/error baselines and requirement-by-requirement deltas so temporary diagnostics do not regress silently.

## Policy
- Default compile behavior: warnings are allowed, errors are not.
- All requirement completions must include warning/error delta validation.
- Net-new errors are blocking.
- Net-new warnings should be fixed before completion.
- If unresolved after up to 3 focused attempts, ask user whether to:
  - accept temporarily and track here, or
  - continue remediation before proceeding.

## Baseline (2026-02-28)
- Command: `dotnet build Daiv3.FoundryLocal.slnx --nologo --verbosity minimal`
- Result: `316 Warning(s), 0 Error(s)`

### Baseline by Project/Code
| Project | Level | Code | Observed Count |
|---|---|---|---:|
| `src/Daiv3.Api/Daiv3.Api.csproj` | warning | NU1903 | 8 |
| `src/Daiv3.App.Cli/Daiv3.App.Cli.csproj` | warning | NU1903 | 8 |
| `src/Daiv3.App.Maui/Daiv3.App.Maui.csproj` | warning | NU1903 | 8 |
| `src/Daiv3.App.Maui/Daiv3.App.Maui.csproj` | warning | CS0618 | 80 |
| `src/Daiv3.Infrastructure.Shared/Daiv3.Infrastructure.Shared.csproj` | warning | CS0168 | 4 |
| `src/Daiv3.Knowledge.DocProc/Daiv3.Knowledge.DocProc.csproj` | warning | NU1903 | 8 |
| `src/Daiv3.Knowledge/Daiv3.Knowledge.csproj` | warning | NU1903 | 12 |
| `src/Daiv3.Orchestration/Daiv3.Orchestration.csproj` | warning | NU1903 | 12 |
| `src/Daiv3.Worker/Daiv3.Worker.csproj` | warning | NU1903 | 8 |
| `tests/unit/Daiv3.UnitTests/Daiv3.UnitTests.csproj` | warning | NU1903 | 12 |
| `tests/unit/Daiv3.UnitTests/Daiv3.UnitTests.csproj` | warning | IDISP001 | 268 |
| `tests/unit/Daiv3.UnitTests/Daiv3.UnitTests.csproj` | warning | IDISP003 | 124 |
| `tests/unit/Daiv3.UnitTests/Daiv3.UnitTests.csproj` | warning | IDISP004 | 24 |
| `tests/unit/Daiv3.UnitTests/Daiv3.UnitTests.csproj` | warning | IDISP005 | 4 |
| `tests/unit/Daiv3.UnitTests/Daiv3.UnitTests.csproj` | warning | IDISP006 | 12 |
| `tests/unit/Daiv3.UnitTests/Daiv3.UnitTests.csproj` | warning | IDISP016 | 12 |
| `tests/unit/Daiv3.UnitTests/Daiv3.UnitTests.csproj` | warning | IDISP017 | 4 |
| `tests/unit/Daiv3.UnitTests/Daiv3.UnitTests.csproj` | warning | IDISP025 | 16 |
| `tests/unit/Daiv3.UnitTests/Daiv3.UnitTests.csproj` | warning | xUnit2002 | 8 |

## Requirement Delta Log
| Date | Requirement | Build/Test Commands | New Errors | New Warnings | Resolution | User Decision |
|---|---|---|---:|---:|---|---|
| 2026-02-28 | PTS-REQ-002 | targeted `dotnet test` (ProjectRootPaths + ProjectRepositoryIntegrationTests), `dotnet build Daiv3.FoundryLocal.slnx --nologo --verbosity minimal`, `dotnet test Daiv3.FoundryLocal.slnx --nologo --verbosity minimal` | 0 | 0 | Added project root path normalization/validation + CLI root path options and persistence compatibility; no net-new warning/error pattern introduced beyond baseline classes | N/A |
| 2026-02-28 | PTS-REQ-001 | targeted `dotnet test` (ProjectRepository unit/integration), `dotnet build Daiv3.FoundryLocal.slnx --nologo --verbosity minimal`, `dotnet test Daiv3.FoundryLocal.slnx --nologo --verbosity minimal` | 0 | 0 | Added project persistence-backed CLI flows and project repository tests; no net-new warning/error pattern introduced beyond existing baseline classes | N/A |
| 2026-02-28 | PTS-DATA-002 | `dotnet build Daiv3.FoundryLocal.slnx`; targeted `dotnet test` runs | 0 | 0 | No diagnostic delta introduced | N/A |
| 2026-02-28 | Scheduler test remediation | targeted `runTests` (scheduler files); `dotnet test Daiv3.FoundryLocal.slnx --nologo --verbosity minimal` | 0 | 0 | Fixed scheduler DI activation and timeout test setup; full suite green on rerun | N/A |

## Full Suite Test Baseline
- Date: 2026-02-28
- Command: `dotnet test Daiv3.FoundryLocal.slnx --nologo --verbosity minimal`
- Observed totals:
  - Aggregate observed total: **1525 tests** (0 failed, 1515 passed, 10 skipped)
- Use this baseline to detect test under-discovery from tool-only runs.

## Resolved Warning/Error Patterns (Prevention Notes)
Add short notes whenever a warning/error class is fixed so future requirements avoid reintroducing it.

| Date | Code | Root Cause | Fix Applied | Prevention Rule |
|---|---|---|---|---|
| _TBD_ | _TBD_ | _TBD_ | _TBD_ | _TBD_ |
