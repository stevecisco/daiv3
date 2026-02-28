# MQ-REQ-014 Implementation Documentation

## Summary

**Requirement:** The system SHALL require user confirmation based on configurable rules (always, above X tokens, or auto within budget).

**Status:** ✅ COMPLETE  
**Progress:** 100%

---

## Implementation

### Owning Component
- `OnlineProviderRouter` in `src/Daiv3.ModelExecution/OnlineProviderRouter.cs`

### Core Behavior Added

#### 1. Confirmation Mode Enum
Created `ConfirmationMode` enum with four modes:
- **Always**: Always require user confirmation for online requests
- **AboveThreshold**: Require confirmation only when estimated tokens exceed configured threshold
- **AutoWithinBudget**: Automatically approve requests within budget; require confirmation when exceeding budget
- **Never**: Never require confirmation (auto-approve all requests)

#### 2. Configuration Enhancement
Updated `OnlineProviderOptions`:
- Replaced `bool RequireUserConfirmation` with `ConfirmationMode ConfirmationMode`
- Retained `int ConfirmationThreshold` property (used by AboveThreshold mode)
- Default mode: `ConfirmationMode.AboveThreshold` with 1000 token threshold

#### 3. Confirmation Details Model
Created `ConfirmationDetails` class with:
- `ProviderName`: Selected provider for execution
- `EstimatedInputTokens`: Estimated request token count
- `EstimatedOutputTokens`: Estimated response token count
- `TotalEstimatedTokens`: Combined estimate
- `CurrentDailyInputTokens`: Current daily usage
- `DailyInputLimit`: Daily budget limit
- `RemainingDailyBudget`: Remaining budget (calculated property)
- `ExceedsBudget`: Whether request would exceed budget (calculated property)
- `ConfirmationReason`: Human-readable explanation

#### 4. Execution Status Enhancement
Added `AwaitingConfirmation` to `ExecutionStatus` enum:
```csharp
public enum ExecutionStatus
{
    Pending,              // Offline queueing
    AwaitingConfirmation, // NEW: Waiting for user confirmation
    Queued,
    Processing,
    Completed,
    Failed,
    Cancelled
}
```

#### 5. Enhanced OnlineProviderRouter API
Added three new interface methods to `IOnlineProviderRouter`:

**RequiresConfirmation()**
- Checks if confirmation is required based on configuration
- Returns `bool` indicating confirmation requirement
- Decision logic:
  - `Always`: Returns true
  - `Never`: Returns false
  - `AboveThreshold`: Returns true if estimated tokens > threshold
  - `AutoWithinBudget`: Returns true if estimated tokens exceed remaining budget

**GetConfirmationDetails()**
- Provides detailed information for user confirmation prompt
- Returns `ConfirmationDetails` with:
  - Estimated token usage
  - Current budget status
  - Human-readable reason for confirmation

**ExecuteWithConfirmationAsync()**
- Executes request with confirmation already granted
- Bypasses confirmation check
- Still validates network connectivity and hard budget limits
- Used after user explicitly approves via UI

#### 6. Updated ExecuteAsync() Logic
Enhanced execution flow in `OnlineProviderRouter.ExecuteAsync()`:
1. Check network connectivity (MQ-REQ-013)
2. **NEW**: Check if confirmation required via `RequiresConfirmation()`
3. If confirmation needed: Return `ExecutionResult` with `AwaitingConfirmation` status
4. Otherwise: Proceed with budget check and execution

### Confirmation Flow Example

```csharp
// 1. Client checks if confirmation needed
var router = serviceProvider.GetRequiredService<IOnlineProviderRouter>();
if (router.RequiresConfirmation(request))
{
    // 2. Get details for UI display
    var details = router.GetConfirmationDetails(request);
    
    // 3. Show UI prompt: "Allow online request to {details.ProviderName}?"
    //    "Estimated tokens: {details.TotalEstimatedTokens}"
    //    "Remaining budget: {details.RemainingDailyBudget}"
    //    "Reason: {details.ConfirmationReason}"
    
    // 4a. User approves: Execute with confirmation
    if (userApproved)
    {
        var result = await router.ExecuteWithConfirmationAsync(request);
    }
    
    // 4b. User denies: Cancel request
    else
    {
        // Handle cancellation
    }
}
else
{
    // Auto-approved, execute directly
    var result = await router.ExecuteAsync(request);
}
```

### Compliance Mapping
- ✅ **Always mode**: Mimics original `RequireUserConfirmation = true` behavior
- ✅ **AboveThreshold mode**: Implements token-based gating (default 1000 tokens)
- ✅ **AutoWithinBudget mode**: Implements budget-aware auto-approval
- ✅ **Never mode**: Implements unrestricted execution
- ✅ Returns `ExecutionStatus.AwaitingConfirmation` when confirmation needed
- ✅ Provides detailed confirmation information for UI integration

---

## Testing

### Unit Tests Created

#### OnlineProviderRouterConfirmationTests.cs (16 tests)

**RequiresConfirmation Tests (6 tests)**
- `RequiresConfirmation_Always_ReturnsTrue`: Validates Always mode always requires confirmation
- `RequiresConfirmation_Never_ReturnsFalse`: Validates Never mode never requires confirmation
- `RequiresConfirmation_AboveThreshold_BelowLimit_ReturnsFalse`: Short request auto-approved
- `RequiresConfirmation_AboveThreshold_ExceedsLimit_ReturnsTrue`: Large request requires confirmation
- `RequiresConfirmation_AutoWithinBudget_WithinBudget_ReturnsFalse`: Within budget auto-approves
- `RequiresConfirmation_AutoWithinBudget_ExceedsBudget_ReturnsTrue`: Exceeding budget requires confirmation

**GetConfirmationDetails Tests (4 tests)**
- `GetConfirmationDetails_ReturnsCorrectEstimates`: Validates token estimation and budget calculations
- `GetConfirmationDetails_Always_CorrectReason`: Verifies Always mode reason text
- `GetConfirmationDetails_AboveThreshold_CorrectReason`: Verifies threshold explanation with token counts
- `GetConfirmationDetails_AutoWithinBudget_ExceedsBudget_CorrectReason`: Verifies budget explanation

**ExecuteAsync Integration Tests (4 tests)**
- `ExecuteAsync_Always_ReturnsAwaitingConfirmation`: Always mode blocks execution
- `ExecuteAsync_Never_Executes`: Never mode proceeds immediately
- `ExecuteAsync_AboveThreshold_BelowLimit_Executes`: Below threshold auto-executes
- `ExecuteAsync_AboveThreshold_ExceedsLimit_ReturnsAwaitingConfirmation`: Above threshold blocks

**ExecuteWithConfirmationAsync Tests (2 tests)**
- `ExecuteWithConfirmationAsync_BypassesConfirmation_Executes`: Confirmation bypass works
- `ExecuteWithConfirmationAsync_Offline_StillQueues`: Offline detection still applies

### Validation Result
- ✅ All 16 tests passed (RequiresConfirmation, GetConfirmationDetails, ExecuteAsync, ExecuteWithConfirmationAsync)
- ✅ Tests run across both net10.0 and net10.0-windows10.0.26100 frameworks
- ✅ Total test runs: 32 (16 tests × 2 frameworks)
- ✅ **Test summary: total: 32, failed: 0, succeeded: 32, skipped: 0, duration: 2.0s**

---

## Design Decisions

### 1. Enum-Based Configuration
**Rationale**: Replaced boolean `RequireUserConfirmation` with `ConfirmationMode` enum for:
- Clear intent (Always/AboveThreshold/AutoWithinBudget/Never)
- Future extensibility (can add modes like "AskOnFirstUse" without breaking changes)
- Better developer experience (explicit options vs. boolean + threshold interpretation)

### 2. Separate Confirmation API
**Rationale**: Exposed `RequiresConfirmation()` and `GetConfirmationDetails()` separately from execution:
- Allows UI to check before execution (prevents unnecessary async calls)
- Enables pre-fetching confirmation details for better UX
- Supports "preview mode" where users see estimates before committing

### 3. ExecuteWithConfirmationAsync() Method
**Rationale**: Separate execution path for confirmed requests:
- Avoids re-checking confirmation after user approves (cleaner flow)
- Still validates critical constraints (network, hard budget limits)
- Makes user approval explicit in code (audit trail)

### 4. AwaitingConfirmation Status
**Rationale**: Added dedicated status vs. overloading Pending:
- Semantic clarity (Pending = offline queue, AwaitingConfirmation = user decision)
- Allows different UI treatment (offline message vs. confirmation dialog)
- Future-proofs for retry logic (offline retry ≠ confirmation retry)

### 5. Token Estimation
**Rationale**: Uses simple `text.Length / 4` estimation:
- Fast calculation (no tokenizer overhead)
- Conservative (slightly overestimates)
- Sufficient for confirmation gates (exact count not critical)
- Future enhancement: Use `Microsoft.ML.Tokenizers` for accurate counts

### 6. Default Mode
**Rationale**: Default to `AboveThreshold` (1000 tokens):
- Balances UX (small requests auto-approve) with safety (large requests gated)
- 1000 tokens ≈ 750 words (reasonable conversation threshold)
- Matches original `RequireUserConfirmation = true` + threshold behavior

---

## Configuration Examples

### Always Require Confirmation
```json
{
  "OnlineProviderOptions": {
    "ConfirmationMode": "Always"
  }
}
```

### Token-Based Confirmation (500 token threshold)
```json
{
  "OnlineProviderOptions": {
    "ConfirmationMode": "AboveThreshold",
    "ConfirmationThreshold": 500
  }
}
```

### Budget-Aware Auto-Approval
```json
{
  "OnlineProviderOptions": {
    "ConfirmationMode": "AutoWithinBudget"
  }
}
```

### Never Require Confirmation (Unrestricted)
```json
{
  "OnlineProviderOptions": {
    "ConfirmationMode": "Never"
  }
}
```

---

## Related Requirements
- **MQ-REQ-013**: Offline queueing (confirmation still applies when online)
- **MQ-REQ-012**: Online provider routing (confirmation integrates with provider selection)

---

## Notes

- Confirmation is checked **after** network connectivity but **before** budget validation
- `ExecuteWithConfirmationAsync()` still validates hard budget limits (no confirmation bypasses limits)
- Confirmation details use estimated tokens (not actual - actual counts known only after execution)
- UI integration responsibility: Orchestration or Presentation Layer (not Model Execution Layer)
- Future enhancement: Add user preference storage (remember user's confirmation choices)
- Future enhancement: Add cost estimates in ConfirmationDetails (requires provider pricing data)
