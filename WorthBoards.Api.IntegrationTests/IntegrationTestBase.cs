using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Headers;
using WorthBoards.Data.Database;
using Xunit;

namespace WorthBoards.Api.IntegrationTests;

[Collection("Integration Tests")]
public abstract class IntegrationTestBase : IDisposable, IAsyncLifetime
{
    protected readonly TestWebApplicationFactory Factory;
    protected readonly HttpClient Client;
    protected readonly ApplicationDbContext DbContext;

    protected IntegrationTestBase(TestWebApplicationFactory factory)
    {
        Factory = factory;
        Client = CreateAuthenticatedClient();

        // Get a scoped DbContext for test operations
        var scope = Factory.Services.CreateScope();
        DbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    }

    public async Task InitializeAsync()
    {
        // Clear the database before each test to ensure isolation
        await CleanDatabaseAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task CleanDatabaseAsync()
    {
        // Ensure database is created
        await DbContext.Database.EnsureCreatedAsync();

        // Delete in order to respect foreign key constraints
        DbContext.TasksOnUsers.RemoveRange(DbContext.TasksOnUsers);
        DbContext.BoardOnUsers.RemoveRange(DbContext.BoardOnUsers);
        DbContext.NotificationsOnUsers.RemoveRange(DbContext.NotificationsOnUsers);
        DbContext.Comments.RemoveRange(DbContext.Comments);
        DbContext.BoardTasks.RemoveRange(DbContext.BoardTasks);
        DbContext.Boards.RemoveRange(DbContext.Boards);
        DbContext.Notifications.RemoveRange(DbContext.Notifications);
        DbContext.Users.RemoveRange(DbContext.Users);

        await DbContext.SaveChangesAsync();
    }

    protected HttpClient CreateAuthenticatedClient(string userId = "1", string email = "test@example.com")
    {
        var client = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(TestAuthHandler.AuthenticationScheme, $"{userId}:{email}");

        return client;
    }

    protected HttpClient CreateUnauthenticatedClient()
    {
        return Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    public void Dispose()
    {
        Client?.Dispose();
        GC.SuppressFinalize(this);
    }
}
