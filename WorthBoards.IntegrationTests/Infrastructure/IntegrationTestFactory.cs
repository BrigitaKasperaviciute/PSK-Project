using System.Data.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WorthBoards.Data.Database;

namespace WorthBoards.IntegrationTests.Infrastructure;

public sealed class WorthBoardsApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public WorthBoardsApiFactory()
    {
        Environment.SetEnvironmentVariable("WBJwtKey", "WorthBoards.IntegrationTests.SecretKey.123!");
        Environment.SetEnvironmentVariable("WBIssuer", "WorthBoards.IntegrationTests");
        Environment.SetEnvironmentVariable("WBAudience", "WorthBoards.IntegrationTests");
        Environment.SetEnvironmentVariable("DATABASE_URL", "Host=localhost;Database=worthboards_integration_tests;Username=test;Password=test");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WBJwtKey"] = "WorthBoards.IntegrationTests.SecretKey.123!",
                ["WBIssuer"] = "WorthBoards.IntegrationTests",
                ["WBAudience"] = "WorthBoards.IntegrationTests",
                ["WorthBoardsConnection"] = "Host=localhost;Database=worthboards_integration_tests;Username=test;Password=test",
                ["FrontendBaseUrl"] = "http://localhost:3000",
                ["Logger:UseHttpLoggingMiddleware"] = "false",
                ["Logger:UseControllerLoggingActionFilter"] = "false",
                ["Email:Provider"] = "Gmail",
                ["Email:UseEmailLogging"] = "false",
                ["GmailOptions:Host"] = "localhost",
                ["GmailOptions:Port"] = "2525",
                ["GmailOptions:Email"] = "tests@worthboards.local",
                ["GmailOptions:Password"] = "tests",
                ["GmailOptions:Name"] = "WorthBoards Tests",
                ["Logger:UseHttpLoggingMiddleware"] = "true",
                ["Logger:UseControllerLoggingActionFilter"] = "true"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<ApplicationDbContext>();
            services.RemoveAll<DbConnection>();

            services.AddSingleton<DbConnection>(_connection);
            services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
            {
                var connection = serviceProvider.GetRequiredService<DbConnection>();
                options.UseSqlite((SqliteConnection)connection);
            });
            services.AddScoped<ApplicationDbContext>(serviceProvider =>
                new TestApplicationDbContext(serviceProvider.GetRequiredService<DbContextOptions<ApplicationDbContext>>()));
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        _connection.Open();
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.Database.EnsureCreated();

        return host;
    }

    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
    }
}