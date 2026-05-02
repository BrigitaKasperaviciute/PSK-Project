using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
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
    internal const string TestJwtKey = "integration-test-secret-key-worthboards-32chars!!";
    internal const string TestIssuer = "WorthBoards-Test";
    internal const string TestAudience = "WorthBoards-Test";

    private readonly SqliteConnection _connection;

    public CustomWebApplicationFactory()
    {
        Environment.SetEnvironmentVariable("WBJwtKey", TestJwtKey);
        Environment.SetEnvironmentVariable("WBIssuer", TestIssuer);
        Environment.SetEnvironmentVariable("WBAudience", TestAudience);

        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WBJwtKey"] = TestJwtKey,
                ["WBIssuer"] = TestIssuer,
                ["WBAudience"] = TestAudience,
                ["Logger:UseHttpLoggingMiddleware"] = "true",
                ["Logger:UseControllerLoggingActionFilter"] = "true",
                ["Email:UseEmailLogging"] = "false",
            });
        });

        builder.ConfigureServices(services =>
        {
            var dbCtxDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (dbCtxDescriptor != null)
                services.Remove(dbCtxDescriptor);

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlite(_connection)
                       .ReplaceService<IModelCustomizer, SqliteCompatibleModelCustomizer>());

            var emailDescriptors = services
                .Where(d => d.ServiceType == typeof(IEmailService))
                .ToList();
            foreach (var d in emailDescriptors)
                services.Remove(d);
            services.AddScoped<IEmailService, FakeEmailService>();

            var fileDescriptors = services.Where(d => d.ServiceType == typeof(IFileService)).ToList();
            foreach (var d in fileDescriptors) services.Remove(d);
            services.AddScoped<IFileService, FakeFileService>();

            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Database.EnsureCreated();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Close();
            _connection.Dispose();
        }
    }
}
