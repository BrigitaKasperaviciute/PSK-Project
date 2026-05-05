using Microsoft.EntityFrameworkCore;

namespace WorthBoards.IntegrationTests.Controllers;

[Collection(nameof(ApiCollection))]
[Trait("Category", "Integration")]
public sealed class BoardTaskControllerTests(CustomWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    // ── GET /api/boards/{boardId}/tasks ───────────────────────────────────────

    [Fact]
    public async Task GetAllActiveBoardTasks_AsViewer_ReturnsOkWithTasks()
    {
        // Arrange
        var (_, ownerToken) = await RegisterAndLoginAsync();
        SetAuthToken(ownerToken);
        var board = await CreateBoardAsync();
        await CreateTaskAsync(board.Id, "Task One");
        await CreateTaskAsync(board.Id, "Task Two");

        var (viewerId, viewerToken) = await RegisterAndLoginAsync();
        await LinkUserToBoardDirectlyAsync(board.Id, viewerId, UserRoleEnum.VIEWER);
        SetAuthToken(viewerToken);

        // Act
        var response = await Client.GetAsync($"/api/boards/{board.Id}/tasks");

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<List<BoardTaskResponse>>();
        body.Should().NotBeNull();
        body!.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task GetAllActiveBoardTasks_WithoutBoardMembership_ReturnsForbidden()
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

    // ── GET /api/boards/{boardId}/tasks/archived ──────────────────────────────

    [Fact]
    public async Task GetAllArchivedBoardTasks_AsViewer_ReturnsOkWithArchivedTasks()
    {
        // Arrange – create a task, then update it to ARCHIVED
        var (_, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);
        var board = await CreateBoardAsync();
        var task = await CreateTaskAsync(board.Id, "To Archive");

        // Update task status to ARCHIVED
        var updateRequest = new BoardTaskUpdateRequest
        {
            Title = task.Title,
            TaskStatus = TaskStatusEnum.ARCHIVED,
            Version = task.Version
        };
        await Client.PutAsJsonAsync($"/api/boards/{board.Id}/tasks/{task.Id}", updateRequest);

        // Act
        var response = await Client.GetAsync($"/api/boards/{board.Id}/tasks/archived");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<BoardTaskResponse>>();
        body.Should().NotBeNull();
        body!.Should().Contain(t => t.Id == task.Id && t.TaskStatus == TaskStatusEnum.ARCHIVED);
    }

    [Fact]
    public async Task GetAllArchivedBoardTasks_WithoutBoardMembership_ReturnsForbidden()
    {
        // Arrange
        var (_, ownerToken) = await RegisterAndLoginAsync();
        SetAuthToken(ownerToken);
        var board = await CreateBoardAsync();

        var (_, strangerToken) = await RegisterAndLoginAsync();
        SetAuthToken(strangerToken);

        // Act
        var response = await Client.GetAsync($"/api/boards/{board.Id}/tasks/archived");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── GET /api/boards/{boardId}/tasks/{boardTaskId} ─────────────────────────

    [Fact]
    public async Task GetBoardTaskById_WithExistingTask_ReturnsOkWithTask()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);
        var board = await CreateBoardAsync();
        var task = await CreateTaskAsync(board.Id, "Find Me");

        // Act
        var response = await Client.GetAsync($"/api/boards/{board.Id}/tasks/{task.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<BoardTaskResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(task.Id);
        body.Title.Should().Be("Find Me");
    }

    [Fact]
    public async Task GetBoardTaskById_WithNonexistentTaskId_ReturnsNotFound()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);
        var board = await CreateBoardAsync();

        // Act
        var response = await Client.GetAsync($"/api/boards/{board.Id}/tasks/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── POST /api/boards/{boardId}/tasks ──────────────────────────────────────

    [Fact]
    public async Task CreateBoardTask_AsEditor_ReturnsCreatedAndPersistsTask()
    {
        // Arrange
        var (_, ownerToken) = await RegisterAndLoginAsync();
        SetAuthToken(ownerToken);
        var board = await CreateBoardAsync();

        var (editorId, editorToken) = await RegisterAndLoginAsync();
        await LinkUserToBoardDirectlyAsync(board.Id, editorId, UserRoleEnum.EDITOR);
        SetAuthToken(editorToken);

        var request = new BoardTaskRequest
        {
            Title = "New Task",
            Description = "Task description",
            TaskStatus = TaskStatusEnum.PENDING
        };

        // Act
        var response = await Client.PostAsJsonAsync($"/api/boards/{board.Id}/tasks", request);

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<BoardTaskResponse>();
        body.Should().NotBeNull();
        body!.Title.Should().Be("New Task");
        body.BoardId.Should().Be(board.Id);
        body.TaskStatus.Should().Be(TaskStatusEnum.PENDING);

        // Assert – database state
        await using var db = CreateDbContext();
        var persisted = await db.BoardTasks.AsNoTracking()
            .SingleOrDefaultAsync(t => t.Id == body.Id);
        persisted.Should().NotBeNull();
        persisted!.Title.Should().Be("New Task");
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

        var request = new BoardTaskRequest
        {
            Title = "Viewer Task",
            TaskStatus = TaskStatusEnum.PENDING
        };

        // Act
        var response = await Client.PostAsJsonAsync($"/api/boards/{board.Id}/tasks", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── DELETE /api/boards/{boardId}/tasks/{boardTaskId} ──────────────────────

    [Fact]
    public async Task DeleteBoardTask_AsEditor_ReturnsNoContentAndRemovesTask()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);
        var board = await CreateBoardAsync();
        var task = await CreateTaskAsync(board.Id, "Delete Me");

        // Act
        var response = await Client.DeleteAsync($"/api/boards/{board.Id}/tasks/{task.Id}");

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert – database state
        await using var db = CreateDbContext();
        var deleted = await db.BoardTasks.AsNoTracking()
            .SingleOrDefaultAsync(t => t.Id == task.Id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteBoardTask_AsViewer_ReturnsForbidden()
    {
        // Arrange
        var (_, ownerToken) = await RegisterAndLoginAsync();
        SetAuthToken(ownerToken);
        var board = await CreateBoardAsync();
        var task = await CreateTaskAsync(board.Id, "Protected Task");

        var (viewerId, viewerToken) = await RegisterAndLoginAsync();
        await LinkUserToBoardDirectlyAsync(board.Id, viewerId, UserRoleEnum.VIEWER);
        SetAuthToken(viewerToken);

        // Act
        var response = await Client.DeleteAsync($"/api/boards/{board.Id}/tasks/{task.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Assert – task still exists
        await using var db = CreateDbContext();
        var still = await db.BoardTasks.AsNoTracking()
            .SingleOrDefaultAsync(t => t.Id == task.Id);
        still.Should().NotBeNull();
    }

    // ── PUT /api/boards/{boardId}/tasks/{boardTaskId} ─────────────────────────

    [Fact]
    public async Task UpdateBoardTask_AsEditor_ReturnsOkWithUpdatedTask()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);
        var board = await CreateBoardAsync();
        var task = await CreateTaskAsync(board.Id, "Original Title");

        var updateRequest = new BoardTaskUpdateRequest
        {
            Title = "Updated Title",
            TaskStatus = TaskStatusEnum.IN_PROGRESS,
            Version = task.Version
        };

        // Act
        var response = await Client.PutAsJsonAsync($"/api/boards/{board.Id}/tasks/{task.Id}", updateRequest);

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<BoardTaskResponse>();
        body.Should().NotBeNull();
        body!.Title.Should().Be("Updated Title");
        body.TaskStatus.Should().Be(TaskStatusEnum.IN_PROGRESS);

        // Assert – database state
        await using var db = CreateDbContext();
        var updated = await db.BoardTasks.AsNoTracking()
            .SingleOrDefaultAsync(t => t.Id == task.Id);
        updated!.Title.Should().Be("Updated Title");
    }

    [Fact]
    public async Task UpdateBoardTask_WithStatusChange_CreatesNotificationForOtherBoardMembers()
    {
        // Arrange – owner + editor on the board, editor changes task status
        var (ownerId, ownerToken) = await RegisterAndLoginAsync();
        SetAuthToken(ownerToken);
        var board = await CreateBoardAsync();
        var task = await CreateTaskAsync(board.Id, "Status Change Task");

        var (editorId, editorToken) = await RegisterAndLoginAsync();
        await LinkUserToBoardDirectlyAsync(board.Id, editorId, UserRoleEnum.EDITOR);
        SetAuthToken(editorToken);

        var updateRequest = new BoardTaskUpdateRequest
        {
            Title = task.Title,
            TaskStatus = TaskStatusEnum.COMPLETED,
            Version = task.Version
        };

        // Act
        var response = await Client.PutAsJsonAsync($"/api/boards/{board.Id}/tasks/{task.Id}", updateRequest);

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert – a notification was created for the owner (who is not the one making the change)
        await using var db = CreateDbContext();
        var notification = await db.Notifications.AsNoTracking()
            .FirstOrDefaultAsync(n => n.TaskId == task.Id &&
                                       n.NotificationType == NotificationEventTypeEnum.TASK_STATUS_CHANGE);
        notification.Should().NotBeNull();
    }

    // ── DELETE /api/boards/{boardId}/tasks/archived ───────────────────────────

    [Fact]
    public async Task DeleteArchivedBoardTasks_AsEditor_ReturnsNoContentAndRemovesArchived()
    {
        // Arrange – create a task and archive it
        var (_, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);
        var board = await CreateBoardAsync();
        var task = await CreateTaskAsync(board.Id, "Archive Me");

        var archiveRequest = new BoardTaskUpdateRequest
        {
            Title = task.Title,
            TaskStatus = TaskStatusEnum.ARCHIVED,
            Version = task.Version
        };
        await Client.PutAsJsonAsync($"/api/boards/{board.Id}/tasks/{task.Id}", archiveRequest);

        // Act
        var response = await Client.DeleteAsync($"/api/boards/{board.Id}/tasks/archived");

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert – archived task is gone
        await using var db = CreateDbContext();
        var archived = await db.BoardTasks.AsNoTracking()
            .Where(t => t.BoardId == board.Id && t.TaskStatus == TaskStatusEnum.ARCHIVED)
            .ToListAsync();
        archived.Should().BeEmpty();
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
}
