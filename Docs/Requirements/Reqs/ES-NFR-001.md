# ES-NFR-001

Source Spec: 1. Executive Summary - Requirements

## Requirement
The system SHOULD run efficiently on Copilot+ PCs with NPUs and remain usable on CPU-only fallback.

## Status
**Status:** Complete  
**Date Completed:** 2026-03-08

## Acceptance Criteria

| Criterion | Description | Validation |
|-----------|-------------|------------|
| AC-1: Accelerated Preference | On Copilot+ capable environments, runtime prefers accelerated execution (NPU/GPU via DirectML) before CPU fallback | `OnnxSessionOptionsFactoryTests.Create_WithAutoPreference_SelectsBestAvailable` |
| AC-2: Deterministic Fallback | If accelerators are unavailable or disabled, system falls back deterministically to CPU execution without failure | `OnnxSessionOptionsFactoryTests.Create_WithAutoPreference_FallsBackToCpuWhenNoAccelerators` and `EmbeddingCpuPerformanceTests.HardwareOverrides_ConfigureDetectionCorrectly` |
| AC-3: CPU Usability | CPU-only vector search path remains within defined usable thresholds for Tier 1/Tier 2 workloads | `VectorSimilarityPerformanceBenchmarkTests` (critical 10k x 384 benchmark and related cases) |
| AC-4: Observability | Performance telemetry and diagnostics are available for troubleshooting and regression detection | `VectorSimilarityMetricsCollectionTests` and `PerformanceMetrics*` unit tests |

## Implementation Summary

ES-NFR-001 is satisfied by existing hardware-aware embedding execution and CPU fallback performance guardrails already implemented under hardware and knowledge-layer requirements.

### Runtime Hardware Strategy

1. **Automatic provider selection**
- `src/Daiv3.Knowledge.Embedding/OnnxSessionOptionsFactory.cs`
- Auto mode resolves available tiers from `IHardwareDetectionProvider` and prefers DirectML when NPU/GPU is available.

2. **NPU/GPU to CPU degradation path**
- `src/Daiv3.Infrastructure.Shared/Hardware/HardwareDetectionProvider.cs`
- `src/Daiv3.Knowledge.Embedding/OnnxSessionOptionsFactory.cs`
- Fallback order follows `NPU -> GPU -> CPU` semantics.

3. **CPU SIMD path for usability under fallback**
- `src/Daiv3.Knowledge.Embedding/CpuVectorSimilarityService.cs`
- Uses `System.Numerics.TensorPrimitives` batch cosine similarity for Tier 1/Tier 2 search workloads.

4. **Optional telemetry for regression visibility**
- `src/Daiv3.Knowledge.Embedding/PerformanceMetrics.cs`
- `src/Daiv3.Knowledge.Embedding/PerformanceMetricsOptions.cs`

### Measurable Thresholds (Adopted)

This executive-summary NFR adopts thresholds already codified in `HW-NFR-002`:

- Single cosine similarity target: `< 100us` (with practical tolerance in test harness)
- Tier 1 batch target: `10,000 x 384` under defined threshold window
- Tier 2 batch target: `1,000 x 768` under defined threshold window
- Behavior requirement: graceful operation on CPU-only deployments

## Configuration and Operational Controls

### Primary Configuration
- `EmbeddingOnnxOptions.ExecutionProviderPreference` (default `Auto`)
- CPU tuning knobs in embedding options (`IntraOpNumThreads`, `InterOpNumThreads`, memory settings)

### Operational Overrides
- `DAIV3_FORCE_CPU_ONLY=true`
- `DAIV3_DISABLE_NPU=1`
- `DAIV3_DISABLE_GPU=1`

These controls allow explicit fallback testing and safe operation on constrained hardware.

## Testing Plan

### Requirement Traceability Coverage

- `tests/unit/Daiv3.Knowledge.Tests/Embedding/OnnxSessionOptionsFactoryTests.cs`
	- Auto provider selection and CPU fallback behavior
- `tests/unit/Daiv3.Infrastructure.Shared.Tests/Shared/Hardware/HardwareDetectionProviderTests.cs`
	- Tier detection ordering and CPU availability guarantees
- `tests/integration/Daiv3.Knowledge.Embedding.IntegrationTests/EmbeddingCpuPerformanceTests.cs`
	- Environment override paths for CPU/NPU/GPU behavior
- `tests/integration/Daiv3.Knowledge.Embedding.IntegrationTests/VectorSimilarityPerformanceBenchmarkTests.cs`
	- CPU usability benchmarks including critical large-batch case
- `tests/integration/Daiv3.Knowledge.Embedding.IntegrationTests/VectorSimilarityMetricsCollectionTests.cs`
	- Telemetry instrumentation validation

### Validation Runs (2026-03-08)

- `dotnet test tests/unit/Daiv3.Knowledge.Tests/Daiv3.Knowledge.Tests.csproj --nologo --verbosity minimal`
	- Result: **296 total, 0 failed, 296 passed, 0 skipped**
- `dotnet test tests/unit/Daiv3.Infrastructure.Shared.Tests/Daiv3.Infrastructure.Shared.Tests.csproj --nologo --verbosity minimal`
	- Result: **38 total, 0 failed, 38 passed, 0 skipped**
- `dotnet test tests/integration/Daiv3.Knowledge.Embedding.IntegrationTests/Daiv3.Knowledge.Embedding.IntegrationTests.csproj --nologo --verbosity minimal`
	- Result: **29 total, 0 failed, 27 passed, 2 skipped**
- `dotnet test Daiv3.FoundryLocal.slnx --nologo --verbosity minimal`
	- Result: **2446 total, 0 failed, 2431 passed, 15 skipped**

### Warning/Build Evidence (2026-03-08)

- `dotnet build Daiv3.FoundryLocal.slnx --nologo --verbosity minimal`
	- Result: **0 warnings, 0 errors**

## Usage and Operational Notes

- No user action is required for normal behavior; auto hardware selection is default.
- On Copilot+ systems, accelerated execution is attempted automatically.
- On CPU-only systems, search and embedding operations remain functional with documented latency envelopes.
- For diagnostics, operators can force CPU-only mode and compare telemetry.
- This requirement is fully compatible with offline/local-first operation.

## Dependencies
- HW-REQ-004
- HW-REQ-006

## Related Requirements
- HW-NFR-002
- HW-NFR-001
- KM-NFR-001
