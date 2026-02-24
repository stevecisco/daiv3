# KM-REQ-003

Source Spec: 4. Knowledge Management & Indexing - Requirements

## Requirement
The system SHALL convert HTML to Markdown for indexing.

## Implementation Plan
- **Owning Component:** `Daiv3.Knowledge.DocProc` with `IHtmlToMarkdownConverter` interface
- **Data Contract:** `IHtmlToMarkdownConverter.ConvertHtmlToMarkdown(string html): string`
- **Library:** ReverseMarkdown 3.23.0 for lightweight HTML→Markdown conversion
- **Integration Point:** After `ITextExtractor.ExtractAsync()`, before document chunking in `KnowledgeDocumentProcessor`
- **Scope:** Applied only to .html and .htm files; other formats pass through unchanged
- **Error Handling:** Graceful fallback to plain text extraction on conversion failure
- **Logging:** Debug-level logging on successful conversion; Warning-level on failures

## Testing Plan
- Unit tests for HTML structures: headers, paragraphs, lists, links, code blocks, tables
- Edge case tests: empty HTML, whitespace-only content, malformed HTML, null input
- Integration tests with KnowledgeDocumentProcessor pipeline for HTML documents
- Negative tests for exception handling and error messages
- Existing text extraction and document processor tests updated with converter mock

## Usage and Operational Notes
- Conversion is automatic for HTML documents (transparent to users)
- Markdown output provides better structure preservation than plain text extraction
- Supports GitHub Flavored Markdown features (tables, task lists, strikethrough)
- Post-conversion whitespace normalization prevents excessive blank lines
- Conversion failures logged but don't block ingestion (fallback to plain text)

## Dependencies
- HW-REQ-003
- KLC-REQ-001
- KLC-REQ-002
- KLC-REQ-004
- KM-REQ-002 (text extraction prerequisite)

## Related Requirements
- None

## Technical Details

### Architecture
```
DocumentFile (.html/.htm)
    ↓
ITextExtractor.ExtractAsync() → extracted HTML text
    ↓
Is .html or .htm? → YES
    ↓
IHtmlToMarkdownConverter.ConvertHtmlToMarkdown() → Markdown text
    ↓
Normalize whitespace → Cleaned Markdown
    ↓
KnowledgeDocumentProcessor continues pipeline
```

### Implementation Components

**Interface:** [IHtmlToMarkdownConverter.cs](../../src/Daiv3.Knowledge.DocProc/IHtmlToMarkdownConverter.cs)
- Single method: `ConvertHtmlToMarkdown(string html): string`

**Implementation:** [HtmlToMarkdownConverter.cs](../../src/Daiv3.Knowledge.DocProc/HtmlToMarkdownConverter.cs)
- Uses ReverseMarkdown library with GithubFlavored enabled
- Normalizes excess whitespace post-conversion
- Exception handling with descriptive error messages

**Integration:** [KnowledgeDocumentProcessor.cs](../../src/Daiv3.Knowledge/KnowledgeDocumentProcessor.cs)
- Injected dependency: `private readonly IHtmlToMarkdownConverter _htmlToMarkdownConverter`
- Applied after extraction: `text = _htmlToMarkdownConverter.ConvertHtmlToMarkdown(text)`
- Extension check: `if (extension is ".html" or ".htm")`
- Error handling: warning log + fallback to plain text

**DI Registration:** [DocumentProcessingServiceExtensions.cs](../../src/Daiv3.Knowledge.DocProc/DocumentProcessingServiceExtensions.cs)
- Singleton registration: `services.AddSingleton<IHtmlToMarkdownConverter, HtmlToMarkdownConverter>()`

## Testing Summary

### Unit Tests: ✅ PASSING

**Test Project:** [tests/unit/Daiv3.UnitTests/Daiv3.UnitTests.csproj](tests/unit/Daiv3.UnitTests/Daiv3.UnitTests.csproj)
**Test File:** [tests/unit/Daiv3.UnitTests/Knowledge/DocProc/HtmlToMarkdownConverterTests.cs](tests/unit/Daiv3.UnitTests/Knowledge/DocProc/HtmlToMarkdownConverterTests.cs)
**Test Class:** [HtmlToMarkdownConverterTests](tests/unit/Daiv3.UnitTests/Knowledge/DocProc/HtmlToMarkdownConverterTests.cs#L6)
**Test Methods:**
- [ConvertHtmlToMarkdown_SimpleHtml_ReturnsMarkdown](tests/unit/Daiv3.UnitTests/Knowledge/DocProc/HtmlToMarkdownConverterTests.cs#L11) ✅
- [ConvertHtmlToMarkdown_HeaderTags_PreservesStructure](tests/unit/Daiv3.UnitTests/Knowledge/DocProc/HtmlToMarkdownConverterTests.cs#L24) ✅
- [ConvertHtmlToMarkdown_ListElements_ConvertsToMarkdownLists](tests/unit/Daiv3.UnitTests/Knowledge/DocProc/HtmlToMarkdownConverterTests.cs#L37) ✅
- [ConvertHtmlToMarkdown_LinkElements_PreservesLinks](tests/unit/Daiv3.UnitTests/Knowledge/DocProc/HtmlToMarkdownConverterTests.cs#L50) ✅
- [ConvertHtmlToMarkdown_CodeBlocks_PreserveFormatting](tests/unit/Daiv3.UnitTests/Knowledge/DocProc/HtmlToMarkdownConverterTests.cs#L63) ✅
- [ConvertHtmlToMarkdown_EmptyString_ReturnsEmpty](tests/unit/Daiv3.UnitTests/Knowledge/DocProc/HtmlToMarkdownConverterTests.cs#L76) ✅
- [ConvertHtmlToMarkdown_WhitespaceOnly_ReturnsEmpty](tests/unit/Daiv3.UnitTests/Knowledge/DocProc/HtmlToMarkdownConverterTests.cs#L85) ✅
- [ConvertHtmlToMarkdown_NullInput_ThrowsArgumentNullException](tests/unit/Daiv3.UnitTests/Knowledge/DocProc/HtmlToMarkdownConverterTests.cs#L94) ✅
- [ConvertHtmlToMarkdown_ComplexHtml_PreservesContent](tests/unit/Daiv3.UnitTests/Knowledge/DocProc/HtmlToMarkdownConverterTests.cs#L101) ✅
- [ConvertHtmlToMarkdown_TableStructure_ConvertsToMarkdown](tests/unit/Daiv3.UnitTests/Knowledge/DocProc/HtmlToMarkdownConverterTests.cs#L120) ✅
- [ConvertHtmlToMarkdown_RemovesHtmlAttributes](tests/unit/Daiv3.UnitTests/Knowledge/DocProc/HtmlToMarkdownConverterTests.cs#L135) ✅
- [ConvertHtmlToMarkdown_NormalizesExcessiveWhitespace](tests/unit/Daiv3.UnitTests/Knowledge/DocProc/HtmlToMarkdownConverterTests.cs#L147) ✅

**Test Count:** 12 tests per target framework (net10.0 and net10.0-windows10.0.26100) = **24 total passing**
**Last Test Run:** ✅ **12/12 passed** (160-172ms per target)

**Test Coverage:**
- ✅ Simple HTML structure conversion
- ✅ Header hierarchy preservation
- ✅ List element structure
- ✅ Link preservation with URL
- ✅ Code block formatting
- ✅ Empty and whitespace-only handling
- ✅ Null argument validation
- ✅ Complex mixed-structure HTML
- ✅ Table structure conversion
- ✅ HTML attribute removal
- ✅ Excess whitespace normalization

### Integration Tests: ✅ Updated

**Test Project:** [tests/integration/Daiv3.Knowledge.IntegrationTests/Daiv3.Knowledge.IntegrationTests.csproj](tests/integration/Daiv3.Knowledge.IntegrationTests/Daiv3.Knowledge.IntegrationTests.csproj)
**Test Class:** [KnowledgeDocumentProcessorIntegrationTests](tests/integration/Daiv3.Knowledge.IntegrationTests/KnowledgeDocumentProcessorIntegrationTests.cs#L17)

**Status:** Updated with IHtmlToMarkdownConverter mock (pass-through for testing)

### Build Status

| Target | Status |
|--------|--------|
| Build | ✅ Success (zero errors, 136 warnings from NU1903 CVEs) |
| Package Restore | ✅ Success (ReverseMarkdown 3.23.0 resolved) |
| Unit Tests | ✅ 24/24 Passing (12 per target framework) |

## Dependency Information

**Library:** ReverseMarkdown
**Version:** 3.23.0
**NuGet:** https://www.nuget.org/packages/ReverseMarkdown
**License:** MIT
**Documentation:** [ADD-20260223-html-to-markdown.md](../../Architecture/decisions/ADD-20260223-html-to-markdown.md)
