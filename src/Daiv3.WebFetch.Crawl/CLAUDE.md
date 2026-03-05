# Daiv3.WebFetch.Crawl – Claude Context

> **Parent guidelines:** [Root CLAUDE.md](../../CLAUDE.md) | [Full AI Instructions](../../Docs/AI-Instructions.md)

---

## Purpose

Web content crawling and fetching library. Downloads web pages, parses HTML using `AngleSharp`, converts to clean Markdown, and feeds results into the knowledge ingestion pipeline. Integrates with `Daiv3.Scheduler` for recurring scheduled crawls.

## Project Type

Library

## Target Framework

`net10.0`

## Key Responsibilities

- `IWebCrawler` — fetch a URL or crawl a site up to a configured depth
- HTML parsing and content extraction (main content, remove navigation/ads)
- HTML → Markdown conversion for downstream embedding
- `CrawlJob` — scheduler-integrated recurring crawl task
- Robots.txt respect and politeness delay

## Dependencies (All Pre-Approved)

- `AngleSharp` — HTML parsing
- `ReverseMarkdown` — HTML to Markdown conversion

## Rules

- Always respect `robots.txt` and configurable crawl delays
- Do not store raw HTML in the knowledge base — always convert to Markdown first
- Crawl results flow into `Daiv3.Knowledge` for indexing, not stored directly here

## Related Projects

| Project | Relationship |
|---------|-------------|
| `Daiv3.Knowledge` | Receives crawled Markdown documents for indexing |
| `Daiv3.Scheduler` | Triggers recurring crawl jobs |
| `Daiv3.Orchestration` | Can initiate on-demand crawls via `WebContentIngestionService` |

## Test Projects

```powershell
dotnet test tests/unit/Daiv3.WebFetch.Crawl.Tests/Daiv3.WebFetch.Crawl.Tests.csproj --nologo --verbosity minimal
```
