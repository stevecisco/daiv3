# KLC-REQ-011

Source Spec: 12. Key .NET Libraries & Components - Requirements

## Requirement
The UI SHALL be implemented with WinUI 3 or Windows App SDK (or MAUI if chosen).

## Implementation Summary

**Framework Choice:** .NET MAUI (Multi-platform App UI)

The Daiv3 system uses **Microsoft.MAUI** as the primary UI framework for cross-platform user interface development. MAUI is the modern successor to Xamarin.Forms and provides a single codebase for Windows, macOS, iOS, and Android applications. For this project, we target **Windows 10+ (net10.0-windows10.0.26100)** using the native Windows MAUI implementation.

## Implementation Details

### Framework Setup
- **Project Location**: `src/Daiv3.App.Maui/`
- **Target Framework**: `net10.0-windows10.0.26100`
- **MAUI Configuration**: 
  - `SingleProject=true` (unified project structure)
  - `UseMaui=true` (MAUI runtime enabled)
  - `MauiXamlInflator=SourceGen` (compile-time XAML code generation for performance)
- **Dependency Injection**: MVVM patterns with DI via `MauiProgram.cs`

### Architecture
The MAUI application follows a clean MVVM (Model-View-ViewModel) architecture:

#### Pages (View Layer)
- **ChatPage** (`Pages/ChatPage.xaml`): Conversational AI interface with message history and input
- **DashboardPage** (`Pages/DashboardPage.xaml`): System status, hardware info, task queue monitoring
- **ProjectsPage** (`Pages/ProjectsPage.xaml`): Project management and listing
- **SettingsPage** (`Pages/SettingsPage.xaml`): Configuration for directories, model preferences, token budgets

#### ViewModels (ViewModel Layer)
- **BaseViewModel**: Abstract base with `Title` property and notification support
- **ChatViewModel**: Message state, input handling, send command
- **DashboardViewModel**: Hardware status, task queue statistics, system metrics
- **ProjectsViewModel**: Project list, create/update operations
- **SettingsViewModel**: Configuration display and updates

#### Shell Navigation
- **AppShell.xaml**: Tab-based navigation between Chat, Dashboard, Projects, and Settings

#### Resource Organization
- **Converters/**: Value converters for XAML binding (e.g., visibility, color mapping)
- **Resources/**: Color schemes, themes, string resources
- **Services/**: Service layer for DI integration (to be wired with orchestration)

### Integration with Broader System
- **Entry Point**: `MauiProgram.cs` - Configures dependency injection, services, and platform initialization
- **Knowledge Layer Integration**: `AddKnowledgeLayer()` DI extension (added in KM-REQ-018) enables startup embedding cache initialization
- **Orchestration Integration**: Placeholder for task orchestration services and agent/skill execution

### Critical Fix: DLL File Locking Resolution
**Issue**: The test project (`tests/unit/Daiv3.UnitTests/`) referenced the MAUI executable, causing the compiled DLL to be copied to the test bin directory. During test discovery, the DLL would be locked by the .NET Host process, preventing subsequent test runs and rebuilds.

**Resolution**: Updated `Daiv3.UnitTests.csproj` to add `<PrivateAssets>All</PrivateAssets>` to the MAUI project reference (along with existing `<ExcludeAssets>buildTransitive</ExcludeAssets>`). This prevents the executable DLL from being copied to the test output directory while allowing ViewModel unit tests to reference MAUI types directly via compile-time assembly loading.

**Impact**: 
- Presentation layer unit tests (ChatViewModelTests, DashboardViewModelTests, etc.) continue to execute successfully
- Test builds no longer encounter DLL file locking errors
- Full solution builds without errors or file access issues
- Test suite remains unaffected (1621+ tests for Windows target framework)

## Testing Plan

### Unit Tests
- **ViewModel Tests** (`Daiv3.UnitTests/Presentation/`):
  - `BaseViewModelTests.cs`: Base class property initialization
  - `ChatViewModelTests.cs`: Message handling, command validation (9 tests)
  - `DashboardViewModelTests.cs`: Status display, metric aggregation
  - `ProjectsViewModelTests.cs`: Project CRUD operations
  - `SettingsViewModelTests.cs`: Configuration updates
- **Test Execution**: Run with Windows target framework (`net10.0-windows10.0.26100`)
- **Status**: 43/43 unit tests passing (per ARCH-REQ-002 report)

### Integration Tests
- Placeholder for UI-to-orchestration integration tests when orchestration layer is complete
- Pending: Chat message routing to task orchestrator, dashboard real-time updates, settings persistence

### Manual Verification
- **Startup**: Launch MAUI app via `dotnet run --project src/Daiv3.App.Maui/`
- **Navigation**: Test tab navigation between pages
- **ViewModels**: Verify ViewModel initialization and state management (breakpoints in IDE)
- **Resource Loading**: Confirm styles and icons render correctly

## Usage and Operational Notes

### Running the MAUI Application
```bash
cd c:\_prj\stevecisco\private\daiv3
dotnet run --project src/Daiv3.App.Maui/Daiv3.App.Maui.csproj
```

### Development Workflow
1. **XAML Editing**: VS Code with MAUI extension provides IntelliSense and hot reload
2. **ViewModel Testing**: Unit tests via `dotnet test` with ViewModel mocking
3. **DI Configuration**: Register services in `MauiProgram.cs` using standard IServiceCollection patterns
4. **Resource Customization**: Update styles in `Resources/Styles/Colors.xaml`, `Styles.xaml`

### User-Visible Features
- **Tab-Based Navigation**: Four main tabs (Chat, Dashboard, Projects, Settings)
- **Responsive Layout**: Adapts to window size (responsive grid layouts)
- **Real-Time Updates**: Placeholder for live dashboard metrics (requires signal integration)
- **Offline Mode**: UI state preservation when offline (requires persistence layer wiring)

### Configuration & Defaults
- **Initial Title**: Each page displays its domain (Chat, Dashboard, etc.)
- **Theme**: Default light theme (customizable via Resources/Styles)
- **State Persistence**: Placeholder for ViewModel state caching (requires application lifecycle integration)

### Platform Constraints
- **Windows Only**: Targets Windows 10+ via `net10.0-windows10.0.26100`
- **Future Expansion**: iOS/macOS support requires adding platform-specific target frameworks and Platforms/ configurations
- **ARM64 & x64**: Builds for both architectures via `RuntimeIdentifiers=win-x64;win-arm64`

## Related Requirements
- **ARCH-REQ-002**: Presentation Layer implementation (Chat, Dashboard, Projects, Settings pages)
- **CT-REQ-002**: Settings UI for configuring directories, model preferences, token budgets
- **CT-REQ-003**: Real-time transparency dashboard for system activity
- **GLO-REQ-002**: UI label consistency with glossary definitions

## Acceptance Criteria
- ✅ MAUI framework selected and configured for Windows targeting
- ✅ Four core pages implemented (Chat, Dashboard, Projects, Settings)
- ✅ ViewModels with MVVM patterns and unit testable design
- ✅ Proper dependency injection architecture foundation
- ✅ DLL file locking issue resolved for robust test execution
- ✅ ViewModel unit tests passing (43/43 confirmed)
- ✅ Full solution builds without errors
- ✅ XAML compilation with source generation enabled for performance

## Status
- **Overall**: Complete
- **Last Updated**: 2026-03-03
- **Build Status**: ✅ 0 Errors, baseline warnings only
- **Test Status**: ✅ 43/43 ViewModel tests passing (Windows target)
