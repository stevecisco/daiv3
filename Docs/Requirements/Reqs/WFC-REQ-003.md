# WFC-REQ-003

Source Spec: 10. Web Fetch, Crawl & Content Ingestion - Requirements

## Requirement
The system SHALL support crawl mode with configurable depth within a domain.

## Implementation Status
**Status:** ✅ Complete  
**Completed:** March 2, 2026

## Implementation Summary

Implemented domain-bounded crawl mode in `Daiv3.WebFetch.Crawl` with configurable depth and breadth-first traversal.

### Components Implemented

- **`IWebCrawler` interface** (`src/Daiv3.WebFetch.Crawl/IWebCrawler.cs`)
  - `CrawlAsync(startUrl, maxDepth, cancellationToken)` for depth-limited crawl execution.
  - Crawl contracts:
    - `CrawlResult` (start URL/domain/depth, timestamps, crawled pages, skipped URLs)
    - `CrawlPageResult` (URL, depth, parent URL, fetch result, discovered links)

- **`WebCrawlerOptions`** (`src/Daiv3.WebFetch.Crawl/WebCrawlerOptions.cs`)
  - `DefaultMaxDepth` (default: 1)
  - `MaxAllowedDepth` (default: 5)
  - `MaxPagesToCrawl` (default: 100)
  - `RestrictToSameDomain` (default: true)

- **`WebCrawler` implementation** (`src/Daiv3.WebFetch.Crawl/WebCrawler.cs`)
  - Uses breadth-first traversal via queue.
  - Enforces depth limit and optional same-domain boundary.
  - Resolves relative links to absolute URLs.
  - Normalizes URLs (removes fragments, trims trailing slash in non-root paths).
  - Filters non-crawlable links (`javascript:`, `mailto:`, `tel:`, fragment-only links).
  - Deduplicates visited/enqueued URLs.
  - Continues crawling when individual page fetch or parse errors occur.
  - Supports cancellation token propagation.
  - Uses structured logging through `ILogger<WebCrawler>`.

- **DI registration updates** (`src/Daiv3.WebFetch.Crawl/WebFetchServiceExtensions.cs`)
  - `AddWebCrawler()` with default options.
  - `AddWebCrawler(Action<WebCrawlerOptions>)` for explicit configuration.
  - `AddWebCrawler(Func<IServiceProvider, WebCrawlerOptions>)` for factory-based options.

## Behavior Details

### Domain Boundary
- Crawl boundary is based on the start URL host (case-insensitive host comparison).
- External-domain links are skipped when `RestrictToSameDomain = true`.

### Depth Model
- Depth `0` = start page only.
- Depth `1` = start page + directly linked pages.
- Deeper levels follow breadth-first traversal up to configured max depth.

### Error Handling
- Invalid or unsupported start URL throws `InvalidOperationException`.
- Null/empty start URL throws `ArgumentNullException`.
- If requested depth exceeds `MaxAllowedDepth`, crawl fails with `InvalidOperationException`.
- Individual page failures are logged and added to skipped URLs; crawl continues.

## Testing

### Unit + DI Tests Added
- **`tests/unit/Daiv3.UnitTests/WebFetch/WebCrawlerTests.cs`** (8 tests)
  - Depth 0 behavior
  - Depth-limited traversal (no over-crawl)
  - Domain restriction behavior
  - Relative link resolution
  - URL deduplication and fragment normalization
  - Continue-on-error behavior when a child page fails
  - Invalid start URL validation
  - Max allowed depth validation

- **`tests/unit/Daiv3.UnitTests/WebFetch/WebFetchServiceExtensionsTests.cs`** (+5 tests)
  - Crawler DI registration
  - Default options registration
  - Custom options configuration
  - Null service collection validation
  - Null options-factory validation

### Validation Runs
- Targeted tests (`WebCrawlerTests.cs` + `WebFetchServiceExtensionsTests.cs`): **52 passed**
- Expanded WebFetch test run: **196 passed**
- Full solution build: **0 errors**, baseline warnings only

## Usage and Operational Notes

### Basic Registration
```csharp
services.AddHtmlParser();
services.AddWebFetcher();
services.AddWebCrawler();
```

### Configured Registration
```csharp
services.AddWebCrawler(opts =>
{
    opts.DefaultMaxDepth = 2;
    opts.MaxAllowedDepth = 8;
    opts.MaxPagesToCrawl = 250;
    opts.RestrictToSameDomain = true;
});
```

### Invocation
```csharp
var crawler = serviceProvider.GetRequiredService<IWebCrawler>();
var result = await crawler.CrawlAsync("https://example.com", maxDepth: 2, cancellationToken);
```

## Dependencies
- KLC-REQ-007 (HTML parsing support)
- WFC-REQ-001 (single-page fetch foundation)

## Out of Scope for This Requirement
- `robots.txt` compliance and crawl rate limiting (handled by WFC-REQ-004)
- Scheduled recrawl orchestration (handled by WFC-REQ-008)
