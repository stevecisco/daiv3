# 12. Key .NET Libraries & Components - Requirements

## Overview
This document specifies requirements derived from Section 12 of the design document. It enumerates required libraries and their usage expectations.

## Goals
- Standardize the core library stack.
- Ensure consistent component selection for v0.1.

## Functional Requirements
- KLC-REQ-001: The system SHALL use Microsoft.ML.OnnxRuntime.DirectML for in-process inference and embedding generation.
- KLC-REQ-002: The system SHALL use Microsoft.ML.Tokenizers for tokenization.
- KLC-REQ-003: The system SHALL use System.Numerics.TensorPrimitives for CPU vector math.
- KLC-REQ-004: The system SHALL use Microsoft.Data.Sqlite for persistence.
- KLC-REQ-005: The system SHALL integrate Foundry Local via Microsoft.Extensions.AI and the Foundry Local SDK.
- KLC-REQ-006: The system SHALL use Microsoft.Extensions.AI abstractions for online providers.
- KLC-REQ-007: The system SHALL use AngleSharp or HtmlAgilityPack for HTML parsing.
- KLC-REQ-008: The system SHALL use the Model Context Protocol .NET SDK for MCP tool support.
- KLC-REQ-009: The system SHALL use PdfPig and Open XML SDK for PDF and DOCX extraction.
- KLC-REQ-010: The system SHALL use a custom hosted service for scheduling (Quartz.NET rejected).
- KLC-REQ-011: The UI SHALL be implemented with WinUI 3 or Windows App SDK (or MAUI if chosen).

## Non-Functional Requirements
- KLC-NFR-001: Libraries SHOULD be compatible with .NET 10 and Windows 11.

## Dependencies
- NuGet package sources for ONNX and Foundry Local.

## Acceptance Criteria
- KLC-ACC-001: All listed libraries are documented in the build and dependency list.
- KLC-ACC-002: Each component has a defined responsibility and usage in architecture docs.

## Out of Scope
- Alternative library evaluations beyond those listed for v0.1.

## Risks and Open Questions
- Decide between AngleSharp and HtmlAgilityPack.
- **DECISION MADE:** Use custom scheduler (Quartz.NET rejected for complexity and overhead).
- Confirm final UI framework choice.
