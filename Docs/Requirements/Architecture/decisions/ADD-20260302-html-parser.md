# Architecture Decision: HTML Parser Library Selection

**Date:** March 2, 2026  
**Status:** Accepted  
**Requirement:** KLC-REQ-007  

## Problem Statement

The DAIv3 system needs to parse HTML content for web fetch and knowledge extraction (WFC-REQ-001). Two primary libraries are candidates:
- AngleSharp: Modern, fully-managed implementation
- HtmlAgilityPack: Established, XPATH-based parsing

## Decision

**Chosen: AngleSharp**

## Rationale

### 1. Modern, Actively Maintained
- AngleSharp is actively developed and targets modern .NET
- HtmlAgilityPack is mature but development pace is slower
- DAIv3 targets .NET 10, requiring a library with modern framework support

### 2. Performance
- AngleSharp has better performance characteristics for typical parsing workloads
- More efficient memory usage (critical when processing multiple documents in knowledge pipeline)
- HtmlAgilityPack can have higher memory overhead

### 3. Standards Compliance
- AngleSharp fully implements HTML5 standard
- Better handling of modern web content and DOM structures
- More reliable result parsing from varied web sources

### 4. API Design
- AngleSharp DOM API matches browser's DOM closely
- Easier to use for developers familiar with JavaScript/browser APIs
- CSS selectors are more intuitive than XPATH expressions
- Better integration with async/await patterns

### 5. Integration with .NET Ecosystem
- Better compatibility with .NET 10 and Windows 11
- Cleaner async/await support
- No legacy API baggage

## Trade-offs

**Accepting:**
- Smaller ecosystem compared to HtmlAgilityPack (but adequate for requirements)
- No XPATH support (but CSS selectors cover needed functionality)

**Avoiding:**
- Memory overhead (HtmlAgilityPack)
- XPATH verbosity (HtmlAgilityPack)
- Legacy API patterns (HtmlAgilityPack)

## Implementation Plan

1. Add AngleSharp to approved dependencies
2. Create `IHtmlParser` interface for HTML parsing abstractions
3. Implement `HtmlParser` service using AngleSharp
4. Integrate into document processing pipeline
5. Create comprehensive unit tests

## Related Requirements

- **KLC-REQ-007**: Use AngleSharp or HtmlAgilityPack for HTML parsing
- **WFC-REQ-001**: Fetch a single URL and extract meaningful content

## Version

- **AngleSharp**: 1.0.1 (latest stable release for .NET 10)

## References

- [KLC-REQ-007.md](../Reqs/KLC-REQ-007.md)
- [Approved Dependencies](approved-dependencies.md)

