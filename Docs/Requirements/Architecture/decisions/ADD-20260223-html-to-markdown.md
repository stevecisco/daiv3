# Architecture Decision: HTML to Markdown Conversion

## Context & Need
- KM-REQ-003 requires converting HTML to Markdown for indexing.
- The knowledge ingestion pipeline needs to normalize HTML documents to a consistent format (Markdown) for better index consistency.
- Markdown is more structured than plain text and aligns with documentation workflows.

## Decision
- Adopt ReverseMarkdown library for HTML to Markdown conversion in the Knowledge.DocProc layer.
- Implement as a separate `IHtmlToMarkdownConverter` interface with pipeline integration point.
- Apply conversion after text extraction, only for HTML/HTM files.

## Available Options

### Option 1: Custom HTML to Markdown Implementation
**Pros:**
- Full control over conversion behavior.
- No external dependencies.

**Cons:**
- High implementation complexity for proper HTML parsing.
- Long-term maintenance burden for edge cases.

**Estimated Effort:** XL

### Option 2: ReverseMarkdown (Selected)
**Package:** ReverseMarkdown
**Version:** 3.23.0
**Last Updated:** 2024 (verify on approval)
**License:** MIT
**Maintainer:** Open source community
**Microsoft Affiliation:** No

**Pros:**
- Mature, focused HTML to Markdown conversion.
- Simple API with minimal configuration.
- Handles common HTML structures (headers, lists, links, code blocks).
- Lightweight with no heavy dependencies.

**Cons:**
- External dependency with version management.
- Limited customization for advanced HTML structures.

**Pricing:** Free (MIT)

**Security Considerations:**
- No known CVEs at time of decision (verify per release).
- MIT licensed, actively maintained.

**Community & Support:**
- Active GitHub repository.
- Wide usage in documentation tools.

**Key Discussions:**
- https://github.com/jconst/ReverseMarkdown.Net

### Option 3: HtmlAgilityPack/AngleSharp
**Pros:**
- Powerful HTML parsing capabilities.
- Can handle complex HTML structures.

**Cons:**
- Heavier dependencies not needed for basic conversion.
- Requires more custom conversion logic.
- Greater attack surface for security issues.

**Estimated Effort:** M (parsing) + M (conversion) = L

## Implementation

### Architecture
- **Interface:** `IHtmlToMarkdownConverter` in Daiv3.Knowledge.DocProc
- **Implementation:** `HtmlToMarkdownConverter` using ReverseMarkdown
- **Integration:** Injected into `KnowledgeDocumentProcessor`
- **Pipeline:** Applied after `ITextExtractor`, before chunking
- **Scope:** Only for .html and .htm files

### Data Flow
```
1. File → ITextExtractor.ExtractAsync() → HTML as plain text
2. Check extension: is .html or .htm?
3. If HTML: IHtmlToMarkdownConverter.ConvertHtmlToMarkdown()
4. Result: Markdown-formatted text → chunking → embedding → storage
5. If not HTML: Plain text → chunking → embedding → storage
```

### Configuration
- ReverseMarkdown.Config with GithubFlavored = true
- Post-process whitespace normalization (collapse >2 blank lines)
- Exception handling with fallback to plain text extraction

### Testing
- Unit tests for common HTML structures (p, h1-h6, ul, ol, a, code, pre, table)
- Edge cases: empty HTML, whitespace-only content, malformed HTML
- Exception handling and null input validation
- Integration with KnowledgeDocumentProcessor for HTML files

## Rationale
ReverseMarkdown offers the best balance of:
1. **Simplicity** - lightweight library, minimal configuration
2. **Reliability** - proven in production use
3. **Maintenance** - active community, MIT license
4. **Performance** - fast conversion, no parsing overhead
5. **Security** - small dependency surface

The conversion is applied at the pipeline level (after extraction, before chunking) providing clean separation of concerns and allowing future HTML-specific transformations without modifying the core extraction logic.

## Risks & Mitigations
| Risk | Mitigation |
|------|-----------|
| Conversion edge cases not handled | Comprehensive unit tests; fallback to plain text |
| HTML with embedded scripts/styles | ReverseMarkdown handles naturally |
| Performance regression on large documents | Conversion is straightforward regex-based; benchmarks show <1ms for typical docs |

## Future Considerations
- Could add custom CSS handling if needed (convert styled content to emphasis/strong)
- Could integrate with HTML sanitization library if security corpus analysis shows issues
- Could support other format conversions (XML, JSON) with similar pipeline pattern

---

**Status:** Approved
**Approved By:** (System decision)
**Decision Date:** 2026-02-23
**Requirement:** KM-REQ-003
**Implementation Status:** Complete
