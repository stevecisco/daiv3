# AST-ACC-002

Source Spec: 8. Agents, Skills & Tools - Requirements

## Requirement
Agents can invoke registered MCP tools.

## Detailed Acceptance Criteria

This acceptance test verifies **intelligent MCP tool invocation within the broader multi-backend tool routing system**. It specifically validates:

### 1. MCP Tool Registration
- When an MCP server is configured and connected, its tools are registered in the system tool registry
- Each tool includes: name, description, parameters, and backend type = "MCP"
- Tools remain discoverable for agent selection

### 2. Agent MCP Tool Selection
- Agents can select MCP tools from the registry when appropriate
- Tool descriptions are visible to agent during planning (for context, not required to use)
- Agents understand parameters and invoke with correct argument structure

### 3. Successful MCP Tool Invocation
- Agent executing task calls MCP-backed tool with correct parameters
- Tool invocation completes and result is returned to agent
- Result processing matches tool schema (parameters extracted, formatted)
- Agent can integrate result into execution context

### 4. Intelligent Backend Selection (CRITICAL)
- **Do not use MCP for local tools**: Direct C# tools are never routed through MCP
- **Do not use MCP for CLI-capable operations**: File operations, local utilities use CLI backend when available
- **Use MCP only for remote services**: GitHub, AWS, external REST APIs, third-party SaaS
- When agent could use either direct/CLI or MCP, system routes to more efficient backend
- Logging shows backend used for each invocation

### 5. Error Handling
- If MCP server connection fails, agent gracefully adapts (skips MCP tools, uses alternatives)
- If MCP tool invocation fails (timeout, malformed response), error is logged and does not crash agent
- Agent can continue execution with non-MCP tools

### 6. Context Overhead Awareness
- System tracks context tokens consumed by MCP tool invocations
- Logged per-tool and per-task (for observability)
- Warnings generated if MCP overhead exceeds threshold

## Implementation Plan

### Test Scenario: Agent Uses MCP Tool for External Service

**Setup**:
- Configure mock MCP server with sample "github-search" tool
- Tool takes `query` parameter and returns search results
- Register MCP server and tool in registry

**Execution**:
- Create agent task: "Search GitHub repositories for ML frameworks"
- Agent selects github-search MCP tool
- Agent invokes with parameters: `{"query": "ML frameworks"}`
- MCP tool returns results
- Agent processes results and responds

**Assertions**:
- MCP tool invoked (not direct, not CLI)
- Tool returned results successfully
- Agent integrated results into task completion
- Logs show backend="MCP" for this invocation
- No context overhead warnings (single invocation)

### Test Scenario: Agent Prefers Efficient Backend

**Setup**:
- Register two tools: direct C# "ListFiles" and MCP "file-operations" (same functionality)
- Agent task requires file listing

**Execution**:
- Agent executes task requiring file listing
- System routes to direct C# backend (not MCP)

**Assertions**:
- Direct backend used (lower overhead, lower latency)
- MCP not invoked
- Logs confirm routing decision

## Testing Plan

### Unit Tests
- `McpToolInvocationTests`:
  - Tool registration from MCP server
  - Parameter validation before invocation
  - Response parsing and result extraction
  - Error handling (server unavailable, tool not found, timeout)
  - Context overhead tracking

- `AgentMcpToolSelectionTests`:
  - Agent receives MCP tools in planning context
  - Agent can select and invoke MCP tool
  - Result integration into agent state
  - Fallback when MCP unavailable

### Integration Tests
- Mock MCP server with sample tools
- Agent task using mock MCP tool invocation
- Full execution flow with multiple invocations
- Mixed environment (direct + CLI + MCP tools)
- Context overhead measurement and logging

### System Tests
- End-to-end workflow: agent task → tool selection → MCP invocation → result processing
- MCP server connection/disconnection handling
- Backend selection verification (direct preferred over MCP)
- Graceful degradation when MCP unavailable
- Performance characterization (MCP invocation latency)

## Usage and Operational Notes

### How Agents Use MCP Tools
- During agent planning, available tools are presented (including MCP tools)
- Agent selects appropriate tool based on task requirements
- System routes invocation to correct backend
- Result returned and integrated into agent context

### Developer Configuration
- Register MCP servers in configuration:
  ```json
  { "McpServers": [{ "name": "github", "url": "...", "type": "stdio" }] }
  ```
- Tools automatically discoverable after server connection

### Constraints
- **MCP is optional**: System fully functional without any MCP servers
- **Not for local operations**: Direct C# and CLI tools are preferred for efficiency
- **Remote services only**: MCP intended for external APIs and SaaS integrations
- **Token budget**: MCP invocations count toward online provider token budgets
- **Reliability**: MCP server failures do not block agent execution

## Dependencies
- KLC-REQ-008: MCP SDK integration
- AST-REQ-008: MCP tool server support

## Related Requirements
- None
