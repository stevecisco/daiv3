# HW-REQ-001

Source Spec: 2. Target Hardware & Runtime Environment - Requirements

## Requirement
The system SHALL support Windows 11 Copilot+ devices with NPUs.

## Status
Superseded by HW-CON-001. This requirement is fully covered by the runtime targeting and hardware tier definition in HW-CON-001; no separate implementation or testing work remains.

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
- Hardware tier detection tests (17 unit tests passing)
- NPU device detection validation
- Windows 11 Copilot+ platform verification
- Integration testing on Snapdragon X Elite hardware

## Usage and Operational Notes
- N/A (covered by HW-CON-001)

## Dependencies
- KLC-REQ-001
- KLC-REQ-003

## Related Requirements
- HW-CON-001
