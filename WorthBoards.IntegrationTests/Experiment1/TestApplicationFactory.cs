using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WorthBoards.Business.Utils.EmailService.Interfaces;
using WorthBoards.Data.Database;

namespace WorthBoards.IntegrationTests.Experiment1;

public sealed class TestApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"WorthBoards_IntegrationTests_{Guid.NewGuid():N}";

    public TestApplicationFactory()
    {
        Environment.SetEnvironmentVariable("WBJwtKey", "ThisIsATestSigningKeyWithAtLeast32Chars!");
        Environment.SetEnvironmentVariable("WBIssuer", "worthboards-tests");
        Environment.SetEnvironmentVariable("WBAudience", "worthboards-tests-client");
        Environment.SetEnvironmentVariable("DATABASE_URL", "Host=localhost;Database=worthboards_tests;Username=test;Password=test");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            var testSettings = new Dictionary<string, string?>
            {
                ["WBJwtKey"] = "ThisIsATestSigningKeyWithAtLeast32Chars!",
                ["WBIssuer"] = "worthboards-tests",
                ["WBAudience"] = "worthboards-tests-client",
                ["BaseUrl"] = "http://localhost:5001",
                ["ConnectionStrings:WorthBoardsConnection"] = "Host=localhost;Database=worthboards_tests;Username=test;Password=test",
                ["GmailOptions:Host"] = "localhost",
                ["GmailOptions:Port"] = "2525",
                ["GmailOptions:Name"] = "WorthBoards Tests",
                ["GmailOptions:Email"] = "test@worthboards.local",
                ["GmailOptions:Password"] = "password"
            };

            configBuilder.AddInMemoryCollection(testSettings);
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll(typeof(DbContextOptions<ApplicationDbContext>));
            services.RemoveAll(typeof(ApplicationDbContext));
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseInMemoryDatabase(_databaseName);
            });

            services.RemoveAll(typeof(IEmailService));
            services.AddScoped<IEmailService, FakeEmailService>();

            using var serviceProvider = services.BuildServiceProvider();
            using var scope = serviceProvider.CreateScope();

            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            dbContext.Database.EnsureDeleted();
            dbContext.Database.EnsureCreated();
        });
    }
}
