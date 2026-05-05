using Microsoft.EntityFrameworkCore;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Common.Enums;
using WorthBoards.IntegrationTests.Infrastructure;

namespace WorthBoards.IntegrationTests.Controllers;

public sealed class BoardTaskControllerTests(CustomWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    // ── GetAllActiveBoardTasks ─────────────────────────────────────────────────

    [Fact]
    public async Task GetAllActiveBoardTasks_WithExistingTasks_ReturnsOkWithTasks()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);
        var board = await CreateBoardAsync();
        await CreateTaskAsync(board.Id, "Task 1");
        await CreateTaskAsync(board.Id, "Task 2");

        // Act
        var response = await Client.GetAsync($"/api/boards/{board.Id}/tasks");

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert – response body
        var body = await response.Content.ReadFromJsonAsync<IEnumerable<BoardTaskResponse>>();
        body.Should().NotBeNull();
        body!.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task GetAllActiveBoardTasks_AsNonMember_ReturnsForbidden()
    {
        // Arrange
        var (_, ownerToken) = await RegisterAndLoginAsync();
        SetAuthToken(ownerToken);
        var board = await CreateBoardAsync();

        var (_, strangerToken) = await RegisterAndLoginAsync();
        SetAuthToken(strangerToken);

        // Act
        var response = await Client.GetAsync($"/api/boards/{board.Id}/tasks");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── GetAllArchivedBoardTasks ──────────────────────────────────────────────

    [Fact]
    public async Task GetAllArchivedBoardTasks_WithArchivedTasks_ReturnsOkWithArchivedTasks()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);
        var board = await CreateBoardAsync();

        var createRequest = new BoardTaskRequest
        {
            Title = "Archived Task",
            Description = "Will be archived",
            TaskStatus = TaskStatusEnum.ARCHIVED
        };
        await Client.PostAsJsonAsync($"/api/boards/{board.Id}/tasks", createRequest);

        // Act
        var response = await Client.GetAsync($"/api/boards/{board.Id}/tasks/archived");

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert – response body
        var body = await response.Content.ReadFromJsonAsync<IEnumerable<BoardTaskResponse>>();
        body.Should().NotBeNull();
        body!.Should().Contain(t => t.TaskStatus == TaskStatusEnum.ARCHIVED);
    }

    [Fact]
    public async Task GetAllArchivedBoardTasks_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);
        var board = await CreateBoardAsync();
        ClearAuthToken();

        // Act
        var response = await Client.GetAsync($"/api/boards/{board.Id}/tasks/archived");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── GetBoardTaskById ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetBoardTaskById_WithExistingTask_ReturnsOkWithTask()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);
        var board = await CreateBoardAsync();
        var task = await CreateTaskAsync(board.Id, "Specific Task");

        // Act
        var response = await Client.GetAsync($"/api/boards/{board.Id}/tasks/{task.Id}");

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert – response body
        var body = await response.Content.ReadFromJsonAsync<BoardTaskResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(task.Id);
        body.Title.Should().Be("Specific Task");
    }

    [Fact]
    public async Task GetBoardTaskById_WithNonExistentTask_ReturnsNotFound()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);
        var board = await CreateBoardAsync();

        // Act
        var response = await Client.GetAsync($"/api/boards/{board.Id}/tasks/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── CreateBoardTask ───────────────────────────────────────────────────────

    [Fact]
    public async Task CreateBoardTask_AsEditor_ReturnsCreatedAndPersistsTask()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);
        var board = await CreateBoardAsync();

        var request = new BoardTaskRequest
        {
            Title = "New Task",
            Description = "Task description",
            TaskStatus = TaskStatusEnum.PENDING,
            DeadlineEnd = DateTime.UtcNow.AddDays(7)
        };

        // Act
        var response = await Client.PostAsJsonAsync($"/api/boards/{board.Id}/tasks", request);

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        // Assert – response body
        var body = await response.Content.ReadFromJsonAsync<BoardTaskResponse>();
        body.Should().NotBeNull();
        body!.Title.Should().Be(request.Title);
        body.BoardId.Should().Be(board.Id);
        body.TaskStatus.Should().Be(TaskStatusEnum.PENDING);

        // Assert – database state
        await using var db = Factory.CreateDbContext();
        var persisted = await db.BoardTasks.AsNoTracking().SingleOrDefaultAsync(t => t.Id == body.Id);
        persisted.Should().NotBeNull();
        persisted!.Title.Should().Be(request.Title);
    }

    [Fact]
    public async Task CreateBoardTask_AsViewer_ReturnsForbidden()
    {
        // Arrange
        var (_, ownerToken) = await RegisterAndLoginAsync();
        SetAuthToken(ownerToken);
        var board = await CreateBoardAsync();

        var (viewerId, viewerToken) = await RegisterAndLoginAsync();
        await LinkUserToBoardDirectlyAsync(board.Id, viewerId, UserRoleEnum.VIEWER);
        SetAuthToken(viewerToken);

        // Act
        var response = await Client.PostAsJsonAsync($"/api/boards/{board.Id}/tasks",
            new BoardTaskRequest { Title = "Unauthorized Task", TaskStatus = TaskStatusEnum.PENDING });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── DeleteBoardTask ───────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteBoardTask_AsEditor_ReturnsNoContentAndRemovesFromDb()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);
        var board = await CreateBoardAsync();
        var task = await CreateTaskAsync(board.Id);

        // Act
        var response = await Client.DeleteAsync($"/api/boards/{board.Id}/tasks/{task.Id}");

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert – database state
        await using var db = Factory.CreateDbContext();
        var persisted = await db.BoardTasks.AsNoTracking().SingleOrDefaultAsync(t => t.Id == task.Id);
        persisted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteBoardTask_AsViewer_ReturnsForbidden()
    {
        // Arrange
        var (_, ownerToken) = await RegisterAndLoginAsync();
        SetAuthToken(ownerToken);
        var board = await CreateBoardAsync();
        var task = await CreateTaskAsync(board.Id);

        var (viewerId, viewerToken) = await RegisterAndLoginAsync();
        await LinkUserToBoardDirectlyAsync(board.Id, viewerId, UserRoleEnum.VIEWER);
        SetAuthToken(viewerToken);

        // Act
        var response = await Client.DeleteAsync($"/api/boards/{board.Id}/tasks/{task.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── DeleteArchivedBoardTasks ──────────────────────────────────────────────

    [Fact]
    public async Task DeleteArchivedBoardTasks_AsEditor_ReturnsNoContentAndRemovesArchivedTasks()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);
        var board = await CreateBoardAsync();

        await Client.PostAsJsonAsync($"/api/boards/{board.Id}/tasks", new BoardTaskRequest
        {
            Title = "Archived1",
            TaskStatus = TaskStatusEnum.ARCHIVED
        });
        await Client.PostAsJsonAsync($"/api/boards/{board.Id}/tasks", new BoardTaskRequest
        {
            Title = "Archived2",
            TaskStatus = TaskStatusEnum.ARCHIVED
        });

        // Act
        var response = await Client.DeleteAsync($"/api/boards/{board.Id}/tasks/archived");

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert – database state
        await using var db = Factory.CreateDbContext();
        var archivedCount = await db.BoardTasks.AsNoTracking()
            .CountAsync(t => t.BoardId == board.Id && t.TaskStatus == TaskStatusEnum.ARCHIVED);
        archivedCount.Should().Be(0);
    }

    [Fact]
    public async Task DeleteArchivedBoardTasks_AsViewer_ReturnsForbidden()
    {
        // Arrange
        var (_, ownerToken) = await RegisterAndLoginAsync();
        SetAuthToken(ownerToken);
        var board = await CreateBoardAsync();

        var (viewerId, viewerToken) = await RegisterAndLoginAsync();
        await LinkUserToBoardDirectlyAsync(board.Id, viewerId, UserRoleEnum.VIEWER);
        SetAuthToken(viewerToken);

        // Act
        var response = await Client.DeleteAsync($"/api/boards/{board.Id}/tasks/archived");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── UpdateBoardTask ───────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateBoardTask_AsEditor_ReturnsOkAndUpdatesDb()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);
        var board = await CreateBoardAsync();
        var task = await CreateTaskAsync(board.Id, "Original");

        var updateRequest = new BoardTaskUpdateRequest
        {
            Title = "Updated Title",
            Description = "Updated description",
            TaskStatus = TaskStatusEnum.IN_PROGRESS
        };

        // Act
        var response = await Client.PutAsJsonAsync($"/api/boards/{board.Id}/tasks/{task.Id}", updateRequest);

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert – response body
        var body = await response.Content.ReadFromJsonAsync<BoardTaskResponse>();
        body!.Title.Should().Be(updateRequest.Title);
        body.TaskStatus.Should().Be(TaskStatusEnum.IN_PROGRESS);

        // Assert – database state
        await using var db = Factory.CreateDbContext();
        var persisted = await db.BoardTasks.AsNoTracking().SingleOrDefaultAsync(t => t.Id == task.Id);
        persisted!.Title.Should().Be(updateRequest.Title);
        persisted.TaskStatus.Should().Be(TaskStatusEnum.IN_PROGRESS);
    }

    [Fact]
    public async Task UpdateBoardTask_WithNonExistentTask_ReturnsNotFound()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);
        var board = await CreateBoardAsync();

        // Act
        var response = await Client.PutAsJsonAsync($"/api/boards/{board.Id}/tasks/99999",
            new BoardTaskUpdateRequest { Title = "Ghost", TaskStatus = TaskStatusEnum.PENDING });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── PatchBoardTask ────────────────────────────────────────────────────────

    [Fact]
    public async Task PatchBoardTask_AsEditor_ReturnsOkAndUpdatesTitle()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);
        var board = await CreateBoardAsync();
        var task = await CreateTaskAsync(board.Id, "Patch Target");

        var patch = new[] { new { op = "replace", path = "/title", value = "Patched Task Title" } };

        // Act
        var response = await Client.PatchAsJsonAsync($"/api/boards/{board.Id}/tasks/{task.Id}", patch);

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert – database state
        await using var db = Factory.CreateDbContext();
        var persisted = await db.BoardTasks.AsNoTracking().SingleOrDefaultAsync(t => t.Id == task.Id);
        persisted!.Title.Should().Be("Patched Task Title");
    }

    [Fact]
    public async Task PatchBoardTask_AsViewer_ReturnsForbidden()
    {
        // Arrange
        var (_, ownerToken) = await RegisterAndLoginAsync();
        SetAuthToken(ownerToken);
        var board = await CreateBoardAsync();
        var task = await CreateTaskAsync(board.Id);

        var (viewerId, viewerToken) = await RegisterAndLoginAsync();
        await LinkUserToBoardDirectlyAsync(board.Id, viewerId, UserRoleEnum.VIEWER);
        SetAuthToken(viewerToken);

        var patch = new[] { new { op = "replace", path = "/title", value = "Hacked" } };

        // Act
        var response = await Client.PatchAsJsonAsync($"/api/boards/{board.Id}/tasks/{task.Id}", patch);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
