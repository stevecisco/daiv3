# 6. Knowledge Back-Propagation - Requirements

## Overview
This document specifies requirements derived from Section 6 of the design document. It defines how learned knowledge is promoted across scopes.

## Goals
- Allow users and agents to promote knowledge across hierarchical scopes.
- Preserve auditability and user control.

## Functional Requirements
- KBP-REQ-001: The system SHALL support promotion levels: Context, Sub-task, Task, Sub-topic, Topic, Project, Organization (future), Internet (export).
- KBP-REQ-002: The system SHALL allow users to select promotion targets when a task is completed.
- KBP-REQ-003: Agents MAY propose promotions but SHALL require user confirmation.
- KBP-REQ-004: The system SHALL generate a summary of new knowledge when promotion is triggered.
- KBP-REQ-005: Internet-level promotion SHALL create a draft artifact (e.g., blog post) for user review.

## Non-Functional Requirements
- KBP-NFR-001: Promotions SHOULD be transparent and reversible.
- KBP-NFR-002: The system SHOULD store provenance for each promotion action.

## Data Requirements
- KBP-DATA-001: Promotions SHALL reference source task/session IDs.
- KBP-DATA-002: Promotions SHALL store target scope and timestamps.

## Dependencies
- Task orchestration for completion events.
- UI workflow for promotion approvals.

## Acceptance Criteria
- KBP-ACC-001: User can promote task learnings to project scope.
- KBP-ACC-002: Promotion actions are recorded and visible in the dashboard.

## Out of Scope
- Organization-wide sharing for v0.1.

## Risks and Open Questions
- Define default promotion suggestions for agents.
- Clarify if promotions can be batched.
