using System.Net;
using System.Net.Http.Json;
using WorthBoards.Business.Dtos.Identity;
using WorthBoards.Data.Identity;
using WorthBoards.Domain.Entities;
using Xunit;

namespace WorthBoards.Api.IntegrationTests.Controllers;

public class TaskOnUserControllerTests : IntegrationTestBase
{
    public TaskOnUserControllerTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task GetUsersLinkedToTask_WhenTaskExists_ReturnsUsers()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = 1,
            FirstName = "Test",
            LastName = "User",
            UserName = "testuser",
            Email = "test@example.com",
            CreationDate = DateTime.UtcNow
        };
        DbContext.Users.Add(user);

        var board = new Board
        {
            Id = 1,
            Title = "Test Board",
            Description = "Test Description",
            ImageName = "test.png",
            CreationDate = DateTime.UtcNow
        };
        DbContext.Boards.Add(board);

        var boardOnUser = new BoardOnUser
        {
            BoardId = 1,
            UserId = 1,
            UserRole = Common.Enums.UserRoleEnum.VIEWER,
            AddedAt = DateTime.UtcNow
        };
        DbContext.BoardOnUsers.Add(boardOnUser);

        var task = new BoardTask
        {
            Id = 1,
            BoardId = 1,
            Title = "Test Task",
            Description = "Test Task Description",
            TaskStatus = Common.Enums.TaskStatusEnum.PENDING,
            CreationDate = DateTime.UtcNow
        };
        DbContext.BoardTasks.Add(task);

        var taskOnUser = new TaskOnUser
        {
            BoardTaskId = 1,
            UserId = 1
        };
        DbContext.TasksOnUsers.Add(taskOnUser);

        await DbContext.SaveChangesAsync();

        var client = CreateAuthenticatedClient("1", "test@example.com");

        // Act
        var response = await client.GetAsync("/api/boards/1/tasks/1/users");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<IEnumerable<UserResponse>>();
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task GetUsersLinkedToTask_WhenTaskDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = 1,
            FirstName = "Test",
            LastName = "User",
            UserName = "testuser",
            Email = "test@example.com",
            CreationDate = DateTime.UtcNow
        };
        DbContext.Users.Add(user);

        var board = new Board
        {
            Id = 1,
            Title = "Test Board",
            Description = "Test Description",
            ImageName = "test.png",
            CreationDate = DateTime.UtcNow
        };
        DbContext.Boards.Add(board);

        var boardOnUser = new BoardOnUser
        {
            BoardId = 1,
            UserId = 1,
            UserRole = Common.Enums.UserRoleEnum.VIEWER,
            AddedAt = DateTime.UtcNow
        };
        DbContext.BoardOnUsers.Add(boardOnUser);

        await DbContext.SaveChangesAsync();

        var client = CreateAuthenticatedClient("1", "test@example.com");

        // Act
        var response = await client.GetAsync("/api/boards/1/tasks/999/users");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
