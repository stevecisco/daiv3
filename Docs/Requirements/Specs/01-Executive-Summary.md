# 1. Executive Summary - Requirements

## Overview
This document specifies requirements derived from Section 1 (Executive Summary) of the design document. It defines the foundational product goals for Daiv3 as a locally-first, privacy-respecting AI assistant that runs on Copilot+ PCs.

## Goals
- Deliver a local-first assistant that runs fully offline by default.
- Provide explicit, controlled fallback to online AI providers.
- Keep the system self-contained without external server dependencies.
- Ensure transparency of system activity to the user.
- Support extensibility via skills, agents, and MCP tools.
- Manage model resources to reduce thrashing and latency.
- Learn over time via structured, inspectable memory.

## Functional Requirements
- ES-REQ-001: The system SHALL process user requests using local models by default.
- ES-REQ-002: The system SHALL provide a configurable online fallback path that requires explicit user configuration or per-call confirmation.
- ES-REQ-003: The system SHALL operate without external servers or Docker dependencies for core functions (search, embeddings, storage).
- ES-REQ-004: The system SHALL expose a transparency view that shows model usage, indexing status, queue state, and agent activity.
- ES-REQ-005: The system SHALL support modular skills and agents that can be added without rebuilding the core application.
- ES-REQ-006: The system SHALL maintain a model request queue that batches tasks by model affinity.
- ES-REQ-007: The system SHALL store structured learnings from agent activity for reuse in future tasks.

## Non-Functional Requirements
- ES-NFR-001: The system SHOULD run efficiently on Copilot+ PCs with NPUs and remain usable on CPU-only fallback.
- ES-NFR-002: The system SHOULD not transmit user documents to online providers unless explicitly configured or confirmed.
- ES-NFR-003: The system SHOULD provide clear visibility into any online calls and token usage.

## Constraints
- ES-CON-001: The application MUST be locally installable and self-contained.
- ES-CON-002: The initial implementation targets .NET 10.

## Dependencies
- Microsoft Foundry Local for local SLM execution.
- ONNX Runtime for embeddings and local inference.
- SQLite for persistence.

## Acceptance Criteria
- ES-ACC-001: In offline mode, the system completes chat and search workflows without network access.
- ES-ACC-002: Users can enable online providers and must see usage and budget indicators.
- ES-ACC-003: Adding a new skill does not require a core app rebuild.

## Out of Scope
- Cloud-hosted central services.
- Mandatory online-only features.

## Risks and Open Questions
- Clarify which user actions require confirmation for online calls by default.
- Confirm the minimum viable transparency data set for v0.1.
