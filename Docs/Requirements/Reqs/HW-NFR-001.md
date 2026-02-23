# HW-NFR-001

Source Spec: 2. Target Hardware & Runtime Environment - Requirements

## Requirement
Execution provider selection SHOULD be automatic without user intervention.

## Status
**Complete** - 2026-02-23

## Implementation Summary

Automatic execution provider selection is implemented via `OnnxSessionOptionsFactory` in the
`Daiv3.Knowledge.Embedding` project. When `OnnxExecutionProviderPreference.Auto` is specified
(the default), the factory uses `IHardwareDetectionProvider` to query available hardware tiers
and automatically selects the best execution provider:

1. **NPU detected**: Uses DirectML execution provider for NPU acceleration
2. **GPU detected (no NPU)**: Uses DirectML execution provider for GPU acceleration  
3. **CPU only**: Falls back to CPU execution provider with SIMD optimizations

The selection logic is in `OnnxSessionOptionsFactory.ResolvePreference()` which:
- Queries `IHardwareDetectionProvider.GetAvailableTiers()` to discover hardware
- Selects DirectML provider for NPU or GPU tiers
- Falls back to CPU provider when no accelerators are available
- Logs selection decisions for observability

No user intervention is required; the system automatically selects the best provider based on
runtime hardware detection. Users can override via `EmbeddingOnnxOptions.ExecutionProviderPreference`
for testing or special use cases, but the default Auto mode handles all scenarios.

## Implementation Plan
- [X] Define hardware detection interface and query method
- [X] Implement automatic provider resolution logic based on hardware tiers
- [X] Add logging for selection decisions and fallback scenarios
- [X] Configure Auto as default preference in options
- [X] Document configuration knobs for advanced scenarios

## Implementation Tasks
- [X] Query `IHardwareDetectionProvider.GetAvailableTiers()` to discover hardware
- [X] Implement `ResolvePreference()` method with tier-based selection logic
- [X] Map NPU/GPU tiers to DirectML provider
- [X] Map CPU-only tier to CPU provider
- [X] Add info-level logging for selection decisions
- [X] Add warning-level logging for fallback scenarios
- [X] Set `ExecutionProviderPreference.Auto` as default in `EmbeddingOnnxOptions`
- [X] Support explicit CPU/DirectML preferences for testing

## Metrics and Thresholds

**Success Metrics:**
- Zero user intervention required for execution provider selection in normal scenarios
- Correct provider selected based on available hardware 100% of the time
- Clear logging of selection decisions for debugging and telemetry

**Performance Characteristics:**
- Provider selection occurs once during service initialization (negligible overhead)
- Hardware detection is lazy-evaluated and cached
- No runtime performance impact after initial selection

## Testing Plan
- Unit tests to verify Auto preference selects correct provider for each hardware tier
- Unit tests to verify explicit CPU/DirectML preferences override Auto logic
- Unit tests to verify fallback behavior when DirectML unavailable
- Integration tests with actual hardware to validate end-to-end selection
- Logging verification to ensure selection decisions are captured

## Testing Summary

**Unit Tests:** ✅ PASSING (17 tests in `OnnxSessionOptionsFactoryTests`)
- Auto preference with NPU tier selects DirectML
- Auto preference with GPU tier selects DirectML
- Auto preference with CPU-only tier selects CPU
- Explicit CPU preference overrides hardware detection
- Explicit DirectML preference attempts DirectML (with CPU fallback)
- Multiple calls produce consistent provider selection
- All memory and threading options applied correctly

**Integration Tests:** ❌ NOT IMPLEMENTED (blocked on embedding service end-to-end testing)

**Test Coverage:**
- `OnnxSessionOptionsFactory.ResolvePreference()`: Fully covered
- `OnnxSessionOptionsFactory.Create()`: Fully covered
- Hardware tier to provider mapping: Fully covered
- Fallback logic: Fully covered

## Usage and Operational Notes

### Default Behavior (Recommended)
Users do not need to configure anything. The system automatically:
1. Detects available hardware at startup via `IHardwareDetectionProvider`
2. Selects best execution provider based on hardware tiers (NPU > GPU > CPU)
3. Logs selection decision for observability
4. Uses the selected provider for all ONNX inference operations

### Configuration Options (Advanced)
For testing or special scenarios, users can explicitly set `EmbeddingOnnxOptions.ExecutionProviderPreference`:
- `Auto` (default): Automatic selection based on hardware detection
- `DirectML`: Force DirectML (falls back to CPU if unavailable)
- `Cpu`: Force CPU execution (useful for testing fallback behavior)

### Environment Variable Overrides (Testing/Diagnostics)
Hardware detection can be influenced via environment variables:
- `DAIV3_FORCE_CPU_ONLY=1`: Disables NPU and GPU detection (CPU only)
- `DAIV3_DISABLE_NPU=1`: Disables NPU detection (GPU or CPU)
- `DAIV3_DISABLE_GPU=1`: Disables GPU detection (NPU or CPU)

### Logging and Observability
Selection decisions are logged at `Information` level with messages like:
- "NPU detected; preferring DirectML for embedding inference."
- "NPU unavailable or insufficient; falling back to GPU for embedding inference."
- "NPU/GPU unavailable or insufficient; falling back to CPU for embedding inference."

Fallback scenarios are logged at `Warning` level:
- "DirectML provider initialization failed; falling back to CPU."

### Operational Constraints
- Windows 11 24H2+ with DirectML: Supports NPU and GPU via DirectML
- Other platforms: Falls back to CPU automatically
- No online/network requirements for provider selection
- No special permissions required

## Dependencies
- KLC-REQ-001 (Complete) - DirectML ONNX Runtime infrastructure
- KLC-REQ-003 (Complete) - CPU vector math fallback with TensorPrimitives

## Related Requirements
- HW-REQ-003: ONNX Runtime DirectML execution (parent requirement)
- HW-ACC-001: NPU preference for embedding generation
- HW-ACC-002: GPU fallback behavior
- HW-ACC-003: CPU fallback behavior
