using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using WorthBoards.Common.Enums;
using WorthBoards.Data.Database;
using WorthBoards.Data.Identity;
using WorthBoards.Domain.Entities;

namespace WorthBoards.IntegrationTests.Infrastructure;

public static class IntegrationTestSeed
{
    public const string Password = "P@ssw0rd1!";

    public static async Task<SeedState> ResetAndSeedAsync(WorthBoardsApiFactory factory)
    {
        await factory.ResetDatabaseAsync();

        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<ApplicationDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        var owner = await CreateUserAsync(userManager, "owner", "owner@example.com", "Owner", "User");
        var editor = await CreateUserAsync(userManager, "editor", "editor@example.com", "Editor", "User");
        var viewer = await CreateUserAsync(userManager, "viewer", "viewer@example.com", "Viewer", "User");
        var candidate = await CreateUserAsync(userManager, "candidate", "candidate@example.com", "Candidate", "User");
        var outsider = await CreateUserAsync(userManager, "outsider", "outsider@example.com", "Out", "Sider");

        var board = new Board
        {
            Title = "Seed board",
            Description = "Seed description",
            ImageName = "seed-board.png",
            CreationDate = DateTime.UtcNow,
            Version = 0,
            BoardOnUsers = new List<BoardOnUser>()
        };
        context.Boards.Add(board);
        await context.SaveChangesAsync();

        context.BoardOnUsers.AddRange(
            new BoardOnUser { BoardId = board.Id, UserId = owner.Id, AddedAt = DateTime.UtcNow, UserRole = UserRoleEnum.OWNER, Version = 0 },
            new BoardOnUser { BoardId = board.Id, UserId = editor.Id, AddedAt = DateTime.UtcNow, UserRole = UserRoleEnum.EDITOR, Version = 0 },
            new BoardOnUser { BoardId = board.Id, UserId = viewer.Id, AddedAt = DateTime.UtcNow, UserRole = UserRoleEnum.VIEWER, Version = 0 }
        );
        await context.SaveChangesAsync();

        var task = new BoardTask
        {
            BoardId = board.Id,
            Title = "Seed task",
            Description = "Task description",
            DeadlineEnd = DateTime.UtcNow.AddDays(1),
            CreationDate = DateTime.UtcNow,
            TaskStatus = TaskStatusEnum.PENDING,
            Version = 0
        };
        context.BoardTasks.Add(task);
        await context.SaveChangesAsync();

        var comment = new Comment
        {
            BoardTaskId = task.Id,
            UserId = viewer.Id,
            Content = "Seed comment",
            CreationDate = DateTime.UtcNow,
            Edited = false,
            Version = 0
        };
        context.Comments.Add(comment);
        await context.SaveChangesAsync();

        var invitation = new Notification
        {
            SenderId = owner.Id,
            SendDate = DateTime.UtcNow,
            NotificationType = NotificationEventTypeEnum.INVITATION,
            InvitationRole = UserRoleEnum.VIEWER,
            SubjectUserId = outsider.Id,
            BoardId = board.Id,
            NotificationsOnUsers = new List<NotificationOnUser>
            {
                new() { UserId = outsider.Id }
            }
        };
        context.Notifications.Add(invitation);
        await context.SaveChangesAsync();

        return new SeedState(owner.Id, editor.Id, viewer.Id, candidate.Id, outsider.Id, board.Id, task.Id, comment.Id, invitation.Id);
    }

    private static async Task<ApplicationUser> CreateUserAsync(UserManager<ApplicationUser> userManager, string userName, string email, string firstName, string lastName)
    {
        var user = new ApplicationUser
        {
            UserName = userName,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            CreationDate = DateTime.UtcNow
        };

        var createResult = await userManager.CreateAsync(user, Password);
        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", createResult.Errors.Select(error => error.Description)));
        }

        return user;
    }
}