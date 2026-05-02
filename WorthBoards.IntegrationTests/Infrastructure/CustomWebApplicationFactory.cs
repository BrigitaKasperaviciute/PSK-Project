using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WorthBoards.Business.Services.Interfaces;
using WorthBoards.Business.Utils.EmailService.Interfaces;
using WorthBoards.Data.Database;

namespace WorthBoards.IntegrationTests.Infrastructure;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;

    public const string JwtKey = "SuperSecretTestKeyForIntegrationTests!1";
    public const string JwtIssuer = "TestIssuer";
    public const string JwtAudience = "TestAudience";

    public CustomWebApplicationFactory()
    {
        // Program.cs reads WBJwtKey/WBIssuer/WBAudience via builder.Configuration before
        // Build() runs, so AddInMemoryCollection (which fires during Build()) arrives too late.
        // Setting environment variables here guarantees they are present when the host starts
        // (WebApplicationFactory builds the host lazily, on first CreateClient() call).
        Environment.SetEnvironmentVariable("WBJwtKey", JwtKey);
        Environment.SetEnvironmentVariable("WBIssuer", JwtIssuer);
        Environment.SetEnvironmentVariable("WBAudience", JwtAudience);

        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FrontendBaseUrl"] = "http://localhost:3000",
                ["Email:Provider"] = "Gmail",
                ["Email:UseEmailLogging"] = "false",
                ["Logger:UseControllerLoggingActionFilter"] = "true",
                ["Logger:UseHttpLoggingMiddleware"] = "true",
                // Dummy Gmail options so GmailServiceStrategy does not throw on resolve
                ["GmailOptions:Host"] = "smtp.test.com",
                ["GmailOptions:Port"] = "587",
                ["GmailOptions:Name"] = "Test",
                ["GmailOptions:Email"] = "test@test.com",
                ["GmailOptions:Password"] = "test-password",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Replace PostgreSQL with an in-memory SQLite connection
            var dbContextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (dbContextDescriptor != null)
                services.Remove(dbContextDescriptor);

            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseSqlite(_connection);
                options.ReplaceService<IModelCustomizer, SqliteCompatibleModelCustomizer>();
            });

            // Replace third-party email service with a no-op fake
            var emailDescriptors = services
                .Where(d => d.ServiceType == typeof(IEmailService))
                .ToList();
            foreach (var d in emailDescriptors)
                services.Remove(d);
            services.AddScoped<IEmailService, FakeEmailService>();

            // Replace file service (avoids touching the filesystem)
            var fileDescriptors = services
                .Where(d => d.ServiceType == typeof(IFileService))
                .ToList();
            foreach (var d in fileDescriptors)
                services.Remove(d);
            services.AddScoped<IFileService, FakeFileService>();

            // Ensure the schema is created
            using var scope = services.BuildServiceProvider().CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Database.EnsureCreated();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
            Environment.SetEnvironmentVariable("WBJwtKey", null);
            Environment.SetEnvironmentVariable("WBIssuer", null);
            Environment.SetEnvironmentVariable("WBAudience", null);
        }
    }
}
