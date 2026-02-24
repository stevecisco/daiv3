# Architecture Decision: PDF Text Extraction

## Context & Need
- KM-REQ-002 requires text extraction from PDF documents.
- KLC-REQ-009 mandates use of PdfPig and Open XML SDK for PDF/DOCX extraction.
- The knowledge ingestion pipeline must extract text reliably without online dependencies.

## Decision
- Adopt PdfPig for PDF text extraction in the Knowledge.DocProc layer.
- Use DocumentFormat.OpenXml for DOCX extraction (already pre-approved).

## Available Options

### Option 1: Custom PDF Parsing
**Pros:**
- Full control over parsing and behavior.
- No external dependencies.

**Cons:**
- High implementation complexity and risk.
- Long-term maintenance burden for PDF edge cases.

**Estimated Effort:** XL

### Option 2: PdfPig (Selected)
**Package:** UglyToad.PdfPig
**Version:** 1.7.0-custom-5
**Last Updated:** 2024-xx-xx (verify on approval)
**License:** MIT
**Maintainer:** UglyToad
**Microsoft Affiliation:** No

**Pros:**
- Mature, focused PDF text extraction.
- Works in-process and offline.
- Compatible with .NET 10 and Windows 11.

**Cons:**
- External dependency with version management.
- May require tuning for complex PDFs.

**Pricing:** Free (MIT)
**Security Considerations:**
- No known CVEs at time of decision (verify per release).
**Community & Support:**
- Active GitHub repository and community usage.
**Key Discussions:**
- https://github.com/UglyToad/PdfPig

### Option 3: iText 7
**Package:** itext7
**Version:** Latest
**Last Updated:** 2024-xx-xx (verify on approval)
**License:** AGPL/commercial
**Maintainer:** iText Software
**Microsoft Affiliation:** No

**Pros:**
- Comprehensive PDF support.

**Cons:**
- License restrictions (AGPL) for commercial use.
- Larger dependency surface.

**Pricing:** Commercial for closed-source usage.
**Security Considerations:**
- Must track CVEs and license constraints.
**Community & Support:**
- Commercial support available.

## Comparison Matrix
| Criteria | Custom | PdfPig | iText 7 |
|----------|--------|--------|---------|
| Security Control | ✅ Full | ⚠️ Limited | ⚠️ Limited |
| Maintenance | ❌ High burden | ✅ Moderate | ⚠️ Moderate |
| Feature Fit | ⚠️ Risky | ✅ Strong | ✅ Strong |
| Learning Curve | ❌ High | ✅ Low | ⚠️ Medium |

## Notes
- Confirm PdfPig version on implementation update and record in approved-dependencies.md.
- Validate extraction results on representative PDFs during integration testing.
