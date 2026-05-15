using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WorthBoards.Common.Enums;
using WorthBoards.Data.Database;
using WorthBoards.Data.Identity;
using WorthBoards.Domain.Entities;

namespace WorthBoards.IntegrationTests.Infrastructure;

/// <summary>
/// Creates test entities directly in the database, bypassing the API layer.
/// Used for test setup (Arrange) where pre-existing data is required.
/// </summary>
internal sealed class DatabaseSeeder
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public DatabaseSeeder(IServiceScope scope)
    {
        _db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        _userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    }

    /// <summary>Creates an ApplicationUser with a deterministic strong password.</summary>
    public async Task<ApplicationUser> CreateUserAsync(string? userName = null, string? email = null)
    {
        var unique = Guid.NewGuid().ToString("N")[..8];
        var user = new ApplicationUser
        {
            UserName = userName ?? $"testuser{unique}",
            Email = email ?? $"test{unique}@example.com",
            FirstName = "Test",
            LastName = "User",
            CreationDate = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, "Test@12345!");
        if (!result.Succeeded)
            throw new InvalidOperationException(
                $"Failed to create test user: {string.Join(", ", result.Errors.Select(e => e.Description))}");

        return user;
    }

    /// <summary>Creates a Board with default values.</summary>
    public async Task<Board> CreateBoardAsync(string title = "Test Board")
    {
        var board = new Board
        {
            Title = title,
            Description = "Test board description",
            ImageName = "default.jpg",
            CreationDate = DateTime.UtcNow
        };

        _db.Boards.Add(board);
        await _db.SaveChangesAsync();
        return board;
    }

    /// <summary>Links a user to a board with the specified role (required for PermissionHandler).</summary>
    public async Task<BoardOnUser> LinkUserToBoardAsync(int boardId, int userId, UserRoleEnum role)
    {
        var link = new BoardOnUser
        {
            BoardId = boardId,
            UserId = userId,
            UserRole = role,
            AddedAt = DateTime.UtcNow
        };

        _db.BoardOnUsers.Add(link);
        await _db.SaveChangesAsync();
        return link;
    }

    /// <summary>Creates a BoardTask within a board.</summary>
    public async Task<BoardTask> CreateBoardTaskAsync(
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
            CreationDate = DateTime.UtcNow
        };

        _db.BoardTasks.Add(task);
        await _db.SaveChangesAsync();
        return task;
    }

    /// <summary>Creates a Comment on a task by a user.</summary>
    public async Task<Comment> CreateCommentAsync(int boardTaskId, int userId, string content = "Test comment content")
    {
        var comment = new Comment
        {
            BoardTaskId = boardTaskId,
            UserId = userId,
            Content = content,
            Edited = false,
            CreationDate = DateTime.UtcNow
        };

        _db.Comments.Add(comment);
        await _db.SaveChangesAsync();
        return comment;
    }

    /// <summary>
    /// Creates a Notification and links it to a user (NotificationOnUser).
    /// Used to test notification endpoints without going through the full event pipeline.
    /// Pass <paramref name="taskId"/> for task-related types (TASK_CREATED, TASK_ASSIGNED, TASK_STATUS_CHANGE).
    /// Pass <paramref name="subjectUserId"/> for user-related types (TASK_ASSIGNED, USER_*).
    /// </summary>
    public async Task<Notification> CreateNotificationForUserAsync(
        int boardId,
        int senderId,
        int recipientUserId,
        NotificationEventTypeEnum type = NotificationEventTypeEnum.TASK_CREATED,
        int? taskId = null,
        int? subjectUserId = null)
    {
        var notification = new Notification
        {
            SenderId = senderId,
            BoardId = boardId,
            NotificationType = type,
            TaskId = taskId,
            SubjectUserId = subjectUserId,
            SendDate = DateTime.UtcNow
        };

        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync();

        _db.NotificationsOnUsers.Add(new NotificationOnUser
        {
            NotificationId = notification.Id,
            UserId = recipientUserId
        });
        await _db.SaveChangesAsync();

        return notification;
    }

    /// <summary>
    /// Creates an INVITATION-type notification for <paramref name="inviteeUserId"/> from
    /// <paramref name="senderId"/>. <paramref name="invitationRole"/> is the role the invitee
    /// will receive when they accept. The NotificationOnUser link is also created so
    /// AcceptInvitation can verify the recipient.
    /// </summary>
    public async Task<Notification> CreateInvitationNotificationAsync(
        int boardId,
        int senderId,
        int inviteeUserId,
        UserRoleEnum invitationRole = UserRoleEnum.EDITOR)
    {
        var notification = new Notification
        {
            SenderId = senderId,
            BoardId = boardId,
            NotificationType = NotificationEventTypeEnum.INVITATION,
            InvitationRole = invitationRole,
            SubjectUserId = inviteeUserId,
            SendDate = DateTime.UtcNow
        };

        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync();

        _db.NotificationsOnUsers.Add(new NotificationOnUser
        {
            NotificationId = notification.Id,
            UserId = inviteeUserId
        });
        await _db.SaveChangesAsync();

        return notification;
    }

    /// <summary>
    /// Deletes all rows in FK-safe order. Call in each test class's InitializeAsync
    /// so tests start from a clean slate regardless of execution order.
    /// </summary>
    public static async Task CleanDatabaseAsync(ApplicationDbContext db)
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
