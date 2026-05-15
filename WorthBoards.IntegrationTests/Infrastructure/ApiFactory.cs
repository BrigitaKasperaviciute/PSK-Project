using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Testcontainers.PostgreSql;
using WorthBoards.Business.Services.Interfaces;
using WorthBoards.Business.Utils.EmailService.Interfaces;
using WorthBoards.Data.Database;
using Xunit;

namespace WorthBoards.IntegrationTests.Infrastructure;

/// <summary>
/// Boots the real API (full DI, middleware, routing, EF Core, JWT auth) against a
/// disposable PostgreSQL container. Only IEmailService (third-party SMTP) is mocked.
/// One container + one host is shared per test collection.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("worthboards_tests")
        .Build();

    internal const string JwtKey = "test-jwt-signing-key-that-is-at-least-32-characters!!";
    internal const string JwtIssuer = "WorthBoardsTest";
    internal const string JwtAudience = "WorthBoardsTest";

    public Mock<IEmailService> EmailServiceMock { get; } = new(MockBehavior.Loose);
    public Mock<IFileService> FileServiceMock { get; } = new(MockBehavior.Loose);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("IntegrationTests");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WBJwtKey"] = JwtKey,
                ["WBIssuer"] = JwtIssuer,
                ["WBAudience"] = JwtAudience,
                ["BaseUrl"] = "http://localhost:0",
                ["FrontendBaseUrl"] = "http://localhost:3000",
                ["ConnectionStrings:WorthBoardsConnection"] = _db.GetConnectionString(),
                ["Logger:UseHttpLoggingMiddleware"] = "true",
                ["Logger:UseControllerLoggingActionFilter"] = "true",
                ["Email:Provider"] = "Gmail",
                ["Email:UseEmailLogging"] = "false",
                ["GmailOptions:Host"] = "smtp.test.example.com",
                ["GmailOptions:Port"] = "587",
                ["GmailOptions:Name"] = "Test Sender",
                ["GmailOptions:Email"] = "no-reply@test.example.com",
                ["GmailOptions:Password"] = "test-password"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Replace production DbContext options with test-container connection.
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(o =>
                o.UseNpgsql(
                    _db.GetConnectionString(),
                    npgsql => npgsql.MigrationsAssembly("WorthBoards.Data")));

            // Mock third-party integrations (last registration wins).
            services.AddScoped<IEmailService>(_ => EmailServiceMock.Object);
            FileServiceMock.Setup(f => f.UploadImage(It.IsAny<Microsoft.AspNetCore.Http.IFormFile>()))
                .ReturnsAsync("test-image.jpg");
            services.AddScoped<IFileService>(_ => FileServiceMock.Object);
        });
    }

    public async Task InitializeAsync()
    {
        await _db.StartAsync();
        using var scope = Services.CreateScope();
        await scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>()
            .Database.EnsureCreatedAsync();
    }

    public new async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await base.DisposeAsync();
    }

    /// <summary>Creates a DbContext scoped to the test container (for DB-state assertions).</summary>
    public ApplicationDbContext CreateDbContext() =>
        Services.CreateScope().ServiceProvider.GetRequiredService<ApplicationDbContext>();

    /// <summary>Creates a service scope for accessing DI services (e.g. UserManager).</summary>
    public IServiceScope CreateServiceScope() => Services.CreateScope();

    /// <summary>Creates an HttpClient pre-configured with a valid JWT for the given user.</summary>
    public HttpClient CreateAuthenticatedClient(int userId, string username, string email)
    {
        var token = JwtTokenHelper.GenerateToken(userId, username, email);
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
