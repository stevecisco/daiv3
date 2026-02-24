# KM-REQ-002

Source Spec: 4. Knowledge Management & Indexing - Requirements

## Requirement
The system SHALL extract text from supported formats: PDF, DOCX, HTML, MD, TXT, and images (future).

## Implementation Plan
- Owning component: `Daiv3.Knowledge.DocProc` with `ITextExtractor` and `DocumentTextExtractor`.
- Data contract: `ITextExtractor.ExtractAsync(string, CancellationToken)` returns extracted text.
- Implement PDF (PdfPig), DOCX (Open XML SDK), HTML (tag stripping), MD/TXT (raw text).
- Integrate extractor into `KnowledgeDocumentProcessor` ingestion flow.
- Log warnings when extraction yields empty output and surface errors via exceptions.

## Testing Plan
- Unit tests for each supported format and unsupported extension handling.
- Integration tests already exercise TXT extraction through the ingestion pipeline.
- Negative test for unsupported extension throws `NotSupportedException`.

## Usage and Operational Notes
- Extraction is invoked by `IKnowledgeDocumentProcessor.ProcessDocumentAsync`.
- File type is inferred from extension: `.pdf`, `.docx`, `.html`, `.htm`, `.md`, `.txt`.
- HTML extraction strips tags and decodes entities; Markdown is treated as plain text.
- PDF extraction uses PdfPig; DOCX extraction uses Open XML SDK.
- Images are not supported yet (future requirement).

## Dependencies
- HW-REQ-003
- KLC-REQ-001
- KLC-REQ-002
- KLC-REQ-004
- KLC-REQ-009 (PdfPig, Open XML SDK)

## Related Requirements
- None

## Testing Summary

### Unit Tests: ✅ PASSING

**Test Project:** [tests/unit/Daiv3.UnitTests/Daiv3.UnitTests.csproj](tests/unit/Daiv3.UnitTests/Daiv3.UnitTests.csproj)
**Test File:** [tests/unit/Daiv3.UnitTests/Knowledge/DocProc/DocumentTextExtractorTests.cs](tests/unit/Daiv3.UnitTests/Knowledge/DocProc/DocumentTextExtractorTests.cs)
**Test Class:** [DocumentTextExtractorTests](tests/unit/Daiv3.UnitTests/Knowledge/DocProc/DocumentTextExtractorTests.cs#L10)
**Test Methods:**
- [ExtractAsync_Txt_ReturnsContent](tests/unit/Daiv3.UnitTests/Knowledge/DocProc/DocumentTextExtractorTests.cs#L29) ✅
- [ExtractAsync_Markdown_ReturnsContent](tests/unit/Daiv3.UnitTests/Knowledge/DocProc/DocumentTextExtractorTests.cs#L40) ✅
- [ExtractAsync_Html_StripsTags](tests/unit/Daiv3.UnitTests/Knowledge/DocProc/DocumentTextExtractorTests.cs#L52) ✅
- [ExtractAsync_Docx_ReturnsContent](tests/unit/Daiv3.UnitTests/Knowledge/DocProc/DocumentTextExtractorTests.cs#L63) ✅
- [ExtractAsync_Pdf_ReturnsContent](tests/unit/Daiv3.UnitTests/Knowledge/DocProc/DocumentTextExtractorTests.cs#L74) ✅
- [ExtractAsync_UnsupportedExtension_Throws](tests/unit/Daiv3.UnitTests/Knowledge/DocProc/DocumentTextExtractorTests.cs#L85) ✅

**Test Count:** 6 tests per target framework (net10.0 and net10.0-windows10.0.26100) = **12 total passing**
**Last Test Run:** ✅ **6/6 passed** (528-533ms per target)
**Test Coverage:**
- ✅ TXT extraction returns content
- ✅ Markdown extraction returns content  
- ✅ HTML extraction strips tags and decodes text
- ✅ DOCX extraction returns paragraph text
- ✅ PDF extraction returns page text (PdfPig 1.7.0-custom-5)
- ✅ Unsupported extension throws `NotSupportedException`

### Integration Tests: ✅ Existing

**Test Project:** [tests/integration/Daiv3.Knowledge.IntegrationTests/Daiv3.Knowledge.IntegrationTests.csproj](tests/integration/Daiv3.Knowledge.IntegrationTests/Daiv3.Knowledge.IntegrationTests.csproj)
**Test File:** [tests/integration/Daiv3.Knowledge.IntegrationTests/KnowledgeDocumentProcessorIntegrationTests.cs](tests/integration/Daiv3.Knowledge.IntegrationTests/KnowledgeDocumentProcessorIntegrationTests.cs)
**Test Class:** [KnowledgeDocumentProcessorIntegrationTests](tests/integration/Daiv3.Knowledge.IntegrationTests/KnowledgeDocumentProcessorIntegrationTests.cs#L17)

**Test Methods:**
- [ProcessDocumentAsync_IngestsDocumentIntoDatabase](tests/integration/Daiv3.Knowledge.IntegrationTests/KnowledgeDocumentProcessorIntegrationTests.cs#L30)
- [ProcessDocumentsAsync_ProcessesMultipleDocuments](tests/integration/Daiv3.Knowledge.IntegrationTests/KnowledgeDocumentProcessorIntegrationTests.cs#L55)

**Coverage Note:** Integration tests validate the full extraction pipeline (file → text → chunking → embedding → storage) with real SQLite database.

### Build Status

| Target | Status |
|--------|--------|
| Build | ✅ Success (no errors, all DLLs generated) |
| Package Restore | ✅ Success (16 NU1903 CVE warnings only, 0 NU1603 subpackage mismatches) |
| Unit Tests | ✅ 12/12 Passing |


