using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using WorthBoards.Business.Services.Interfaces;
using WorthBoards.Business.Utils.EmailService.Interfaces;
using WorthBoards.Data.Database;

namespace WorthBoards.IntegrationTests.Infrastructure;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;

    static CustomWebApplicationFactory()
    {
        // Must be set before Program.cs reads them in ConfigureAuthentication.
        Environment.SetEnvironmentVariable("WBJwtKey", "IntegrationTestSuperSecretKey_32Chars!!");
        Environment.SetEnvironmentVariable("WBIssuer", "integration-test-issuer");
        Environment.SetEnvironmentVariable("WBAudience", "integration-test-audience");
    }

    public CustomWebApplicationFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // ── Replace PostgreSQL DbContext with SQLite ──────────────────────
            var contextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (contextDescriptor is not null)
                services.Remove(contextDescriptor);

            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseSqlite(_connection);
                options.ReplaceService<IModelCustomizer, SqliteCompatibleModelCustomizer>();
            });

            // ── Replace email service with no-op fake ─────────────────────────
            var emailDescriptors = services
                .Where(d => d.ServiceType == typeof(IEmailService))
                .ToList();
            foreach (var d in emailDescriptors)
                services.Remove(d);
            services.AddScoped<IEmailService, FakeEmailService>();

            // ── Replace file service with no-op fake ──────────────────────────
            var fileDescriptors = services
                .Where(d => d.ServiceType == typeof(IFileService))
                .ToList();
            foreach (var d in fileDescriptors)
                services.Remove(d);
            services.AddScoped<IFileService, FakeFileService>();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _connection.Dispose();
    }
}
