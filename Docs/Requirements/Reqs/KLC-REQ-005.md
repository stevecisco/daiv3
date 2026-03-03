# KLC-REQ-005

Source Spec: 12. Key .NET Libraries & Components - Requirements

## Requirement
The system SHALL integrate Foundry Local via Microsoft.Extensions.AI and the Foundry Local SDK.

## Status
**Complete (100%)**

## Implementation Summary
KLC-REQ-005 is implemented by wiring Foundry lifecycle orchestration into the model execution layer and app startup composition:

1. **Foundry SDK-backed lifecycle operations**
   - `FoundryLocalManagementService` now exposes lifecycle APIs used by execution flow:
     - `IsModelAvailableAsync(...)`
     - `LoadModelAsync(...)`
     - `UnloadModelAsync(...)`
     - `GetLoadedModelAsync(...)`
   - Lifecycle calls use SDK-version-tolerant invocation so the same code path works across current Foundry SDK surfaces.

2. **Single-model constraint + real lifecycle bridge**
   - `ModelLifecycleManager` now delegates load/unload behavior to Foundry management when registered.
   - Existing single-model enforcement, lock safety, metrics, and idempotency behavior remain intact.

3. **Microsoft.Extensions.AI integration path**
   - `FoundryBridge` accepts `IChatClient` for local execution through Microsoft.Extensions.AI abstractions when available.
   - If no `IChatClient` is registered, execution falls back to safe local behavior while still honoring model lifecycle switching.

4. **DI and startup integration**
   - `ModelExecutionServiceExtensions` registers `FoundryLocalManagementService` with model execution services.
   - CLI and MAUI startup registration now call `AddModelExecutionServices(...)` to ensure Foundry integration is active in host composition.

5. **Dependency alignment required by Foundry SDK graph**
   - Package versions were aligned for `Microsoft.Extensions.AI.Abstractions`, `Microsoft.Extensions.Logging.Abstractions`, and `Microsoft.Extensions.DependencyInjection.Abstractions` where required to eliminate NU1605 downgrade errors.

## Validation
- Targeted model execution tests passed:
  - `ModelLifecycleManagerTests`
  - `FoundryBridgeTests`
- Full solution build passed:
  - `dotnet build Daiv3.FoundryLocal.slnx --nologo --verbosity minimal -clp:ErrorsOnly`

## Files Updated (KLC-REQ-005 scope)
- `src/Daiv3.FoundryLocal.Management/FoundryLocalManagementService.cs`
- `src/Daiv3.ModelExecution/ModelLifecycleManager.cs`
- `src/Daiv3.ModelExecution/FoundryBridge.cs`
- `src/Daiv3.ModelExecution/ModelExecutionServiceExtensions.cs`
- `src/Daiv3.ModelExecution/Daiv3.ModelExecution.csproj`
- `src/Daiv3.App.Cli/Program.cs`
- `src/Daiv3.App.Cli/Daiv3.App.Cli.csproj`
- `src/Daiv3.App.Maui/MauiProgram.cs`
- `src/Daiv3.App.Maui/Daiv3.App.Maui.csproj`
- Plus package/version alignment in affected `src/**/*.csproj` and `tests/**/*.csproj` to keep restore/build healthy.

## Dependencies
- Microsoft.AI.Foundry.Local / Microsoft.AI.Foundry.Local.WinML
- Microsoft.Extensions.AI.Abstractions

## Related Requirements
- MM-REQ-001, MM-REQ-007, MM-REQ-014, MM-REQ-019
- MQ-REQ-001 (removes prior “awaiting KLC-REQ-005 SDK integration” blocker)
