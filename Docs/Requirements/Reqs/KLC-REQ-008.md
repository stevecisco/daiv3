# KLC-REQ-008

Source Spec: 12. Key .NET Libraries & Components - Requirements

## Requirement
The system SHALL use the Model Context Protocol .NET SDK for MCP tool support.

## Strategic Context

MCP integration must be implemented as **optional extensibility for persistent remote services**, not as the default transport for all tools. This reflects two critical constraints:

1. **Token Budget Discipline**: Each MCP tool schema, description, and parameters are encoded as context tokens. With Daiv3's explicit token budget tracking (daily/monthly limits on online providers), this overhead must be carefully managed.

2. **Local-First Architecture**: Daiv3 is designed for on-device execution using Foundry Local and ONNX embeddings. Overhead for local tools (knowledge search, scheduling, model queue) must be zero.

## Tool Backend Decision Framework

The system supports a **multi-backend tool registry** with this priority:

| Backend | Use Case | Context Overhead | Examples |
|---------|----------|-----------------|----------|
| **Direct (C#)** | Local services, tight integration, zero latency | None | Knowledge search, model queue, scheduling, document processing |
| **CLI** | Offline external tools, local utilities, shell automation | Minimal (only per-invocation) | File operations, Windows native tools, local external binaries |
| **MCP** | Persistent remote services, shared third-party integrations | High (per-task) | GitHub API, AWS, REST integrations, external SaaS tooling |

**Default routing:** Direct → CLI → MCP, based on tool type and availability.

## Scope: MCP Support for Third-Party Tool Servers

KLC-REQ-008 implementation SHALL:

- **Provide MCP .NET SDK integration** for connecting to external MCP tool servers
- **Support tool server discovery and registration** via configuration
- **Enable agent-to-MCP routing** through the orchestration layer
- **Track context overhead** for MCP tools (token count per tool, per request)
- **NOT require** all tools to use MCP (direct C# and CLI tools are primary)
- **Document" when MCP is appropriate vs. when to use direct or CLI backends

## Implementation Plan

### 1. MCP Integration Layer
- Create `IMcpToolProvider` interface for connecting to MCP tool servers
- Implement `McpClientFactory` for tool server connection and lifecycle management
- Add `McpToolDescriptor` data contract mapping MCP tool schema to system tool format
- Support configuration via `appsettings.json` or CLI for tool server URLs/credentials

### 2. Tool Registry Enhancement
- Extend existing tool registry to support multiple backends (Direct, CLI, MCP)
- Add `ToolBackendType` enum to classify tool transport
- Implement `IToolRouterService` to route agent calls to appropriate backend
- Ensure routing logic prioritizes efficiency (direct first, then CLI, then MCP)

### 3. Context Overhead Tracking
- Log context token count for each MCP tool invocation
- Include in instrumentation: tool name, backend type, tokens consumed
- Track cumulative MCP token overhead per agent execution
- Warn when MCP context overhead exceeds threshold (configurable)

### 4. Error Handling & Resilience
- Handle MCP server connection failures with graceful fallback
- Implement timeout protection for remote tool server calls
- Log and surface MCP tool errors with context (which server, which tool)
- Optionally retry transient failures

### 5. Documentation
- Document tool registry configuration (adding new tools, MCP servers)
- Provide examples: when to use direct, CLI, vs. MCP
- Include MCP server setup guide (e.g., Claude.dev resources)
- Document overhead implications for token-budgeted scenarios

## Testing Plan

### Unit Tests
- `McpClientFactoryTests`: Connection establishment, server discovery, lifecycle
- `McpToolDescriptorTests`: Schema mapping, parameter validation, error serialization
- `McpToolProviderTests`: Tool invocation, timeout handling, error cases

### Integration Tests
- Tool registry with mixed backends (direct + CLI + MCP)
- Agent routing to MCP tools via orchestration layer
- Context overhead tracking and logging validation
- MCP server failure scenarios (connection loss, timeout, malformed response)

### System Tests
- End-to-end agent task with MCP tool invocation
- Token overhead measurement across task execution
- CLI tool invocation (negative test: ensure CLI tools don't use MCP)
- Direct tool invocation (negative test: ensure direct tools don't use MCP)

## Usage and Operational Notes

### Configuration
- **Enabling MCP servers**: Add server URLs/credentials to configuration
  ```json
  {
    "McpServers": [
      { "name": "github-mcp", "url": "http://localhost:3000", "type": "stdio" }
    ]
  }
  ```
- **Tool routing**: Automatic based on tool backend type; configurable per-tool

### User-Visible Effects
- Tools discoverable in agent tool selection UI
- MCP tool invocations show in execution logs with backend type label ("[MCP]")
- Context token overhead visible in instrumentation dashboard (if implemented)
- Warnings when MCP overhead exceeds threshold

### Constraints
- **Offline mode**: MCP tools unavailable when offline; fallback to direct/CLI
- **Token budgets**: MCP usage counts toward online provider token budgets
- **Security**: MCP servers must be trusted (no sandbox); configurable allowlist
- **Latency**: MCP adds RPC latency; not suitable for low-latency operations

### When NOT to Use MCP
- Knowledge search, document processing, scheduling (use direct C#)
- File operations, local utilities (use CLI)
- High-frequency calls or time-critical operations (use direct)

## Dependencies
- None (MCP is optional; system functions without it)

## Related Requirements
- AST-REQ-008: Support MCP tool servers and register them as tools
- AST-ACC-002: Agents can invoke registered MCP tools
