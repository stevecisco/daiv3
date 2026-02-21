# 9. Learning Memory - Requirements

## Overview
This document specifies requirements derived from Section 9 of the design document. It defines how learnings are created, stored, retrieved, and governed.

## Goals
- Capture corrections and improvements as structured learnings.
- Reuse learnings to improve future task performance.
- Provide full user control over learning memory.

## Functional Requirements
- LM-REQ-001: The system SHALL create a learning when triggered by user feedback, self-correction, compilation error, tool failure, knowledge conflict, or explicit call.
- LM-REQ-002: Each learning SHALL include fields: id, title, description, trigger_type, scope, source_agent, source_task_id, embedding, tags, confidence, status, times_applied, timestamps, created_by.
- LM-REQ-003: The system SHALL store learnings in a dedicated SQLite table.
- LM-REQ-004: The system SHALL generate embeddings for learning descriptions for semantic retrieval.
- LM-REQ-005: Before agent execution, relevant learnings SHALL be retrieved and injected into prompts.
- LM-REQ-006: The system SHALL filter learnings by scope and rank by similarity.

## User Control Requirements
- LM-REQ-007: Users SHALL view, filter, and edit learnings.
- LM-REQ-008: Users SHALL suppress, promote, or supersede learnings.
- LM-REQ-009: Users SHALL be able to manually create learnings.

## Non-Functional Requirements
- LM-NFR-001: Learning retrieval SHOULD be fast and not block the UI.
- LM-NFR-002: Learnings SHOULD be transparent and auditable.

## Data Requirements
- LM-DATA-001: Learning records SHALL store provenance and timestamps.

## Dependencies
- Embedding pipeline for learning text.
- Transparency dashboard UI.

## Acceptance Criteria
- LM-ACC-001: A corrected answer results in a new learning entry.
- LM-ACC-002: Relevant learnings appear in agent prompts for similar tasks.
- LM-ACC-003: Users can suppress a learning and it is no longer injected.

## Out of Scope
- Cross-device learning sync for v0.1.

## Risks and Open Questions
- Define similarity threshold for learning injection.
- Decide default confidence scoring heuristics.
