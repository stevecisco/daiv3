using Daiv3.Persistence;
using Daiv3.Persistence.Repositories;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Daiv3.Persistence.Tests;

public class TaskRepositoryValidationTests
{
    [Fact]
    public void Constructor_WithNullDatabaseContext_ThrowsArgumentNullException()
    {
        var logger = new Mock<ILogger<TaskRepository>>().Object;

        Assert.Throws<ArgumentNullException>(() => new TaskRepository(null!, logger));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        var databaseContext = new Mock<IDatabaseContext>().Object;

        Assert.Throws<ArgumentNullException>(() => new TaskRepository(databaseContext, null!));
    }
}
