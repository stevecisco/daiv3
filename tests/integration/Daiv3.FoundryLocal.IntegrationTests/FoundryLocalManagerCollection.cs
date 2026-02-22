using Xunit;

namespace Daiv3.FoundryLocal.IntegrationTests;

[CollectionDefinition("FoundryLocalManager collection", DisableParallelization = true)]
public sealed class FoundryLocalManagerCollection : ICollectionFixture<FoundryLocalManagerFixture>
{
}
