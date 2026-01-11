using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using WorthBoards.Common.Enums;
using WorthBoards.Data.Database;
using WorthBoards.Data.Identity;
using WorthBoards.Domain.Entities;

namespace WorthBoards.Api.Tests.Infrastructure;

public static class TestHelpers
{
    public static string GenerateJwtToken(int userId, string email, string jwtSecret, string issuer, string audience)
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        {
            KeyId = "test-key-id"
        };
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(ClaimTypes.Email, email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddHours(1),
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = credentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    public static async Task<ApplicationUser> CreateTestUserAsync(IServiceProvider services, string email = "test@example.com", string password = "Test123!")
    {
        using var scope = services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = "Test",
            LastName = "User",
            CreationDate = DateTime.UtcNow
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            throw new Exception($"Failed to create user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        return user;
    }

    public static async Task<Board> CreateTestBoardAsync(IServiceProvider services, int ownerId)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var board = new Board
        {
            Title = "Test Board",
            Description = "Test Description",
            ImageName = "test.jpg",
            CreationDate = DateTime.UtcNow
        };

        dbContext.Boards.Add(board);
        await dbContext.SaveChangesAsync();

        var boardOnUser = new BoardOnUser
        {
            BoardId = board.Id,
            UserId = ownerId,
            UserRole = UserRoleEnum.OWNER,
            AddedAt = DateTime.UtcNow
        };

        dbContext.BoardOnUsers.Add(boardOnUser);
        await dbContext.SaveChangesAsync();

        return board;
    }

    public static async Task<BoardTask> CreateTestTaskAsync(IServiceProvider services, int boardId)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var task = new BoardTask
        {
            Title = "Test Task",
            Description = "Test Task Description",
            TaskStatus = TaskStatusEnum.PENDING,
            BoardId = boardId,
            CreationDate = DateTime.UtcNow
        };

        dbContext.BoardTasks.Add(task);
        await dbContext.SaveChangesAsync();

        return task;
    }

    public static async Task<Comment> CreateTestCommentAsync(IServiceProvider services, int taskId, int userId)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var comment = new Comment
        {
            Content = "Test Comment",
            BoardTaskId = taskId,
            UserId = userId,
            CreationDate = DateTime.UtcNow,
            Edited = false
        };

        dbContext.Comments.Add(comment);
        await dbContext.SaveChangesAsync();

        return comment;
    }

    public static async Task<Notification> CreateTestNotificationAsync(IServiceProvider services, int userId, int boardId)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var notification = new Notification
        {
            NotificationType = NotificationEventTypeEnum.INVITATION,
            SenderId = userId,
            BoardId = boardId,
            SendDate = DateTime.UtcNow
        };

        dbContext.Notifications.Add(notification);
        await dbContext.SaveChangesAsync();

        // Create the link to user
        var notificationOnUser = new NotificationOnUser
        {
            NotificationId = notification.Id,
            UserId = userId
        };

        dbContext.NotificationsOnUsers.Add(notificationOnUser);
        await dbContext.SaveChangesAsync();

        return notification;
    }

    public static async Task<TaskOnUser> CreateTestTaskOnUserAsync(IServiceProvider services, int taskId, int userId)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var taskOnUser = new TaskOnUser
        {
            BoardTaskId = taskId,
            UserId = userId
        };

        dbContext.TasksOnUsers.Add(taskOnUser);
        await dbContext.SaveChangesAsync();

        return taskOnUser;
    }

    public static async Task ClearDatabaseAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Clear all tables in reverse dependency order
        dbContext.NotificationsOnUsers.RemoveRange(dbContext.NotificationsOnUsers);
        dbContext.TasksOnUsers.RemoveRange(dbContext.TasksOnUsers);
        dbContext.Comments.RemoveRange(dbContext.Comments);
        dbContext.BoardTasks.RemoveRange(dbContext.BoardTasks);
        dbContext.BoardOnUsers.RemoveRange(dbContext.BoardOnUsers);
        dbContext.Boards.RemoveRange(dbContext.Boards);
        dbContext.Notifications.RemoveRange(dbContext.Notifications);
        dbContext.Users.RemoveRange(dbContext.Users);

        await dbContext.SaveChangesAsync();
    }
}
