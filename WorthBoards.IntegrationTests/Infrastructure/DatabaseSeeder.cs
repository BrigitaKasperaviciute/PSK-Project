using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using WorthBoards.Common.Enums;
using WorthBoards.Data.Database;
using WorthBoards.Data.Identity;
using WorthBoards.Domain.Entities;

namespace WorthBoards.IntegrationTests.Infrastructure;

public class DatabaseSeeder
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public DatabaseSeeder(IServiceScope scope)
    {
        _db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        _userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    }

    public async Task<ApplicationUser> CreateUserAsync(string? suffix = null)
    {
        suffix ??= Guid.NewGuid().ToString("N")[..8];
        var user = new ApplicationUser
        {
            UserName = $"user_{suffix}",
            Email = $"user_{suffix}@test.com",
            FirstName = "Test",
            LastName = "User",
            CreationDate = DateTime.UtcNow,
        };
        var result = await _userManager.CreateAsync(user, "Test@12345!");
        if (!result.Succeeded)
            throw new InvalidOperationException($"Failed to create user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        return user;
    }

    public async Task<Board> CreateBoardAsync(ApplicationUser owner, string? title = null)
    {
        var board = new Board
        {
            Title = title ?? $"Board_{Guid.NewGuid():N}",
            Description = "Test board description",
            ImageName = "default.jpg",
            CreationDate = DateTime.UtcNow,
            BoardOnUsers = new List<BoardOnUser>
            {
                new() { UserId = owner.Id, UserRole = UserRoleEnum.OWNER, AddedAt = DateTime.UtcNow }
            }
        };
        _db.Boards.Add(board);
        await _db.SaveChangesAsync();
        return board;
    }

    public async Task<BoardTask> CreateTaskAsync(Board board, string? title = null, TaskStatusEnum status = TaskStatusEnum.PENDING)
    {
        var task = new BoardTask
        {
            BoardId = board.Id,
            Title = title ?? $"Task_{Guid.NewGuid():N}"[..20],
            Description = "Test task description",
            TaskStatus = status,
            CreationDate = DateTime.UtcNow,
        };
        _db.BoardTasks.Add(task);
        await _db.SaveChangesAsync();
        return task;
    }

    public async Task<Comment> CreateCommentAsync(BoardTask task, ApplicationUser user, string? content = null)
    {
        var comment = new Comment
        {
            BoardTaskId = task.Id,
            UserId = user.Id,
            Content = content ?? "Test comment content",
            CreationDate = DateTime.UtcNow,
            Edited = false,
        };
        _db.Comments.Add(comment);
        await _db.SaveChangesAsync();
        return comment;
    }

    public async Task LinkUserToBoardAsync(Board board, ApplicationUser user, UserRoleEnum role)
    {
        _db.BoardOnUsers.Add(new BoardOnUser
        {
            BoardId = board.Id,
            UserId = user.Id,
            UserRole = role,
            AddedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();
    }

    public async Task<(Notification notification, NotificationOnUser link)> CreateInvitationAsync(
        Board board, ApplicationUser sender, ApplicationUser recipient, UserRoleEnum role = UserRoleEnum.VIEWER)
    {
        var notification = new Notification
        {
            SenderId = sender.Id,
            BoardId = board.Id,
            NotificationType = NotificationEventTypeEnum.INVITATION,
            SubjectUserId = recipient.Id,
            InvitationRole = role,
            SendDate = DateTime.UtcNow,
        };
        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync();

        var link = new NotificationOnUser { NotificationId = notification.Id, UserId = recipient.Id };
        _db.NotificationsOnUsers.Add(link);
        await _db.SaveChangesAsync();

        return (notification, link);
    }

    public async Task<(Notification notification, NotificationOnUser link)> CreateGenericNotificationAsync(
        Board board, ApplicationUser sender, ApplicationUser recipient)
    {
        var notification = new Notification
        {
            SenderId = sender.Id,
            BoardId = board.Id,
            NotificationType = NotificationEventTypeEnum.INVITATION,
            SubjectUserId = recipient.Id,
            SendDate = DateTime.UtcNow,
        };
        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync();

        var link = new NotificationOnUser { NotificationId = notification.Id, UserId = recipient.Id };
        _db.NotificationsOnUsers.Add(link);
        await _db.SaveChangesAsync();

        return (notification, link);
    }
}
