# HW-REQ-002

Source Spec: 2. Target Hardware & Runtime Environment - Requirements

## Requirement
The system SHALL support ARM64 (Snapdragon X Elite) and x64 (Intel Core Ultra NPU) architectures.

## Status
Superseded by HW-CON-001. The optimized tier explicitly targets Snapdragon X and Intel Core Ultra, and the multi-targeting pattern includes win-arm64 and win-x64; no separate implementation or testing work remains.

## Implementation Plan
- N/A (covered by HW-CON-001)

## Testing Plan
- N/A (covered by HW-CON-001)

## Testing Summary

**Status:** Superseded by HW-CON-001

**Test Project:** [tests/unit/Daiv3.UnitTests/Daiv3.UnitTests.csproj](tests/unit/Daiv3.UnitTests/Daiv3.UnitTests.csproj)
**Test File:** [tests/unit/Daiv3.UnitTests/Infrastructure/Shared/Hardware/HardwareDetectionProviderTests.cs](tests/unit/Daiv3.UnitTests/Infrastructure/Shared/Hardware/HardwareDetectionProviderTests.cs)
**Test Methods:** See [HW-CON-001 Testing Summary](HW-CON-001.md#testing-summary)

**Test Coverage:** See [HW-CON-001 Testing Summary](HW-CON-001.md#testing-summary) for:
- Multi-architecture build validation (win-arm64, win-x64)
- Runtime identifier configuration tests
- Hardware detection on Snapdragon X Elite (ARM64) ✅ Tested
- Hardware detection on Intel Core Ultra (x64) ⏸️ Pending hardware access
- TFM multi-targeting pattern validation

## Usage and Operational Notes
- N/A (covered by HW-CON-001)

## Dependencies
- KLC-REQ-001
- KLC-REQ-003

## Related Requirements
- HW-CON-001
