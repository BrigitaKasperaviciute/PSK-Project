using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WorthBoards.Business.Utils.EmailService;
using WorthBoards.Business.Utils.EmailService.Interfaces;
using WorthBoards.Business.Utils.EmailService.Services;
using WorthBoards.Data.Database;

namespace WorthBoards.IntegrationTests.Infrastructure;

public class WorthBoardsWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;

    public WorthBoardsWebApplicationFactory(SqliteConnection connection)
    {
        _connection = connection;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WBJwtKey"] = "your-secret-key-must-be-at-least-32-characters-long!!!",
                ["WBIssuer"] = "WorthBoards",
                ["WBAudience"] = "WorthBoards",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Remove the app's DbContext registration
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // Add test DbContext
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseSqlite(_connection);
            });

            // Replace external email delivery with a deterministic no-op for integration tests.
            var emailDescriptors = services.Where(d =>
                    d.ServiceType == typeof(IEmailService) ||
                    d.ServiceType == typeof(EmailContextService) ||
                    d.ServiceType == typeof(GmailServiceStrategy) ||
                    d.ServiceType == typeof(OutlookServiceStrategy))
                .ToList();

            foreach (var emailDescriptor in emailDescriptors)
            {
                services.Remove(emailDescriptor);
            }

            services.AddSingleton<IEmailService, NoOpEmailService>();
        });

        builder.UseEnvironment("Testing");
    }

    private sealed class NoOpEmailService : IEmailService
    {
        public Task SendEmailAsync(SendEmailRequest sendEmailRequest, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
