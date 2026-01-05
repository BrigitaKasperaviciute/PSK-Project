using Microsoft.AspNetCore.Identity;
using WorthBoards.Common.Enums;
using WorthBoards.Data.Database;
using WorthBoards.Data.Identity;
using WorthBoards.Domain.Entities;

namespace WorthBoards.Api.IntegrationTests.Support;

public static class TestDataSeeder
{
    public static async Task SeedAsync(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        if (context.Users.Any())
        {
            return;
        }

        var owner = new ApplicationUser
        {
            Id = 1,
            UserName = "owner",
            Email = "owner@test.local",
            FirstName = "Owner",
            LastName = "One",
            CreationDate = DateTime.UtcNow
        };
        var viewer = new ApplicationUser
        {
            Id = 2,
            UserName = "viewer",
            Email = "viewer@test.local",
            FirstName = "Viewer",
            LastName = "One",
            CreationDate = DateTime.UtcNow
        };
        var otherOwner = new ApplicationUser
        {
            Id = 3,
            UserName = "otherowner",
            Email = "otherowner@test.local",
            FirstName = "Owner",
            LastName = "Two",
            CreationDate = DateTime.UtcNow
        };

        const string defaultPassword = "Str0ngP@ssword!";
        await userManager.CreateAsync(owner, defaultPassword);
        await userManager.CreateAsync(viewer, defaultPassword);
        await userManager.CreateAsync(otherOwner, defaultPassword);

        var board1 = new Board
        {
            Id = 10,
            Title = "Alpha Board",
            Description = "Seeded board for happy-path scenarios",
            ImageName = "alpha.png",
            CreationDate = DateTime.UtcNow
        };

        var board2 = new Board
        {
            Id = 20,
            Title = "Removable Board",
            Description = "Used to test delete operations",
            ImageName = "removable.png",
            CreationDate = DateTime.UtcNow
        };

        var board3 = new Board
        {
            Id = 30,
            Title = "Restricted Board",
            Description = "Used to test authorization failures",
            ImageName = "restricted.png",
            CreationDate = DateTime.UtcNow
        };

        context.Boards.AddRange(board1, board2, board3);
        await context.SaveChangesAsync();

        context.BoardOnUsers.AddRange(
            new BoardOnUser { BoardId = board1.Id, UserId = owner.Id, AddedAt = DateTime.UtcNow, UserRole = UserRoleEnum.OWNER },
            new BoardOnUser { BoardId = board2.Id, UserId = owner.Id, AddedAt = DateTime.UtcNow, UserRole = UserRoleEnum.OWNER },
            new BoardOnUser { BoardId = board3.Id, UserId = otherOwner.Id, AddedAt = DateTime.UtcNow, UserRole = UserRoleEnum.OWNER },
            new BoardOnUser { BoardId = board3.Id, UserId = viewer.Id, AddedAt = DateTime.UtcNow, UserRole = UserRoleEnum.VIEWER }
        );
        await context.SaveChangesAsync();

        var task1 = new BoardTask
        {
            Id = 100,
            BoardId = board1.Id,
            Title = "Seed Task",
            Description = "Initial task for update/comment scenarios",
            CreationDate = DateTime.UtcNow,
            TaskStatus = TaskStatusEnum.PENDING
        };

        context.BoardTasks.Add(task1);
        await context.SaveChangesAsync();

        var notification = new Notification
        {
            Id = 200,
            SenderId = owner.Id,
            NotificationType = NotificationEventTypeEnum.TASK_CREATED,
            BoardId = board1.Id,
            TaskId = task1.Id,
            SendDate = DateTime.UtcNow,
            NotificationsOnUsers = new List<NotificationOnUser>()
        };
        context.Notifications.Add(notification);
        await context.SaveChangesAsync();

        context.NotificationsOnUsers.Add(new NotificationOnUser { NotificationId = notification.Id, UserId = owner.Id });
        await context.SaveChangesAsync();
    }
}
