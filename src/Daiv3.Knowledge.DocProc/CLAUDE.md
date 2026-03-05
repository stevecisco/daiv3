# Daiv3.Knowledge.DocProc – Claude Context

> **Parent guidelines:** [Root CLAUDE.md](../../CLAUDE.md) | [Full AI Instructions](../../Docs/AI-Instructions.md)

---

## Purpose

Document processing engine. Parses PDF, Office (Word, Excel, PowerPoint), HTML, and Markdown formats into clean text chunks suitable for embedding. Uses `ML.Tokenizers` for token-aware text splitting and `PdfPig` for PDF parsing.

## Project Type

Library

## Target Framework

`net10.0` (no platform-specific dependencies).

## Key Responsibilities

- Multi-format document parsing (PDF, DOCX/XLSX/PPTX via DocumentFormat.OpenXml, HTML via ReverseMarkdown)
- Token-aware chunking strategies (configurable chunk size and overlap)
- Text cleaning and normalisation
- Returns structured `DocumentChunk` objects consumed by `Daiv3.Knowledge`

## Dependencies (All Pre-Approved)

- `Microsoft.ML.Tokenizers` — text tokenisation and splitting
- `PdfPig` — PDF text extraction
- `DocumentFormat.OpenXml` — Office document parsing
- `AngleSharp` / `ReverseMarkdown` — HTML to Markdown conversion

## Test Projects

Unit tests are covered within the `Daiv3.Knowledge.Tests` project:

```powershell
dotnet test tests/unit/Daiv3.Knowledge.Tests/Daiv3.Knowledge.Tests.csproj --nologo --verbosity minimal
```

There is no separate dedicated test project for DocProc. If you add significant new parsing logic, add it to `Daiv3.Knowledge.Tests` under a `DocProc/` subfolder.
