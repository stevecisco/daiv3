# AST-REQ-008

Source Spec: 8. Agents, Skills & Tools - Requirements

## Requirement
The system SHALL support MCP tool servers and register them as tools.

## Strategic Context

This requirement enables **optional** MCP tool integration for third-party remote services. However, agents MUST intelligently route tool calls to the most efficient backend:

- **Direct C# invocation** for local services (knowledge search, scheduling, model queue)
- **CLI execution** for offline external tools (file operations, native utilities)
- **MCP** for persistent remote service integrations (GitHub, AWS, REST APIs, external SaaS)

Agents should prefer efficiency over protocol standardization: use direct/CLI whenever possible to minimize context overhead and latency.

## Implementation Plan

### 1. Tool Registry with Multi-Backend Support
- Extend `IToolRegistry` to store backend type with each tool (Direct, CLI, MCP)
- Implement `ToolRoutingService` to dispatch calls to appropriate backend
- Ensure Direct tools never route through MCP, and CLI tools are preferred over MCP for local operations

### 2. Agent Tool Invocation with Intelligent Routing
- Add `IToolInvoker` interface with routing logic:
  - Try direct C# service interface first (if available)
  - Fall back to CLI execution (if tool defines CLI command)
  - Use MCP only for remote services or when other backends unavailable
- Log routing decisions and backend used for each invocation
- Track context overhead (token count) when using MCP

### 3. MCP Tool Registration
- Implement `McpToolRegistry` to subscribe to MCP tool servers
- When MCP server connects, register each tool with backend=MCP
- Include tool schema, parameters, and context cost in registry entry
- Support dynamic tool registration (adding servers at runtime)

### 4. Agent Context & Tool Selection
- Agents receive available tools filtered by backend (direct/CLI preferred during local execution)
- When agent selects tool, routing service determines invocation method
- Log which backend was used and why (for observability)

### 5. Error Handling & Fallback
- If MCP tool server fails, log error and prevent agent retry (MCP is optional)
- If direct tool unavailable, try CLI fallback if applicable
- Agents never block on MCP failures; gracefully degrade to other tools

## Testing Plan

### Unit Tests
- `ToolRoutingServiceTests`: Route to correct backend based on tool type
  - Direct tools always use C# invocation
  - CLI tools prefer command execution over MCP
  - MCP tools only used for remote services
  - Backend selection logic and priority
  - Error cases (tool not found, backend unavailable)

- `AgentToolInvocationTests`: Agent selects and invokes tools
  - Agent receives correct tool list (with appropriate backends)
  - Tool invocation succeeds via correct backend
  - Tool invocation fails gracefully when backend unavailable
  - Routing logic honored in agent execution

### Integration Tests
- Mixed tool environment: Direct + CLI + MCP tools available
- Agent task using all tool types in single execution
- MCP server connection/disconnection during execution
- Tool registry consistency after MCP server update
- Context overhead tracking for MCP tools

### System Tests
- End-to-end agent task with tool selection from all backends
- Verify backends used match tool types (no MCP for direct tools)
- CLI tool execution in agent task
- MCP tool server unavailable scenario (graceful fallback)
- Performance: direct invocation faster than MCP for equivalent operations

## Usage and Operational Notes

### Configuration
- Register available tool backends in dependency injection
- Configure MCP servers via `McpServers` section
- Mark tools with backend hints (e.g., `[ToolBackend("Direct")]`)

### Agent Behavior
- **Tool selection**: Agents see all available tools, grouped by backend
- **Invocation**: System automatically routes to most efficient backend
- **Logging**: Each invocation logs backend used, duration, context overhead
- **Fallback**: If MCP server unavailable, agent adapts tool choices

### User-Visible Effects
- Tools discoverable in agent context with backend label ([Direct], [CLI], [MCP])
- Tool invocation logs show backend used
- Instrumentation dashboard shows backend distribution and context overhead
- Warnings if agent heavily relies on MCP tools (high context cost)

### Design Philosophy
MCP is a **delegation mechanism for remote services**, not a universalizing protocol. Examples:

- ❌ **Don't** expose knowledge search as MCP tool (use direct C# interface)
- ✅ **Do** expose GitHub operations via MCP (external remote service)
- ❌ **Don't** use MCP for file operations (use CLI if local exec not available)
- ✅ **Do** use MCP for AWS operations (standard remote integration)

## Dependencies
- KLC-REQ-008: MCP SDK integration (required for MCP tools; system functions without it)

## Related Requirements
- AST-ACC-002: Agents can invoke registered MCP tools (via intelligent routing)
