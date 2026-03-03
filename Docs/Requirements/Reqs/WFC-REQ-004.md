# WFC-REQ-004

Source Spec: 10. Web Fetch, Crawl & Content Ingestion - Requirements

## Requirement
The system SHALL respect robots.txt and apply rate limits.

## Implementation Status
**Status:** ✅ Complete  
**Completed:** March 2, 2026

## Implementation Summary

Implemented respectful crawling behavior directly in `Daiv3.WebFetch.Crawl.WebCrawler` with:
- `robots.txt` policy loading and per-host caching
- path-level allow/disallow evaluation
- optional crawl-delay parsing from `robots.txt`
- per-host request rate limiting with configurable defaults

### Components Updated

- **`WebCrawlerOptions`** (`src/Daiv3.WebFetch.Crawl/WebCrawlerOptions.cs`)
	- Added `RespectRobotsTxt` (default: `true`)
	- Added `RobotsUserAgent` (default: `Daiv3Crawler`)
	- Added `ApplyRateLimit` (default: `true`)
	- Added `RateLimitDelayMs` (default: `1000`)

- **`WebCrawler`** (`src/Daiv3.WebFetch.Crawl/WebCrawler.cs`)
	- Fetches `/{host}/robots.txt` once per host and caches parsed rules.
	- Applies robots path filtering before page fetch; blocked URLs are logged and tracked in `SkippedUrls`.
	- Parses `User-agent`, `Allow`, `Disallow`, and `Crawl-delay` directives.
	- Evaluates rules with longest-match precedence (`Allow` wins ties).
	- Applies per-host delay before each fetch using max of configured delay and robots `Crawl-delay`.
	- Graceful fallback: if robots fetch/parse fails, crawler continues with allow-all policy.

## Usage and Operational Notes

### Default Behavior
- Crawler respects `robots.txt` by default.
- Crawler applies a 1-second minimum delay between requests to the same host by default.

### Configuration Example
```csharp
services.AddWebCrawler(opts =>
{
		opts.RespectRobotsTxt = true;
		opts.RobotsUserAgent = "Daiv3Crawler";
		opts.ApplyRateLimit = true;
		opts.RateLimitDelayMs = 750;
});
```

### Operational Notes
- Robots policy and rate limit behavior are enforced in crawl mode (`IWebCrawler.CrawlAsync`).
- When `RestrictToSameDomain = false`, robots/rate limiting are still evaluated per target host.
- Setting `ApplyRateLimit = false` disables delay enforcement.
- Setting `RespectRobotsTxt = false` disables robots policy checks.

## Testing

### Unit Tests
- `tests/unit/Daiv3.UnitTests/WebFetch/WebCrawlerTests.cs`
	- Added robots disallow coverage
	- Added allow-over-disallow precedence coverage
	- Added rate-limit delay timing coverage
- `tests/unit/Daiv3.UnitTests/WebFetch/WebFetchServiceExtensionsTests.cs`
	- Extended default/custom option assertions for new crawler politeness settings

### Validation Results
- Targeted test run (`WebCrawlerTests.cs` + `WebFetchServiceExtensionsTests.cs`): **78 passed, 0 failed**
- Full solution build: **0 errors**, baseline warning families only

## Dependencies
- KLC-REQ-007
- PTS-REQ-007

## Related Requirements
- None
