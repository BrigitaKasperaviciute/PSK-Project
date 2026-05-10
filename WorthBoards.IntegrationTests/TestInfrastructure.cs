using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using WorthBoards.Api.Utils;
using WorthBoards.Business.Dtos.Identity;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Business.Services.Interfaces;
using WorthBoards.Common.Enums;
using WorthBoards.Common.Exceptions.Custom;

namespace WorthBoards.IntegrationTests;

internal static class TestEnvironment
{
    private static bool configured;

    public static void EnsureConfigured()
    {
        if (configured)
        {
            return;
        }

        Environment.SetEnvironmentVariable("WBJwtKey", "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef");
        Environment.SetEnvironmentVariable("WBIssuer", "WorthBoards.Tests");
        Environment.SetEnvironmentVariable("WBAudience", "WorthBoards.Tests");
        Environment.SetEnvironmentVariable("DATABASE_URL", "Host=localhost;Port=5432;Database=worthboards_tests;Username=postgres;Password=postgres");

        configured = true;
    }
}

internal sealed class WorthBoardsApiFactory : WebApplicationFactory<Program>
{
    public Mock<IAuthService> AuthServiceMock { get; } = new(MockBehavior.Strict);
    public Mock<IBoardService> BoardServiceMock { get; } = new(MockBehavior.Strict);
    public Mock<IBoardOnUserService> BoardOnUserServiceMock { get; } = new(MockBehavior.Strict);
    public Mock<IBoardTaskService> BoardTaskServiceMock { get; } = new(MockBehavior.Strict);
    public Mock<ICommentService> CommentServiceMock { get; } = new(MockBehavior.Strict);
    public Mock<INotificationService> NotificationServiceMock { get; } = new(MockBehavior.Strict);
    public Mock<ITaskOnUserService> TaskOnUserServiceMock { get; } = new(MockBehavior.Strict);
    public Mock<IUserService> UserServiceMock { get; } = new(MockBehavior.Strict);
    public Mock<IFileService> FileServiceMock { get; } = new(MockBehavior.Strict);

    public WorthBoardsApiFactory()
    {
        TestEnvironment.EnsureConfigured();

        BoardServiceMock
            .Setup(service => service.GetUserRoleByBoardIdAndUserIdAsync(It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(UserRoleEnum.OWNER);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Logger:UseHttpLoggingMiddleware"] = "true",
                ["Logger:UseControllerLoggingActionFilter"] = "true"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                options.DefaultScheme = TestAuthHandler.SchemeName;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                options.DefaultScheme = TestAuthHandler.SchemeName;
            });

            services.RemoveAll<IAuthService>();
            services.RemoveAll<IBoardService>();
            services.RemoveAll<IBoardOnUserService>();
            services.RemoveAll<IBoardTaskService>();
            services.RemoveAll<ICommentService>();
            services.RemoveAll<INotificationService>();
            services.RemoveAll<ITaskOnUserService>();
            services.RemoveAll<IUserService>();
            services.RemoveAll<IFileService>();

            services.AddSingleton(AuthServiceMock.Object);
            services.AddSingleton(BoardServiceMock.Object);
            services.AddSingleton(BoardOnUserServiceMock.Object);
            services.AddSingleton(BoardTaskServiceMock.Object);
            services.AddSingleton(CommentServiceMock.Object);
            services.AddSingleton(NotificationServiceMock.Object);
            services.AddSingleton(TaskOnUserServiceMock.Object);
            services.AddSingleton(UserServiceMock.Object);
            services.AddSingleton(FileServiceMock.Object);
        });
    }

    public HttpClient CreateAuthorizedClient()
    {
        return CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
    }
}

internal sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "WorthBoardsTestScheme";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, "test-user")
        };

        if (Request.Headers.TryGetValue("X-Test-UserId", out var userIdHeader) &&
            int.TryParse(userIdHeader.ToString(), out var userId))
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.ToString()));
        }

        if (Request.Headers.TryGetValue("X-Test-Email", out var emailHeader) &&
            !string.IsNullOrWhiteSpace(emailHeader.ToString()))
        {
            claims.Add(new Claim(ClaimTypes.Email, emailHeader.ToString()));
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

internal static class TestRequestFactory
{
    public static HttpRequestMessage CreateJsonRequest(HttpMethod method, string uri, object? body = null, int? userId = 1, string? email = "tester@example.com")
    {
        var request = new HttpRequestMessage(method, uri);

        if (userId.HasValue)
        {
            request.Headers.Add("X-Test-UserId", userId.Value.ToString());
        }

        if (email is not null)
        {
            request.Headers.Add("X-Test-Email", email);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }

    public static HttpRequestMessage CreateMultipartImageRequest(string uri, string fileName = "board.png", int? userId = 1)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, uri);

        if (userId.HasValue)
        {
            request.Headers.Add("X-Test-UserId", userId.Value.ToString());
        }

        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("fake-image-bytes"));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");

        var multipartContent = new MultipartFormDataContent
        {
            { fileContent, "image", fileName }
        };

        request.Content = multipartContent;
        return request;
    }

    public static HttpRequestMessage CreateRawJsonRequest(HttpMethod method, string uri, string rawJson, int? userId = 1, string? email = "tester@example.com")
    {
        var request = new HttpRequestMessage(method, uri);

        if (userId.HasValue)
        {
            request.Headers.Add("X-Test-UserId", userId.Value.ToString());
        }

        if (email is not null)
        {
            request.Headers.Add("X-Test-Email", email);
        }

        request.Content = new StringContent(rawJson, Encoding.UTF8, "application/json");
        return request;
    }
}
