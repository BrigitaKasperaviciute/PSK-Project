using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Testcontainers.PostgreSql;
using WorthBoards.Business.Utils.EmailService.Interfaces;
using WorthBoards.Data.Database;
using Xunit;

namespace WorthBoards.IntegrationTests.Infrastructure;

public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private PostgreSqlContainer? _db;
    private bool _useTestcontainers;

    public Mock<IEmailService> EmailServiceMock { get; } = new(MockBehavior.Loose);

    static ApiFactory()
    {
        // Must be set via env vars: Program.cs reads these during service configuration
        // (before ConfigureAppConfiguration callbacks run) and calls Environment.Exit(1) if missing.
        Environment.SetEnvironmentVariable("WBJwtKey", JwtTokenHelper.TestJwtKey);
        Environment.SetEnvironmentVariable("WBIssuer", JwtTokenHelper.TestIssuer);
        Environment.SetEnvironmentVariable("WBAudience", JwtTokenHelper.TestAudience);
        Environment.SetEnvironmentVariable("GmailOptions__Host", "smtp.test.com");
        Environment.SetEnvironmentVariable("GmailOptions__Port", "587");
        Environment.SetEnvironmentVariable("GmailOptions__Name", "Test Sender");
        Environment.SetEnvironmentVariable("GmailOptions__Email", "test@test.com");
        Environment.SetEnvironmentVariable("GmailOptions__Password", "testpassword");
        Environment.SetEnvironmentVariable("FrontendBaseUrl", "http://localhost:3000");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("IntegrationTests");

        // Try to initialize Testcontainers; if Docker is unavailable, fall back to an
        // in-memory database so tests can run in environments without Docker.
        try
        {
            var disabled = Environment.GetEnvironmentVariable("TESTCONTAINERS_DISABLE");
            if (!string.Equals(disabled, "true", StringComparison.OrdinalIgnoreCase))
            {
                _db = new PostgreSqlBuilder()
                    .WithImage("postgres:16-alpine")
                    .WithDatabase("worthboards_tests")
                    .Build();
                _db.StartAsync().GetAwaiter().GetResult();
                _useTestcontainers = true;
            }
            else
            {
                _useTestcontainers = false;
                Console.WriteLine("Testcontainers disabled via TESTCONTAINERS_DISABLE env var; using in-memory DB.");
            }
        }
        catch (Exception ex)
        {
            _useTestcontainers = false;
            Console.WriteLine($"Testcontainers unavailable, falling back to in-memory DB. Reason: {ex.Message}");
        }

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();

            if (_useTestcontainers && _db is not null)
            {
                services.AddDbContext<ApplicationDbContext>(o =>
                    o.UseNpgsql(_db.GetConnectionString()));
            }
            else
            {
                services.AddDbContext<ApplicationDbContext>(o =>
                    o.UseInMemoryDatabase("WorthBoardsIntegrationTests"));
            }

            // Mock only third-party email integration.
            services.RemoveAll<IEmailService>();
            services.AddSingleton(EmailServiceMock.Object);
        });
    }

    public async Task InitializeAsync()
    {
        if (_useTestcontainers && _db is not null)
        {
            using var scope = Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.MigrateAsync();
        }
        else
        {
            using var scope = Services.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await ctx.Database.EnsureCreatedAsync();
        }
    }

    public new async Task DisposeAsync()
    {
        if (_useTestcontainers && _db is not null)
        {
            await _db.DisposeAsync();
        }

        await base.DisposeAsync();
    }

    public ApplicationDbContext CreateDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    }

    public HttpClient CreateAuthenticatedClient(int userId, string username = "testuser", string email = "test@test.com")
    {
        var client = CreateClient();
        var token = JwtTokenHelper.GenerateToken(userId, username, email);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
