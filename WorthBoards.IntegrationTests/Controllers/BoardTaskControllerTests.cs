using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Common.Enums;
using WorthBoards.Common.Exceptions;
using WorthBoards.IntegrationTests.Infrastructure;
using Xunit;

namespace WorthBoards.IntegrationTests.Controllers;

[Collection(nameof(ApiCollection))]
[Trait("Category", "Integration")]
public sealed class BoardTaskControllerTests : IAsyncLifetime
{
    private readonly ApiFactory _factory;

    public BoardTaskControllerTests(ApiFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        await using var db = _factory.CreateDbContext();
        await DatabaseSeeder.CleanDatabaseAsync(db);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ---------------------------------------------------------------------------
    // GET /api/boards/{boardId}/tasks  [AuthorizeRole(VIEWER)]
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetAllActiveBoardTasks_AsViewer_ReturnsActiveTasks()
    {
        // Arrange - board with two active tasks and one archived task
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var user = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync();
        await seeder.LinkUserToBoardAsync(board.Id, user.Id, UserRoleEnum.VIEWER);
        await seeder.CreateBoardTaskAsync(board.Id, "Pending Task", TaskStatusEnum.PENDING);
        await seeder.CreateBoardTaskAsync(board.Id, "In Progress Task", TaskStatusEnum.IN_PROGRESS);
        await seeder.CreateBoardTaskAsync(board.Id, "Archived Task", TaskStatusEnum.ARCHIVED);

        var client = _factory.CreateAuthenticatedClient(user.Id, user.UserName!, user.Email!);

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}/tasks");

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - response body: only active tasks, not archived
        var tasks = await response.Content.ReadFromJsonAsync<IEnumerable<BoardTaskResponse>>();
        tasks.Should().NotBeNull();
        tasks!.Should().HaveCount(2);
        tasks.Should().NotContain(t => t.TaskStatus == TaskStatusEnum.ARCHIVED);
    }

    [Fact]
    public async Task GetAllActiveBoardTasks_AsViewer_WhenNoTasks_ReturnsEmptyList()
    {
        // Arrange - board with no tasks
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var user = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync();
        await seeder.LinkUserToBoardAsync(board.Id, user.Id, UserRoleEnum.VIEWER);

        var client = _factory.CreateAuthenticatedClient(user.Id, user.UserName!, user.Email!);

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}/tasks");

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - empty collection
        var tasks = await response.Content.ReadFromJsonAsync<IEnumerable<BoardTaskResponse>>();
        tasks.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public async Task GetAllActiveBoardTasks_AsViewer_WhenNotBoardMember_ReturnsForbidden()
    {
        // Arrange - user has no board membership
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var nonMember = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync();

        var client = _factory.CreateAuthenticatedClient(nonMember.Id, nonMember.UserName!, nonMember.Email!);

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}/tasks");

        // Assert - not a board member, PermissionHandler fails
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---------------------------------------------------------------------------
    // GET /api/boards/{boardId}/tasks/archived  [AuthorizeRole(VIEWER)]
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetAllArchivedBoardTasks_AsViewer_ReturnsOnlyArchivedTasks()
    {
        // Arrange
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var user = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync();
        await seeder.LinkUserToBoardAsync(board.Id, user.Id, UserRoleEnum.VIEWER);
        await seeder.CreateBoardTaskAsync(board.Id, "Active Task", TaskStatusEnum.PENDING);
        await seeder.CreateBoardTaskAsync(board.Id, "Archived Task", TaskStatusEnum.ARCHIVED);

        var client = _factory.CreateAuthenticatedClient(user.Id, user.UserName!, user.Email!);

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}/tasks/archived");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var tasks = await response.Content.ReadFromJsonAsync<IEnumerable<BoardTaskResponse>>();
        tasks.Should().NotBeNull();
        tasks!.Should().HaveCount(1);
        tasks.Should().OnlyContain(t => t.TaskStatus == TaskStatusEnum.ARCHIVED);
    }

    // ---------------------------------------------------------------------------
    // GET /api/boards/{boardId}/tasks/{boardTaskId}  [AuthorizeRole(VIEWER)]
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetBoardTaskById_WithExistingTask_ReturnsTask()
    {
        // Arrange
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var user = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync();
        await seeder.LinkUserToBoardAsync(board.Id, user.Id, UserRoleEnum.VIEWER);
        var task = await seeder.CreateBoardTaskAsync(board.Id, "My Task");

        var client = _factory.CreateAuthenticatedClient(user.Id, user.UserName!, user.Email!);

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}/tasks/{task.Id}");

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - response body
        var body = await response.Content.ReadFromJsonAsync<BoardTaskResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(task.Id);
        body.Title.Should().Be("My Task");
        body.BoardId.Should().Be(board.Id);
    }

    [Fact]
    public async Task GetBoardTaskById_WithNonExistentTask_ReturnsNotFound()
    {
        // Arrange
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var user = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync();
        await seeder.LinkUserToBoardAsync(board.Id, user.Id, UserRoleEnum.VIEWER);

        var client = _factory.CreateAuthenticatedClient(user.Id, user.UserName!, user.Email!);

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}/tasks/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------------------------------------------------------------------------
    // POST /api/boards/{boardId}/tasks  [AuthorizeRole(EDITOR)]
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task CreateBoardTask_AsEditor_ReturnsCreatedAndPersistsTask()
    {
        // Arrange
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var editor = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync();
        await seeder.LinkUserToBoardAsync(board.Id, editor.Id, UserRoleEnum.EDITOR);

        var client = _factory.CreateAuthenticatedClient(editor.Id, editor.UserName!, editor.Email!);

        var request = new BoardTaskRequest
        {
            Title = "New Task",
            Description = "Task description",
            TaskStatus = TaskStatusEnum.PENDING,
            DeadlineEnd = DateTime.UtcNow.AddDays(7)
        };

        // Act
        var response = await client.PostAsJsonAsync($"/api/boards/{board.Id}/tasks", request);

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        // Assert - response body
        var body = await response.Content.ReadFromJsonAsync<BoardTaskResponse>();
        body.Should().NotBeNull();
        body!.Title.Should().Be("New Task");
        body.BoardId.Should().Be(board.Id);
        body.TaskStatus.Should().Be(TaskStatusEnum.PENDING);
        body.Id.Should().BeGreaterThan(0);

        // Assert - database state
        await using var db = _factory.CreateDbContext();
        var persisted = await db.BoardTasks.AsNoTracking()
            .SingleOrDefaultAsync(t => t.Id == body.Id);
        persisted.Should().NotBeNull();
        persisted!.Title.Should().Be("New Task");
        persisted.BoardId.Should().Be(board.Id);
    }

    [Fact]
    public async Task CreateBoardTask_AsViewer_ReturnsForbidden()
    {
        // Arrange
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var viewer = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync();
        await seeder.LinkUserToBoardAsync(board.Id, viewer.Id, UserRoleEnum.VIEWER);

        var client = _factory.CreateAuthenticatedClient(viewer.Id, viewer.UserName!, viewer.Email!);

        var request = new BoardTaskRequest
        {
            Title = "Forbidden Task",
            TaskStatus = TaskStatusEnum.PENDING
        };

        // Act
        var response = await client.PostAsJsonAsync($"/api/boards/{board.Id}/tasks", request);

        // Assert - viewer role (2) > required EDITOR role (1)
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---------------------------------------------------------------------------
    // DELETE /api/boards/{boardId}/tasks/{boardTaskId}  [AuthorizeRole(EDITOR)]
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task DeleteBoardTask_AsEditor_ReturnsNoContentAndRemovesTask()
    {
        // Arrange
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var editor = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync();
        await seeder.LinkUserToBoardAsync(board.Id, editor.Id, UserRoleEnum.EDITOR);
        var task = await seeder.CreateBoardTaskAsync(board.Id, "Task to Delete");

        var client = _factory.CreateAuthenticatedClient(editor.Id, editor.UserName!, editor.Email!);

        // Act
        var response = await client.DeleteAsync($"/api/boards/{board.Id}/tasks/{task.Id}");

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert - database state
        await using var db = _factory.CreateDbContext();
        var deleted = await db.BoardTasks.AsNoTracking()
            .SingleOrDefaultAsync(t => t.Id == task.Id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteBoardTask_AsViewer_ReturnsForbidden()
    {
        // Arrange
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var viewer = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync();
        await seeder.LinkUserToBoardAsync(board.Id, viewer.Id, UserRoleEnum.VIEWER);
        var task = await seeder.CreateBoardTaskAsync(board.Id, "Protected Task");

        var client = _factory.CreateAuthenticatedClient(viewer.Id, viewer.UserName!, viewer.Email!);

        // Act
        var response = await client.DeleteAsync($"/api/boards/{board.Id}/tasks/{task.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Assert - task still exists
        await using var db = _factory.CreateDbContext();
        var stillExists = await db.BoardTasks.AsNoTracking()
            .AnyAsync(t => t.Id == task.Id);
        stillExists.Should().BeTrue();
    }

    // ---------------------------------------------------------------------------
    // PUT /api/boards/{boardId}/tasks/{boardTaskId}  [AuthorizeRole(EDITOR)]
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task UpdateBoardTask_AsEditor_ReturnsOkWithUpdatedTask()
    {
        // Arrange
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var editor = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync();
        await seeder.LinkUserToBoardAsync(board.Id, editor.Id, UserRoleEnum.EDITOR);
        var task = await seeder.CreateBoardTaskAsync(board.Id, "Original Title");

        var client = _factory.CreateAuthenticatedClient(editor.Id, editor.UserName!, editor.Email!);

        // Fetch current version for optimistic concurrency
        var getResponse = await client.GetAsync($"/api/boards/{board.Id}/tasks/{task.Id}");
        var currentTask = await getResponse.Content.ReadFromJsonAsync<BoardTaskResponse>();

        var request = new BoardTaskUpdateRequest
        {
            Title = "Updated Title",
            Description = "Updated description",
            TaskStatus = TaskStatusEnum.IN_PROGRESS,
            Version = currentTask!.Version
        };

        // Act
        var response = await client.PutAsJsonAsync($"/api/boards/{board.Id}/tasks/{task.Id}", request);

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - response body
        var body = await response.Content.ReadFromJsonAsync<BoardTaskResponse>();
        body.Should().NotBeNull();
        body!.Title.Should().Be("Updated Title");
        body.TaskStatus.Should().Be(TaskStatusEnum.IN_PROGRESS);

        // Assert - database state
        await using var db = _factory.CreateDbContext();
        var persisted = await db.BoardTasks.AsNoTracking().SingleAsync(t => t.Id == task.Id);
        persisted.Title.Should().Be("Updated Title");
        persisted.TaskStatus.Should().Be(TaskStatusEnum.IN_PROGRESS);
    }

    [Fact]
    public async Task UpdateBoardTask_AsViewer_ReturnsForbidden()
    {
        // Arrange
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var viewer = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync();
        await seeder.LinkUserToBoardAsync(board.Id, viewer.Id, UserRoleEnum.VIEWER);
        var task = await seeder.CreateBoardTaskAsync(board.Id, "Protected Task");

        var client = _factory.CreateAuthenticatedClient(viewer.Id, viewer.UserName!, viewer.Email!);

        var request = new BoardTaskUpdateRequest
        {
            Title = "Should Not Update",
            TaskStatus = TaskStatusEnum.IN_PROGRESS,
            Version = 0
        };

        // Act
        var response = await client.PutAsJsonAsync($"/api/boards/{board.Id}/tasks/{task.Id}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---------------------------------------------------------------------------
    // DELETE /api/boards/{boardId}/tasks/archived  [AuthorizeRole(EDITOR)]
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task DeleteArchivedBoardTasks_AsEditor_ReturnsNoContentAndClearsArchive()
    {
        // Arrange
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var editor = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync();
        await seeder.LinkUserToBoardAsync(board.Id, editor.Id, UserRoleEnum.EDITOR);
        await seeder.CreateBoardTaskAsync(board.Id, "Active Task", TaskStatusEnum.PENDING);
        await seeder.CreateBoardTaskAsync(board.Id, "Archived Task 1", TaskStatusEnum.ARCHIVED);
        await seeder.CreateBoardTaskAsync(board.Id, "Archived Task 2", TaskStatusEnum.ARCHIVED);

        var client = _factory.CreateAuthenticatedClient(editor.Id, editor.UserName!, editor.Email!);

        // Act
        var response = await client.DeleteAsync($"/api/boards/{board.Id}/tasks/archived");

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert - only archived tasks were removed
        await using var db = _factory.CreateDbContext();
        var remaining = await db.BoardTasks.AsNoTracking()
            .Where(t => t.BoardId == board.Id)
            .ToListAsync();
        remaining.Should().HaveCount(1);
        remaining.Should().OnlyContain(t => t.TaskStatus != TaskStatusEnum.ARCHIVED);
    }

    [Fact]
    public async Task DeleteArchivedBoardTasks_AsViewer_ReturnsForbidden()
    {
        // Arrange
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var viewer = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync();
        await seeder.LinkUserToBoardAsync(board.Id, viewer.Id, UserRoleEnum.VIEWER);
        await seeder.CreateBoardTaskAsync(board.Id, "Archived Task", TaskStatusEnum.ARCHIVED);

        var client = _factory.CreateAuthenticatedClient(viewer.Id, viewer.UserName!, viewer.Email!);

        // Act
        var response = await client.DeleteAsync($"/api/boards/{board.Id}/tasks/archived");

        // Assert - viewer role (2) > required EDITOR role (1)
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Assert - archived task still exists
        await using var db = _factory.CreateDbContext();
        var archivedCount = await db.BoardTasks.AsNoTracking()
            .CountAsync(t => t.BoardId == board.Id && t.TaskStatus == TaskStatusEnum.ARCHIVED);
        archivedCount.Should().Be(1);
    }

    // ---------------------------------------------------------------------------
    // GET /api/boards/{boardId}/tasks/archived — negative
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetAllArchivedBoardTasks_WhenNotBoardMember_ReturnsForbidden()
    {
        // Arrange
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var nonMember = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync();

        var client = _factory.CreateAuthenticatedClient(nonMember.Id, nonMember.UserName!, nonMember.Email!);

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}/tasks/archived");

        // Assert - not a board member, PermissionHandler fails
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---------------------------------------------------------------------------
    // PATCH /api/boards/{boardId}/tasks/{boardTaskId}  [AuthorizeRole(EDITOR)]
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task PatchBoardTask_AsEditor_ReturnsOkWithPatchedTitle()
    {
        // Arrange
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var editor = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync();
        await seeder.LinkUserToBoardAsync(board.Id, editor.Id, UserRoleEnum.EDITOR);
        var task = await seeder.CreateBoardTaskAsync(board.Id, "Original Task Title");

        var client = _factory.CreateAuthenticatedClient(editor.Id, editor.UserName!, editor.Email!);

        // Fetch current task to get version for optimistic concurrency
        var getResponse = await client.GetAsync($"/api/boards/{board.Id}/tasks/{task.Id}");
        var current = await getResponse.Content.ReadFromJsonAsync<BoardTaskResponse>();

        var patchDoc = new object[]
        {
            new { op = "replace", path = "/title", value = (object)"Patched Task Title" },
            new { op = "replace", path = "/version", value = (object)current!.Version }
        };
        var content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(patchDoc),
            System.Text.Encoding.UTF8,
            "application/json-patch+json");

        // Act
        var response = await client.PatchAsync($"/api/boards/{board.Id}/tasks/{task.Id}", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<BoardTaskResponse>();
        body!.Title.Should().Be("Patched Task Title");
    }
}
