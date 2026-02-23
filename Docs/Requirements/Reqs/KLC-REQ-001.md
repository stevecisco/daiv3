# KLC-REQ-001

Source Spec: 12. Key .NET Libraries & Components - Requirements

## Requirement
The system SHALL use Microsoft.ML.OnnxRuntime.DirectML for in-process inference and embedding generation.

## Implementation Plan
- Owning component: Daiv3.Knowledge.Embedding with `IOnnxInferenceSessionProvider` and `IOnnxSessionOptionsFactory`.
- Define configuration via `EmbeddingOnnxOptions` (model path, provider preference, thread settings).
- Implement DirectML session creation on Windows with CPU fallback and structured logging.
- Expose DI registration via `AddEmbeddingServices` for orchestration/UI wiring.
- Document configuration and operational behavior below.

## Implementation Tasks
- [X] **Task 1**: Add ONNX session options factory with DirectML preference (2 hours)
- [X] **Task 2**: Implement inference session provider with model path validation (2 hours)
- [X] **Task 3**: Register embedding services and options in DI (1 hour)
- [X] **Task 4**: Add unit tests for options and session creation (2 hours)
- [X] **Task 5**: Add integration test for DirectML preference handling (1 hour)

## Testing Plan
- Unit tests: options validation, environment expansion, CPU preference selection, missing model path error.
- Integration test: DirectML-preferred session options creation (fallback to CPU allowed).
- Negative tests: missing model path throws before session initialization.
- Performance checks deferred to KM-REQ-013/HW-REQ-003 when real models are wired.

## Usage and Operational Notes
- Register with DI via `EmbeddingServiceExtensions.AddEmbeddingServices` and configure `EmbeddingOnnxOptions.ModelPath`.
- Default provider preference is Auto: DirectML on Windows, CPU fallback on other targets or DirectML init failure.
- Model path supports environment variable expansion (e.g., `%TEMP%\model.onnx`).
- No UI surfaces yet; service is used by higher-level embedding workflows.

## Dependencies
- None

## Related Requirements
- None
