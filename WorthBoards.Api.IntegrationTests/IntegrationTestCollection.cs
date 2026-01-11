using Xunit;

namespace WorthBoards.Api.IntegrationTests;

/// <summary>
/// Collection fixture to share TestWebApplicationFactory across all test classes.
/// This improves performance by creating the test server only once.
/// </summary>
[CollectionDefinition("Integration Tests")]
public class IntegrationTestCollection : ICollectionFixture<TestWebApplicationFactory>
{
    // This class is never instantiated. It's just used to define the collection.
}
