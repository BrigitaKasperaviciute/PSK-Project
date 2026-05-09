using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WorthBoards.Api;
using WorthBoards.Data.Database;
using WorthBoards.Data.Identity;
using Xunit;

namespace WorthBoards.IntegrationTests.Infrastructure;

/// <summary>
/// WebApplicationFactory for integration tests. Boots the real API against a PostgreSQL testcontainer.
/// Only third-party dependencies are mocked. Provides methods to create test clients and access the DbContext.
/// Lifetime: one container per test class collection.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=file:worthboards-tests?mode=memory&cache=shared");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("IntegrationTests");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Logger:UseHttpLoggingMiddleware"] = "true",
                ["Logger:UseControllerLoggingActionFilter"] = "true"
            });
        });
        builder.ConfigureTestServices(services =>
        {
            // Remove production database configuration.
            services.RemoveAll<ApplicationDbContext>();
            services.RemoveAll<IntegrationTestDbContext>();
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<DbContextOptions<IntegrationTestDbContext>>();

            // Use a shared in-memory SQLite database for fast, isolated integration tests.
            services.AddDbContext<IntegrationTestDbContext>(options =>
                options.UseSqlite(_connection));
            services.AddScoped<ApplicationDbContext>(sp => sp.GetRequiredService<IntegrationTestDbContext>());

            // Replace external email delivery with a no-op test implementation.
            services.RemoveAll<WorthBoards.Business.Utils.EmailService.Interfaces.IEmailService>();
            services.AddScoped<WorthBoards.Business.Utils.EmailService.Interfaces.IEmailService, NoOpEmailService>();
        });
    }

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreatedAsync();
    }

    public new async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        await base.DisposeAsync();
    }

    /// <summary>
    /// Creates a database context for test assertions and setup.
    /// </summary>
    public ApplicationDbContext CreateDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    }

    /// <summary>
    /// Gets a UserManager instance from the DI container with proper dependencies.
    /// </summary>
    public UserManager<ApplicationUser> GetUserManager(ApplicationDbContext dbContext)
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    }

    /// <summary>
    /// Creates an HTTP client with Bearer token authentication for the specified user role.
    /// </summary>
    public HttpClient CreateClientForUser(int userId, string userRole)
    {
        var client = CreateClient();
        var token = TestJwtTokenGenerator.GenerateToken(userId, userRole);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>
    /// Resets all database tables to ensure test isolation.
    /// </summary>
    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();
    }
}

[CollectionDefinition(nameof(ApiCollection))]
public sealed class ApiCollection : ICollectionFixture<ApiFactory> { }
