using Xunit;

namespace WorthBoards.IntegrationTests.Infrastructure;

[CollectionDefinition(nameof(IntegrationCollection))]
public sealed class IntegrationCollection : ICollectionFixture<IntegrationTestAppFactory>
{
}