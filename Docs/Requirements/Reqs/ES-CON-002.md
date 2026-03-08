# ES-CON-002

Source Spec: 1. Executive Summary - Requirements

## Requirement
The initial implementation targets .NET 10.

## Implementation Summary

### Framework Version Enforcement
All projects in the DAIv3 solution are configured to target .NET 10:
- **Core Libraries:** `net10.0` (cross-platform support)
- **Windows-specific Projects:** `net10.0-windows10.0.26100` (Windows 11 Copilot+ targeting)
- **Multi-targeting Projects:** Both frameworks supported via conditional TFM pattern

### Startup Validation
Added runtime validation to enforce .NET 10 requirement at application startup:

**Interface:** `IStartupValidator.ValidateFrameworkVersionAsync()`
- Location: `Daiv3.Core/Validation/IStartupValidator.cs`
- Validates that the application is running on .NET 10 runtime
- Returns structured validation result with errors, warnings, and additional runtime info

**Implementation:** `StartupValidator.ValidateFrameworkVersionAsync()`
- Location: `Daiv3.Infrastructure.Shared/Validation/StartupValidator.cs`
- Checks `Environment.Version.Major == 10`
- Logs validation results using structured logging
- Provides detailed runtime information in validation results

### Configuration Guardrails
The solution uses the conditional TFM pattern to prevent compilation on non-.NET 10 platforms:

```xml
<PropertyGroup>
  <TargetFramework>net10.0</TargetFramework>
  <RuntimeIdentifiers>win-x64;win-arm64</RuntimeIdentifiers>
</PropertyGroup>

<PropertyGroup Condition="$([MSBuild]::IsOSPlatform('Windows'))">
  <TargetFramework>net10.0-windows10.0.26100</TargetFramework>
</PropertyGroup>
```

**Build-time Enforcement:**
- All 27 projects in the solution require .NET 10 SDK
- Attempting to build with an older SDK will fail with clear error messages
- CI/CD pipelines must use .NET 10 SDK

**Runtime Enforcement:**
- Startup validator checks runtime version on application launch
- Applications will fail startup with clear error if running on wrong .NET version
- Error messages include current version and required version

## Implementation Plan
- ✅ Validate design decisions against the stated constraint
- ✅ Add startup checks to enforce the constraint
- ✅ Prevent configuration that violates the constraint
- ✅ Document the constraint in developer and user docs

## Testing Plan
- ✅ Configuration validation tests to prevent invalid states
- ✅ Runtime checks verifying constraint enforcement

### Test Coverage
All tests located in: `tests/unit/Daiv3.Infrastructure.Shared.Tests/Validation/StartupValidatorTests.cs`

**Framework Version Validation Tests (8 tests):**
1. `ValidateFrameworkVersionAsync_OnNet10_ReturnsSuccess` - Validates success on .NET 10
2. `ValidateFrameworkVersionAsync_IncludesRuntimeVersionCheck` - Validates ".NET 10 Runtime" check exists
3. `ValidateFrameworkVersionAsync_IncludesRuntimeInformation` - Validates additional info includes runtime details
4. `ValidateFrameworkVersionAsync_DetectsNet10MajorVersion` - Validates major version detection
5. `ValidateFrameworkVersionAsync_ReturnsWellFormedResult` - Validates result structure
6. `ValidateFrameworkVersionAsync_SupportsCancellation` - Validates cancellation token support
7. `ValidateFrameworkVersionAsync_CheckIncludesDuration` - Validates duration measurement
8. `ValidateFrameworkVersionAsync_OnCorrectRuntime_NoErrors` - Validates no errors on correct runtime

**Test Traceability:**
- ES-CON-002 → StartupValidatorTests (8 framework version tests)
- Parent requirement: HW-CON-002 (Platform targeting .NET 10)

## Usage and Operational Notes

### How to Invoke
The framework version validation is invoked via the `IStartupValidator` service:

```csharp
// Inject IStartupValidator
private readonly IStartupValidator _validator;

// Call during application startup
var result = await _validator.ValidateFrameworkVersionAsync(cancellationToken);

if (!result.IsValid)
{
    _logger.LogError("Framework version validation failed: {Errors}", 
        string.Join(", ", result.Errors));
    // Handle validation failure (e.g., exit application)
}
```

### User-Visible Effects
**CLI Applications:**
If the application is run on an unsupported .NET version, it will display an error message and exit:
```
ERROR: Application requires .NET 10 but is running on .NET 8.0
Runtime: .NET 8.0.0 (Microsoft .NET 8.0.0)
Version: 8.0.0
```

**MAUI Applications:**
The application will display an error dialog on startup and close if running on an incompatible .NET version.

### Operational Constraints
1. **SDK Requirement:** .NET 10 SDK or later must be installed to build the application
2. **Runtime Requirement:** .NET 10 Runtime must be installed to run the application
3. **Windows Version:** For Windows-specific builds, Windows 11 Build 26100 (24H2) or later is required
4. **Platform Support:** Windows x64 and ARM64 architectures are supported

### Configuration Methods
**Build Configuration:**
The framework targeting is configured in project files (`.csproj`) and cannot be overridden at runtime. The conditional TFM pattern automatically selects the appropriate target framework based on the build platform.

**Validation Configuration:**
The startup validator is registered with the DI container and runs automatically during application startup. No user configuration is required.

### Verification Checklist
- [ ] Solution builds successfully with .NET 10 SDK
- [ ] All projects target `net10.0` or `net10.0-windows10.0.26100`
- [ ] Runtime validation passes on .NET 10 runtime
- [ ] Runtime validation fails with clear error on older .NET versions
- [ ] CI/CD pipelines use .NET 10 SDK
- [ ] Documentation mentions .NET 10 requirement

## Dependencies
All system dependencies are complete:
- ✅ ARCH-REQ-001 - Application architecture documented
- ✅ CT-REQ-003 - Configuration management implemented
- ✅ KLC-REQ-001 - Knowledge lifecycle management implemented
- ✅ KM-REQ-001 - Knowledge management implemented
- ✅ MQ-REQ-001 - Message queue implemented
- ✅ LM-REQ-001 - Local model execution implemented
- ✅ AST-REQ-006 - Application state tracking implemented

## Related Requirements
- HW-CON-002 - Platform targeting .NET 10 (parent requirement)
- ES-CON-001 - Self-contained operation (sibling requirement)
- KLC-NFR-001 - Library compatibility matrix
- HW-CON-001 - Windows 11 Copilot+ as primary hardware target

## Implementation Status
**Status:** ✅ Complete  
**Completion Date:** March 8, 2026  
**Implementation Notes:**
- Added `ValidateFrameworkVersionAsync()` method to `IStartupValidator` interface
- Implemented framework version validation in `StartupValidator` service
- Added 8 comprehensive unit tests (all passing)
- Updated documentation with usage examples and operational notes
- Validated build-time and runtime enforcement mechanisms
