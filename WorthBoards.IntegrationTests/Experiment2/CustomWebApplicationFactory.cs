using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WorthBoards.Business.Utils.EmailService.Interfaces;
using WorthBoards.Data.Database;

namespace WorthBoards.IntegrationTests.Experiment2;

public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"WorthBoardsExperiment2-{Guid.NewGuid()}";

    public CustomWebApplicationFactory()
    {
        Environment.SetEnvironmentVariable("WBJwtKey", "integration-tests-super-secret-key-123456");
        Environment.SetEnvironmentVariable("WBIssuer", "WorthBoards.IntegrationTests");
        Environment.SetEnvironmentVariable("WBAudience", "WorthBoards.IntegrationTests.Client");
        Environment.SetEnvironmentVariable("DATABASE_URL", "Host=localhost;Database=worthboards_tests;Username=test;Password=test");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            var testConfig = new Dictionary<string, string?>
            {
                ["WBJwtKey"] = "integration-tests-super-secret-key-123456",
                ["WBIssuer"] = "WorthBoards.IntegrationTests",
                ["WBAudience"] = "WorthBoards.IntegrationTests.Client"
            };

            configBuilder.AddInMemoryCollection(testConfig);
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll(typeof(DbContextOptions<ApplicationDbContext>));
            services.RemoveAll(typeof(ApplicationDbContext));
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));

            services.RemoveAll<IEmailService>();
            services.RemoveAll<CapturingEmailService>();
            services.AddSingleton<CapturingEmailService>();
            services.AddSingleton<IEmailService>(sp => sp.GetRequiredService<CapturingEmailService>());
        });
    }

    public async Task ResetStateAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<CapturingEmailService>();

        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
        emailService.Clear();
    }

    public async Task<T> WithDbContextAsync<T>(Func<ApplicationDbContext, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await action(db);
    }

    public async Task WithDbContextAsync(Func<ApplicationDbContext, Task> action)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await action(db);
    }

    public IReadOnlyList<WorthBoards.Business.Utils.EmailService.SendEmailRequest> SentEmails
    {
        get
        {
            using var scope = Services.CreateScope();
            var emailService = scope.ServiceProvider.GetRequiredService<CapturingEmailService>();
            return emailService.SentEmails;
        }
    }
}
