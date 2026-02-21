# 3. System Architecture Overview - Requirements

## Overview
This document specifies requirements derived from Section 3 of the design document. It defines the layered architecture and boundaries between layers.

## Goals
- Establish clear layer responsibilities and interfaces.
- Enable maintainable separation of concerns.

## Functional Requirements
- ARCH-REQ-001: The system SHALL implement distinct layers: Presentation, Orchestration, Model Execution, Knowledge, and Persistence.
- ARCH-REQ-002: The Presentation Layer SHALL provide UI surfaces for Chat, Status Dashboard, Project Manager, and Settings.
- ARCH-REQ-003: The Orchestration Layer SHALL contain Task Orchestrator, Intent Resolution, Agent Manager, and Skill Registry.
- ARCH-REQ-004: The Model Execution Layer SHALL include Model Queue, Foundry Local Bridge, Online Provider Router, and ONNX Embedding Engine.
- ARCH-REQ-005: The Knowledge Layer SHALL include Two-Tier Index, SQLite Vector Store, Document Processor, and Knowledge Graph (placeholder).
- ARCH-REQ-006: The Persistence Layer SHALL persist data using SQLite and the file system.

## Non-Functional Requirements
- ARCH-NFR-001: Each layer SHOULD expose interfaces that allow mocking for testing.
- ARCH-NFR-002: The architecture SHOULD allow the Knowledge Graph to be added later without changing external interfaces.

## Constraints
- ARCH-CON-001: Cross-layer dependencies MUST be unidirectional (higher layers depend on lower layers only).

## Dependencies
- UI framework (WinUI 3 or MAUI).
- SQLite for persistence.

## Acceptance Criteria
- ARCH-ACC-001: There is a documented interface boundary for each layer.
- ARCH-ACC-002: Orchestration can be tested independently of the UI.
- ARCH-ACC-003: Model Execution can be swapped between local and online providers without UI changes.

## Out of Scope
- Full knowledge graph implementation for v0.1.

## Risks and Open Questions
- Decide between WinUI 3 and MAUI for v0.1.
- Confirm interface contracts between Orchestration and Model Execution layers.
