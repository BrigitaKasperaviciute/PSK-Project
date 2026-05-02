using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Common.Enums;
using WorthBoards.IntegrationTests.Infrastructure;

namespace WorthBoards.IntegrationTests.Controllers;

public class BoardTaskControllerTests(CustomWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    // ── GET /api/boards/{boardId}/tasks ───────────────────────────────────────

    [Fact]
    public async Task GetAllActiveBoardTasks_BoardMember_ReturnsOkWithTasks()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(token);
        var task = await CreateTaskAsync(board.Id, token);
        SetAuthToken(token);

        // Act
        var response = await Client.GetAsync($"/api/boards/{board.Id}/tasks");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<IEnumerable<BoardTaskResponse>>();
        body.Should().NotBeNull();
        body!.Should().Contain(t => t.Id == task.Id);
    }

    [Fact]
    public async Task GetAllActiveBoardTasks_UnauthenticatedUser_ReturnsUnauthorized()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(token);
        ClearAuthToken();

        // Act
        var response = await Client.GetAsync($"/api/boards/{board.Id}/tasks");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── GET /api/boards/{boardId}/tasks/{taskId} ──────────────────────────────

    [Fact]
    public async Task GetBoardTaskById_ExistingTask_ReturnsOkWithTask()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(token);
        var task = await CreateTaskAsync(board.Id, token);
        SetAuthToken(token);

        // Act
        var response = await Client.GetAsync($"/api/boards/{board.Id}/tasks/{task.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<BoardTaskResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(task.Id);
        body.BoardId.Should().Be(board.Id);
    }

    [Fact]
    public async Task GetBoardTaskById_NonExistingTask_ReturnsNotFound()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(token);
        SetAuthToken(token);

        // Act
        var response = await Client.GetAsync($"/api/boards/{board.Id}/tasks/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── POST /api/boards/{boardId}/tasks ──────────────────────────────────────

    [Fact]
    public async Task CreateBoardTask_EditorUser_ReturnsCreatedAndPersistsToDb()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(token);
        SetAuthToken(token);

        var request = new BoardTaskRequest
        {
            Title = "New Task",
            Description = "Task description",
            TaskStatus = TaskStatusEnum.PENDING
        };

        // Act
        var response = await Client.PostAsJsonAsync($"/api/boards/{board.Id}/tasks", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<BoardTaskResponse>();
        body.Should().NotBeNull();
        body!.Title.Should().Be("New Task");
        body.BoardId.Should().Be(board.Id);

        var dbTask = await QueryAsync(db => db.BoardTasks.FindAsync(body.Id).AsTask());
        dbTask.Should().NotBeNull();
        dbTask!.Title.Should().Be("New Task");
    }

    [Fact]
    public async Task CreateBoardTask_ViewerUser_ReturnsForbidden()
    {
        // Arrange
        var (_, ownerToken) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(ownerToken);

        var (viewerId, viewerToken) = await RegisterAndLoginAsync();
        await LinkUserToBoardDirectlyAsync(board.Id, viewerId, UserRoleEnum.VIEWER);
        SetAuthToken(viewerToken);

        var request = new BoardTaskRequest
        {
            Title = "Viewer Task Attempt",
            TaskStatus = TaskStatusEnum.PENDING
        };

        // Act
        var response = await Client.PostAsJsonAsync($"/api/boards/{board.Id}/tasks", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── PUT /api/boards/{boardId}/tasks/{taskId} ──────────────────────────────

    [Fact]
    public async Task UpdateBoardTask_EditorUser_ReturnsOkWithUpdatedTask()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(token);
        var task = await CreateTaskAsync(board.Id, token);
        SetAuthToken(token);

        var updateRequest = new BoardTaskUpdateRequest
        {
            Title = "Updated Task Title",
            Description = "Updated description",
            TaskStatus = TaskStatusEnum.IN_PROGRESS,
            Version = task.Version
        };

        // Act
        var response = await Client.PutAsJsonAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<BoardTaskResponse>();
        body.Should().NotBeNull();
        body!.Title.Should().Be("Updated Task Title");
        body.TaskStatus.Should().Be(TaskStatusEnum.IN_PROGRESS);
    }

    [Fact]
    public async Task UpdateBoardTask_ViewerUser_ReturnsForbidden()
    {
        // Arrange
        var (_, ownerToken) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(ownerToken);
        var task = await CreateTaskAsync(board.Id, ownerToken);

        var (viewerId, viewerToken) = await RegisterAndLoginAsync();
        await LinkUserToBoardDirectlyAsync(board.Id, viewerId, UserRoleEnum.VIEWER);
        SetAuthToken(viewerToken);

        var updateRequest = new BoardTaskUpdateRequest
        {
            Title = "Viewer Attempt",
            TaskStatus = TaskStatusEnum.IN_PROGRESS,
            Version = task.Version
        };

        // Act
        var response = await Client.PutAsJsonAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── DELETE /api/boards/{boardId}/tasks/{taskId} ───────────────────────────

    [Fact]
    public async Task DeleteBoardTask_EditorUser_ReturnsNoContentAndRemovesFromDb()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(token);
        var task = await CreateTaskAsync(board.Id, token);
        SetAuthToken(token);

        // Act
        var response = await Client.DeleteAsync($"/api/boards/{board.Id}/tasks/{task.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var dbTask = await QueryAsync(db => db.BoardTasks.FindAsync(task.Id).AsTask());
        dbTask.Should().BeNull();
    }

    [Fact]
    public async Task DeleteBoardTask_ViewerUser_ReturnsForbidden()
    {
        // Arrange
        var (_, ownerToken) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(ownerToken);
        var task = await CreateTaskAsync(board.Id, ownerToken);

        var (viewerId, viewerToken) = await RegisterAndLoginAsync();
        await LinkUserToBoardDirectlyAsync(board.Id, viewerId, UserRoleEnum.VIEWER);
        SetAuthToken(viewerToken);

        // Act
        var response = await Client.DeleteAsync($"/api/boards/{board.Id}/tasks/{task.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── GET /api/boards/{boardId}/tasks/archived ──────────────────────────────

    [Fact]
    public async Task GetAllArchivedBoardTasks_BoardMember_ReturnsOkWithArchivedTasks()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(token);
        var task = await CreateTaskAsync(board.Id, token);
        SetAuthToken(token);

        // Archive the task via update
        var updateReq = new BoardTaskUpdateRequest
        {
            Title = task.Title,
            TaskStatus = TaskStatusEnum.ARCHIVED,
            Version = task.Version
        };
        await Client.PutAsJsonAsync($"/api/boards/{board.Id}/tasks/{task.Id}", updateReq);

        // Act
        var response = await Client.GetAsync($"/api/boards/{board.Id}/tasks/archived");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<IEnumerable<BoardTaskResponse>>();
        body.Should().NotBeNull();
        body!.Should().Contain(t => t.Id == task.Id);
    }
}
