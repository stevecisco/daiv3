# 7. Projects, Tasks & Scheduling - Requirements

## Overview
This document specifies requirements derived from Section 7 of the design document. It covers projects, tasks, dependencies, and scheduling.

## Goals
- Provide project-scoped knowledge and configuration.
- Support task dependency management and scheduling.

## Functional Requirements
- PTS-REQ-001: The system SHALL support projects with name, description, status, and timestamps.
- PTS-REQ-002: Projects SHALL have root paths for scoped document indexing.
- PTS-REQ-003: Projects SHALL store project-level instructions and model preferences.
- PTS-REQ-004: The system SHALL support tasks with title, description, status, priority, dependencies, and schedule.
- PTS-REQ-005: The orchestrator SHALL resolve task dependencies before enqueueing model requests.
- PTS-REQ-006: Task status SHALL follow Pending -> Queued -> In Progress -> Complete/Failed/Blocked.
- PTS-REQ-007: The scheduler SHALL support one-time, cron-based, and event-triggered tasks.
- PTS-REQ-008: Users SHALL be able to view, pause, and modify scheduled jobs.

## Non-Functional Requirements
- PTS-NFR-001: Dependency resolution SHOULD be deterministic.
- PTS-NFR-002: Scheduling SHOULD not block foreground UI interactions.

## Data Requirements
- PTS-DATA-001: The database SHALL store projects and tasks with dependency metadata.
- PTS-DATA-002: Scheduled tasks SHALL record next-run and last-run timestamps.

## Dependencies
- Orchestration layer for dependency evaluation.
- Scheduler service (Quartz.NET or custom).

## Acceptance Criteria
- PTS-ACC-001: A task with dependencies does not execute until dependencies are complete.
- PTS-ACC-002: Scheduled tasks can be paused and resumed.

## Out of Scope
- Cross-user or organizational scheduling for v0.1.

## Risks and Open Questions
- Decide cron syntax and UI representation.
- Define maximum task concurrency rules.
