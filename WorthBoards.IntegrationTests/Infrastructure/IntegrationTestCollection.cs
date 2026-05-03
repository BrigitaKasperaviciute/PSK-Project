using Xunit;

namespace WorthBoards.IntegrationTests.Infrastructure;

[CollectionDefinition(CollectionName, DisableParallelization = true)]
public sealed class IntegrationTestCollection : ICollectionFixture<WorthBoardsApiFactory>
{
    public const string CollectionName = "WorthBoards integration tests";
}