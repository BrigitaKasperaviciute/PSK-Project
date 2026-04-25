using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using WorthBoards.Business.Utils.EmailService.Interfaces;
using WorthBoards.Data.Database;

namespace WorthBoards.IntegrationTests.Infrastructure;

public class WorthBoardsWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"TestDb_{Guid.NewGuid()}";

    public WorthBoardsWebApplicationFactory()
    {
        // WebApplicationBuilder reads config during construction (before ConfigureWebHost runs),
        // so JWT keys must be available via env vars to prevent AuthenticationConfiguration.TryGetConfigValue
        // from calling Environment.Exit(1).
        Environment.SetEnvironmentVariable("WBJwtKey", JwtTokenHelper.SecretKey);
        Environment.SetEnvironmentVariable("WBIssuer", "test-issuer");
        Environment.SetEnvironmentVariable("WBAudience", "test-audience");
        Environment.SetEnvironmentVariable("FrontendBaseUrl", "http://localhost:3000");
        Environment.SetEnvironmentVariable("BaseUrl", "http://localhost:5001");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Logger:UseHttpLoggingMiddleware"] = "false",
                ["Logger:UseControllerLoggingActionFilter"] = "false",
                ["Email:Provider"] = "Gmail",
                ["ConnectionStrings:WorthBoardsConnection"] = "Host=localhost;Database=test_placeholder;Username=test;Password=test",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Replace Npgsql DbContext with InMemory
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (descriptor != null) services.Remove(descriptor);

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase(_dbName));

            // Replace email service chain with no-op mock
            var emailDescriptors = services.Where(d => d.ServiceType == typeof(IEmailService)).ToList();
            foreach (var d in emailDescriptors) services.Remove(d);
            services.AddSingleton<IEmailService>(Mock.Of<IEmailService>());
        });
    }
}
