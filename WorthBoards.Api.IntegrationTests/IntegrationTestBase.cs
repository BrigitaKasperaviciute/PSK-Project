using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Headers;
using WorthBoards.Data.Database;
using Xunit;

namespace WorthBoards.Api.IntegrationTests;

public abstract class IntegrationTestBase : IClassFixture<TestWebApplicationFactory>, IDisposable
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

        // Clear the database before each test to ensure isolation
        CleanDatabase();
    }

    private void CleanDatabase()
    {
        // Remove all entities from the database
        DbContext.Users.RemoveRange(DbContext.Users);
        DbContext.Boards.RemoveRange(DbContext.Boards);
        DbContext.BoardTasks.RemoveRange(DbContext.BoardTasks);
        DbContext.Comments.RemoveRange(DbContext.Comments);
        DbContext.Notifications.RemoveRange(DbContext.Notifications);
        DbContext.NotificationsOnUsers.RemoveRange(DbContext.NotificationsOnUsers);
        DbContext.BoardOnUsers.RemoveRange(DbContext.BoardOnUsers);
        DbContext.TasksOnUsers.RemoveRange(DbContext.TasksOnUsers);
        DbContext.SaveChanges();
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
