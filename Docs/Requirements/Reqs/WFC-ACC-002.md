# WFC-ACC-002

Source Spec: 10. Web Fetch, Crawl & Content Ingestion - Requirements

## Requirement
Crawl mode respects depth and domain limits.

## Implementation Status
**Status:** ✅ Complete  
**Updated:** March 3, 2026

## Implementation Summary

Acceptance coverage was added for crawl behavior in:
- `tests/unit/Daiv3.UnitTests/WebFetch/WebCrawlerAcceptanceTests.cs`

### Compilation Error Fixes (March 3, 2026)
Resolved pre-existing blockers that prevented test execution:
- **WebFetchMetadataServiceTests.cs**: Fixed namespace collision by adding `using WebFetchEntity = Daiv3.Persistence.Entities.WebFetch;` alias
- **WebFetchMetadataServiceIntegrationTests.cs**: Fixed DatabaseContext API usage, replaced non-existent `IDocumentRepository` with `DocumentRepository`, updated Document entity properties to match schema
- **MarkdownContentStoreTests.cs**: Added lazy-initialized `_store` property to fix null reference errors

All compilation errors resolved. Solution builds with 0 errors.

### Acceptance Scenarios Implemented
1. **Depth 1 + same-domain restriction**
	- Start page includes both internal and external links.
	- Verifies only start page and first-level internal page are crawled.
	- Verifies deeper internal pages are not crawled at depth 1.
	- Verifies cross-domain links are skipped.

2. **Same-domain restriction across deeper crawl**
	- Depth 2 crawl with mixed internal/external links.
	- Verifies only in-domain pages are crawled across levels.
	- Verifies external-domain URLs are excluded and recorded in skipped list.

### Existing Requirement Foundation
The acceptance tests validate behavior already implemented in:
- `WFC-REQ-003` (`IWebCrawler`, BFS depth-limited crawl, domain-boundary filtering)
- `WFC-REQ-004` (`robots.txt` and rate-limit support)

## Test Execution / Validation

✅ **Validation Successful** (March 3, 2026)

```bash
# Build command
dotnet build Daiv3.FoundryLocal.slnx --nologo --verbosity minimal
# Result: 0 errors, 348 warnings (baseline)

# Target test command
dotnet test tests/unit/Daiv3.UnitTests/Daiv3.UnitTests.csproj -f net10.0 --filter "FullyQualifiedName~WebCrawlerAcceptanceTests"
# Result: 2/2 tests passed
```

**Test Results:**
- `WebCrawlerAcceptanceTests.Depth1SameDomain_OnlyCrawlsStartPageAndFirstLevelInternalPages`: ✅ Passed
- `WebCrawlerAcceptanceTests.SameDomainRestriction_OnlyCrawlsInternalPages_AcrossDepths`: ✅ Passed

**Test Coverage:**
- Depth-limited crawling (maxDepth parameter)
- Same-domain boundary enforcement
- Skipped URL tracking for cross-domain links

## Usage and Operational Notes
- Crawl behavior is invoked via `IWebCrawler.CrawlAsync(startUrl, maxDepth, cancellationToken)`.
- Domain limiting is controlled by `WebCrawlerOptions.RestrictToSameDomain`.
- Depth behavior is controlled by `maxDepth` (request-time) and `WebCrawlerOptions.MaxAllowedDepth` (guardrail).
- Skipped URLs are observable through `CrawlResult.SkippedUrls` for diagnostics.

## Dependencies
- KLC-REQ-007
- PTS-REQ-007

## Related Requirements
- WFC-REQ-003
- WFC-REQ-004
