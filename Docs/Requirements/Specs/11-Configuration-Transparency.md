# 11. Configuration & User Transparency - Requirements

## Overview
This document specifies requirements derived from Section 11 of the design document. It covers settings and transparency dashboard behavior.

## Goals
- Provide comprehensive, local configuration control.
- Make system activity transparent to users.

## Functional Requirements
- CT-REQ-001: The system SHALL store all settings locally.
- CT-REQ-002: The settings UI SHALL configure watched directories, model preferences, token budgets, online access rules, agents, skills, scheduling, knowledge paths, and skill marketplace sources.
- CT-REQ-003: The system SHALL provide a real-time transparency dashboard.
- CT-REQ-004: The dashboard SHALL display model queue status, current model, and pending requests by priority.
- CT-REQ-005: The dashboard SHALL display indexing progress, last scan time, and errors.
- CT-REQ-006: The dashboard SHALL display agent activity, iterations, and token usage.
- CT-REQ-007: The dashboard SHALL display online token usage and budget status.
- CT-REQ-008: The dashboard SHALL display scheduled jobs and results.
- CT-REQ-009: The dashboard SHALL display pending knowledge promotions.

## Non-Functional Requirements
- CT-NFR-001: The dashboard SHOULD update in near real-time without blocking UI.
- CT-NFR-002: Settings changes SHOULD be validated and applied safely.

## Data Requirements
- CT-DATA-001: Settings SHALL be versioned to support upgrades.

## Dependencies
- UI framework (WinUI 3 or MAUI).
- Local storage for settings (JSON/SQLite).

## Acceptance Criteria
- CT-ACC-001: Users can configure online access rules and see them applied.
- CT-ACC-002: Users can observe active model queue state in the dashboard.
- CT-ACC-003: Users can see token usage vs budget per provider.

## Out of Scope
- Cloud-synced settings for v0.1.

## Risks and Open Questions
- Define refresh rates and data push mechanisms for dashboard updates.
- Determine settings storage format for v0.1.
