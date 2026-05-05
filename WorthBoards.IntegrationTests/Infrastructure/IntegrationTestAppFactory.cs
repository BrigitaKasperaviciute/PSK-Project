using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using WorthBoards.Data.Database;
using WorthBoards.Data.Identity;
using WorthBoards.Domain.Entities;
using WorthBoards.Common.Enums;
using Testcontainers.PostgreSql;
using Xunit;

namespace WorthBoards.IntegrationTests.Infrastructure;

public sealed class IntegrationTestAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string JwtKey = "WorthBoards.IntegrationTests.SigningKey.1234567890!";
    private const string JwtIssuer = "WorthBoards.IntegrationTests";
    private const string JwtAudience = "WorthBoards.IntegrationTests";
    public const string DefaultPassword = "Password123!";

    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("worthboards_integration_tests")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public IntegrationTestAppFactory()
    {
        Environment.SetEnvironmentVariable("WBJwtKey", JwtKey);
        Environment.SetEnvironmentVariable("WBIssuer", JwtIssuer);
        Environment.SetEnvironmentVariable("WBAudience", JwtAudience);
        Environment.SetEnvironmentVariable("DATABASE_URL", "Host=localhost;Database=WorthBoardsIntegrationTests;Username=postgres;Password=postgres");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("IntegrationTests");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(_db.GetConnectionString()));
        });
    }

    public async Task InitializeAsync()
    {
        await _db.StartAsync();

        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.Database.EnsureCreatedAsync();
    }

    public new async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await base.DisposeAsync();
    }

    public async Task ResetDatabaseAsync()
    {
        await WithDbContextAsync(async db =>
        {
            var tables = new[]
            {
                "NotificationsOnUsers",
                "TasksOnUsers",
                "Comments",
                "Notifications",
                "BoardsOnUsers",
                "Tasks",
                "Boards",
                "AspNetUserClaims",
                "AspNetUserLogins",
                "AspNetUserTokens",
                "AspNetUserRoles",
                "AspNetRoleClaims",
                "AspNetRoles",
                "AspNetUsers"
            };

            var tableList = string.Join(", ", tables.Select(table => $"\"{table}\""));
            await db.Database.ExecuteSqlRawAsync($"TRUNCATE TABLE {tableList} RESTART IDENTITY CASCADE;");
        });
    }

    public async Task<TResult> WithDbContextAsync<TResult>(Func<ApplicationDbContext, Task<TResult>> action)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await action(db);
    }

    public async Task WithDbContextAsync(Func<ApplicationDbContext, Task> action) =>
        await WithDbContextAsync(async db =>
        {
            await action(db);
            return 0;
        });

    public async Task<ApplicationUser> CreateUserAsync(string firstName, string lastName, string prefix, string? imageName = null, string password = DefaultPassword)
    {
        using var scope = Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var userName = LimitUserName($"{prefix}-{Guid.NewGuid():N}");
        var email = $"{prefix}-{Guid.NewGuid():N}@example.com";
        var user = new ApplicationUser
        {
            FirstName = firstName,
            LastName = lastName,
            UserName = userName,
            Email = email,
            CreationDate = DateTime.UtcNow,
            ImageName = imageName
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(error => error.Description)));
        }

        return user;
    }

    public HttpClient CreateAuthenticatedClient(ApplicationUser user)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateJwtToken(user));
        return client;
    }

    public string CreateJwtToken(ApplicationUser user)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.UserName ?? string.Empty),
            new Claim(ClaimTypes.Email, user.Email ?? string.Empty)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: JwtIssuer,
            audience: JwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task<Board> SeedBoardAsync(int ownerUserId, string title, string description, string imageName = "board.png")
    {
        return await WithDbContextAsync(async db =>
        {
            var board = new Board
            {
                Title = title,
                Description = description,
                ImageName = imageName,
                CreationDate = DateTime.UtcNow,
                BoardOnUsers = new List<BoardOnUser>
                {
                    new()
                    {
                        UserId = ownerUserId,
                        UserRole = UserRoleEnum.OWNER,
                        AddedAt = DateTime.UtcNow
                    }
                }
            };

            db.Boards.Add(board);
            await db.SaveChangesAsync();
            return board;
        });
    }

    public async Task<BoardOnUser> SeedBoardUserAsync(int boardId, int userId, UserRoleEnum role)
    {
        return await WithDbContextAsync(async db =>
        {
            var boardOnUser = new BoardOnUser
            {
                BoardId = boardId,
                UserId = userId,
                UserRole = role,
                AddedAt = DateTime.UtcNow
            };

            db.BoardOnUsers.Add(boardOnUser);
            await db.SaveChangesAsync();
            return boardOnUser;
        });
    }

    public async Task<BoardTask> SeedTaskAsync(int boardId, string title, string? description, TaskStatusEnum status)
    {
        return await WithDbContextAsync(async db =>
        {
            var boardTask = new BoardTask
            {
                BoardId = boardId,
                Title = title,
                Description = description,
                TaskStatus = status,
                CreationDate = DateTime.UtcNow
            };

            db.BoardTasks.Add(boardTask);
            await db.SaveChangesAsync();
            return boardTask;
        });
    }

    public async Task<TaskOnUser> SeedTaskUserAsync(int boardTaskId, int userId)
    {
        return await WithDbContextAsync(async db =>
        {
            var taskOnUser = new TaskOnUser
            {
                BoardTaskId = boardTaskId,
                UserId = userId,
                AssignedAt = DateTime.UtcNow
            };

            db.TasksOnUsers.Add(taskOnUser);
            await db.SaveChangesAsync();
            return taskOnUser;
        });
    }

    public async Task<Comment> SeedCommentAsync(int taskId, int userId, string content, bool edited = false)
    {
        return await WithDbContextAsync(async db =>
        {
            var comment = new Comment
            {
                BoardTaskId = taskId,
                UserId = userId,
                Content = content,
                CreationDate = DateTime.UtcNow,
                Edited = edited
            };

            db.Comments.Add(comment);
            await db.SaveChangesAsync();
            return comment;
        });
    }

    public async Task<Notification> SeedNotificationAsync(
        int boardId,
        int senderId,
        NotificationEventTypeEnum type,
        int? subjectUserId = null,
        int? taskId = null,
        UserRoleEnum? invitationRole = null,
        IEnumerable<int>? notificationUserIds = null,
        TaskStatusEnum oldTaskStatus = TaskStatusEnum.PENDING,
        TaskStatusEnum newTaskStatus = TaskStatusEnum.PENDING)
    {
        return await WithDbContextAsync(async db =>
        {
            var notification = new Notification
            {
                BoardId = boardId,
                SenderId = senderId,
                NotificationType = type,
                SubjectUserId = subjectUserId,
                TaskId = taskId,
                InvitationRole = invitationRole,
                OldTaskStatus = oldTaskStatus,
                NewTaskStatus = newTaskStatus,
                SendDate = DateTime.UtcNow,
                NotificationsOnUsers = notificationUserIds?.Select(userId => new NotificationOnUser
                {
                    UserId = userId
                }).ToList() ?? new List<NotificationOnUser>()
            };

            db.Notifications.Add(notification);
            await db.SaveChangesAsync();
            return notification;
        });
    }

    private static string LimitUserName(string value) => value.Length <= 30 ? value : value[..30];
}