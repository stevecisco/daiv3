# 8. Agents, Skills & Tools - Requirements

## Overview
This document specifies requirements derived from Section 8 of the design document. It covers agent behavior, skill modules, MCP tools, and external integrations.

## Goals
- Provide autonomous agents with guardrails.
- Enable modular skill integration.
- Support MCP tool servers.

## Functional Requirements
- AST-REQ-001: Agents SHALL execute multi-step tasks with iteration limits.
- AST-REQ-002: Agents SHALL be definable via declarative configuration (JSON or YAML).
- AST-REQ-003: The system SHALL allow dynamic creation of agents for new task types.
- AST-REQ-004: Agents SHALL communicate via a message bus in the orchestration layer.
- AST-REQ-005: Agents SHALL support self-correction against success criteria.
- AST-REQ-006: Skills SHALL be modular and attachable to agents or invoked directly.
- AST-REQ-007: The system SHALL support built-in, user-defined, and imported skills.
- AST-REQ-008: The system SHALL support MCP tool servers and register them as tools.
- AST-REQ-009: Agents SHALL call external applications via REST APIs when available.
- AST-REQ-010: Agents SHALL support UI automation via Windows accessibility APIs when needed.

## Non-Functional Requirements
- AST-NFR-001: Agent execution SHOULD be observable and interruptible by the user.
- AST-NFR-002: Skill execution SHOULD be sandboxed where feasible.

## Data Requirements
- AST-DATA-001: Agent definitions SHALL be stored in a user-editable config format.
- AST-DATA-002: Skill metadata SHALL include name, category, inputs, outputs, and permissions.

## Dependencies
- MCP SDK.
- Windows UIAutomation APIs.
- REST client abstractions.

## Acceptance Criteria
- AST-ACC-001: A new skill can be added without recompiling the core app.
- AST-ACC-002: Agents can invoke registered MCP tools.
- AST-ACC-003: Agents can be paused or stopped by the user.

## Out of Scope
- Skill marketplace trust and sandbox model for v0.1.

## Risks and Open Questions
- Define standard schema for skill inputs and outputs.
- Confirm agent iteration defaults and limits.
