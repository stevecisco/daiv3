# WFC-NFR-002

Source Spec: 10. Web Fetch, Crawl & Content Ingestion - Requirements

## Requirement
Crawling SHOULD avoid excessive network load.

## Implementation Status
**Status:** ✅ Complete  
**Completed:** March 3, 2026

### Implementation Summary
Implemented explicit network-politeness guardrails and measurable telemetry in the web crawl stack:

1. **Per-host request cap guardrail**
   - Added `WebCrawlerOptions.MaxRequestsPerHostPerCrawl` (default: `50`)
   - Crawler skips additional URLs for a host once the cap is reached
   - Prevents pathological over-crawling against a single host

2. **Requests-per-minute threshold evaluation**
   - Added `WebCrawlerOptions.TargetMaxRequestsPerMinutePerHost` (default: `30`)
   - Per-host observed request rates are computed at crawl completion
   - Threshold breaches are recorded and logged for operational tuning

3. **Crawl-load telemetry instrumentation**
   - Added `ICrawlLoadMetrics` + `CrawlLoadMetrics` (thread-safe)
   - Captures request counts, rate-limited requests, applied delay, robots blocks, host-cap skips, and RPM threshold breaches
   - Registered in DI through `AddWebCrawler(...)` service wiring

4. **Crawl result observability surface**
   - Extended `CrawlResult` with network-load observability fields:
     - `RequestsByHost`
     - `RequestsPerMinuteByHost`
     - `TotalAppliedRateLimitDelayMs`
     - `RateLimitedRequestCount`
     - `RobotsPolicySkipCount`
     - `HostRequestCapSkipCount`
     - `RequestsPerMinuteThresholdBreaches`

### Files Added
- `src/Daiv3.WebFetch.Crawl/ICrawlLoadMetrics.cs`
- `src/Daiv3.WebFetch.Crawl/CrawlLoadMetrics.cs`
- `tests/unit/Daiv3.UnitTests/WebFetch/CrawlLoadMetricsTests.cs`

### Files Modified
- `src/Daiv3.WebFetch.Crawl/WebCrawlerOptions.cs`
- `src/Daiv3.WebFetch.Crawl/WebCrawler.cs`
- `src/Daiv3.WebFetch.Crawl/IWebCrawler.cs`
- `src/Daiv3.WebFetch.Crawl/WebFetchServiceExtensions.cs`
- `tests/unit/Daiv3.UnitTests/WebFetch/WebCrawlerTests.cs`
- `tests/unit/Daiv3.UnitTests/WebFetch/WebFetchServiceExtensionsTests.cs`

## Measurable Metrics and Thresholds

### Guardrails
- **Per-host request cap:** `MaxRequestsPerHostPerCrawl = 50` (default)
- **Host request pacing:** `ApplyRateLimit = true`, `RateLimitDelayMs = 1000` (from WFC-REQ-004)
- **Robots compliance:** `RespectRobotsTxt = true` (from WFC-REQ-004)

### Performance/Politeness Targets
- **Target max host request rate:** `TargetMaxRequestsPerMinutePerHost = 30` (default)
- Breaches are non-fatal (NFR SHOULD semantics) but observable via metrics and warning logs

### Telemetry Captured
- Total requests
- Rate-limited request count
- Total/average applied delay
- URLs blocked by robots policy
- URLs skipped by host request cap
- Hosts breaching RPM threshold
- Per-host request and RPM distributions

## Testing

### Unit Tests Added/Updated
- **`CrawlLoadMetricsTests`**: validates counters, snapshots, reset, validation, and thread safety
- **`WebCrawlerTests`** updates:
  - host request cap skip behavior
  - crawl result load metrics exposure
  - threshold breach reporting
  - rate-limit metric fields validation
- **`WebFetchServiceExtensionsTests`** updates:
  - DI registration for `ICrawlLoadMetrics`
  - default option assertions for new politeness knobs

### Validation Results
- Targeted tests (`runTests`): **100 passed, 0 failed**
- Project build: `dotnet build src/Daiv3.WebFetch.Crawl/Daiv3.WebFetch.Crawl.csproj --nologo --verbosity minimal` ✅
- Solution build: `dotnet build Daiv3.FoundryLocal.slnx --nologo --verbosity minimal` ✅ (`0` errors, baseline warning families)

## Usage and Operational Notes

### Configuration
Tune politeness behavior via `WebCrawlerOptions`:
- `MaxRequestsPerHostPerCrawl`
- `TargetMaxRequestsPerMinutePerHost`
- `ApplyRateLimit`
- `RateLimitDelayMs`
- `RespectRobotsTxt`
- `RobotsUserAgent`

### Operational Effects
- High-link-density pages no longer allow unbounded request fan-out to a single host
- Operators receive measurable, host-specific telemetry for crawl load tuning
- Existing robots/rate-limit behavior remains backward compatible and enabled by default

## Dependencies
- KLC-REQ-007
- PTS-REQ-007

## Related Requirements
- WFC-REQ-004 (robots.txt + rate limiting)
- WFC-REQ-003 (crawl depth/domain behavior)
- WFC-NFR-001 (fetch cancellation observability)
