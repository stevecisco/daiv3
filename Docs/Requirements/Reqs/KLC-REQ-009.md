# KLC-REQ-009

Source Spec: 12. Key .NET Libraries & Components - Requirements

## Requirement
The system SHALL use PdfPig and Open XML SDK for PDF and DOCX extraction.

## Status: ✅ COMPLETE

## Implementation Plan
- ✅ Owning component: `Daiv3.Knowledge.DocProc` with `ITextExtractor` and `DocumentTextExtractor` (shared with KM-REQ-002).
- ✅ Dependencies: `UglyToad.PdfPig` v1.7.0-custom-5 for PDF extraction, `DocumentFormat.OpenXml` v3.0.2 for DOCX extraction.
- ✅ Interface: `ITextExtractor.ExtractAsync(string filePath, CancellationToken)` returns extracted text as string.
- ✅ Implementation:
  - PDF extraction uses `PdfDocument.Open()` from UglyToad.PdfPig, iterates pages and extracts text
  - DOCX extraction uses `WordprocessingDocument.Open()` from DocumentFormat.OpenXml.Packaging, extracts paragraph text
  - Whitespace normalization applied to all extracted content
  - Logging warnings when PDF extraction returns empty text
- ✅ Error handling: `NotSupportedException` for unsupported file types, proper resource disposal with `using` statements.
- ✅ DI registration via `DocumentProcessingServiceExtensions.AddDocumentProcessing()`.

## Testing Plan
- ✅ Unit tests for PDF and DOCX extraction behavior.
- ✅ Integration tests through `KnowledgeDocumentProcessorIntegrationTests` exercising full pipeline.
- ✅ Negative test for unsupported extension.

## Usage and Operational Notes
- **Invocation:** Called by `IKnowledgeDocumentProcessor.ProcessDocumentAsync()` during document ingestion.
- **File Type Detection:** Extension-based (.pdf, .docx) via switch expression.
- **Libraries Used:**
  - **UglyToad.PdfPig v1.7.0-custom-5:** PDF text extraction with page-level text access
  - **DocumentFormat.OpenXml v3.0.2:** DOCX paragraph text extraction via Open XML SDK
- **Configuration:** No configuration required; libraries selected based on file extension.
- **Operational Constraints:** 
  - File must exist on local disk (no streaming support yet)
  - PDF extraction requires valid PDF structure
  - DOCX extraction requires valid Open XML package structure
  - Whitespace normalized to prevent excessive spacing in extracted content

## Dependencies
- UglyToad.PdfPig (approved in [approved-dependencies.md](../Architecture/approved-dependencies.md))
- DocumentFormat.OpenXml (pre-approved for .NET)
- KM-REQ-002 (shared implementation in DocumentTextExtractor)

## Related Requirements
- [KM-REQ-002](KM-REQ-002.md) - Text extraction from multiple formats (primary requirement)

## Implementation Summary

### Core Implementation: ✅ COMPLETE

**Component:** [Daiv3.Knowledge.DocProc](../../../src/Daiv3.Knowledge.DocProc/)
**Interface:** [ITextExtractor.cs](../../../src/Daiv3.Knowledge.DocProc/ITextExtractor.cs)
**Implementation:** [DocumentTextExtractor.cs](../../../src/Daiv3.Knowledge.DocProc/DocumentTextExtractor.cs)

**Key Methods:**
- [ExtractAsync()](../../../src/Daiv3.Knowledge.DocProc/DocumentTextExtractor.cs#L37) - Main entry point, routes by extension
- [ExtractPdf()](../../../src/Daiv3.Knowledge.DocProc/DocumentTextExtractor.cs#L98) - Uses PdfPig to extract text from all PDF pages
- [ExtractDocx()](../../../src/Daiv3.Knowledge.DocProc/DocumentTextExtractor.cs#L76) - Uses Open XML SDK to extract paragraph text from DOCX

**Libraries:**
```xml
<PackageReference Include="UglyToad.PdfPig" Version="1.7.0-custom-5" />
<PackageReference Include="DocumentFormat.OpenXml" Version="3.0.2" />
```

**PDF Extraction (PdfPig):**
```csharp
using var document = PdfDocument.Open(filePath);
foreach (var page in document.GetPages())
{
    if (!string.IsNullOrWhiteSpace(page.Text))
    {
        builder.AppendLine(page.Text);
    }
}
```

**DOCX Extraction (Open XML SDK):**
```csharp
using var document = WordprocessingDocument.Open(filePath, false);
var body = document.MainDocumentPart?.Document?.Body;
var paragraphs = body.Elements<Paragraph>()
    .Select(p => p.InnerText)
    .Where(text => !string.IsNullOrWhiteSpace(text));
return string.Join(Environment.NewLine, paragraphs);
```

### Testing Summary

### Unit Tests: ✅ PASSING

**Test Project:** [tests/unit/Daiv3.UnitTests/Daiv3.UnitTests.csproj](../../../tests/unit/Daiv3.UnitTests/Daiv3.UnitTests.csproj)
**Test File:** [tests/unit/Daiv3.UnitTests/Knowledge/DocProc/DocumentTextExtractorTests.cs](../../../tests/unit/Daiv3.UnitTests/Knowledge/DocProc/DocumentTextExtractorTests.cs)
**Test Class:** [DocumentTextExtractorTests](../../../tests/unit/Daiv3.UnitTests/Knowledge/DocProc/DocumentTextExtractorTests.cs#L10)

**Test Methods:**
- [ExtractAsync_Docx_ReturnsContent](../../../tests/unit/Daiv3.UnitTests/Knowledge/DocProc/DocumentTextExtractorTests.cs#L63) ✅ - Uses Open XML SDK to create test DOCX
- [ExtractAsync_Pdf_ReturnsContent](../../../tests/unit/Daiv3.UnitTests/Knowledge/DocProc/DocumentTextExtractorTests.cs#L74) ✅ - Uses custom PDF builder for test content
- [ExtractAsync_UnsupportedExtension_Throws](../../../tests/unit/Daiv3.UnitTests/Knowledge/DocProc/DocumentTextExtractorTests.cs#L85) ✅ - Validates error handling

**Test Count:** 6 tests × 2 target frameworks = **12 total tests passing**

**Coverage:**
- ✅ DOCX extraction with Open XML SDK (creates test file, validates paragraph extraction)
- ✅ PDF extraction with PdfPig (creates test PDF, validates text extraction)
- ✅ Whitespace normalization
- ✅ Error handling for unsupported formats

### Integration Tests: ✅ PASSING

**Test Project:** [tests/integration/Daiv3.Knowledge.IntegrationTests/Daiv3.Knowledge.IntegrationTests.csproj](../../../tests/integration/Daiv3.Knowledge.IntegrationTests/Daiv3.Knowledge.IntegrationTests.csproj)
**Test File:** [tests/integration/Daiv3.Knowledge.IntegrationTests/KnowledgeDocumentProcessorIntegrationTests.cs](../../../tests/integration/Daiv3.Knowledge.IntegrationTests/KnowledgeDocumentProcessorIntegrationTests.cs)

**Coverage Note:** Integration tests validate full document processing pipeline including PDF/DOCX extraction → chunking → embedding → database storage.

### Build Status

| Aspect | Status |
|--------|--------|
| Build | ✅ Zero errors |
| Package Restore | ✅ Success (PdfPig 1.7.0-custom-5, DocumentFormat.OpenXml 3.0.2) |
| Unit Tests | ✅ 12/12 Passing (6 per target framework) |
| Integration Tests | ✅ Passing as part of document processing pipeline |

## Acceptance Criteria

✅ **AC1:** DocumentTextExtractor uses UglyToad.PdfPig for PDF extraction  
✅ **AC2:** DocumentTextExtractor uses DocumentFormat.OpenXml for DOCX extraction  
✅ **AC3:** Both libraries approved in [approved-dependencies.md](../Architecture/approved-dependencies.md)  
✅ **AC4:** Unit tests verify PDF extraction returns correct text  
✅ **AC5:** Unit tests verify DOCX extraction returns correct paragraph text  
✅ **AC6:** Integration tests validate end-to-end document processing  

## Notes

- Implementation shared with [KM-REQ-002](KM-REQ-002.md) in DocumentTextExtractor class
- PdfPig v1.7.0-custom-5 selected to resolve subpackage version mismatches (see [ADD-20260223-pdf-processing.md](../Architecture/decisions/ADD-20260223-pdf-processing.md))
- Open XML SDK v3.0.2 is the latest stable version
- Both libraries use read-only file access for safety
- Proper resource disposal via `using` statements prevents file handle leaks
