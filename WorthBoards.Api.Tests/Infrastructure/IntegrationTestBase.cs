using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace WorthBoards.Api.Tests.Infrastructure;

public class IntegrationTestBase : IDisposable
{
    protected readonly TestWebApplicationFactory Factory;
    protected readonly HttpClient Client;
    protected readonly IConfiguration Configuration;

    public IntegrationTestBase()
    {
        Factory = new TestWebApplicationFactory();
        Client = Factory.CreateClient();

        // Get configuration for JWT settings
        using var scope = Factory.Services.CreateScope();
        Configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    }

    protected string GetJwtToken(int userId, string email)
    {
        var jwtSecret = Configuration["WBJwtKey"] ?? "ee7oQdNAf7ANXrD9C5RaC48JbBdaEMYk";
        var issuer = Configuration["WBIssuer"] ?? "https://auth.WorthBoards.com";
        var audience = Configuration["WBAudience"] ?? "https://business.com";

        return TestHelpers.GenerateJwtToken(userId, email, jwtSecret, issuer, audience);
    }

    protected async Task ClearDatabaseAsync()
    {
        await TestHelpers.ClearDatabaseAsync(Factory.Services);
    }

    public void Dispose()
    {
        Client?.Dispose();
        Factory?.Dispose();
        GC.SuppressFinalize(this);
    }
}
