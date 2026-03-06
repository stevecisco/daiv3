# DAIv3 CLI Command Examples

> **Auto-Updated**: This file is automatically updated as new CLI commands are implemented.
> **Last Updated**: February 28, 2026

## Overview

The DAIv3 CLI provides command-line access to all system features for testing, automation, and scripting. Commands are organized by functional area.

## Usage

```bash
# Using the run script (Windows)
.\run-cli.bat [command] [options]

# Direct execution
dotnet run --project src/Daiv3.App.Cli/Daiv3.App.Cli.csproj -- [command] [options]

# After build
.\src\Daiv3.App.Cli\bin\Debug\net10.0-windows10.0.26100\Daiv3.App.Cli.exe [command] [options]
```

---

## Database Commands

### Initialize Database
```bash
.\run-cli.bat db init
```
Creates the SQLite database with all required tables and schema.

**Output Example:**
```
Initializing Daiv3 database...
✓ Database initialized successfully
  Path: C:\Users\[user]\AppData\Local\Daiv3\daiv3.db
  Schema Version: 1
```

### Check Database Status
```bash
.\run-cli.bat db status
```
Displays database location, size, and schema version.

**Output Example:**
```
Database Status:
  Path: C:\Users\[user]\AppData\Local\Daiv3\daiv3.db
  Size: 98,304 bytes
  Last Modified: 2/23/2026 2:30:45 PM
  Schema Version: 1
```

---

## Dashboard Commands

### Show System Dashboard
```bash
.\run-cli.bat dashboard
```
Displays system status, hardware detection, and task queue information.

**Output Example:**
```
═══════════════════════════════════════════════════════════
                    DAIV3 DASHBOARD
═══════════════════════════════════════════════════════════

HARDWARE STATUS:
  Overall: System Ready
  NPU: Detection pending (integration pending)
  GPU: Detection pending (integration pending)

TASK QUEUE:
  Queued Tasks: 0
  Completed Tasks: 0
  Current Activity: Ready for tasks

NOTE: Full hardware detection pending integration.
      Use 'db status' to check database, 'projects list' for projects.
```

### Show System Admin Dashboard (CT-REQ-010)
```bash
.\run-cli.bat dashboard admin
```
Displays infrastructure metrics (CPU, memory, storage, queue, and agent workload).

### Show System Admin Dashboard as JSON
```bash
.\run-cli.bat dashboard admin --json
```
Outputs the latest snapshot as structured JSON for automation.

### Watch Live Admin Metrics
```bash
.\run-cli.bat dashboard admin --watch
```
Refreshes metrics every 3 seconds until `Ctrl+C`.

### Show 24-Hour Admin Trends
```bash
.\run-cli.bat dashboard admin --history
```
Shows min/avg/max trends over the last 24 hours using persisted snapshots.

### Show 24-Hour Admin Trends as JSON
```bash
.\run-cli.bat dashboard admin --history --json
```
Outputs the 24-hour trend summary in JSON format.

---

## Chat Commands

### Interactive Chat
```bash
.\run-cli.bat chat
```
Starts an interactive chat session. Type messages and press Enter. Type `exit` to quit.

**Example Session:**
```
═══════════════════════════════════════════════════════════
                 DAIV3 CHAT INTERFACE
═══════════════════════════════════════════════════════════
Type your message and press Enter. Type 'exit' to quit.

You: Hello, how are you?
AI: Echo: Hello, how are you? (Orchestration integration pending)
You: What can you do?
AI: Echo: What can you do? (Orchestration integration pending)
You: exit
Goodbye!
```

### Single Message Mode
```bash
.\run-cli.bat chat --message "Hello from CLI"
# or
.\run-cli.bat chat -m "Hello from CLI"
```
Sends a single message and exits immediately.

**Output Example:**
```
User: Hello from CLI
AI: Echo: Hello from CLI (Orchestration integration pending)
```

---

## Project Management Commands

### List All Projects
```bash
.\run-cli.bat projects list
```
Displays all projects in the system.

**Output Example:**
```
PROJECTS:
  ID: 7e0f2e8f-4c3e-4b91-93f8-001fd0d9589f
  Name: My Project
  Description: Project description
  Root Paths:
    - C:\repo\src
    - C:\repo\docs
  Instructions: Keep responses concise and prioritize reliability.
  Preferred Model: phi-4-mini
  Fallback Model: gpt-4o-mini
  Status: active
  Created: 2026-02-28 18:42:11 UTC
  Updated: 2026-02-28 18:42:11 UTC
```

### Create New Project
```bash
.\run-cli.bat projects create --name "My Project" --description "Project description"
# or short form
.\run-cli.bat projects create -n "My Project" -d "Project description"
# with explicit root path(s)
.\run-cli.bat projects create --name "My Project" -r "C:\repo\src"
.\run-cli.bat projects create --name "My Project" -r "C:\repo\src" -r "C:\repo\docs"
# with project-level instructions and model preferences
.\run-cli.bat projects create --name "My Project" -i "Keep responses concise" --preferred-model "phi-4-mini" --fallback-model "gpt-4o-mini"
```
Creates a new project with the specified name and optional description. Root paths default to the current working directory if `--root-path` is not provided.

**Output Example:**
```
✓ Project created successfully
  ID: ccc3d3cd-75e7-4a90-a2ec-c04109ce1e0c
  Name: My Project
  Description: Project description
  Root Paths:
    - C:\repo\src
  Instructions: Keep responses concise
  Preferred Model: phi-4-mini
  Fallback Model: gpt-4o-mini
  Status: active
  Created: 2026-02-28 18:42:11 UTC
```

---

## Task Management Commands

### List All Tasks
```bash
.\run-cli.bat tasks list
# filter by project
.\run-cli.bat tasks list --project-id "7e0f2e8f-4c3e-4b91-93f8-001fd0d9589f"
# or short form
.\run-cli.bat tasks list -p "7e0f2e8f-4c3e-4b91-93f8-001fd0d9589f"
# filter by status
.\run-cli.bat tasks list --status pending
# or short form
.\run-cli.bat tasks list -s in-progress
```
Displays all tasks, optionally filtered by project ID or status.

**Output Example:**
```
TASKS:
  ID: a1b2c3d4-e5f6-4g7h-8i9j-0k1l2m3n4o5p
  Title: Implement API
  Description: Create REST endpoints for user management
  Project: 7e0f2e8f-4c3e-4b91-93f8-001fd0d9589f
  Status: in-progress
  Priority: 8
  Dependencies: ["dep-task-1", "dep-task-2"]
  Next Run: 2026-03-01 10:00:00 UTC
  Last Run: 2026-02-28 14:30:00 UTC
  Created: 2026-02-28 09:15:30 UTC
  Updated: 2026-02-28 18:42:11 UTC
```

### Create New Task
```bash
.\run-cli.bat tasks create --title "My Task"
# with description
.\run-cli.bat tasks create --title "My Task" --description "Task description"
# with short form
.\run-cli.bat tasks create -t "My Task" -d "Task description"
# associate with project
.\run-cli.bat tasks create --title "My Task" --project-id "7e0f2e8f-4c3e-4b91-93f8-001fd0d9589f"
# or short form
.\run-cli.bat tasks create -t "My Task" -p "7e0f2e8f-4c3e-4b91-93f8-001fd0d9589f"
# set priority (0-9, default: 5)
.\run-cli.bat tasks create --title "My Task" --priority 8
# add dependencies
.\run-cli.bat tasks create --title "My Task" --dependency "dep-task-1" --dependency "dep-task-2"
# combine all options
.\run-cli.bat tasks create -t "Implement API" -d "Create REST endpoints" -p "7e0f2e8f-4c3e-4b91-93f8-001fd0d9589f" --priority 8 --dep "planning-task" --dep "design-task"
```
Creates a new task with specified properties. Title is required; all other fields are optional.

**Output Example:**
```
✓ Task created successfully
  ID: a1b2c3d4-e5f6-4g7h-8i9j-0k1l2m3n4o5p
  Title: Implement API
  Description: Create REST endpoints
  Project: 7e0f2e8f-4c3e-4b91-93f8-001fd0d9589f
  Status: pending
  Priority: 8
  Dependencies: ["planning-task", "design-task"]
  Created: 2026-02-28 18:42:11 UTC
```

### Update Task
```bash
.\run-cli.bat tasks update --id "a1b2c3d4-e5f6-4g7h-8i9j-0k1l2m3n4o5p" --status in-progress
# or short form
.\run-cli.bat tasks update --id "a1b2c3d4-e5f6-4g7h-8i9j-0k1l2m3n4o5p" -s complete
# update priority
.\run-cli.bat tasks update --id "a1b2c3d4-e5f6-4g7h-8i9j-0k1l2m3n4o5p" --priority 9
# update both status and priority
.\run-cli.bat tasks update --id "a1b2c3d4-e5f6-4g7h-8i9j-0k1l2m3n4o5p" --status complete --priority 1
```
Updates an existing task's status and/or priority. Setting status to "complete" or "completed" automatically records the completion timestamp.

**Output Example:**
```
✓ Task updated successfully
  ID: a1b2c3d4-e5f6-4g7h-8i9j-0k1l2m3n4o5p
  Title: Implement API
  Status: complete
  Priority: 1
  Updated: 2026-02-28 19:15:30 UTC
```

---

## Schedule Management Commands

### List Scheduled Jobs
```bash
.\run-cli.bat schedule list
# Filter by status
.\run-cli.bat schedule list --status pending
.\run-cli.bat schedule list --status running
.\run-cli.bat schedule list -s completed
```
Lists all scheduled jobs with their current status, schedule type, and execution details.

**Output Example:**
```
SCHEDULED JOBS:
  Job ID: job_20260228120000_000001
  Name: daily-backup
  Type: Cron
  Status: Scheduled
  Scheduled At: 2026-03-01 00:00:00 UTC
  Cron Expression: 0 0 * * *
  Execution Count: 5
  Last Started: 2026-02-28 00:00:00 UTC
  Last Completed: 2026-02-28 00:00:15 UTC
  Last Duration: 15234 ms
  Created: 2026-02-23 14:30:00 UTC

  Job ID: job_20260228120030_000002
  Name: file-processor
  Type: EventTriggered
  Status: Pending
  Event Type: filesystem.file_created
  Execution Count: 12
  Last Completed: 2026-02-28 18:45:33 UTC
  Created: 2026-02-25 09:15:22 UTC
```

### Schedule Cron Job
```bash
# Schedule a daily job at midnight
.\run-cli.bat schedule cron --name "daily-backup" --expression "0 0 * * *"
# or short form
.\run-cli.bat schedule cron -n "daily-backup" -e "0 0 * * *"

# Every 15 minutes
.\run-cli.bat schedule cron --name "frequent-check" --expression "*/15 * * * *"

# Weekdays at noon
.\run-cli.bat schedule cron --name "weekday-report" --expression "0 12 * * 1-5"

# Multiple times per day
.\run-cli.bat schedule cron --name "twice-daily" --expression "0 9,17 * * *"
```
Schedules a job using a standard 5-field cron expression. The job will execute automatically at each matching time.

**Cron Format:** `minute hour day month dayOfWeek`
- minute: 0-59
- hour: 0-23  
- day: 1-31
- month: 1-12
- dayOfWeek: 0-6 (0=Sunday)

**Special Characters:**
- `*` - any value
- `,` - value list (e.g., `1,3,5`)
- `-` - range (e.g., `1-5`)
- `/` - step (e.g., `*/15` or `0-30/5`)

**Output Example:**
```
✓ Cron job scheduled successfully
  Job ID: job_20260228142530_000003
  Name: daily-backup
  Cron Expression: 0 0 * * *

Note: This is a demo job. In production, integrate with actual task execution.
```

### Schedule One-Time Job
```bash
# Schedule for specific UTC time
.\run-cli.bat schedule once --name "one-time-task" --time "2026-03-01T10:30:00Z"
# or short form
.\run-cli.bat schedule once -n "one-time-task" -t "2026-03-01T10:30:00Z"
```
Schedules a job to run once at a specific UTC time. Time must be in ISO 8601 format.

**Output Example:**
```
✓ One-time job scheduled successfully
  Job ID: job_20260228142545_000004
  Name: one-time-task
  Scheduled Time: 2026-03-01 10:30:00 UTC

Note: This is a demo job. In production, integrate with actual task execution.
```

### Schedule Event-Triggered Job
```bash
# Register job to run on file system events
.\run-cli.bat schedule on-event --name "file-processor" --event-type "filesystem.file_created"
# or short form
.\run-cli.bat schedule on-event -n "file-processor" -e "filesystem.file_created"

# Other event types
.\run-cli.bat schedule on-event --name "db-sync" --event-type "database.record_updated"
.\run-cli.bat schedule on-event --name "notification-handler" --event-type "user.message_received"
```
Registers a job to execute whenever the specified event type is raised. The job will remain pending and can execute multiple times.

**Common Event Types:**
- `filesystem.file_created`
- `filesystem.file_modified`
- `database.record_inserted`
- `database.record_updated`
- Custom application events

**Output Example:**
```
✓ Event-triggered job scheduled successfully
  Job ID: job_20260228142600_000005
  Name: file-processor
  Event Type: filesystem.file_created

Note: This job will execute when an event of the specified type is raised.
      Use your application's event system to trigger execution.
```

### Cancel Scheduled Job
```bash
.\run-cli.bat schedule cancel --id "job_20260228120000_000001"
```
Cancels a scheduled job, preventing any future executions. Running jobs will be signaled to cancel gracefully.

**Output Example:**
```
✓ Job cancelled successfully
  Job ID: job_20260228120000_000001
```

### Show Job Details
```bash
.\run-cli.bat schedule info --id "job_20260228120000_000001"
```
Displays detailed information about a specific scheduled job including execution history.

**Output Example:**
```
JOB DETAILS:
  Job ID: job_20260228120000_000001
  Name: daily-backup
  Type: Cron
  Status: Scheduled
  Scheduled At: 2026-03-01 00:00:00 UTC
  Cron Expression: 0 0 * * *
  Execution Count: 5
  Last Started: 2026-02-28 00:00:00 UTC
  Last Completed: 2026-02-28 00:00:15 UTC
  Last Duration: 15234 ms
  Created: 2026-02-23 14:30:00 UTC
```

### Pause Scheduled Job
```bash
.\run-cli.bat schedule pause --id "job_20260228120000_000001"
```
Pauses a scheduled job, preventing it from executing until resumed. The job remains in the system and can be resumed later.

**Restrictions:**
- Cannot pause jobs that are currently running
- Cannot pause jobs that are already completed or cancelled

**Output Example:**
```
✓ Job paused successfully
  Job ID: job_20260228120000_000001

The job will not execute while paused. Use 'schedule resume' to resume it.
```

### Resume Paused Job
```bash
.\run-cli.bat schedule resume --id "job_20260228120000_000001"
```
Resumes a paused job, allowing it to execute according to its schedule.

**Output Example:**
```
✓ Job resumed successfully
  Job ID: job_20260228120000_000001

The job is now active and will execute according to its schedule.
```

### Modify Job Schedule
```bash
# Modify a one-time job's scheduled time
.\run-cli.bat schedule modify --id "job_20260228120000_000001" --time "2026-03-02T15:30:00Z"

# Modify a recurring job's interval (in seconds)
.\run-cli.bat schedule modify --id "job_20260228120000_000002" --interval 300

# Modify a cron job's expression
.\run-cli.bat schedule modify --id "job_20260228120000_000003" --cron "0 */2 * * *"
# or short form
.\run-cli.bat schedule modify --id "job_20260228120000_000003" -c "0 */2 * * *"

# Modify an event-triggered job's event type
.\run-cli.bat schedule modify --id "job_20260228120000_000004" --event-type "filesystem.file_updated"
# or short form
.\run-cli.bat schedule modify --id "job_20260228120000_000004" -e "filesystem.file_updated"
```
Modifies the scheduling parameters of an existing job. The modification parameter must match the job's schedule type.

**Schedule Type Requirements:**
- **OneTime jobs:** Use `--time` (UTC, ISO 8601 format)
- **Recurring jobs:** Use `--interval` (seconds)
- **Cron jobs:** Use `--cron` (5-field expression)
- **EventTriggered jobs:** Use `--event-type`

**Restrictions:**
- Cannot modify jobs that are currently running
- Cannot modify jobs that are completed or cancelled
- Cannot modify immediate-type jobs
- Only one modification parameter allowed per command

**Output Example:**
```
✓ Job schedule modified successfully
  Job ID: job_20260228120000_000003
  Job Name: daily-backup
  Schedule Type: Cron
  New Cron Expression: 0 */2 * * *
```

### Combined Workflow Example
```bash
# Pause a job
.\run-cli.bat schedule pause --id "job_20260228120000_000001"

# Modify while paused
.\run-cli.bat schedule modify --id "job_20260228120000_000001" --time "2026-03-05T09:00:00Z"

# Resume with new schedule
.\run-cli.bat schedule resume --id "job_20260228120000_000001"

# Verify the changes
.\run-cli.bat schedule info --id "job_20260228120000_000001"
```

---

## Agent Management Commands

### List All Agents
```bash
.\run-cli.bat agent list
```
Lists all configured agents with their details.

**Output Example:**
```
AGENTS:
  Agent ID: 550e8400-e29b-41d4-a716-446655440000
  Name: CodeReviewer
  Purpose: Reviews code for best practices
  Enabled Skills: code-analysis, lint-check
  Created: 2026-02-28 14:30:00 UTC

  Agent ID: 6ba7b810-9dad-11d1-80b4-00c04fd430c8
  Name: DocumentGenerator
  Purpose: Generates comprehensive documentation
  Enabled Skills: document-generation, markdown-formatting
  Created: 2026-02-28 15:00:00 UTC
```

### Create New Agent
```bash
# Basic agent creation
.\run-cli.bat agent create --name "CodeReviewer" --purpose "Reviews code for best practices"

# Agent with enabled skills
.\run-cli.bat agent create \
  --name "CodeReviewer" \
  --purpose "Reviews code for best practices" \
  --skills "code-analysis" \
  --skills "lint-check" \
  --skills "security-scan"

# Short form
.\run-cli.bat agent create -n "DocGen" -p "Documentation generator" -s "doc-gen" -s "markdown"
```
Creates a new agent with the specified name, purpose, and optional enabled skills.

**Parameters:**
- `--name, -n`: Agent name (required)
- `--purpose, -p`: Agent purpose or description (required)
- `--skills, -s`: Enabled skill names (repeat for multiple skills, optional)

**Output Example:**
```
✓ Agent created successfully
  Agent ID: 550e8400-e29b-41d4-a716-446655440000
  Name: CodeReviewer
  Purpose: Reviews code for best practices
  Enabled Skills: code-analysis, lint-check, security-scan
  Created: 2026-02-28 14:30:00 UTC
```

### Get Agent Details
```bash
.\run-cli.bat agent get --id "550e8400-e29b-41d4-a716-446655440000"
```
Retrieves detailed information about a specific agent.

### Create or Reuse Dynamic Agent by Task Type
```bash
# Basic dynamic mapping (auto name/purpose/skills from orchestration defaults)
.\run-cli.bat agent create-for-task --task-type "search"

# Override generated name and purpose
.\run-cli.bat agent create-for-task \
  --task-type "document-analysis" \
  --name "DocAnalysisAgent" \
  --purpose "Analyzes and summarizes technical documents"

# Override skills for this dynamic mapping
.\run-cli.bat agent create-for-task \
  --task-type "web-fetch" \
  --skills "web-fetch" \
  --skills "content-extract"
```
Resolves an agent for a task type. If a matching dynamic agent already exists, it is reused; otherwise a new one is created.

**Parameters:**
- `--task-type, -t`: Task type to map to a dynamic agent (required)
- `--name, -n`: Optional explicit agent name override
- `--purpose, -p`: Optional explicit purpose override
- `--skills, -s`: Optional explicit skills (repeat for multiple)

**Output Example:**
```
✓ Dynamic task-type agent resolved successfully
  Task Type: search
  Agent ID: 9f6f5ac5-3982-4976-a12d-85fd13a0f47e
  Name: task-search-agent
  Purpose: Auto-generated agent for task type 'search'.
  Enabled Skills: (none)
  Created: 2026-02-28 23:44:02 UTC
```

**Output Example:**
```
AGENT DETAILS:
  Agent ID: 550e8400-e29b-41d4-a716-446655440000
  Name: CodeReviewer
  Purpose: Reviews code for best practices
  Enabled Skills: code-analysis, lint-check, security-scan
  Created: 2026-02-28 14:30:00 UTC
  Configuration:
    review_depth: thorough
    output_format: markdown
```

### Delete Agent
```bash
.\run-cli.bat agent delete --id "550e8400-e29b-41d4-a716-446655440000"
```
Deletes an agent, removing it from the system.

**Output Example:**
```
✓ Agent 550e8400-e29b-41d4-a716-446655440000 deleted successfully
```

### Execute Task with Agent
```bash
# Basic execution with defaults
.\run-cli.bat agent execute \
  --agent-id "550e8400-e29b-41d4-a716-446655440000" \
  --goal "Review the authentication module for security issues"

# Custom execution parameters
.\run-cli.bat agent execute \
  --agent-id "550e8400-e29b-41d4-a716-446655440000" \
  --goal "Generate comprehensive API documentation" \
  --max-iterations 15 \
  --timeout 900 \
  --token-budget 20000

# Short form
.\run-cli.bat agent execute \
  -a "550e8400-e29b-41d4-a716-446655440000" \
  -g "Analyze the codebase for performance bottlenecks" \
  -i 20 \
  -t 1800 \
  -b 50000
```
Executes a task using the specified agent with multi-step iteration and configurable limits.

**Parameters:**
- `--agent-id, -a`: Agent ID to use for execution (required)
- `--goal, -g`: Task goal or objective (required)
- `--max-iterations, -i`: Maximum number of iterations (default: 10)
- `--timeout, -t`: Execution timeout in seconds (default: 600)
- `--token-budget, -b`: Token budget for execution (default: 10,000)

**Execution Features:**
- Multi-step iteration with configurable max iterations
- Timeout enforcement to prevent runaway execution
- Token budget tracking to control costs
- Step-by-step observability
- Multiple termination conditions (Success, MaxIterations, Timeout, TokenBudgetExceeded, Error, Cancelled)

**Output Example:**
```
AGENT EXECUTION
===============
Agent ID: 550e8400-e29b-41d4-a716-446655440000
Goal: Review the authentication module for security issues
Max Iterations: 10
Timeout: 600s
Token Budget: 10000

Executing...

EXECUTION RESULT:
  Execution ID: 7c9e6679-7425-40de-944b-e07fc1f90ae7
  Status: ✓ Success
  Termination Reason: Success
  Iterations Executed: 5
  Tokens Consumed: 1250
  Duration: 2.34s

EXECUTION STEPS (5):
  Step 1: Planning
    Description: Iteration 1: Planning step for goal: Review the authentication module for security issues
    Status: ✓
    Tokens: 100
    Output: Step 1 output (placeholder)

  Step 2: Execution
    Description: Iteration 2: Execution step for goal: Review the authentication module for security issues
    Status: ✓
    Tokens: 100
    Output: Step 2 output (placeholder)

  Step 3: Execution
    Description: Iteration 3: Execution step for goal: Review the authentication module for security issues
    Status: ✓
    Tokens: 100
    Output: Step 3 output (placeholder)

  Step 4: Execution
    Description: Iteration 4: Execution step for goal: Review the authentication module for security issues
    Status: ✓
    Tokens: 100
    Output: Step 4 output (placeholder)

  Step 5: Completion
    Description: Iteration 5: Completion step for goal: Review the authentication module for security issues
    Status: ✓
    Tokens: 100
    Output: Step 5 output (placeholder)

FINAL OUTPUT:
Step 5 output (placeholder)
```

**Error Examples:**
```
# Agent not found
✗ Agent not found: 550e8400-e29b-41d4-a716-446655440000

# Max iterations reached
EXECUTION RESULT:
  Status: ✗ Failed
  Termination Reason: MaxIterations
  Error: Maximum iterations (10) reached without completion

# Token budget exceeded
EXECUTION RESULT:
  Status: ✗ Failed
  Termination Reason: TokenBudgetExceeded
  Error: Token budget exceeded: 10100/10000

# Timeout
EXECUTION RESULT:
  Status: ✗ Failed
  Termination Reason: Timeout
  Error: Execution timeout (600s) exceeded
```

**Integration Note:**
Current implementation uses placeholder execution logic. Future integration will include:
- Language model calls for reasoning and planning
- Skill execution based on agent's enabled skills
- Tool invocation via IToolInvoker
- Success criteria evaluation
- Self-correction learning from failures

Core iteration loop, termination logic, token tracking, and observability are fully operational.


## Settings Commands

### Show Current Settings
```bash
.\run-cli.bat settings show
```
Displays all current system settings and configuration.

**Output Example:**
```
CURRENT SETTINGS:

Directories:
  Data Directory: C:\Users\steve\AppData\Local\Daiv3\Data
  Models Directory: C:\Users\steve\AppData\Local\Daiv3\Models

Hardware Preferences:
  Use NPU: True (default)
  Use GPU: True (default)

Model Execution:
  Allow Online Providers: False (default)
  Token Budget: 8192 (default)

NOTE: Settings persistence integration pending.
```

---

## Learning Management Commands

### Create a Manual Learning
```bash
# Minimal: Create with required fields
.\run-cli.bat learning create --title "My Learning" --description "Description of what was learned"

# With all options
.\run-cli.bat learning create --title "Testing Best Practice" \
  --description "Always use arrange-act-assert pattern in unit tests" \
  --scope Project \
  --confidence 0.95 \
  --tags "testing,quality,best-practice" \
  --source-agent my-skill \
  --source-task task-123

# Short form with common options
.\run-cli.bat learning create -t "DI Pattern" -d "Constructor injection enables better testing" \
  -s Global -c 0.9 -g "architecture,di"
```

Creates a new learning manually (triggered with type "Explicit" and created by "user"). This allows users to capture patterns, techniques, and best practices that should be available to agents in future tasks.

**Input Options:**
- `--title, -t` (required): Short human-readable summary of the learning
- `--description, -d` (required): Full explanation of what was learned
- `--scope, -s`: Scope where this applies: Global, Agent, Skill, Project, Domain (default: Global)
- `--confidence, -c`: Confidence score 0.0-1.0 (default: 0.7)
- `--tags, -g`: Comma-separated tags for filtering (e.g., "csharp,file-io")
- `--source-agent, -a`: Agent identifier that should benefit from this learning
- `--source-task`: Task ID for provenance tracking

**Output Example:**
```
✓ Learning created successfully!

Learning ID: lm-abc12345-6789-1011-1213-141516171819
  Title: Testing Best Practice
  Scope: Project
  Status: Active
  Confidence: 0.950
  Trigger Type: Explicit
  Created: 2026-03-01 14:22:30 UTC
  Tags: testing,quality,best-practice
  Source Agent: my-skill

The learning can be injected into prompts for similar tasks. Use 'learning view --id lm-abc12345...' to view details.
```

**Use Cases:**
- Capture effective patterns and techniques you've discovered
- Document workarounds and edge cases found during development
- Share quality standards and best practices with agents
- Create domain-specific knowledge for repeated problem patterns
- Build project-specific guidelines agents should follow

**Confidence Levels:**
- `0.9-1.0`: High confidence - automatically inject into similar tasks
- `0.7-0.9`: Normal confidence - inject as reinforcement for related work
- `0.5-0.7`: Low confidence - inject as a suggested approach
- `<0.5`: Experimental - inject only when explicitly requested

---

### List Learnings
```bash
# List all learnings
.\run-cli.bat learning list

# Filter by status
.\run-cli.bat learning list --status Active
.\run-cli.bat learning list --status Suppressed
.\run-cli.bat learning list -s Archived

# Filter by scope
.\run-cli.bat learning list --scope Global
.\run-cli.bat learning list --scope Agent
.\run-cli.bat learning list -c Project

# Filter by source agent
.\run-cli.bat learning list --agent agent-123
.\run-cli.bat learning list -a agent-456

# Filter by minimum confidence
.\run-cli.bat learning list --min-confidence 0.8
.\run-cli.bat learning list -m 0.9

# Combine filters
.\run-cli.bat learning list --status Active --scope Global --min-confidence 0.85
```

Lists all learnings with optional filtering. Results are sorted by confidence and times applied.

**Output Example:**
```
LEARNINGS:
==========
Found 3 learning(s)

ID: abc12345-6789-1011-1213-141516171819
  Title: Use dependency injection for testability
  Scope: Global
  Status: Active
  Confidence: 0.950
  Trigger: UserFeedback
  Times Applied: 12
  Created: 2026-02-28 15:30:45 UTC
  Source Agent: agent-001

ID: def23456-7890-1112-1314-151617181920
  Title: Always close database connections
  Scope: Project
  Status: Active
  Confidence: 0.850
  Trigger: CompilationError
  Times Applied: 5
  Created: 2026-03-01 10:15:22 UTC
  Source Agent: agent-002

ID: ghi34567-8901-1213-1415-161718192021
  Title: Outdated learning pattern
  Scope: Global
  Status: Suppressed
  Confidence: 0.750
  Trigger: SelfCorrection
  Times Applied: 0
  Created: 2026-02-25 08:45:10 UTC
```

### View Learning Details
```bash
.\run-cli.bat learning view --id abc12345-6789-1011-1213-141516171819
```

Displays comprehensive details for a specific learning including metadata, provenance, and embedding status.

**Output Example:**
```
LEARNING DETAILS:
=================
ID: abc12345-6789-1011-1213-141516171819
Title: Use dependency injection for testability
Description: Always use constructor injection instead of service locator pattern.
Provides better testability, clearer dependencies, and easier refactoring.
Observed in task-789 where tight coupling caused maintenance issues.

METADATA:
  Trigger Type: UserFeedback
  Scope: Global
  Status: Active
  Confidence: 0.950
  Times Applied: 12
  Tags: architecture,di,best-practice

PROVENANCE:
  Source Agent: agent-001
  Source Task: task-789
  Created By: user
  Created At: 2026-02-28 15:30:45 UTC
  Updated At: 2026-02-28 15:30:45 UTC

EMBEDDING:
  Dimensions: 384
  Size: 1536 bytes
  Status: Ready for semantic search
```

### Edit Learning
```bash
# Edit title
.\run-cli.bat learning edit --id abc12345... --title "New title"

# Edit description
.\run-cli.bat learning edit --id abc12345... -d "Updated description"

# Edit confidence
.\run-cli.bat learning edit --id abc12345... --confidence 0.92
.\run-cli.bat learning edit --id abc12345... -c 0.88

# Edit tags
.\run-cli.bat learning edit --id abc12345... --tags "updated,refined,production"
.\run-cli.bat learning edit --id abc12345... -t "tag1,tag2"

# Change status
.\run-cli.bat learning edit --id abc12345... --status Suppressed
.\run-cli.bat learning edit --id abc12345... -s Active

# Change scope
.\run-cli.bat learning edit --id abc12345... --scope Project

# Edit multiple fields
.\run-cli.bat learning edit --id abc12345... --title "Better Title" --confidence 0.95 --status Active
```

Edit learning properties. Changes are saved immediately. At least one field must be specified.

**Valid Values:**
- **Status:** Active, Suppressed, Superseded, Archived
- **Scope:** Global, Project, Agent, Task, User
- **Confidence:** 0.0 to 1.0

**Output Example:**
```
CURRENT STATE:
  Title: Use dependency injection
  Description: Original description
  Confidence: 0.850
  Tags: architecture,di
  Status: Active
  Scope: Global

✓ Learning updated successfully

UPDATED STATE:
  Title: Use dependency injection for better testability
  Description: Original description
  Confidence: 0.950
  Tags: architecture,di,best-practice
  Status: Active
  Scope: Global
```

### Show Learning Statistics
```bash
.\run-cli.bat learning stats
```

Displays aggregate statistics about all learnings.

**Output Example:**
```
LEARNING STATISTICS:
===================
Total Learnings: 47

BY STATUS:
  Active: 38
  Suppressed: 6
  Archived: 3

BY SCOPE:
  Global: 25
  Project: 12
  Agent: 8
  Task: 2

BY TRIGGER TYPE:
  UserFeedback: 18
  CompilationError: 12
  SelfCorrection: 9
  ToolFailure: 5
  Explicit: 3

AVERAGES:
  Average Confidence: 0.847
  Average Times Applied: 3.2

MOST APPLIED (Top 5):
  [24x] Use async/await for I/O operations
  [18x] Validate input parameters
  [15x] Log errors with context
  [12x] Use dependency injection
  [10x] Close resources in finally blocks

EMBEDDING STATUS:
  With Embeddings: 45
  Without Embeddings: 2
```

### Suppress Learning
```bash
.\run-cli.bat learning suppress --id abc12345-6789-1011-1213-141516171819
```

Suppresses a learning to prevent it from being injected into agent prompts. The learning remains in the database but is no longer active.

**Output Example:**
```
SUPPRESSING LEARNING:
  ID: abc12345-6789-1011-1213-141516171819
  Title: Use dependency injection for testability
  Current Status: Active

✓ Learning suppressed successfully

This learning will no longer be injected into agent prompts.
To reactivate, use: learning edit --id abc12345... --status Active
```

**Use Cases:**
- Temporarily disable a learning that may not apply to current work
- Prevent injection of learnings that caused issues in specific contexts
- Deactivate outdated learnings without deleting them

### Promote Learning
```bash
.\run-cli.bat learning promote --id abc12345-6789-1011-1213-141516171819
```

Promotes a learning to the next broader scope level in the hierarchy: Skill → Agent → Project → Domain → Global.

**Output Example:**
```
PROMOTING LEARNING:
  ID: abc12345-6789-1011-1213-141516171819
  Title: Validate input parameters before processing
  Current Scope: Agent

✓ Learning promoted successfully
  New Scope: Project

Scope hierarchy: Task → Agent → Project → Domain → Global
```

**Output Example (Already at Global):**
```
PROMOTING LEARNING:
  ID: abc12345-6789-1011-1213-141516171819
  Title: Always use async/await for I/O operations
  Current Scope: Global

⚠ Learning is already at Global scope (highest level).

Scope hierarchy: Skill → Agent → Project → Domain → Global
```

**Scope Hierarchy:**
- **Skill:** Applies to a specific skill or capability
- **Agent:** Applies to all tasks executed by a specific agent
- **Project:** Applies to all agents working on a project
- **Domain:** Applies across multiple related projects
- **Global:** Applies universally to all agents and projects

**Use Cases:**
- Promote skill-specific insights that apply more broadly
- Share agent-learned best practices across project team
- Elevate project learnings to domain or organization-wide standards
- Build global knowledge base from proven local improvements

### Supersede Learning
```bash
.\run-cli.bat learning supersede --id abc12345-6789-1011-1213-141516171819
```

Marks a learning as superseded, indicating it has been replaced by a newer, more accurate learning. The learning is no longer injected into agent prompts.

**Output Example:**
```
SUPERSEDING LEARNING:
  ID: abc12345-6789-1011-1213-141516171819
  Title: Use callback pattern for async operations
  Current Status: Active

✓ Learning marked as superseded successfully

This learning has been replaced by a newer, more accurate learning.
It will no longer be injected into agent prompts.
```

**Use Cases:**
- Mark old approaches when better patterns are discovered
- Replace incomplete learnings with refined versions
- Maintain historical record while preventing outdated guidance
- Track evolution of best practices over time

**Requirements:** LM-REQ-008 (Complete)

**Notes:**
- **Suppress:** Temporarily disable without marking as outdated
- **Promote:** Expand scope to broader applicability
- **Supersede:** Permanently replace with better approach (keeps historical record)
- Learnings are created automatically by agents during learning triggers
- Embeddings are generated automatically during creation (cannot be edited directly)
- Changing status to Suppressed prevents learning injection into agent prompts
- Confidence and scope can be adjusted to refine learning applicability
- Tags help organize and search learnings

---

## Agent Promotion Proposals

Agent-proposed promotions allow agents to suggest learning promotions based on discovered patterns, with user confirmation required before execution.

### List Pending Proposals
```bash
# List all pending proposals
.\run-cli.bat agent-proposal list

# List proposals by status
.\run-cli.bat agent-proposal list --status Pending
.\run-cli.bat agent-proposal list --status Approved
.\run-cli.bat agent-proposal list -s Rejected
```

Lists all agent-proposed learning promotions. By default shows only Pending proposals awaiting user decision.

**Output Example:**
```
Pending Proposals (3):
┌────────────────┬──────────────────────┬────────────────┬────────────┐
│ Proposal ID    │ Agent                │ Target Scope   │ Confidence │
├────────────────┼──────────────────────┼────────────────┼────────────┤
│ PROP-001       │ agent-pattern-001    │ Project        │ 0.92       │
│ PROP-002       │ agent-advanced-002   │ Domain         │ 0.87       │
│ PROP-003       │ agent-pattern-001    │ Global         │ 0.81       │
└────────────────┴──────────────────────┴────────────────┴────────────┘

Use 'agent-proposal view --id <id>' to see full details.
Use 'agent-proposal approve --id <id>' to accept proposal.
Use 'agent-proposal reject --id <id>' to decline proposal.
```

### View Proposal Details
```bash
.\run-cli.bat agent-proposal view --id PROP-001
```

Displays comprehensive details for a specific proposal including learning context, justification, and timeline.

**Output Example:**
```
PROPOSAL DETAILS
================
Proposal ID: PROP-001
Status: Pending
Created: 2026-02-28T14:30:45Z

AGENT:
  Name: agent-pattern-001
  Type: Pattern Recognition Agent

LEARNING:
  ID: LRN-ABC123
  Title: API response caching optimization
  Current Scope: Skill
  Confidence: 0.85

PROMOTION DETAILS:
  Suggested Target Scope: Project
  Confidence Score: 0.92
  
JUSTIFICATION:
  Pattern observed in 15+ successful API optimizations across different
  endpoints. Consistent 30-40% latency improvement. High confidence in
  applicability to broader project scope based on empirical results.
  
SOURCE:
  Source Task ID: TASK-98765
  Created: 2026-02-28 14:30:45 UTC

DECISION REQUIRED:
  Use one of the following commands:
    agent-proposal approve --id PROP-001
    agent-proposal reject --id PROP-001 --reason "Needs review"
```

### Approve Proposal
```bash
# Basic approval
.\run-cli.bat agent-proposal approve --id PROP-001

# Approval gets recorded with timestamp and reviewer
```

Approves a proposal and executes the learning promotion. The promotion is recorded in the promotion history for audit trail.

**Output Example:**
```
APPROVING PROPOSAL
==================
Proposal ID: PROP-001
Learning: API response caching optimization
Promotion: Skill → Project

Processing approval...
✓ Promotion executed successfully

RESULTS:
  Learning Scope: Skill → Project
  Promoted By: user
  Promoted At: 2026-02-28 15:45:22 UTC
  Proposal Status: Approved
  Reviewed At: 2026-02-28 15:45:22 UTC

The learning is now available to all agents working on project tasks.
```

### Reject Proposal
```bash
# Basic rejection
.\run-cli.bat agent-proposal reject --id PROP-001

# Rejection with reason
.\run-cli.bat agent-proposal reject --id PROP-001 --reason "Needs domain expert review"
.\run-cli.bat agent-proposal reject --id PROP-002 -r "Insufficient data size"
```

Rejects a proposal and records the decision. Learning scope remains unchanged.

**Output Example:**
```
REJECTING PROPOSAL
==================
Proposal ID: PROP-001
Learning: API response caching optimization
Current Scope: Skill (unchanged)

Processing rejection...
✓ Proposal rejected successfully

RESULTS:
  Proposal Status: Rejected
  Rejection Reason: Needs domain expert review
  Reviewed By: user
  Reviewed At: 2026-02-28 15:45:22 UTC

Learning scope remains Skill. To manually promote, use:
  learning promote --id LRN-ABC123
```

### View Statistics
```bash
# Show proposal statistics
.\run-cli.bat agent-proposal stats
```

Displays aggregated statistics for all proposals including counts by status and per-agent breakdown.

**Output Example:**
```
PROPOSAL STATISTICS
===================
Total Proposals: 12

BY STATUS:
  Pending: 3
  Approved: 7
  Rejected: 2

AVERAGE CONFIDENCE (Pending): 0.87

BY AGENT:
  agent-pattern-001: 5 proposals (3 pending, 2 approved)
  agent-advanced-002: 4 proposals (0 pending, 4 approved)
  agent-learning-003: 3 proposals (0 pending, 1 approved, 2 rejected)

Latest Pending (ordered by creation date):
  [3 hours ago] PROP-001 - agent-pattern-001 (Confidence 0.92)
  [5 hours ago] PROP-002 - agent-advanced-002 (Confidence 0.87)
  [1 day ago]   PROP-003 - agent-pattern-001 (Confidence 0.81)
```

**Requirements:** KBP-REQ-003 (Agent-proposed learning promotions with user confirmation)

**Notes:**
- Proposals maintain full audit trail with timestamps and reviewer information
- Each approval is recorded as a promotion in the promotion history
- Rejections are tracked for analysis but don't affect learnings
- Confidence scores reflect agent's assessed likelihood of applicability
- Source task IDs enable tracing patterns back to original observations

---

## Promotion History Commands

Promotion history commands provide visibility into knowledge propagation across scope levels (Skill → Agent → Project → Domain → Global). Every promotion is recorded with full provenance tracking for transparency and audit trails.

### List Promotion History
```bash
# List recent promotions (default: 20 most recent)
.\run-cli.bat promotion-history list

# Specify limit
.\run-cli.bat promotion-history list --limit 50
.\run-cli.bat promotion-history list -l 10
```

Lists all promotions in reverse chronological order (most recent first). Shows promotion ID, learning ID, scope transitions, promoter identity, timestamps, and optional notes.

**Output Example:**
```
PROMOTION HISTORY (Most Recent 20):
===================================

PROMOTION #1:
  Promotion ID: prom-def45678-90ab-cdef-1234-567890abcdef
  Learning ID: lrn-abc12345-6789-4d4e-8c12-3456789012cd
  Scope Change: Agent → Project
  Promoted By: user
  Promoted At: 2026-03-02 10:15:42 UTC
  Source Task: task-202603-001
  Source Agent: agent-pattern-001
  Notes: Pattern consistently successful across multiple APIs

PROMOTION #2:
  Promotion ID: prom-ghi78901-23cd-ef45-6789-012345678901
  Learning ID: lrn-def67890-1234-5e5f-9d23-4567890123de
  Scope Change: Skill → Agent
  Promoted By: agent-advanced-002
  Promoted At: 2026-03-01 14:30:15 UTC
  Source Task: task-202602-045
  Source Agent: agent-advanced-002

PROMOTION #3:
  Promotion ID: prom-jkl01234-56ef-7890-1abc-345678901234
  Learning ID: lrn-ghi90123-4567-6f7g-0e34-5678901234ef
  Scope Change: Project → Domain
  Promoted By: user
  Promoted At: 2026-02-28 09:45:33 UTC
  Source Task: task-202602-012
  Source Agent: agent-learning-003
  Notes: Validated across 3 projects - ready for domain-wide use

(Showing 3 of 20)
Use --limit to adjust the number of promotions displayed.
```

### View Learning Promotion History
```bash
.\run-cli.bat promotion-history view <learning-id>
```

Displays the complete promotion history for a specific learning, showing its journey across scope levels over time.

**Example Command:**
```bash
.\run-cli.bat promotion-history view lrn-abc12345-6789-4d4e-8c12-3456789012cd
```

**Output Example:**
```
PROMOTION HISTORY FOR LEARNING
===============================
Learning ID: lrn-abc12345-6789-4d4e-8c12-3456789012cd
Title: API response caching optimization
Current Scope: Project
Confidence: 0.92

LEARNING METADATA:
  Status: Active
  Trigger: UserFeedback
  Times Applied: 8
  Created: 2026-02-15 12:00:00 UTC

PROMOTION PATH (2 promotions):
-------------------------------

PROMOTION #1:
  Promotion ID: prom-001
  Scope Change: Skill → Agent
  Promoted By: agent-pattern-001
  Promoted At: 2026-02-20 14:30:00 UTC
  Source Task: task-202602-030
  Source Agent: agent-pattern-001
  Notes: Pattern validated in 5+ API integrations

PROMOTION #2:
  Promotion ID: prom-def45678-90ab-cdef-1234-567890abcdef
  Scope Change: Agent → Project
  Promoted By: user
  Promoted At: 2026-03-02 10:15:42 UTC
  Source Task: task-202603-001
  Source Agent: agent-pattern-001
  Notes: Pattern consistently successful across multiple APIs

PROMOTION SUMMARY:
  Total Promotions: 2
  Scope Journey: Skill → Agent → Project
  Time Span: 10 days
  Unique Promoters: 2 (agent-pattern-001, user)
```

### View Promotions by Task
```bash
.\run-cli.bat promotion-history by-task <task-id>
```

Filters promotion history by source task ID, showing all knowledge captured and promoted during a specific task execution.

**Example Command:**
```bash
.\run-cli.bat promotion-history by-task task-202603-001
```

**Output Example:**
```
PROMOTIONS FROM TASK: task-202603-001
======================================

Found 3 promotion(s)

PROMOTION #1:
  Promotion ID: prom-def45678-90ab-cdef-1234-567890abcdef
  Learning ID: lrn-abc12345-6789-4d4e-8c12-3456789012cd
  Learning Title: API response caching optimization
  Scope Change: Agent → Project
  Promoted By: user
  Promoted At: 2026-03-02 10:15:42 UTC
  Source Agent: agent-pattern-001
  Notes: Pattern consistently successful across multiple APIs

PROMOTION #2:
  Promotion ID: prom-xyz98765-43ba-dcef-9876-543210fedcba
  Learning ID: lrn-hij12345-6789-4e4f-5c34-6789012345ab
  Learning Title: Error handling best practices
  Scope Change: Skill → Agent
  Promoted By: agent-pattern-001
  Promoted At: 2026-03-02 10:20:15 UTC
  Source Agent: agent-pattern-001

PROMOTION #3:
  Promotion ID: prom-aaa11111-22bb-33cc-4444-555566667777
  Learning ID: lrn-klm23456-7890-5f6g-6d45-7890123456bc
  Learning Title: Retry logic for transient failures
  Scope Change: Agent → Project
  Promoted By: user
  Promoted At: 2026-03-02 10:25:30 UTC
  Source Agent: agent-pattern-001
  Notes: Improved reliability by 30% in testing

TASK SUMMARY:
  Task ID: task-202603-001
  Total Promotions: 3
  Learnings Promoted: 3
  Unique Scopes: Skill → Agent, Agent → Project
  Promoters: user (2), agent-pattern-001 (1)
```

### View Promotions by Scope
```bash
.\run-cli.bat promotion-history by-scope <scope>
```

Filters promotions by target scope, showing all knowledge promoted to a specific level (Skill, Agent, Project, Domain, Global).

**Example Commands:**
```bash
.\run-cli.bat promotion-history by-scope Project
.\run-cli.bat promotion-history by-scope Domain
.\run-cli.bat promotion-history by-scope Global
```

**Output Example:**
```
PROMOTIONS TO SCOPE: Project
============================

Found 5 promotion(s)

PROMOTION #1:
  Promotion ID: prom-def45678-90ab-cdef-1234-567890abcdef
  Learning ID: lrn-abc12345-6789-4d4e-8c12-3456789012cd
  Learning Title: API response caching optimization
  From Scope: Agent
  Promoted By: user
  Promoted At: 2026-03-02 10:15:42 UTC
  Source Task: task-202603-001
  Source Agent: agent-pattern-001
  Notes: Pattern consistently successful across multiple APIs

PROMOTION #2:
  Promotion ID: prom-aaa11111-22bb-33cc-4444-555566667777
  Learning ID: lrn-klm23456-7890-5f6g-6d45-7890123456bc
  Learning Title: Retry logic for transient failures
  From Scope: Agent
  Promoted By: user
  Promoted At: 2026-03-02 10:25:30 UTC
  Source Task: task-202603-001
  Source Agent: agent-pattern-001
  Notes: Improved reliability by 30% in testing

PROMOTION #3:
  Promotion ID: prom-bbb22222-33cc-44dd-5555-666677778888
  Learning ID: lrn-nop34567-8901-6g7h-7e56-8901234567cd
  Learning Title: Database connection pooling
  From Scope: Agent
  Promoted By: user
  Promoted At: 2026-02-28 15:10:25 UTC
  Source Task: task-202602-089
  Source Agent: agent-database-001

[Additional promotions...]

SCOPE SUMMARY:
  Target Scope: Project
  Total Promotions: 5
  Source Scopes: Agent (4), Skill (1)
  Time Range: 2026-02-28 to 2026-03-02
  Primary Promoter: user (5)
```

### Show Promotion Statistics
```bash
.\run-cli.bat promotion-history stats
```

Displays aggregate statistics about all promotion activity including counts by scope, promoter analysis, time ranges, and recent activity.

**Output Example:**
```
PROMOTION STATISTICS
====================

OVERALL METRICS:
  Total Promotions: 47
  Unique Learnings Promoted: 38
  First Promotion: 2026-02-15 08:30:00 UTC
  Latest Promotion: 2026-03-02 10:25:30 UTC
  Time Span: 16 days

PROMOTIONS BY TARGET SCOPE:
  Skill → Agent: 18
  Agent → Project: 15
  Project → Domain: 9
  Domain → Global: 5

PROMOTIONS BY SOURCE SCOPE:
  Skill: 18
  Agent: 20
  Project: 7
  Domain: 2

PROMOTIONS BY PROMOTER:
  user: 32 (68%)
  agent-pattern-001: 8 (17%)
  agent-advanced-002: 5 (11%)
  agent-learning-003: 2 (4%)

RECENT ACTIVITY:
  Last 24 Hours: 5 promotions
  Last 7 Days: 23 promotions
  Last 30 Days: 47 promotions

TOP PROMOTED LEARNINGS (Most Frequently Promoted):
  [3x] API response caching optimization (lrn-abc12345...)
  [2x] Database connection pooling (lrn-nop34567...)
  [2x] Error handling best practices (lrn-hij12345...)
  [2x] Retry logic for transient failures (lrn-klm23456...)

KNOWLEDGE FLOW VELOCITY:
  Average Promotions per Day: 2.9
  Average Time Between Promotions: 8.2 hours
  Peak Activity Day: 2026-03-02 (7 promotions)
```

**Requirements:** KBP-ACC-002 (Promotion actions are recorded and visible in dashboard)

**Implementation Details:**
- All promotions recorded in `promotions` table (Migration 005, KBP-DATA-001)
- Promotion provenance includes: source_task_id, source_agent, promoted_by, timestamps
- Uses `PromotionRepository` with 6 indexed query methods for efficient retrieval
- CLI commands implemented in `src/Daiv3.App.Cli/Program.cs` (lines 853-4552)
- Acceptance tests in `tests/integration/Daiv3.Persistence.IntegrationTests/PromotionVisibilityAcceptanceTests.cs`

**Notes:**
- Promotion history is immutable (no updates, only inserts)
- CASCADE DELETE removes promotions when learning is deleted
- All queries use database indexes for optimal performance
- Full audit trail enables knowledge flow analysis
- Dashboard UI visibility planned (blocked by CT-REQ-003)

---

## Embedding Commands

### Test Embedding Generation
```bash
.\run-cli.bat embedding test
# With custom text
.\run-cli.bat embedding test --text "Your text here"
# Short form
.\run-cli.bat embedding test -t "Your text here"
```

Tests embedding generation with the ONNX embedding model. Generates a 768-dimensional normalized vector for the input text and displays statistics.

**Requirements:**
- **Two-tier embedding models** (automatically downloaded on first run):
  - **Tier 1 (Topic/Summary):** all-MiniLM-L6-v2 (~86 MB) - 384 dimensions
    - Path: `%LOCALAPPDATA%\Daiv3\models\embeddings\all-MiniLM-L6-v2\model.onnx`
  - **Tier 2 (Chunk):** nomic-embed-text-v1.5 (~522 MB) - 768 dimensions
    - Path: `%LOCALAPPDATA%\Daiv3\models\embeddings\nomic-embed-text-v1.5\model.onnx`
- Download progress is displayed in the console
- Requires internet connectivity for first-time initialization
- Both models use the same tokenizer (r50k_base encoding)

**Output Example (First Run - Two-Tier Model Download):**
```
Tier 1 embedding model (all-MiniLM-L6-v2) not found.
Downloading from Azure Blob Storage...

Tier 1 Progress: 5.0% (4.31 MB / 86.22 MB)
Tier 1 Progress: 10.0% (8.62 MB / 86.22 MB)
...
Tier 1 Progress: 95.0% (81.91 MB / 86.22 MB)
✓ Tier 1 model download completed successfully

Tier 2 embedding model (nomic-embed-text-v1.5) not found.
Downloading from Azure Blob Storage...

Tier 2 Progress: 5.0% (26.10 MB / 521.96 MB)
Tier 2 Progress: 10.0% (52.20 MB / 521.96 MB)
...
Tier 2 Progress: 95.0% (495.86 MB / 521.96 MB)
✓ Tier 2 model download completed successfully

EMBEDDING TEST
==============
Input text: The quick brown fox jumps over the lazy dog

Generating embedding... ✓ Success!

Embedding dimensions: 768
Vector magnitude: 1.0000
Value range: [-0.166835, 0.298630]

First 10 embedding values:
  [  0] = 0.058221
  [  1] = 0.016115
  [  2] = -0.166835
  [  3] = 0.037738
```

**Output Example (Subsequent Runs):**
```
EMBEDDING TEST
==============
Input text: The quick brown fox jumps over the lazy dog

Generating embedding... ✓ Success!
Value range: [-0.166835, 0.298630]

First 10 embedding values:
  [  0] = 0.058221
  [  1] = 0.016115
  [  2] = -0.166835
  [  3] = 0.037738
  [  4] = 0.011134
  [  5] = 0.041877
  [  6] = 0.006807
  [  7] = 0.028149
  [  8] = 0.022657
  [  9] = -0.005705
  ... (758 more values)
```

**Output Example (Model Not Found):**
```
EMBEDDING TEST
==============
Input text: The quick brown fox jumps over the lazy dog

Generating embedding... ✗ Failed to generate embedding: ONNX model file not found.
```

**Validation Points:**
- ✓ Embedding dimensions: 768 (normalized vector)
- ✓ Vector magnitude: ~1.0000 (normalized)
- ✓ Value range: Approximately [-1, 1]
- ✓ No NaN or Infinity values in output
- ✓ Model loaded using DirectML acceleration (or CPU fallback)

---

## Multimodal (CLIP) Commands

### Test CLIP Text Encoding
```bash
.\run-cli.bat multimodal text
# With custom text
.\run-cli.bat multimodal text --text "a person walking in the park"
# Short form
.\run-cli.bat multimodal text -t "a person walking in the park"
```

Tests CLIP multimodal text encoding. Generates a 512-dimensional embedding for image-text similarity matching.

**Requirements:**
- **CLIP Multimodal Models** (automatically downloaded on first run):
  - **Full Precision (NPU/GPU):** xenova/clip-vit-base-patch32
    - Text Encoder: `%LOCALAPPDATA%\Daiv3\models\multimodal\clip-vit-base-patch32\full-precision\model.onnx`
    - Vision Encoder: `%LOCALAPPDATA%\Daiv3\models\multimodal\clip-vit-base-patch32\full-precision\vision_model.onnx`
  - **Quantized (CPU):** uint8 quantized variants
    - Text Encoder (uint8): `%LOCALAPPDATA%\Daiv3\models\multimodal\clip-vit-base-patch32\quantized\model_uint8.onnx`
    - Vision Encoder (int8): `%LOCALAPPDATA%\Daiv3\models\multimodal\clip-vit-base-patch32\quantized\vision_model_int8.onnx`
- Hardware-aware variant selection (NPU/GPU → full precision, CPU → quantized)
- Enables image-text similarity scores and cross-modal retrieval

**Output Example:**
```
CLIP MULTIMODAL TEXT ENCODING TEST
==================================
Input text: a person walking in the park

Status: CLIP text encoder integration pending

Expected capabilities:
  • Text encoding into 512-dimensional vectors
  • Normalized L2 distance for similarity comparison
  • Image-text similarity matching for vision tasks

Model Information:
  • Model: xenova/clip-vit-base-patch32
  • Text Encoder Output Dims: 512
  • Vision Encoder Output Dims: 512
  • Hardware: NPU/GPU (full precision), CPU (quantized)

CLIP text encoding test completed (integration pending)
```

**Validation Points (When Implemented):**
- ✓ Text embeddings: 512 dimensions (L2 normalized)
- ✓ Image embeddings: 512 dimensions (L2 normalized)
- ✓ Similarity scores: Cosine distance between text and image embeddings
- ✓ Hardware variants properly selected based on detected hardware

---

## OCR Commands

### Test OCR Capabilities
```bash
.\run-cli.bat ocr test
```

Tests Optical Character Recognition (OCR) capabilities using TrOCR. Demonstrates document and handwriting text recognition.

**Requirements:**
- **TrOCR Models** (automatically downloaded on first run):
  - **Full Precision (NPU/GPU):** microsoft/trocr-base-printed
    - Encoder (FP16): `%LOCALAPPDATA%\Daiv3\models\ocr\trocr-base-printed\fp16\encoder_model.onnx`
    - Decoder (FP16): `%LOCALAPPDATA%\Daiv3\models\trocr-base-printed\fp16\decoder_model.onnx`
  - **Quantized (CPU):** int8 quantized variants
    - Encoder (int8): `%LOCALAPPDATA%\Daiv3\models\ocr\trocr-base-printed\quantized\encoder_model_int8.onnx`
    - Decoder (int8): `%LOCALAPPDATA%\Daiv3\models\ocr\trocr-base-printed\quantized\decoder_model_int8.onnx`
- Hardware-aware variant selection (NPU/GPU → FP16, CPU → int8)
- Document and handwriting text recognition

**Output Example:**
```
OCR (OPTICAL CHARACTER RECOGNITION) TEST
========================================

Status: TrOCR integration pending

Expected capabilities:
  • Document and handwriting text recognition
  • Support for multiple languages
  • Encoder-decoder architecture for accurate transcription

Model Information:
  • Base Model: microsoft/trocr-base-printed
  • Architecture: Vision Encoder (ViT) + Text Decoder (LSTM)
  • Input: Normalized image patches
  • Output: Text tokens (character sequences)

Hardware Variants:
  • NPU/GPU: FP16 precision for accelerated inference
  • CPU: Quantized (int8) for efficient CPU execution

Usage Example:
  ocr test
    Demonstrates OCR capabilities on sample images
```

**Validation Points (When Implemented):**
- ✓ Document text recognition: Accurate transcription of printed text
- ✓ Handwriting recognition: Support for handwritten documents
- ✓ Multi-language support: Handle various languages and scripts
- ✓ Hardware variants properly selected based on detected hardware
- ✓ Encoder-decoder pipeline coordinates vision understanding with text generation

---

## Test Verification (Canonical)

Use this command for full-suite verification and stable reporting:

```bash
dotnet test Daiv3.FoundryLocal.slnx --nologo --verbosity minimal --logger "console;verbosity=minimal"
```

**Important:** Do not pipe test output to `Select-String`, `grep`, or other filters when validating totals. Filtering can hide the final aggregate `Test summary` line and make counts look lower than they are.

For convenience on Windows, use:

```bash
.\run-tests.bat
```

---


### General Help
```bash
.\run-cli.bat --help
.\run-cli.bat -h
```
Shows all available commands and options.

### Command-Specific Help
.\run-cli.bat [command] --help
```

**Examples:**
```bash
.\run-cli.bat db --help
.\run-cli.bat chat --help
.\run-cli.bat projects --help
.\run-cli.bat settings --help
.\run-cli.bat embedding --help
.\run-cli.bat multimodal --help
.\run-cli.bat ocr --help
```

---

## Integration Status

| Feature Area | Commands | Status | Notes |
|--------------|----------|--------|-------|
| Database | `db init`, `db status` | ✅ Complete | Fully functional |
| Dashboard | `dashboard` | 🔄 Partial | Hardware detection pending |
| Chat | `chat`, `chat -m` | 🔄 Partial | Orchestration layer pending |
| Projects | `projects list`, `projects create` | ✅ Complete | Persistence-backed project listing/creation with explicit root path support (`--root-path`) |
| Settings | `settings show` | 🔄 Partial | Configuration service pending |

**Legend:**
- ✅ Complete - Fully implemented and integrated
- 🔄 Partial - Command works, integration pending
- ⏳ Planned - Not yet implemented

---

## Future Commands (Planned)

The following commands will be added as features are implemented:

### Projects (Additional)
- `projects delete --id <guid>` - Delete a project
- `projects show --id <guid>` - Show project details
- `projects tasks --id <guid>` - List tasks in a project

### Tasks
- `tasks list [--project-id <guid>]` - List all tasks
- `tasks create --name "Task" --project-id <guid>` - Create a task
- `tasks update --id <guid> --status <status>` - Update task status

### Knowledge Management
- `knowledge index --path <directory>` - Index documents
- `knowledge search --query "search terms"` - Search indexed content
- `knowledge status` - Show indexing status

### Model Management
- `models list` - List available models
- `models download --name <model>` - Download a model
- `models test --name <model>` - Test model inference

### Settings (Additional)
- `settings set --key <key> --value <value>` - Update a setting
- `settings reset` - Reset all settings to defaults
- `settings export --file <path>` - Export settings to file
- `settings import --file <path>` - Import settings from file

---

## Notes for Developers

### Adding New Commands
   - Parameter descriptions
   - Expected output
   - Integration status

### Command Naming Conventions

- Use lowercase for commands and subcommands
- Use `--long-form` and `-s` short forms for options
- Keep command names concise but descriptive
- Group related commands under a parent command (e.g., `db init`, `db status`)

### Output Formatting

- Use `✓` for success messages
- Use `✗` for error messages  
- Use `═` for section headers
- Include clear status messages for pending integrations
- Provide helpful next steps in output when appropriate

---

**Version**: 1.0  
**Requirement**: ARCH-REQ-002 (Presentation Layer)  
**Last Updated**: February 25, 2026
