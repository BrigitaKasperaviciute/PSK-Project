using Xunit;

namespace WorthBoards.IntegrationTests.Infrastructure;

public abstract class IntegrationTestBase(IntegrationTestAppFactory factory) : IAsyncLifetime
{
    protected readonly IntegrationTestAppFactory Factory = factory;

    public Task InitializeAsync() => Factory.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;
}