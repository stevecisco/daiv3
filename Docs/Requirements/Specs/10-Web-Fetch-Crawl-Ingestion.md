# 10. Web Fetch, Crawl & Content Ingestion - Requirements

## Overview
This document specifies requirements derived from Section 10 of the design document. It defines web fetch, crawl, and ingestion behavior.

## Goals
- Allow users and agents to pull web content into local knowledge.
- Preserve offline access by saving content locally as Markdown.

## Functional Requirements
- WFC-REQ-001: The system SHALL fetch a single URL and extract meaningful content.
- WFC-REQ-002: The system SHALL convert HTML to Markdown while stripping styling, navigation, and ads.
- WFC-REQ-003: The system SHALL support crawl mode with configurable depth within a domain.
- WFC-REQ-004: The system SHALL respect robots.txt and apply rate limits.
- WFC-REQ-005: The system SHALL store fetched content as Markdown in a configurable directory.
- WFC-REQ-006: The system SHALL add fetched content to the knowledge ingestion pipeline.
- WFC-REQ-007: The system SHALL store source URL and fetch date as metadata.
- WFC-REQ-008: The system SHALL support scheduled refetch intervals.

## Non-Functional Requirements
- WFC-NFR-001: Fetch operations SHOULD be cancellable.
- WFC-NFR-002: Crawling SHOULD avoid excessive network load.

## Data Requirements
- WFC-DATA-001: Metadata SHALL include source URL, fetch date, and content hash.

## Dependencies
- HTML parsing library (AngleSharp or HtmlAgilityPack).
- Scheduling service for refetch.

## Acceptance Criteria
- WFC-ACC-001: A fetched page appears in local Markdown storage and is indexed.
- WFC-ACC-002: Crawl mode respects depth and domain limits.
- WFC-ACC-003: Refetch updates the stored content and reindexes when changed.

## Out of Scope
- JavaScript-heavy page rendering for v0.1.

## Risks and Open Questions
- Decide default crawl depth and rate limit settings.
- Select HTML to Markdown conversion library.
