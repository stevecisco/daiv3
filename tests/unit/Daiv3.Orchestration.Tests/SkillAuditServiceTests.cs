using Daiv3.Orchestration.Services;
using Daiv3.Persistence.Entities;
using Daiv3.Persistence.Repositories;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Xunit;

namespace Daiv3.Orchestration.Tests;

public class SkillAuditServiceTests
{
    private readonly Mock<ISkillAuditRepository> _repositoryMock;
    private readonly Mock<ILogger<SkillAuditService>> _loggerMock;
    private readonly SkillAuditService _service;

    public SkillAuditServiceTests()
    {
        _repositoryMock = new Mock<ISkillAuditRepository>();
        _loggerMock = new Mock<ILogger<SkillAuditService>>();
        _service = new SkillAuditService(_repositoryMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task LogEventAsync_PersistsAuditEventWithMetadataJson()
    {
        // Arrange
        const string skillId = "skill-123";
        const string eventType = "Executed";
        const string actorId = "admin-1";
        var metadata = new Dictionary<string, string>
        {
            ["exitCode"] = "0",
            ["executionTimeMs"] = "42"
        };

        SkillAuditLog? captured = null;
        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<SkillAuditLog>(), It.IsAny<CancellationToken>()))
            .Callback<SkillAuditLog, CancellationToken>((log, _) => captured = log)
            .ReturnsAsync("audit-1");

        // Act
        await _service.LogEventAsync(skillId, eventType, actorId, metadata);

        // Assert
        _repositoryMock.Verify(r => r.AddAsync(It.IsAny<SkillAuditLog>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotNull(captured);
        Assert.Equal(skillId, captured!.SkillId);
        Assert.Equal(eventType, captured.EventType);
        Assert.Equal(actorId, captured.ActorId);
        Assert.True(captured.EventAt > 0);
        Assert.NotNull(captured.MetadataJson);

        var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(captured.MetadataJson!);
        Assert.NotNull(parsed);
        Assert.Equal("0", parsed!["exitCode"]);
    }

    [Fact]
    public async Task GetSkillAuditTrailAsync_ReturnsRepositoryResults()
    {
        // Arrange
        const string skillId = "skill-abc";
        var expected = new List<SkillAuditLog>
        {
            new() { AuditId = "a1", SkillId = skillId, EventType = "Created", ActorId = "user", EventAt = 1000 },
            new() { AuditId = "a2", SkillId = skillId, EventType = "Approved", ActorId = "admin", EventAt = 2000 }
        };

        _repositoryMock
            .Setup(r => r.GetBySkillIdAsync(skillId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _service.GetSkillAuditTrailAsync(skillId);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("a1", result[0].AuditId);
        Assert.Equal("a2", result[1].AuditId);
    }

    [Fact]
    public async Task QueryAuditEventsAsync_DelegatesFiltersToRepository()
    {
        // Arrange
        const string skillId = "skill-query";
        const string eventType = "ExecutionDenied";
        const long fromUnix = 100;
        const long toUnix = 200;

        var expected = new List<SkillAuditLog>
        {
            new() { AuditId = "q1", SkillId = skillId, EventType = eventType, ActorId = "user", EventAt = 150 }
        };

        _repositoryMock
            .Setup(r => r.QueryAsync(skillId, eventType, fromUnix, toUnix, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _service.QueryAuditEventsAsync(skillId, eventType, fromUnix, toUnix);

        // Assert
        Assert.Single(result);
        Assert.Equal("q1", result[0].AuditId);
    }
}
