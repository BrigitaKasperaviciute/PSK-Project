using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WorthBoards.Common.Enums;
using WorthBoards.Data.Database;
using WorthBoards.Data.Identity;
using WorthBoards.Domain.Entities;

namespace WorthBoards.IntegrationTests.Infrastructure;

/// <summary>
/// Test helper for common database operations and test setup.
/// </summary>
internal sealed class TestDataHelper
{
    private readonly ApplicationDbContext _dbContext;

    public TestDataHelper(ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Creates a user with the specified role and persists it to the database.
    /// </summary>
    public async Task<(ApplicationUser user, string password)> CreateUserAsync(
        string? userName = null,
        string? email = null,
        string firstName = "Test",
        string lastName = "User")
    {
        var password = "TestPassword123!";
        var user = new ApplicationUserBuilder()
            .WithFirstName(firstName)
            .WithLastName(lastName);

        if (!string.IsNullOrEmpty(userName))
            user.WithUserName(userName);
        if (!string.IsNullOrEmpty(email))
            user.WithEmail(email);

        var builtUser = user.Build();
        _dbContext.Users.Add(builtUser);
        await _dbContext.SaveChangesAsync();

        return (builtUser, password);
    }

    /// <summary>
    /// Creates a board and assigns the user as owner.
    /// </summary>
    public async Task<Board> CreateBoardAsync(
        int ownerId,
        string? title = null,
        string? description = null)
    {
        var board = new BoardBuilder()
            .WithTitle(title ?? "Test Board")
            .WithDescription(description ?? "Test Description")
            .Build();

        _dbContext.Boards.Add(board);
        await _dbContext.SaveChangesAsync();

        // Add owner to board
        var boardOnUser = new BoardOnUserBuilder()
            .WithBoardId(board.Id)
            .WithUserId(ownerId)
            .WithRole(UserRoleEnum.OWNER)
            .Build();

        _dbContext.BoardOnUsers.Add(boardOnUser);
        await _dbContext.SaveChangesAsync();

        return board;
    }

    /// <summary>
    /// Adds a user to a board with the specified role.
    /// </summary>
    public async Task AddUserToBoardAsync(int boardId, int userId, UserRoleEnum role)
    {
        var existing = await _dbContext.BoardOnUsers
            .SingleOrDefaultAsync(x => x.BoardId == boardId && x.UserId == userId);

        if (existing is null)
        {
            var boardOnUser = new BoardOnUserBuilder()
                .WithBoardId(boardId)
                .WithUserId(userId)
                .WithRole(role)
                .Build();

            _dbContext.BoardOnUsers.Add(boardOnUser);
        }
        else
        {
            existing.UserRole = role;
        }

        await _dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Creates a task on the specified board.
    /// </summary>
    public async Task<BoardTask> CreateBoardTaskAsync(
        int boardId,
        string? title = null,
        TaskStatusEnum status = TaskStatusEnum.PENDING,
        string? description = null,
        DateTime? deadline = null)
    {
        var task = new BoardTaskBuilder()
            .WithBoardId(boardId)
            .WithTitle(title ?? "Test Task")
            .WithDescription(description)
            .WithStatus(status)
            .WithDeadlineEnd(deadline)
            .Build();

        _dbContext.BoardTasks.Add(task);
        await _dbContext.SaveChangesAsync();

        return task;
    }

    /// <summary>
    /// Creates a comment on the specified task.
    /// </summary>
    public async Task<Comment> CreateCommentAsync(
        int boardTaskId,
        int userId,
        string? content = null)
    {
        var comment = new CommentBuilder()
            .WithBoardTaskId(boardTaskId)
            .WithUserId(userId)
            .WithContent(content ?? "Test comment")
            .Build();

        _dbContext.Comments.Add(comment);
        await _dbContext.SaveChangesAsync();

        return comment;
    }

    /// <summary>
    /// Assigns a user to a task.
    /// </summary>
    public async Task AssignUserToTaskAsync(int boardTaskId, int userId)
    {
        var taskOnUser = new TaskOnUserBuilder()
            .WithBoardTaskId(boardTaskId)
            .WithUserId(userId)
            .Build();

        _dbContext.TasksOnUsers.Add(taskOnUser);
        await _dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Gets all users assigned to a task.
    /// </summary>
    public IEnumerable<int> GetUsersAssignedToTask(int boardTaskId)
    {
        return _dbContext.TasksOnUsers
            .Where(t => t.BoardTaskId == boardTaskId)
            .Select(t => t.UserId)
            .ToList();
    }

    /// <summary>
    /// Gets the user's role on a board.
    /// </summary>
    public UserRoleEnum? GetUserBoardRole(int boardId, int userId)
    {
        return _dbContext.BoardOnUsers
            .Where(b => b.BoardId == boardId && b.UserId == userId)
            .Select(b => b.UserRole)
            .FirstOrDefault();
    }
}
