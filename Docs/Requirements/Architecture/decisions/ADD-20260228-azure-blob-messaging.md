# Architecture Decision: Azure Blob Storage for Distributed Agent Messaging

**Date:** 2026-02-28  
**Status:** Proposed  
**Decision Maker:** Copilot (pending review)  
**Related Requirement:** [AST-REQ-004](../../Reqs/AST-REQ-004.md) - Phase 2: Cloud-Scale Message Broker Backend

---

## Context & Need

### What is needed?
Azure Blob Storage backend for the message broker to support distributed agent communication across multiple machines in cloud environments. Phase 1 implemented FileSystem backend for local, single-machine scenarios. Phase 2 extends this to cloud-distributed scenarios.

### Why is it needed?
1. **Distributed Deployments:** Multi-machine agent systems need cross-machine message sharing
2. **Scalability:** Azure Blob Storage scales to millions of messages without local disk limits
3. **Cloud-First Architecture:** Azure integration aligns with enterprise deployment patterns
4. **Unifies Storage:** Same blob container can hold both messages and knowledge documents

### Use Cases
1. Multi-agent systems deployed across Azure VMs or Container Instances
2. Hybrid cloud scenarios (local + cloud agents communicating)
3. Shared knowledge + messaging across distributed teams
4. Enterprise agent networks requiring audit trails (blob storage versioning)

### Requirements Reference
- [AST-REQ-004](../../Reqs/AST-REQ-004.md) - Agent Message Bus with Storage Backends
- Phase 2 implementation plan (lines 138-152 in AST-REQ-004.md)
- Configuration section in AST-REQ-004.md specifies AzureBlobOptions

---

## Decision

**Chosen Approach:** Azure.Storage.Blobs (Microsoft NuGet Package)

**Rationale Summary:**
Azure.Storage.Blobs is Microsoft's official, pre-approved SDK for Azure Blob Storage. It's production-grade, actively maintained with frequent security patches, and integrates seamlessly with .NET 10 and dependency injection patterns already used in Daiv3.

---

## Available Options

### Option 1: Custom Azure Blob Implementation

**Description:**
Implement blob storage operations using Azure SDK's lower-level REST API directly or through custom HTTP client.

**Pros:**
- Full control over storage interactions
- Minimal external dependencies
- Could optimize for specific message patterns
- Easier to mock for testing

**Cons:**
- Significant development effort (URI encoding, SAS tokens, authentication, retry logic)
- Higher maintenance burden for edge cases
- Missing Azure SDK improvements and security patches
- Risk of subtle bugs in authentication/encryption handling
- No automatic SDK updates with platform changes

**Estimated Effort:**
- **Complexity:** XL (Complex)
- **Time Estimate:** 4-6 weeks for production-quality implementation with full features
- **Key Implementation Challenges:**
  - Azure authentication (connection strings, MSI, SAS tokens)
  - Blob URI formatting and path conventions
  - Retry logic with exponential backoff
  - Pagination for large result sets
  - Metadata handling and blob properties

**Security Considerations:**
- Risk of authentication bugs or token handling issues
- No automatic updates from Microsoft security advisories
- Requires careful handling of connection strings in configuration

---

### Option 2: Azure.Storage.Blobs (Official Azure SDK) ✅ **RECOMMENDED**

**Package Information:**
- **NuGet Package:** [Azure.Storage.Blobs](https://www.nuget.org/packages/Azure.Storage.Blobs)
- **Current Version:** 12.20.0 (as of 2026-02-28)
- **Last Updated:** Regular monthly releases
- **License:** MIT
- **Repository:** [Azure SDK for .NET - Blob Storage](https://github.com/Azure/azure-sdk-for-net)

**Popularity & Community:**
- **GitHub Stars:** ~4,000+ (Azure SDK repo)
- **NuGet Downloads:** 50M+ cumulative downloads
- **Active Development:** Yes - weekly updates
- **Issue Response Time:** Fast (Azure team responds within days)
- **Contributors:** 100+ Microsoft team members + community

**Maintainer & Affiliation:**
- **Maintainer:** Microsoft Azure Team
- **Microsoft Affiliation:** Official Microsoft product
- **Commercial Backing:** Microsoft Azure - production support available
- **SLA Support:** Professional support available through Azure support plans

**Pros:**
- **Official Microsoft Support:** Built and maintained by Azure team
- **Security:** Automatic security patch delivery through NuGet
- **Production-Grade:** Used by Microsoft enterprise customers
- **Full Feature Set:** Blob creation, deletion, listing, metadata, snapshots, SAS tokens
- **Authentication Options:** Easy integration with Azure.Identity for managed identities, service principals, connection strings
- **Async/Await Native:** Designed for modern async .NET patterns
- **Error Handling:** Rich exception types and automatic retry policies
- **Pre-Approved Category:** Microsoft Azure packages are pre-approved in approved-dependencies.md
- **Dependency Injection Ready:** No special configuration needed for DI integration
- **Performance:** Optimized by Microsoft for Azure Blob Storage API
- **Testability:** Mock-friendly design, works well with Moq/NSubstitute

**Cons:**
- **External Dependency:** Requires NuGet authentication (mitigated: already trusted by project)
- **API Surface:** Much larger than minimal implementation (manageable - we use subset)
- **Network Dependency:** Requires Azure connectivity (expected for cloud scenarios)
- **Breaking Changes:** Major version updates may require code changes (rare, Microsoft minimizes these)
- **Licensing Restrictions:** MIT license requires license attribution (standard practice)

**Estimated Effort:**
- **Complexity:** M (Medium)
- **Time Estimate:** 1-2 weeks for full implementation with tests
  - 2 days: AzureBlobMessageStore class implementation (~200 LOC)
  - 2 days: Polling watcher for message detection
  - 1 day: DI registration and configuration
  - 3 days: 40+ integration tests (real Azure Blob Storage or emulator)
  - 2 days: Documentation

**Maintenance & Support:**
- **Ongoing Effort:** L (Low) - SDK updates handled by Microsoft
- **Team Expertise Required:** Basic Azure Blob Storage API knowledge
- **Update Frequency:** Monthly minor releases, quarterly major reviews

**Integration Points:**
- Works seamlessly with existing DI container in MessagingServiceExtensions.cs
- Compatible with IOptions<AzureBlobMessageStoreOptions> already defined
- Integrates with existing retry/error handling patterns
- Pairs cleanly with Azure.Identity for managed identities

**Security Considerations:**
- **Advantages:**
  - Automatic TLS 1.2+ enforcement
  - SAS token support for scoped access
  - Azure.Identity integration enables managed identities (no connection string in code)
  - Regular security audits by Microsoft
- **Implementation Notes:**
  - Use managed identities in production (connection strings only for dev/test)
  - Enable blob versioning for audit trails
  - Consider blob encryption settings
  - Use SAS tokens with expiration for temporary access

**Deployment Considerations:**
- **Dev/Test:** Azure Storage Emulator or azurite containers
- **Production:** Azure Blob Storage Standard or Premium tier
- **Cost:** ~$0.018 per GB-month for storage, $0.004 per 10,000 read operations
- **Scalability:** No artificial limits; scales to millions of objects seamlessly

---

## Implementation Plan (Phase 2)

### 1. AzureBlobMessageStore Implementation (Task 2)
```csharp
public class AzureBlobMessageStore : IMessageStore
{
    // Uses Azure.Storage.Blobs.BlobContainerClient
    // Pattern: messages/<topic>/<messageId>.json
    // Metadata: Message status tracked in blob properties
}
```

### 2. Polling Watcher for Blob Changes (Task 6)
- Poll blob container every N seconds (configurable, default 1s)
- Track last-seen modification time to avoid reprocessing
- Efficient filtering using blob metadata

### 3. DI Registration (Task 3)
- Update MessagingServiceExtensions to detect StorageBackend setting
- Register BlobContainerClient from DI
- Handle both connection string and managed identity auth

### 4. Configuration Structure
- Connection string or managed identity URL
- Container name
- Polling interval
- Retry policy

---

## Approval Requirements

**Before Implementation:**
- ✅ This ADD must be reviewed and accepted
- ✅ Team confirmation that Azure Blob Storage is acceptable for AST-REQ-004 Phase 2
- ✅ Confirmation of Azure subscription/credentials for testing

**After Implementation:**
- Unit tests must pass (40+ tests)
- Integration tests with Azure Blob Storage or emulator
- Security review of authentication/SAS token handling
- Documentation of configuration options

---

## Decision Timeline

- **Proposed:** 2026-02-28
- **Review Period:** [TBD - pending approval]
- **Implementation Start:** [Upon approval]
- **Expected Completion:** [2026-03-14 estimated]

---

## References

- [Azure Blob Storage Documentation](https://learn.microsoft.com/en-us/azure/storage/blobs/)
- [Azure SDK for .NET - Blob Storage](https://github.com/Azure/azure-sdk-for-net/tree/main/sdk/storage/Azure.Storage.Blobs)
- [AST-REQ-004 Full Specification](../../Reqs/AST-REQ-004.md)
- [approved-dependencies.md](../approved-dependencies.md) - Pre-approved categories

---

## Sign-Off

**Decision:** [Pending Review]  
**Approved By:** [To be filled upon review]  
**Date:** [To be filled upon approval]

Comments/Notes:
- This decision aligns with the project's use of Microsoft products (ONNX, Tokenizers, SQL Server/SQLite, DirectML)
- Azure.Storage.Blobs is currently used by millions of .NET applications in production
- Alternative custom implementation was evaluated but rejected due to high maintenance burden and security risk
