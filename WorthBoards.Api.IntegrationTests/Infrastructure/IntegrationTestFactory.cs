using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WorthBoards.Api;
using WorthBoards.Business.Services.Interfaces;
using WorthBoards.Business.Utils.EmailService.Interfaces;
using WorthBoards.Common.Enums;
using WorthBoards.Data.Database;
using WorthBoards.Data.Identity;
using WorthBoards.Domain.Entities;

namespace WorthBoards.Api.IntegrationTests.Infrastructure;

public class IntegrationTestFactory : WebApplicationFactory<Program>
{
    public const string DefaultPassword = "Passw0rd!";
    public const string DefaultAuthToken = "valid-token";

    public IntegrationTestFactory()
    {
        Environment.SetEnvironmentVariable("WBJwtKey", "integration-test-secret-key-123456789");
        Environment.SetEnvironmentVariable("WBIssuer", "integration-tests");
        Environment.SetEnvironmentVariable("WBAudience", "integration-tests");
        Environment.SetEnvironmentVariable("DATABASE_URL", "Host=localhost;Database=integration;Username=test;Password=test");
        Environment.SetEnvironmentVariable("FrontendBaseUrl", "http://localhost");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            var databaseName = $"WorthBoardsTests-{Guid.NewGuid()}";
            services.RemoveAll(typeof(DbContextOptions<ApplicationDbContext>));
            services.RemoveAll<ApplicationDbContext>();

            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseInMemoryDatabase(databaseName);
            });

            services.RemoveAll<IEmailService>();
            services.AddSingleton<IEmailService, FakeEmailService>();

            services.RemoveAll<IFileService>();
            services.AddSingleton<IFileService, FakeFileService>();

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.Scheme;
                options.DefaultChallengeScheme = TestAuthHandler.Scheme;
                options.DefaultScheme = TestAuthHandler.Scheme;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.Scheme, _ => { });

            var serviceProvider = services.BuildServiceProvider();
            using var scope = serviceProvider.CreateScope();
            var scopedServices = scope.ServiceProvider;

            var context = scopedServices.GetRequiredService<ApplicationDbContext>();
            var userManager = scopedServices.GetRequiredService<UserManager<ApplicationUser>>();

            context.Database.EnsureCreated();
            SeedDatabaseAsync(context, userManager).GetAwaiter().GetResult();
        });
    }

    public HttpClient CreateAuthenticatedClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.Scheme, DefaultAuthToken);
        return client;
    }

    private static async Task SeedDatabaseAsync(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        if (await userManager.Users.AnyAsync())
        {
            return;
        }

        var owner = new ApplicationUser
        {
            Id = 1,
            UserName = "owner",
            Email = "owner@example.com",
            FirstName = "Owner",
            LastName = "User",
            CreationDate = DateTime.UtcNow,
            ImageName = "owner.png",
            EmailConfirmed = true
        };
        await userManager.CreateAsync(owner, DefaultPassword);

        var viewer = new ApplicationUser
        {
            Id = 2,
            UserName = "viewer",
            Email = "viewer@example.com",
            FirstName = "View",
            LastName = "User",
            CreationDate = DateTime.UtcNow,
            ImageName = "viewer.png",
            EmailConfirmed = true
        };
        await userManager.CreateAsync(viewer, DefaultPassword);

        var board = new Board
        {
            Id = 1,
            Title = "Board One",
            Description = "Seed board",
            ImageName = "seed.png",
            CreationDate = DateTime.UtcNow
        };

        context.Boards.Add(board);
        context.BoardOnUsers.AddRange(
            new BoardOnUser { BoardId = board.Id, UserId = owner.Id, AddedAt = DateTime.UtcNow, UserRole = UserRoleEnum.OWNER },
            new BoardOnUser { BoardId = board.Id, UserId = viewer.Id, AddedAt = DateTime.UtcNow, UserRole = UserRoleEnum.VIEWER }
        );

        var task = new BoardTask
        {
            Id = 1,
            BoardId = board.Id,
            Title = "Seed Task",
            Description = "Task description",
            CreationDate = DateTime.UtcNow,
            TaskStatus = TaskStatusEnum.PENDING
        };
        context.BoardTasks.Add(task);

        context.TasksOnUsers.Add(new TaskOnUser
        {
            BoardTaskId = task.Id,
            UserId = owner.Id,
            AssignedAt = DateTime.UtcNow
        });

        context.Comments.Add(new Comment
        {
            Id = 1,
            BoardTaskId = task.Id,
            UserId = owner.Id,
            Content = "Seed comment",
            CreationDate = DateTime.UtcNow,
            Edited = false
        });

        var notification = new Notification
        {
            Id = 1,
            SenderId = owner.Id,
            SendDate = DateTime.UtcNow,
            NotificationType = NotificationEventTypeEnum.TASK_CREATED,
            BoardId = board.Id,
            TaskId = task.Id,
            OldTaskStatus = TaskStatusEnum.PENDING,
            NewTaskStatus = TaskStatusEnum.PENDING
        };
        context.Notifications.Add(notification);
        context.NotificationsOnUsers.Add(new NotificationOnUser { NotificationId = notification.Id, UserId = owner.Id });

        await context.SaveChangesAsync();
    }
}
