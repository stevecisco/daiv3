# Architecture Decision Documents

This directory contains Architecture Decision Documents (ADDs) that document significant architectural and dependency decisions made during the development of Daiv3.

## Purpose

ADDs capture the context, analysis, and rationale behind key technical decisions, particularly:
- External library and dependency selections
- Architectural patterns and approaches
- Technology platform choices
- Security and performance trade-offs

**See also:** [Approved Dependencies Registry](../approved-dependencies.md) - The single source of truth for all dependency approval decisions.

## Document Format

Each ADD follows a consistent format to enable quick review and informed decision-making.

**File Naming Convention:** `ADD-YYYYMMDD-<feature-or-decision-name>.md`

Examples:
- `ADD-20260222-html-parsing-library.md`
- `ADD-20260223-vector-database-approach.md`
- `ADD-20260225-pdf-extraction-library.md`

## Decision Lifecycle

1. **Proposed** - Initial analysis, awaiting review and decision
2. **Accepted** - Decision approved and implementation authorized
3. **Rejected** - Decision rejected, alternative approach chosen
4. **Superseded** - Decision replaced by a newer ADD (link to replacement)

## When to Create an ADD

Create an ADD when:
- Considering an external NuGet package or library not in the pre-approved list
- Making a significant architectural pattern choice
- Selecting between multiple implementation approaches for a complex feature
- Making a security or performance trade-off decision
- Choosing a technology platform or framework

**Before creating an ADD:**
1. Check [approved-dependencies.md](../approved-dependencies.md) to see if the dependency or similar version is already approved/rejected
2. Review pre-approved categories (all Microsoft/.NET packages are automatically approved)
3. If not found and not pre-approved, create an ADD and present for approval

**After ADD approval:**
- Update [approved-dependencies.md](../approved-dependencies.md) with the decision
- Include approval date, version, and ADD reference

## Pre-Approved Dependencies (No ADD Required)

The following categories do NOT require an ADD:
- .NET BCL (Base Class Library) and runtime libraries
- Microsoft.Extensions.* packages
- Microsoft.ML.* packages (ONNX Runtime, Tokenizers, etc.)
- Azure SDK packages (Azure.*, Microsoft.Azure.*)
- System.* packages that are part of .NET
- Microsoft.Data.Sqlite
- Foundry Local SDK
- Microsoft.Extensions.AI

## Current Decisions

| Date | Decision | Status | Document |
|------|----------|--------|----------|
| 2026-02-22 | ADD Template Created | Reference | [ADD-TEMPLATE.md](ADD-TEMPLATE.md) |

## References

- [Approved Dependencies Registry](../approved-dependencies.md) - **Check this first before creating an ADD**
- [Copilot Instructions - Dependency Management](./../../../.vscode/copilot-instructions.md#3-dependency--library-management-philosophy)
- [Solution Structure](../Specs/Solution-Structure.md)
- [Architecture Overview](../architecture-overview.md)
