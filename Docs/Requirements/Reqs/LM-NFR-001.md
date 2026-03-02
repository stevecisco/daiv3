# LM-NFR-001

Source Spec: 9. Learning Memory - Requirements

## Requirement
Learning retrieval SHOULD be fast and not block the UI.

## Implementation Summary
Implemented bounded-latency learning retrieval in orchestration so agent execution can continue quickly even when storage or embedding generation is slow.

### Measurable Thresholds
- Retrieval timeout budget: **150 ms** default (`LearningRetrievalTimeoutMs`).
- Slow retrieval warning threshold: **75 ms** default (`LearningRetrievalWarningThresholdMs`).
- Similarity candidate cap: **256** default (`LearningRetrievalMaxCandidatesToScore`).
- Max injected learnings: **3** default (`LearningRetrievalMaxResults`).

### Guardrails and Optimizations
- Added timeout cancellation in `LearningRetrievalService.RetrieveLearningsAsync(...)` using linked cancellation token with `CancelAfter(...)`.
- On timeout, retrieval returns empty results gracefully (no exception surfaced to caller), preventing foreground execution stalls.
- Added candidate capping before similarity scoring (ordered by confidence and recency) to bound CPU work.
- Kept `TimesApplied` updates asynchronous and fire-and-forget so retrieval response path remains fast.

### Instrumentation and Observability
- Added retrieval latency measurement via `Stopwatch`.
- Added structured logs for:
	- configured timeout/candidate budget,
	- timeout fallback events,
	- slow retrieval warnings when threshold exceeded,
	- normal completion latency.

### Configuration Knobs
Added orchestration options:
- `LearningRetrievalTimeoutMs`
- `LearningRetrievalWarningThresholdMs`
- `LearningRetrievalMaxCandidatesToScore`
- `LearningRetrievalMaxResults`

These are wired from `AgentManager` into `LearningRetrievalContext` for each retrieval call.

### Abstraction Improvement
- Introduced `ILearningStorageService` in persistence and updated `LearningStorageService` to implement it.
- Updated `LearningRetrievalService` dependency from concrete `LearningStorageService` to `ILearningStorageService` for testability and cleaner orchestration-to-persistence boundaries.

## Testing Plan
- Added/updated unit tests in `LearningRetrievalServiceTests`:
	- validation for new performance context fields,
	- timeout fallback behavior,
	- candidate cap behavior (ensures bounded similarity batch size),
	- existing semantic retrieval/ranking regressions.
- Targeted unit results: **46/46 passing**.
- Build validation:
	- `dotnet build src/Daiv3.Orchestration/Daiv3.Orchestration.csproj --nologo --verbosity minimal` (passes)
	- `dotnet build Daiv3.FoundryLocal.slnx --nologo --verbosity minimal` (passes)

## Usage and Operational Notes
- Invocation path: automatically used by `AgentManager` during learning injection (`RetrieveAndFormatLearningsAsync`).
- User-visible behavior: agent execution proceeds without noticeable blocking when retrieval is slow or timing out; learning injection may be skipped for that iteration under timeout pressure.
- Operational constraints:
	- Retrieval remains local (SQLite + local embedding model), no online dependency required.
	- Timeout and candidate settings can be tuned for lower-latency UI scenarios.
	- If timeout is too aggressive, fewer/no learnings may be injected.

## Dependencies
- KM-REQ-013
- CT-REQ-003

## Related Requirements
- None

## Status
Complete (100%)
