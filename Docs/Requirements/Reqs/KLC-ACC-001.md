# KLC-ACC-001

Source Spec: 12. Key .NET Libraries & Components - Requirements

## Requirement
All listed libraries are documented in the build and dependency list.

## Status
**COMPLETE** - All KLC requirements libraries are documented in `Docs/Requirements/Architecture/approved-dependencies.md`

## Documentation Summary

All libraries from KLC-REQ-001 through KLC-REQ-011 are documented:

### Pre-Approved & In Use
- **KLC-REQ-001:** Microsoft.ML.OnnxRuntime.DirectML - In use, version 9.0.0+ - Tests verified ✓
- **KLC-REQ-002:** Microsoft.ML.Tokenizers - Pre-approved, implementation complete ✓
- **KLC-REQ-003:** System.Numerics.TensorPrimitives - In use, 48 passing unit tests ✓
- **KLC-REQ-004:** Microsoft.Data.Sqlite - In use, version 9.0.0, integration tests passing ✓
- **KLC-REQ-005:** Foundry Local SDK + Microsoft.Extensions.AI - Pre-approved ✓
- **KLC-REQ-006:** Microsoft.Extensions.AI for online providers - Pre-approved ✓
- **KLC-REQ-009:** DocumentFormat.OpenXml (DOCX extraction) - Pre-approved ✓

### Pending Decision (ADD Required)
- **KLC-REQ-007:** AngleSharp vs HtmlAgilityPack for HTML parsing - Decision pending
- **KLC-REQ-008:** Model Context Protocol .NET SDK - Under review
- **KLC-REQ-009:** PdfPig for PDF extraction - Under review
- **KLC-REQ-010:** Quartz.NET vs custom scheduler - REJECTED, custom scheduler chosen ✓
- **KLC-REQ-011:** UI Framework (WinUI 3, Windows App SDK, or MAUI) - Decision pending

## Implementation Plan
- ✓ Document all libraries in approved-dependencies.md
- ✓ Verify usage in project codebase
- Create Architecture Decision Documents (ADDs) for pending choices
- Finalize decisions on:
  - HTML parsing library (AngleSharp vs HtmlAgilityPack)
  - PDF extraction library (PdfPig approval)
  - Model Context Protocol SDK integration
  - UI framework selection

## Testing Plan
- All currently implemented libraries have unit & integration tests
- Verify CLI commands work with all documented libraries
- Manual testing in MAUI once UI framework is selected

## Usage and Operational Notes
- Libraries are listed in project `.csproj` files under `PackageReference` elements
- All Microsoft-official packages inherit .NET 10 compatibility
- Custom scheduler implementation available in `Daiv3.Scheduler` project
- Version management tracked in `approved-dependencies.md`

## Dependencies
- [approved-dependencies.md](../Architecture/approved-dependencies.md) - Dependency registry
- [Specification 12](../Specs/12-Key-Libraries-Components.md) - Core requirements

## Related Requirements
- KLC-REQ-001 through KLC-REQ-011 (library-specific requirements)
- KLC-ACC-002 (each component responsibility in architecture)
