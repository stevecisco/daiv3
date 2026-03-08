using System.Text.Json;
using Daiv3.Persistence.Entities;
using Daiv3.Persistence.Repositories;
using Microsoft.Extensions.Logging;

namespace Daiv3.Orchestration.Services;

/// <summary>
/// Persists and queries skill lifecycle audit events.
/// Implements ES-ACC-002 Phase 4: Skill creator integration + audit trail.
/// </summary>
public class SkillAuditService : ISkillAuditService
{
    private readonly ISkillAuditRepository _repository;
    private readonly ILogger<SkillAuditService> _logger;

    public SkillAuditService(
        ISkillAuditRepository repository,
        ILogger<SkillAuditService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task LogEventAsync(
        string skillId,
        string eventType,
        string actorId,
        IDictionary<string, string>? metadata = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skillId);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);

        var auditLog = new SkillAuditLog
        {
            AuditId = Guid.NewGuid().ToString(),
            SkillId = skillId,
            EventType = eventType,
            ActorId = actorId,
            MetadataJson = metadata == null || metadata.Count == 0
                ? null
                : JsonSerializer.Serialize(metadata),
            EventAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        await _repository.AddAsync(auditLog, ct).ConfigureAwait(false);

        _logger.LogInformation(
            "Logged skill audit event {EventType} for skill {SkillId} by actor {ActorId}",
            eventType,
            skillId,
            actorId);
    }

    public Task<IReadOnlyList<SkillAuditLog>> GetSkillAuditTrailAsync(string skillId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skillId);
        return _repository.GetBySkillIdAsync(skillId, ct);
    }

    public Task<IReadOnlyList<SkillAuditLog>> QueryAuditEventsAsync(
        string? skillId = null,
        string? eventType = null,
        long? fromUnixSeconds = null,
        long? toUnixSeconds = null,
        CancellationToken ct = default)
    {
        return _repository.QueryAsync(skillId, eventType, fromUnixSeconds, toUnixSeconds, ct);
    }
}
