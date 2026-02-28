# MQ-REQ-010

Source Spec: 5. Model Execution & Queue Management - Requirements

## Requirement
The system SHALL assign a queue priority based on task type and context.

## Implementation Plan
### ✅ COMPLETE

**Component:** `Daiv3.ModelExecution`

**Interfaces:**
- `IPriorityAssigner` - Priority assignment service interface

**Models:**
- `PriorityContext` - Context information for priority decisions
- `ExecutionPriority` enum - Already defined (Immediate, Normal, Background)

**Implementation:**
- `PriorityAssigner` - Context-aware priority assignment with elevation rules
- `PriorityAssignerOptions` - Configuration with task-to-priority mappings

**Features:**
- Task type to priority mapping
- Context-based elevation (user-facing, interactive, retry)
- Priority override support
- Configurable elevation rules

**Default Mappings:**
- **Immediate**: Chat, QuestionAnswer (interactive tasks)
- **Normal**: Code, Rewrite, Translation, Generation
- **Background**: Search, Summarize, Analysis, Extraction

**DI Registration:** `ModelExecutionServiceExtensions.AddModelExecutionServices()`

## Testing Plan
### ✅ COMPLETE - 33/33 Tests Passing

**Unit Tests:** `PriorityAssignerTests.cs`
- Default task type mappings (7 test cases)
- Priority override
- User-facing elevation
- Interactive elevation
- Retry elevation
- Custom mappings
- Configuration toggling
- Context combinations

**Test Coverage:**
- All priority levels validated
- Elevation logic tested
- Configuration options verified
- Edge cases confirmed

## Usage and Operational Notes

**Configuration (appsettings.json):**
```json
{
  "PriorityAssigner": {
    "TaskTypePriorityMappings": {
      "Chat": "Immediate",
      "Search": "Background"
    },
    "UserFacingAlwaysImmediate": false,
    "ElevateInteractivePriority": true,
    "ElevateRetryPriority": true,
    "DefaultPriority": "Normal"
  }
}
```

**Usage:**
```csharp
var assigner = serviceProvider.GetRequiredService<IPriorityAssigner>();
var context = new PriorityContext 
{ 
    IsUserFacing = true,
    IsInteractive = true,
    IsRetry = false,
    PriorityOverride = ExecutionPriority.Immediate // optional
};
var priority = assigner.AssignPriority(TaskType.Search, context);
```

**Elevation Rules:**
- **UserFacingAlwaysImmediate**: Elevates all user-facing requests to Immediate
- **ElevateInteractivePriority**: Interactive requests get at least Normal priority
- **ElevateRetryPriority**: Retries elevate one level (Background→Normal→Immediate)
- **PriorityOverride**: Explicit override takes precedence over all rules

**Context Properties:**
- `IsUserFacing`: Request originates from user (vs background process)
- `IsInteractive`: Requires immediate response
- `IsRetry`: Failed request being retried
- `SessionId`, `ProjectId`, `UserId`: Additional context
- `EstimatedProcessingTimeMs`: Optional performance hint

**Operational Notes:**
- Priority assignment is synchronous and fast
- Multiple elevation rules can apply (most restrictive wins)
- Logged for queue observability
- Integrates with ModelQueue for execution scheduling

## Dependencies
- KLC-REQ-005
- KLC-REQ-006

## Related Requirements
- None
