# Architecture Decision: Integration Test Dependency Upgrades

## Context & Need
- Integration test project uses several pinned dependencies that are behind latest versions.
- We want to align with current .NET 10-era packages where appropriate and ensure toolchain stability.
- Upgrades must follow the project dependency governance process.

## Decision
- Proceed with a single coordinated upgrade of the integration test dependencies listed below, pending review of release notes, breaking changes, and security advisories.

## Available Options

### Option 1: Keep Current Versions
**Pros:**
- No compatibility risk
- No immediate testing burden

**Cons:**
- Misses bug fixes and improvements
- Gradually diverges from current toolchain

**Estimated Effort:** S (no change)

### Option 2: Upgrade Integration Test Dependencies (Selected)
**Packages and Targets:**
- coverlet.collector: 6.0.4 -> 8.0.0
- Microsoft.Extensions.Logging: 9.0.10 -> 10.0.3
- Microsoft.Extensions.Logging.Console: 9.0.10 -> 10.0.3
- Microsoft.NET.Test.Sdk: 17.14.1 -> 18.0.1
- OpenAI: 2.5.0 -> 2.8.0
- xunit.runner.visualstudio: 3.1.4 -> 3.1.5

**Pros:**
- Aligns with .NET 10 ecosystem
- Incorporates bug fixes and performance improvements
- Keeps tooling current

**Cons:**
- Potential breaking changes or behavior shifts
- Additional test validation required

**Estimated Effort:** M (version updates plus full test run)

## Comparison Matrix
| Criteria | Keep Current | Upgrade |
|----------|--------------|---------|
| Security Control | ✅ Stable | ✅ Improved (pending CVE review) |
| Maintenance | ⚠️ Gradual drift | ✅ Current |
| Feature Fit | ✅ Known | ✅ Improved |
| Learning Curve | ✅ None | ⚠️ Minor |
| Long-term Cost | ⚠️ Higher | ✅ Lower |

## Recommendation
- Upgrade all listed integration test dependencies together to minimize repeated validation cycles.

## Release Notes & Breaking Changes Summary
- coverlet.collector 8.0.0 (upstream release):
	- Improvements include multi-targeting of collector packages, .NET 8 target framework for core, and removal of Newtonsoft.Json.
	- Breaking change: minimum required .NET SDK/runtime upgraded to .NET 8.0 LTS.
	- Release notes: https://github.com/coverlet-coverage/coverlet/releases/tag/v8.0.0
- Microsoft.Extensions.Logging / Microsoft.Extensions.Logging.Console 10.0.3 (servicing release):
	- .NET 10.0.3 servicing update; release notes are aggregated in .NET release changelogs.
	- No specific breaking changes listed in the release announcement; review runtime/libraries changelog as needed.
	- Release notes hub: https://devblogs.microsoft.com/dotnet/dotnet-and-dotnet-framework-february-2026-servicing-updates/#release-changelogs
- Microsoft.NET.Test.Sdk 18.0.1 (vstest release):
	- Fixes covrun64.dll loading issue on systems with .NET 10 SDK; disables DynamicNative instrumentation by default.
	- Includes internal version/tooling updates.
	- Release notes: https://github.com/microsoft/vstest/releases/tag/v18.0.1
- OpenAI 2.8.0 (changelog):
	- Features: SafetyIdentifier and ConversationOptions in Responses; added response tool limits and log-prob options; setters added for ResponseItem Id/Status.
	- Breaking changes in preview APIs (Responses): renames (OpenAIResponseClient -> ResponsesClient, ResponseCreationOptions -> CreateResponseOptions, OpenAIResponse -> ResponseResult), input items now specified via InputItems, streaming requires StreamingEnabled = true, and model factory removal in favor of setters.
	- Changelog: https://github.com/openai/openai-dotnet/blob/OpenAI_2.8.0/CHANGELOG.md
- xunit.runner.visualstudio 3.1.5:
	- Unable to retrieve upstream changelog or release notes from the repo/tag via automated fetch; manual review needed.

**Action required before approval:** Review upstream release notes/changelogs for any additional breaking changes or security advisories not captured above, especially for xunit.runner.visualstudio.

## Implementation Notes
- Review release notes and breaking changes for each package before upgrading.
- Run `dotnet test FoundryLocal.IntegrationTests.slnx` after updates.
- Record results and any required code adjustments.

## Decision Date
2026-02-22

## Decision Maker
Project Maintainer

## Status
Accepted
