using Xunit;

namespace WorthBoards.IntegrationTests.Experiment1.TestInfrastructure;

[CollectionDefinition(Name)]
public class IntegrationTestCollection : ICollectionFixture<IntegrationTestWebApplicationFactory>
{
    public const string Name = "IntegrationTests";
}
