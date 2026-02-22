using Xunit;

namespace FoundryLocal.IntegrationTests;

[CollectionDefinition("FoundryLocalManager collection", DisableParallelization = true)]
public sealed class FoundryLocalManagerCollection : ICollectionFixture<FoundryLocalManagerFixture>
{
}
