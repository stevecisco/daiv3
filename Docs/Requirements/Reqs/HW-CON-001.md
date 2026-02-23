# HW-CON-001

Source Spec: 2. Target Hardware & Runtime Environment - Requirements

## Requirement
The PRIMARY runtime target is Windows 11 Copilot+ PCs with .NET 10, optimized for NPU hardware acceleration.

However, the system MUST support execution on other platforms and configurations, with graceful degradation:
- **Optimized tier:** Windows 11 Copilot+ devices with NPU hardware (Snapdragon X, Intel Core Ultra, etc.)
- **Fallback tier 1:** Windows 11 devices with dedicated GPU (NVIDIA, AMD) via DirectML
- **Fallback tier 2:** Any device with .NET 10 runtime using CPU-only execution with SIMD optimizations (TensorPrimitives)
- **Future support:** Non-Windows platforms (Linux, macOS) using CPU-only paths (not in initial release)

## Detailed Rationale
This requirement is NOT about blocking non-Windows or non-Copilot+ execution. Rather, it establishes:
1. **Design focus:** All architectural decisions prioritize Windows 11 Copilot+ hardware capabilities
2. **Optimization targets:** Hardware acceleration features are designed for NPU-first execution, with GPU and CPU fallbacks built-in
3. **Cross-platform compatibility:** Code must NOT contain Windows-only APIs; all platform-specific logic must be abstracted and the system must compile/run on other platforms (though without performance optimizations)
4. **Performance expectations:** Non-optimized tiers will have degraded performance but full functionality

## Implementation Plan
- Ensure all code compiles and runs on Windows 11+ with .NET 10 (primary validation target)
- Implement hardware detection to prefer NPU, then GPU, then CPU at runtime
- Avoid platform-specific APIs; use cross-platform abstractions (no P/Invoke to Windows-only APIs)
- Validate graceful fallback when hardware acceleration is unavailable
- Document expected performance on each tier in user documentation
- Support compilation on other platforms via CI/CD testing

## Testing Plan
- Unit tests: Verify hardware detection and selection logic works across all tiers
- Integration tests: Test on Windows 11 Copilot+ devices with NPU (primary validation)
- Integration tests: Test GPU fallback paths on devices with NVIDIA/AMD GPUs
- Integration tests: Test CPU fallback paths with TensorPrimitives SIMD
- Performance benchmarks: Document expected throughput on each tier
- Cross-platform compilation: Verify code compiles on Linux/macOS (though these won't run until MAUI/desktop app supports them)

## Usage and Operational Notes
- System automatically detects available hardware and selects optimal execution path
- No user configuration required for basic operation
- Advanced users can force specific hardware tier via config if needed (future enhancement)
- Non-Windows users will have CPU-only performance tier until platform-specific optimizations are added
- UI (MAUI) is Windows-only in initial release; future versions may support cross-platform UIs

### Test Overrides (Development Only)
- `DAIV3_FORCE_CPU_ONLY=true` forces CPU-only tier selection (disables NPU/GPU).
- `DAIV3_DISABLE_NPU=true` disables NPU detection.
- `DAIV3_DISABLE_GPU=true` disables GPU detection.

## Hardware Acceleration Strategy
| Hardware | Inference | Embedding | Status |
|----------|-----------|-----------|--------|
| NPU | Optimized | Optimized | Primary target (Copilot+ PCs) |
| GPU (DirectML) | Fallback | Fallback | Windows with dedicated GPU |
| CPU (TensorPrimitives) | Fallback | Fallback | Universal .NET 10 support |

## Cross-Platform Implementation Patterns

**Purpose:** Define techniques and preferences for handling platform-specific code, dependencies, and compatibility issues elegantly without creating Windows-only binaries.

### Available Techniques (in order of preference)

#### 1. **Target Framework Moniker (TFM) Multi-Targeting** ✅ PREFERRED
Use `.csproj` conditional logic to support multiple target frameworks:

**When to use:**
- Libraries with Windows-specific features (NPU, DirectML)
- Hardware acceleration components
- Any library that may have platform-specific optimizations
- **Standard practice:** All libraries should support both `net10.0` and `net10.0-windows10.0.26100`

**Pattern:**
```xml
<PropertyGroup>
  <TargetFramework>net10.0</TargetFramework>
  <RuntimeIdentifiers>win-x64;win-arm64</RuntimeIdentifiers>
</PropertyGroup>

<PropertyGroup Condition="$([MSBuild]::IsOSPlatform('Windows'))">
  <TargetFramework>net10.0-windows10.0.26100</TargetFramework>
</PropertyGroup>

<!-- Cross-platform packages (all TFMs) -->
<ItemGroup>
  <PackageReference Include="System.Numerics.Vectors" Version="*" />
</ItemGroup>

<!-- Windows-only packages (Windows TFM only) -->
<ItemGroup Condition="'$(TargetFramework)' == 'net10.0-windows10.0.26100'">
  <PackageReference Include="Microsoft.ML.OnnxRuntime.DirectML" Version="*" />
</ItemGroup>
```

**Advantages:**
- Single project file, no duplication
- Automatic detection of build platform
- Platform-specific dependencies resolved cleanly
- Enables NPU/DirectML acceleration on Windows, CPU fallback on others

#### 2. **Conditional Compilation** ✅ SECONDARY
Use C# preprocessor directives for platform-specific code paths within a single file:

**When to use:**
- Inline optimizations within a class
- Small, scoped platform-specific logic
- Runtime behavior differences (not APIs)
- Building on cross-platform code paths

**Pattern:**
```csharp
#if NET10_0_WINDOWS10_0_26100_OR_GREATER
    // Windows-specific implementation
    var options = SessionOptions.MakeSessionOptionWithDirectML();
    _logger.LogInformation("Using Windows DirectML for NPU acceleration");
#else
    // Cross-platform fallback
    var options = new SessionOptions();
    _logger.LogInformation("Using CPU inference (cross-platform fallback)");
#endif
```

**Advantages:**
- Keeps platform logic visible in the same code
- No separate files to maintain
- Clear what's platform-specific vs. shared

#### 3. **Partial Classes with Platform-Specific Files** ✅ TERTIARY
Split implementation across multiple files using partial class declarations:

**When to use:**
- Large, complex classes with significant platform-specific sections
- Multiple APIs need different implementations
- Code becomes hard to read with many #if directives

**Structure:**
```
MyService.cs              (shared interface, core logic)
MyService.Windows.cs      (platform-specific implementation)
MyService.Linux.cs        (platform-specific implementation)
```

**Pattern in .csproj:**
```xml
<ItemGroup Condition="'$(TargetFramework)' == 'net10.0-windows10.0.26100'">
  <Compile Include="MyService.Windows.cs" />
</ItemGroup>

<ItemGroup Condition="'$(TargetFramework)' == 'net10.0'">
  <Compile Include="MyService.Linux.cs" />
</ItemGroup>
```

**Advantages:**
- Clear separation of platform logic
- Easier to read and maintain large implementations
- Scales well for multiple platform variants

**Disadvantages:**
- More files to manage
- Partial class coordination needed

#### 4. **Adapter Pattern / Dependency Injection** ✅ WHEN APPLICABLE
Create platform-agnostic interfaces and swap implementations at runtime or compile-time:

**When to use:**
- Runtime hardware detection (choose NPU vs. GPU vs. CPU)
- Clean architecture with minimal platform coupling
- Multiple implementations needed simultaneously

**Pattern:**
```csharp
public interface IInferenceSessionProvider
{
    InferenceSession CreateSession(string modelPath);
}

#if NET10_0_WINDOWS10_0_26100_OR_GREATER
public class DirectMLSessionProvider : IInferenceSessionProvider
{
    public InferenceSession CreateSession(string modelPath)
        => new InferenceSession(modelPath, SessionOptions.MakeSessionOptionWithDirectML());
}
#endif

public class CpuSessionProvider : IInferenceSessionProvider // Always available
{
    public InferenceSession CreateSession(string modelPath)
        => new InferenceSession(modelPath);
}
```

**Advantages:**
- Clean separation of concerns
- Testable and mockable
- Runtime platform detection possible
- No platform-specific code in business logic

#### 5. **Separate Project Files** ❌ NOT PREFERRED (avoid)
Creating entirely separate project files for each platform (e.g., `Project.Windows.csproj`, `Project.Linux.csproj`):

**Why NOT preferred:**
- Duplicates code and project configuration
- Harder to keep in sync
- TFM multi-targeting handles this more elegantly
- Creates maintenance burden
- Only use if absolutely necessary for drastically different implementations

### Decision Tree: Choosing the Right Approach

```
Platform-specific code needed?
├─ YES: Code will be in the same binary/assembly
│  ├─ Can abstract with interface? → Use Adapter Pattern (DI)
│  ├─ Small, scoped logic? → Use Conditional Compilation (#if)
│  ├─ Large, multiple sections? → Use Partial Classes
│  └─ Different NuGet packages? → Use TFM Multi-Targeting in .csproj
│
└─ NO: Code works identically on all platforms
   └─ Use TFM `net10.0` only (cross-platform)
```

### Dependency Management & Platform Constraints

**When Adding a NuGet Package:**
1. **Check:** Does it target Windows only or support cross-platform?
   - Windows-only: Must use TFM conditional in .csproj
   - Cross-platform: Can reference normally
2. **Verify:** Does it work with .NET 10?
   - If not approved, see `Docs/Requirements/Architecture/approved-dependencies.md`
3. **Abstract:** If it's a critical path (model execution, embeddings), abstract behind an interface
4. **Test:** Ensure code still compiles for `net10.0` (even if only with CPU fallback)

**Unacceptable Patterns:**
- ❌ Using Windows-only APIs directly without abstraction
- ❌ Platform-specific P/Invoke without conditional compilation
- ❌ `Environment.OSVersion` checks for critical behavior without fallback
- ❌ Hardcoding OS checks that block non-Windows operation
- ❌ Adding Windows-only packages without TFM conditionals

### Reference Documentation

For complete details on TFM patterns, conditional compilation, and examples:
- **AI-Instructions.md Section 2:** Target Framework & Platform Configuration patterns
- **Solution-Structure.md:** Project structure and multi-targeting overview
- **Example project:** `Daiv3.Knowledge.Embedding` (uses TFM multi-targeting with DirectML)
- **Example project:** `Daiv3.Knowledge.DocProc` (CPU-only, cross-platform)

## Dependencies
- KLC-REQ-001
- KLC-REQ-003

## Related Requirements
- None
