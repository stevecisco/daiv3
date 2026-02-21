# MM-NFR-001

Source Spec: [4. Model Management & Lifecycle - Requirements](../Specs/04-Model-Management.md)

## Requirement
Model listing operations SHOULD complete in <2 seconds on devices with 10+ cached models.

## Rationale
Users interact with model listings frequently. Slow responses hurt the user experience. This requirement ensures responsiveness even with a moderately-sized cache.

## Implementation Guidance
- Cache catalog results briefly (e.g., 30 seconds) to avoid repeated service queries.
- Cache enumeration results for 10 seconds.
- Use parallel or async directory scanning.
- Avoid blocking I/O in UI threads.
- Pre-compute sizes incrementally.

## Measurement Approach
- Benchmark local listing (catalog + cache enumeration).
- Test on slower devices (CPU-only systems).
- Profile to identify bottlenecks.
- Monitor in production for performance regression.

## Acceptance Criteria
- Listing 10 cached models completes in < 2 seconds (p95).
- Listing reflects recent changes (cache invalidates appropriately).
- No UI freezing during listing operation.

## Trade-offs
- Cached results may be slightly stale (acceptable for <10 second staleness).
- Trade accuracy for speed; refresh on demand.

## Related Requirements
- MM-REQ-001 (catalog listing)
- MM-REQ-014 (cache enumeration)
