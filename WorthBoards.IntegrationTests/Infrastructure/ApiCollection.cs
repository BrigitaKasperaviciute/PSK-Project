using Xunit;

namespace WorthBoards.IntegrationTests.Infrastructure;

[CollectionDefinition(nameof(ApiCollection))]
public sealed class ApiCollection : ICollectionFixture<ApiFactory> { }
