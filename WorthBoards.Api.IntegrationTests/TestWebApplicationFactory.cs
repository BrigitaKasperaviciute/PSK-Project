using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WorthBoards.Data.Database;

namespace WorthBoards.Api.IntegrationTests;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = Guid.NewGuid().ToString();

    protected override IHost CreateHost(IHostBuilder builder)
    {
        // Set environment variable before host is built
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");

        builder.ConfigureHostConfiguration(config =>
        {
            // Add test configuration at the host level (earliest possible)
            var testConfig = new Dictionary<string, string?>
            {
                ["WBJwtKey"] = "TestKeyForIntegrationTestsWithMinimum32Characters",
                ["WBIssuer"] = "https://auth.WorthBoards.com",
                ["WBAudience"] = "https://business.com",
                ["ConnectionStrings:WorthBoardsConnection"] = "Host=localhost;Database=WorthBoardsTest",
                ["Logger:UseHttpLoggingMiddleware"] = "false",
                ["Logger:UseControllerLoggingActionFilter"] = "false",
                ["Email:Provider"] = "Gmail",
                ["Email:UseEmailLogging"] = "false",
                ["FrontendBaseUrl"] = "http://localhost:3000",
                ["BaseUrl"] = "http://localhost:5000",
                ["GmailOptions:Host"] = "smtp.gmail.com",
                ["GmailOptions:Port"] = "587",
                ["GmailOptions:Name"] = "WorthBoards",
                ["GmailOptions:Email"] = "test@test.com",
                ["GmailOptions:Password"] = "testpassword"
            };

            config.AddInMemoryCollection(testConfig);
        });

        return base.CreateHost(builder);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            // Remove existing authentication and replace with test authentication
            services.Configure<AuthenticationOptions>(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.AuthenticationScheme;
                options.DefaultChallengeScheme = TestAuthHandler.AuthenticationScheme;
                options.DefaultScheme = TestAuthHandler.AuthenticationScheme;
            });

            // Add test authentication handler
            services.AddAuthentication(TestAuthHandler.AuthenticationScheme)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.AuthenticationScheme, options => { });

            // Remove the existing DbContext registration
            services.RemoveAll(typeof(DbContextOptions<ApplicationDbContext>));
            services.RemoveAll(typeof(ApplicationDbContext));

            // Add in-memory database for testing with unique name per factory instance
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseInMemoryDatabase(_databaseName);
            });

            // Build service provider and seed the database
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var scopedServices = scope.ServiceProvider;
            var db = scopedServices.GetRequiredService<ApplicationDbContext>();

            // Ensure the database is created
            db.Database.EnsureCreated();

            // Seed test data
            SeedTestData(db);
        });

        builder.UseEnvironment("Testing");
    }

    private void SeedTestData(ApplicationDbContext context)
    {
        // Clear existing data
        context.Database.EnsureDeleted();
        context.Database.EnsureCreated();

        // This method will be used to seed data if needed
        // For now, tests will create their own data as needed
    }
}
