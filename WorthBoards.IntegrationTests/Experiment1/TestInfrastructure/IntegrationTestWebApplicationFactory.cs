using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WorthBoards.Business.Utils.EmailService.Interfaces;
using WorthBoards.Data.Database;

namespace WorthBoards.IntegrationTests.Experiment1.TestInfrastructure;

public class IntegrationTestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"worthboards-tests-{Guid.NewGuid():N}";

    public IntegrationTestWebApplicationFactory()
    {
        Environment.SetEnvironmentVariable("WBJwtKey", "integration_tests_jwt_key_1234567890123456");
        Environment.SetEnvironmentVariable("WBIssuer", "WorthBoards.IntegrationTests");
        Environment.SetEnvironmentVariable("WBAudience", "WorthBoards.IntegrationTests.Users");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("IntegrationTesting");

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            var configValues = new Dictionary<string, string?>
            {
                ["WBJwtKey"] = "integration_tests_jwt_key_1234567890123456",
                ["WBIssuer"] = "WorthBoards.IntegrationTests",
                ["WBAudience"] = "WorthBoards.IntegrationTests.Users",
                ["Logger:UseHttpLoggingMiddleware"] = "true",
                ["Logger:UseControllerLoggingActionFilter"] = "true",
                ["BaseUrl"] = "http://127.0.0.1:5051"
            };

            configBuilder.AddInMemoryCollection(configValues);
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll(typeof(DbContextOptions<ApplicationDbContext>));
            services.RemoveAll(typeof(ApplicationDbContext));

            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseInMemoryDatabase(_databaseName);
            });

            services.RemoveAll<IEmailService>();
            services.AddScoped<IEmailService, NoOpEmailService>();

            using var scope = services.BuildServiceProvider().CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            dbContext.Database.EnsureDeleted();
            dbContext.Database.EnsureCreated();
        });
    }

    public HttpClient CreateApiClient()
    {
        return CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
    }

    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();
    }
}
