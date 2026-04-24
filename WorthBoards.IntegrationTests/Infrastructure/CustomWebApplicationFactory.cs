using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using WorthBoards.Business.Services.Interfaces;

namespace WorthBoards.IntegrationTests.Infrastructure;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    internal const string JwtKey = "test-jwt-secret-key-that-is-at-least-32-chars!";
    internal const string JwtIssuer = "test-issuer";
    internal const string JwtAudience = "test-audience";

    public Mock<IAuthService> AuthServiceMock { get; } = new();
    public Mock<IBoardService> BoardServiceMock { get; } = new();
    public Mock<IBoardTaskService> BoardTaskServiceMock { get; } = new();
    public Mock<ICommentService> CommentServiceMock { get; } = new();
    public Mock<INotificationService> NotificationServiceMock { get; } = new();
    public Mock<IBoardOnUserService> BoardOnUserServiceMock { get; } = new();
    public Mock<ITaskOnUserService> TaskOnUserServiceMock { get; } = new();
    public Mock<IUserService> UserServiceMock { get; } = new();
    public Mock<IFileService> FileServiceMock { get; } = new();

    static CustomWebApplicationFactory()
    {
        // Must be set before WebApplicationBuilder reads Configuration during Program.cs startup.
        Environment.SetEnvironmentVariable("WBJwtKey", JwtKey);
        Environment.SetEnvironmentVariable("WBIssuer", JwtIssuer);
        Environment.SetEnvironmentVariable("WBAudience", JwtAudience);
        Environment.SetEnvironmentVariable("DATABASE_URL", "Host=localhost;Database=testdb;Username=test;Password=test");
        Environment.SetEnvironmentVariable("Logger__UseHttpLoggingMiddleware", "false");
        Environment.SetEnvironmentVariable("Logger__UseControllerLoggingActionFilter", "false");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            Replace<IAuthService>(services, AuthServiceMock.Object);
            Replace<IBoardService>(services, BoardServiceMock.Object);
            Replace<IBoardTaskService>(services, BoardTaskServiceMock.Object);
            Replace<ICommentService>(services, CommentServiceMock.Object);
            Replace<INotificationService>(services, NotificationServiceMock.Object);
            Replace<IBoardOnUserService>(services, BoardOnUserServiceMock.Object);
            Replace<ITaskOnUserService>(services, TaskOnUserServiceMock.Object);
            Replace<IUserService>(services, UserServiceMock.Object);
            Replace<IFileService>(services, FileServiceMock.Object);
        });
    }

    private static void Replace<T>(IServiceCollection services, T instance) where T : class
    {
        var toRemove = services.Where(d => d.ServiceType == typeof(T)).ToList();
        foreach (var d in toRemove)
            services.Remove(d);
        services.AddScoped<T>(_ => instance);
    }

    public void ResetAllMocks()
    {
        AuthServiceMock.Reset();
        BoardServiceMock.Reset();
        BoardTaskServiceMock.Reset();
        CommentServiceMock.Reset();
        NotificationServiceMock.Reset();
        BoardOnUserServiceMock.Reset();
        TaskOnUserServiceMock.Reset();
        UserServiceMock.Reset();
        FileServiceMock.Reset();
    }
}