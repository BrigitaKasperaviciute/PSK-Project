using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WorthBoards.Common.Enums;
using WorthBoards.Data.Database;
using WorthBoards.Data.Identity;
using WorthBoards.Domain.Entities;

namespace WorthBoards.IntegrationTests.Infrastructure;

public static class DatabaseSeeder
{
    public const string DefaultPassword = "Test@12345!";

    public static async Task<ApplicationUser> CreateUserAsync(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        string? username = null,
        string? email = null,
        string firstName = "Test",
        string lastName = "User")
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        username ??= $"user_{suffix}";
        email ??= $"user_{suffix}@test.com";

        var user = new ApplicationUser
        {
            UserName = username,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            CreationDate = DateTime.UtcNow,
            EmailConfirmed = true,
        };

        var result = await userManager.CreateAsync(user, DefaultPassword);
        if (!result.Succeeded)
            throw new InvalidOperationException($"Failed to create test user: {string.Join(", ", result.Errors.Select(e => e.Description))}");

        return user;
    }

    public static async Task<Board> CreateBoardAsync(
        ApplicationDbContext db,
        int userId,
        UserRoleEnum role = UserRoleEnum.OWNER,
        string title = "Test Board",
        string description = "Test Description")
    {
        var board = new Board
        {
            Title = title,
            Description = description,
            ImageName = "default.jpg",
            CreationDate = DateTime.UtcNow,
        };
        db.Boards.Add(board);

        db.BoardOnUsers.Add(new BoardOnUser
        {
            Board = board,
            UserId = userId,
            UserRole = role,
            AddedAt = DateTime.UtcNow,
        });

        await db.SaveChangesAsync();
        return board;
    }

    public static async Task AddUserToBoardAsync(
        ApplicationDbContext db,
        int boardId,
        int userId,
        UserRoleEnum role)
    {
        db.BoardOnUsers.Add(new BoardOnUser
        {
            BoardId = boardId,
            UserId = userId,
            UserRole = role,
            AddedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    public static async Task<BoardTask> CreateTaskAsync(
        ApplicationDbContext db,
        int boardId,
        string title = "Test Task",
        TaskStatusEnum status = TaskStatusEnum.PENDING)
    {
        var task = new BoardTask
        {
            BoardId = boardId,
            Title = title,
            Description = "Test task description",
            TaskStatus = status,
            CreationDate = DateTime.UtcNow,
        };
        db.BoardTasks.Add(task);
        await db.SaveChangesAsync();
        return task;
    }

    public static async Task<Comment> CreateCommentAsync(
        ApplicationDbContext db,
        int taskId,
        int userId,
        string content = "Test comment content")
    {
        var comment = new Comment
        {
            BoardTaskId = taskId,
            UserId = userId,
            Content = content,
            CreationDate = DateTime.UtcNow,
            Edited = false,
        };
        db.Comments.Add(comment);
        await db.SaveChangesAsync();
        return comment;
    }

    public static async Task<(Notification Notification, NotificationOnUser Link)> CreateInvitationNotificationAsync(
        ApplicationDbContext db,
        int boardId,
        int senderId,
        int recipientId,
        UserRoleEnum role = UserRoleEnum.VIEWER)
    {
        var notification = new Notification
        {
            BoardId = boardId,
            SenderId = senderId,
            NotificationType = NotificationEventTypeEnum.INVITATION,
            SubjectUserId = recipientId,
            InvitationRole = role,
            SendDate = DateTime.UtcNow,
        };
        db.Notifications.Add(notification);

        var link = new NotificationOnUser { Notification = notification, UserId = recipientId };
        db.NotificationsOnUsers.Add(link);

        await db.SaveChangesAsync();
        return (notification, link);
    }

    public static async Task AddUserToTaskAsync(
        ApplicationDbContext db,
        int boardTaskId,
        int userId)
    {
        db.TasksOnUsers.Add(new TaskOnUser
        {
            BoardTaskId = boardTaskId,
            UserId = userId,
            AssignedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    public static async Task CleanDatabaseAsync(ApplicationDbContext db)
    {
        var isInMemory = db.Database.ProviderName?.Contains("InMemory", StringComparison.OrdinalIgnoreCase) == true;

        if (isInMemory)
        {
            db.NotificationsOnUsers.RemoveRange(await db.NotificationsOnUsers.ToListAsync());
            db.TasksOnUsers.RemoveRange(await db.TasksOnUsers.ToListAsync());
            db.Comments.RemoveRange(await db.Comments.ToListAsync());
            db.Notifications.RemoveRange(await db.Notifications.ToListAsync());
            db.BoardTasks.RemoveRange(await db.BoardTasks.ToListAsync());
            db.BoardOnUsers.RemoveRange(await db.BoardOnUsers.ToListAsync());
            db.Boards.RemoveRange(await db.Boards.ToListAsync());
            db.Users.RemoveRange(await db.Users.ToListAsync());
            await db.SaveChangesAsync();
        }
        else
        {
            await db.NotificationsOnUsers.ExecuteDeleteAsync();
            await db.TasksOnUsers.ExecuteDeleteAsync();
            await db.Comments.ExecuteDeleteAsync();
            await db.Notifications.ExecuteDeleteAsync();
            await db.BoardTasks.ExecuteDeleteAsync();
            await db.BoardOnUsers.ExecuteDeleteAsync();
            await db.Boards.ExecuteDeleteAsync();
            await db.Users.ExecuteDeleteAsync();
        }
    }
}
