# HW-CON-002

Source Spec: 2. Target Hardware & Runtime Environment - Requirements

## Requirement
.NET 10 is the baseline runtime.

## Status
**COMPLETE** - All projects validated and migrated to .NET 10.

## Implementation Summary

### Target Framework Configuration
All 27 projects in the solution use .NET 10 as the baseline:
- **Core Libraries:** net10.0 (cross-platform support)
- **Windows-specific Projects:** net10.0-windows10.0.26100 (Windows 11 Copilot+ targeting)  
- **Multi-targeting Projects:** net10.0;net10.0-windows10.0.26100 (both platforms supported)

### Projects Updated
- **Daiv3.FoundryLocal.IntegrationTests** - Migrated from net8.0-windows10.0.26100 to net10.0-windows10.0.26100

### Build Validation
✅ Solution builds successfully with zero errors or warnings
- All 27 projects compile to target frameworks
- Cross-platform (net10.0) and Windows-specific (net10.0-windows10.0.26100) targets verified
- Build time: ~20 seconds with clean restore

### Implementation Plan
- ✅ Validate design decisions against the stated constraint
- ✅ All projects using .NET 10 baseline runtime
- ℹ️ Runtime checks and configuration validation deferred to orchestration layer (future requirements)
- ℹ️ Documentation updates in developer/user docs deferred to Phase 6 (UI & Documentation)

## Testing Plan
- ✅ Configuration validation via successful solution build
- ✅ All projects compile to correct target frameworks
- ℹ️ Runtime version enforcement tests deferred to orchestration layer implementation

## Usage and Operational Notes
### Development
- Target 'net10.0' for cross-platform libraries (Windows/Linux)
- Target 'net10.0-windows10.0.26100' for Windows-specific components
- Use multi-targeting when library supports both

### Build
```bash
dotnet build Daiv3.FoundryLocal.slnx
```

### Supported Platforms
- **Primary:** Windows 11 (Copilot+ devices with NPU)
- **Fallback:** Windows 11 with GPU or CPU acceleration
- **Future:** Linux support via net10.0 cross-platform builds

## Dependencies
- KLC-REQ-001 (ONNX Runtime DirectML - Hardware Context)
- KLC-REQ-003 (TensorPrimitives - Vector Operations)

## Related Requirements
- HW-CON-001 (Windows 11 Copilot+ runtime target)
- HW-REQ-001 through HW-REQ-006 (Hardware abstraction layer)
- HW-ACC-001 through HW-ACC-003 (Hardware acceptance tests)

