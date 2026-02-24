# Approved & Rejected Dependencies

This document tracks all external dependencies and their approval status for the Daiv3 project.

## Purpose

- Maintain a single source of truth for dependency approval decisions
- Track version-specific approvals and rejections
- Document rationale for decisions
- Enable quick reference during development

## Pre-Approved Categories

The following categories are **automatically approved** and do not require entries in this document:

### Microsoft .NET Framework & Official Packages
- All packages in the .NET 10 framework
- All `System.*` packages (e.g., System.CommandLine, System.IO, System.Net.Http, etc.)
- All `Microsoft.Extensions.*` packages
- All `Microsoft.ML.*` packages (ONNX Runtime, Tokenizers, DirectML)
- All Azure SDK packages (`Azure.*`, `Microsoft.Azure.*`)
- Microsoft.Data.Sqlite
- Microsoft.Extensions.AI
- DocumentFormat.OpenXml

### Project Dependencies
- Foundry Local SDK (project requirement)

**Note:** Even pre-approved packages should be evaluated for:
- Version compatibility
- Security vulnerabilities in specific versions
- Breaking changes between versions

## Dependency Registry

| Dependency Name | Version | Status | Description | Decision Date | Reason / Notes | ADD Reference |
|-----------------|---------|--------|-------------|---------------|----------------|---------------|
| UglyToad.PdfPig | 1.7.0-custom-5 | Approved | PDF text extraction | 2026-02-23 | Required for KM-REQ-002 and KLC-REQ-009; includes aligned subpackages | [ADD-20260223-pdf-processing.md](decisions/ADD-20260223-pdf-processing.md) |

## Rejected Dependencies

| Dependency Name | Version | Description | Rejected Date | Reason | Alternative Chosen |
|-----------------|---------|-------------|---------------|--------|-------------------|
| Quartz.NET | All | Job scheduling library | 2026-02-22 | Custom implementation preferred for core scheduling feature; reduces dependencies | Custom scheduler using BackgroundService + SQLite |

## Pending Review (ADD Required)

| Dependency Name | Versions Evaluated | Purpose | ADD Document | Status | KLC Reference |
|-----------------|-------------------|---------|--------------|--------|--------|
| AngleSharp vs HtmlAgilityPack | TBD | HTML parsing for web content extraction | ADD-HtmlParser | Pending Decision | KLC-REQ-007 |
| Model Context Protocol .NET SDK | TBD | Model Context Protocol tool integration | ADD-McpIntegration | Pending | KLC-REQ-008 |
| UI Framework (WinUI 3, Windows App SDK, or MAUI) | TBD | User interface implementation | ADD-UiFramework | Pending Decision | KLC-REQ-011 |

## Pre-Approved Libraries (KLC Requirements Coverage)

The following libraries are already covered by pre-approved categories:

| Library | Version | Pre-Approved Category | KLC Requirement | Notes |
|---------|---------|----------------------|-----------------|-------|
| Microsoft.ML.OnnxRuntime.DirectML | Latest | Microsoft.ML.* packages | KLC-REQ-001 | ONNX Runtime with DirectML acceleration |
| Microsoft.ML.Tokenizers | Latest | Microsoft.ML.* packages | KLC-REQ-002 | Tokenization support |
| System.Numerics.TensorPrimitives | Latest | System.* packages | KLC-REQ-003 | CPU vector math primitives |
| Microsoft.Data.Sqlite | Latest | Pre-approved list | KLC-REQ-004 | Persistence layer |
| Microsoft.Extensions.AI | Latest | Pre-approved list | KLC-REQ-005 & KLC-REQ-006 | Foundry Local integration & online provider abstractions |
| Foundry Local SDK | Latest | Project Dependencies | KLC-REQ-005 | Local model execution |
| DocumentFormat.OpenXml | Latest | Pre-approved list | KLC-REQ-009 | DOCX extraction |
| Quartz.NET | All | Rejected | KLC-REQ-010 | Use custom scheduler instead |

## Version Upgrade Process

**All dependency version upgrades require approval**, even for pre-approved packages.

### Before Upgrading:
1. Review release notes for breaking changes
2. Check for known security vulnerabilities (CVE database)
3. Assess compatibility with .NET 10 and target platforms
4. Test in isolated branch first
5. Document decision in this file

### Approval Process:
1. Create entry in "Pending Upgrades" section below
2. Present justification and risk assessment to user
3. Wait for explicit approval
4. Update version in this document with approval date
5. Proceed with upgrade

## Pending Upgrades

| Package Name | Current Version | Target Version | Justification | Requested Date | Status |
|--------------|-----------------|----------------|---------------|----------------|--------|
| coverlet.collector | 6.0.4 | 8.0.0 | Test coverage tooling; update for latest fixes | 2026-02-22 | Approved |
| Microsoft.Extensions.Logging | 9.0.10 | 10.0.3 | Align with .NET 10 stack | 2026-02-22 | Approved |
| Microsoft.Extensions.Logging.Console | 9.0.10 | 10.0.3 | Align with .NET 10 stack | 2026-02-22 | Approved |
| Microsoft.NET.Test.Sdk | 17.14.1 | 18.0.1 | Latest test SDK for .NET 10 | 2026-02-22 | Approved |
| OpenAI | 2.5.0 | 2.8.0 | Keep client library current; verify non-Microsoft dependency policy | 2026-02-22 | Approved |
| xunit.runner.visualstudio | 3.1.4 | 3.1.5 | Latest runner fixes | 2026-02-22 | Approved |

## Usage Guidelines

### For Developers / AI Assistants:
1. **Before adding ANY external dependency:**
   - Check if it's in a pre-approved category
   - If not, check this registry for explicit approval
   - If not found, STOP and create an ADD document
   
2. **Before upgrading ANY dependency:**
   - Check this registry for version-specific issues
   - Create entry in "Pending Upgrades" section
   - Get user approval before proceeding

3. **When a decision is made:**
   - Update this document immediately
   - Add entry with all required fields
   - Reference ADD document if applicable

### For Code Reviews:
- Verify all dependencies are in this registry or pre-approved categories
- Check that versions match approved versions
- Ensure no rejected dependencies have been added

## Document Maintenance

**Location:** `Docs/Requirements/Architecture/approved-dependencies.md`

**Update Frequency:** Immediately when any dependency decision is made

**Owner:** Project maintainer / architect

**Last Updated:** 2026-02-23

## References

- [Copilot Instructions - Dependency Management](../../../.vscode/copilot-instructions.md#3-dependency--library-management-philosophy)
- [Architecture Decision Documents](./decisions/)
- [Solution Structure](../Specs/Solution-Structure.md)
