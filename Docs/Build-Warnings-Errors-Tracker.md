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
| 2026-02-28 | PTS-DATA-002 | `dotnet build Daiv3.FoundryLocal.slnx`; targeted `dotnet test` runs | 0 | 0 | No diagnostic delta introduced | N/A |

## Full Suite Test Baseline
- Date: 2026-02-28
- Command: `dotnet test Daiv3.FoundryLocal.slnx --nologo --verbosity minimal`
- Observed totals:
  - `Daiv3.UnitTests.dll (net10.0)`: Failed 4, Passed 722, Total 726
  - `Daiv3.UnitTests.dll (net10.0-windows10.0.26100)`: Failed 4, Passed 681, Total 685
  - Aggregate observed total: **1411 tests** (8 failed, 1403 passed)
- Use this baseline to detect test under-discovery from tool-only runs.

## Resolved Warning/Error Patterns (Prevention Notes)
Add short notes whenever a warning/error class is fixed so future requirements avoid reintroducing it.

| Date | Code | Root Cause | Fix Applied | Prevention Rule |
|---|---|---|---|---|
| _TBD_ | _TBD_ | _TBD_ | _TBD_ | _TBD_ |
