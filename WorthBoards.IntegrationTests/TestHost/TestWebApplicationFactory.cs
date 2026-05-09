using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WorthBoards.Business.Services.Interfaces;

namespace WorthBoards.IntegrationTests.TestHost;

public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    public ServiceMocks Mocks { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Environment.SetEnvironmentVariable("WBJwtKey", "this-is-a-test-jwt-key-at-least-32chars");
        Environment.SetEnvironmentVariable("WBIssuer", "worthboards-tests");
        Environment.SetEnvironmentVariable("WBAudience", "worthboards-tests");
        Environment.SetEnvironmentVariable("DATABASE_URL", "Host=localhost;Port=5432;Database=worthboards_test;Username=test;Password=test");

        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WBJwtKey"] = "this-is-a-test-jwt-key-at-least-32chars",
                ["WBIssuer"] = "worthboards-tests",
                ["WBAudience"] = "worthboards-tests",
                ["ConnectionStrings:WorthBoardsConnection"] = "Host=localhost;Port=5432;Database=worthboards_test;Username=test;Password=test",
                ["BaseUrl"] = "http://127.0.0.1:0",
                ["Logger:UseHttpLoggingMiddleware"] = "true",
                ["Logger:UseControllerLoggingActionFilter"] = "true"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                options.DefaultScheme = TestAuthHandler.SchemeName;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

            Replace(services, Mocks.AuthService.Object);
            Replace(services, Mocks.BoardService.Object);
            Replace(services, Mocks.BoardTaskService.Object);
            Replace(services, Mocks.BoardOnUserService.Object);
            Replace(services, Mocks.CommentService.Object);
            Replace(services, Mocks.NotificationService.Object);
            Replace(services, Mocks.TaskOnUserService.Object);
            Replace(services, Mocks.UserService.Object);
            Replace(services, Mocks.FileService.Object);
        });
    }

    private static void Replace<TInterface>(IServiceCollection services, TInterface implementation)
        where TInterface : class
    {
        services.RemoveAll<TInterface>();
        services.AddScoped(_ => implementation);
    }
}
