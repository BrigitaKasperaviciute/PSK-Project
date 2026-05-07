using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Common.Enums;
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

    private async Task<(int UserId, HttpClient Client)> CreateUserWithClientAsync(string? username = null, string? email = null)
    {
        await using var db = _factory.CreateDbContext();
        var userManager = _factory.Services.CreateScope().ServiceProvider
            .GetRequiredService<UserManager<Data.Identity.ApplicationUser>>();
        var user = await DatabaseSeeder.CreateUserAsync(db, userManager, username, email);
        return (user.Id, _factory.CreateAuthenticatedClient(user.Id, user.UserName!, user.Email!));
    }

    // --- GET /api/boards/{boardId}/tasks ---

    [Fact]
    public async Task GetAllActiveBoardTasks_AsViewer_ReturnsOkWithTasks()
    {
        // Arrange
        var (userId, client) = await CreateUserWithClientAsync();
        await using var db = _factory.CreateDbContext();
        var board = await DatabaseSeeder.CreateBoardAsync(db, userId, UserRoleEnum.VIEWER);
        await DatabaseSeeder.CreateTaskAsync(db, board.Id, "Task 1", TaskStatusEnum.PENDING);
        await DatabaseSeeder.CreateTaskAsync(db, board.Id, "Task 2", TaskStatusEnum.IN_PROGRESS);
        await DatabaseSeeder.CreateTaskAsync(db, board.Id, "Archived Task", TaskStatusEnum.ARCHIVED);

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}/tasks");

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - response body: only active tasks (not archived)
        var body = await response.Content.ReadFromJsonAsync<IEnumerable<BoardTaskResponse>>();
        body.Should().NotBeNull();
        body.Should().HaveCount(2);
        body.Should().NotContain(t => t.TaskStatus == TaskStatusEnum.ARCHIVED);
    }

    [Fact]
    public async Task GetAllActiveBoardTasks_WhenNotOnBoard_ReturnsForbidden()
    {
        // Arrange - user is authenticated but not linked to the board
        var (ownerId, _) = await CreateUserWithClientAsync("owner_task", "owner_task@test.com");
        var (otherId, otherClient) = await CreateUserWithClientAsync("other_task", "other_task@test.com");
        await using var db = _factory.CreateDbContext();
        var board = await DatabaseSeeder.CreateBoardAsync(db, ownerId, UserRoleEnum.OWNER);

        // Act
        var response = await otherClient.GetAsync($"/api/boards/{board.Id}/tasks");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // --- GET /api/boards/{boardId}/tasks/archived ---

    [Fact]
    public async Task GetAllArchivedBoardTasks_AsViewer_ReturnsOnlyArchivedTasks()
    {
        // Arrange
        var (userId, client) = await CreateUserWithClientAsync();
        await using var db = _factory.CreateDbContext();
        var board = await DatabaseSeeder.CreateBoardAsync(db, userId, UserRoleEnum.VIEWER);
        await DatabaseSeeder.CreateTaskAsync(db, board.Id, "Active Task", TaskStatusEnum.PENDING);
        await DatabaseSeeder.CreateTaskAsync(db, board.Id, "Archived Task", TaskStatusEnum.ARCHIVED);

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}/tasks/archived");

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - response body: only archived tasks
        var body = await response.Content.ReadFromJsonAsync<IEnumerable<BoardTaskResponse>>();
        body.Should().NotBeNull();
        body.Should().HaveCount(1);
        body!.Single().TaskStatus.Should().Be(TaskStatusEnum.ARCHIVED);
    }

    [Fact]
    public async Task GetAllArchivedBoardTasks_WhenNotOnBoard_ReturnsForbidden()
    {
        // Arrange
        var (ownerId, _) = await CreateUserWithClientAsync("arch_owner", "arch_owner@test.com");
        var (otherId, otherClient) = await CreateUserWithClientAsync("arch_other", "arch_other@test.com");
        await using var db = _factory.CreateDbContext();
        var board = await DatabaseSeeder.CreateBoardAsync(db, ownerId, UserRoleEnum.OWNER);
        await DatabaseSeeder.CreateTaskAsync(db, board.Id, "Archived Task", TaskStatusEnum.ARCHIVED);

        // Act
        var response = await otherClient.GetAsync($"/api/boards/{board.Id}/tasks/archived");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // --- GET /api/boards/{boardId}/tasks/{boardTaskId} ---

    [Fact]
    public async Task GetBoardTaskById_WhenTaskExists_ReturnsOkWithTask()
    {
        // Arrange
        var (userId, client) = await CreateUserWithClientAsync();
        await using var db = _factory.CreateDbContext();
        var board = await DatabaseSeeder.CreateBoardAsync(db, userId, UserRoleEnum.VIEWER);
        var task = await DatabaseSeeder.CreateTaskAsync(db, board.Id, "Specific Task");

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}/tasks/{task.Id}");

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - response body
        var body = await response.Content.ReadFromJsonAsync<BoardTaskResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(task.Id);
        body.Title.Should().Be("Specific Task");
        body.BoardId.Should().Be(board.Id);
    }

    [Fact]
    public async Task GetBoardTaskById_WhenTaskNotFound_ReturnsNotFound()
    {
        // Arrange
        var (userId, client) = await CreateUserWithClientAsync();
        await using var db = _factory.CreateDbContext();
        var board = await DatabaseSeeder.CreateBoardAsync(db, userId, UserRoleEnum.VIEWER);

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}/tasks/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- POST /api/boards/{boardId}/tasks ---

    [Fact]
    public async Task CreateBoardTask_AsEditor_ReturnsCreatedAndPersistsTask()
    {
        // Arrange
        var (userId, client) = await CreateUserWithClientAsync();
        await using var db = _factory.CreateDbContext();
        var board = await DatabaseSeeder.CreateBoardAsync(db, userId, UserRoleEnum.EDITOR);

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

        // Assert - database state
        await using var verifyDb = _factory.CreateDbContext();
        var persisted = await verifyDb.BoardTasks.AsNoTracking().SingleOrDefaultAsync(t => t.Id == body.Id);
        persisted.Should().NotBeNull();
        persisted!.Title.Should().Be("New Task");
    }

    [Fact]
    public async Task CreateBoardTask_AsViewer_ReturnsForbidden()
    {
        // Arrange
        var (userId, client) = await CreateUserWithClientAsync();
        await using var db = _factory.CreateDbContext();
        var board = await DatabaseSeeder.CreateBoardAsync(db, userId, UserRoleEnum.VIEWER);

        var request = new BoardTaskRequest
        {
            Title = "Forbidden Task",
            TaskStatus = TaskStatusEnum.PENDING
        };

        // Act
        var response = await client.PostAsJsonAsync($"/api/boards/{board.Id}/tasks", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // --- DELETE /api/boards/{boardId}/tasks/{boardTaskId} ---

    [Fact]
    public async Task DeleteBoardTask_AsEditor_ReturnsNoContentAndRemovesTask()
    {
        // Arrange
        var (userId, client) = await CreateUserWithClientAsync();
        await using var db = _factory.CreateDbContext();
        var board = await DatabaseSeeder.CreateBoardAsync(db, userId, UserRoleEnum.EDITOR);
        var task = await DatabaseSeeder.CreateTaskAsync(db, board.Id, "Task to Delete");

        // Act
        var response = await client.DeleteAsync($"/api/boards/{board.Id}/tasks/{task.Id}");

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert - database state
        await using var verifyDb = _factory.CreateDbContext();
        var deleted = await verifyDb.BoardTasks.AsNoTracking().SingleOrDefaultAsync(t => t.Id == task.Id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteBoardTask_WhenTaskNotFound_ReturnsNotFound()
    {
        // Arrange
        var (userId, client) = await CreateUserWithClientAsync();
        await using var db = _factory.CreateDbContext();
        var board = await DatabaseSeeder.CreateBoardAsync(db, userId, UserRoleEnum.EDITOR);

        // Act
        var response = await client.DeleteAsync($"/api/boards/{board.Id}/tasks/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- DELETE /api/boards/{boardId}/tasks/archived ---

    [Fact]
    public async Task DeleteArchivedBoardTasks_AsEditor_ReturnsNoContentAndRemovesArchivedTasks()
    {
        // Arrange
        var (userId, client) = await CreateUserWithClientAsync();
        await using var db = _factory.CreateDbContext();
        var board = await DatabaseSeeder.CreateBoardAsync(db, userId, UserRoleEnum.EDITOR);
        await DatabaseSeeder.CreateTaskAsync(db, board.Id, "Archived 1", TaskStatusEnum.ARCHIVED);
        await DatabaseSeeder.CreateTaskAsync(db, board.Id, "Archived 2", TaskStatusEnum.ARCHIVED);
        await DatabaseSeeder.CreateTaskAsync(db, board.Id, "Active Task", TaskStatusEnum.PENDING);

        // Act
        var response = await client.DeleteAsync($"/api/boards/{board.Id}/tasks/archived");

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert - database: archived tasks removed, active task remains
        await using var verifyDb = _factory.CreateDbContext();
        var remaining = await verifyDb.BoardTasks.AsNoTracking()
            .Where(t => t.BoardId == board.Id)
            .ToListAsync();
        remaining.Should().HaveCount(1);
        remaining.Single().TaskStatus.Should().Be(TaskStatusEnum.PENDING);
    }

    [Fact]
    public async Task DeleteArchivedBoardTasks_WhenNotOnBoard_ReturnsForbidden()
    {
        // Arrange
        var (ownerId, _) = await CreateUserWithClientAsync("delarch_owner", "delarch_owner@test.com");
        var (otherId, otherClient) = await CreateUserWithClientAsync("delarch_other", "delarch_other@test.com");
        await using var db = _factory.CreateDbContext();
        var board = await DatabaseSeeder.CreateBoardAsync(db, ownerId, UserRoleEnum.OWNER);
        await DatabaseSeeder.CreateTaskAsync(db, board.Id, "Archived 1", TaskStatusEnum.ARCHIVED);

        // Act
        var response = await otherClient.DeleteAsync($"/api/boards/{board.Id}/tasks/archived");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // --- PUT /api/boards/{boardId}/tasks/{boardTaskId} ---

    [Fact]
    public async Task UpdateBoardTask_AsEditor_ReturnsOkWithUpdatedTask()
    {
        // Arrange
        var (userId, client) = await CreateUserWithClientAsync();
        await using var db = _factory.CreateDbContext();
        var board = await DatabaseSeeder.CreateBoardAsync(db, userId, UserRoleEnum.EDITOR);
        var task = await DatabaseSeeder.CreateTaskAsync(db, board.Id, "Old Title", TaskStatusEnum.PENDING);

        var request = new BoardTaskUpdateRequest
        {
            Title = "Updated Title",
            Description = "Updated description",
            TaskStatus = TaskStatusEnum.IN_PROGRESS,
            Version = task.Version
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
        await using var verifyDb = _factory.CreateDbContext();
        var persisted = await verifyDb.BoardTasks.AsNoTracking().SingleOrDefaultAsync(t => t.Id == task.Id);
        persisted!.Title.Should().Be("Updated Title");
        persisted.TaskStatus.Should().Be(TaskStatusEnum.IN_PROGRESS);
    }

    [Fact]
    public async Task UpdateBoardTask_AsViewer_ReturnsForbidden()
    {
        // Arrange
        var (userId, client) = await CreateUserWithClientAsync();
        await using var db = _factory.CreateDbContext();
        var board = await DatabaseSeeder.CreateBoardAsync(db, userId, UserRoleEnum.VIEWER);
        var task = await DatabaseSeeder.CreateTaskAsync(db, board.Id, "Viewer's Task");

        var request = new BoardTaskUpdateRequest
        {
            Title = "Unauthorized Update",
            TaskStatus = TaskStatusEnum.COMPLETED,
            Version = task.Version
        };

        // Act
        var response = await client.PutAsJsonAsync($"/api/boards/{board.Id}/tasks/{task.Id}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // --- PATCH /api/boards/{boardId}/tasks/{boardTaskId} ---

    [Fact]
    public async Task PatchBoardTask_AsEditor_ReturnsOkWithPatchedTask()
    {
        // Arrange
        var (userId, client) = await CreateUserWithClientAsync();
        await using var db = _factory.CreateDbContext();
        var board = await DatabaseSeeder.CreateBoardAsync(db, userId, UserRoleEnum.EDITOR);
        var task = await DatabaseSeeder.CreateTaskAsync(db, board.Id, "Patch Target", TaskStatusEnum.PENDING);

        var patchDoc = new[]
        {
            new { op = "replace", path = "/title", value = (object)"Patched Task Title" },
            new { op = "replace", path = "/taskStatus", value = (object)1 },
            new { op = "replace", path = "/version", value = (object)task.Version }
        };

        var request = new System.Net.Http.StringContent(
            System.Text.Json.JsonSerializer.Serialize(patchDoc),
            System.Text.Encoding.UTF8,
            "application/json-patch+json"
        );

        // Act
        var response = await client.PatchAsync($"/api/boards/{board.Id}/tasks/{task.Id}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<BoardTaskResponse>();
        body.Should().NotBeNull();
        body!.Title.Should().Be("Patched Task Title");
    }

    [Fact]
    public async Task PatchBoardTask_AsViewer_ReturnsForbidden()
    {
        // Arrange
        var (ownerId, _) = await CreateUserWithClientAsync("patch_task_owner", "patch_task_owner@test.com");
        var (viewerId, viewerClient) = await CreateUserWithClientAsync("patch_task_viewer", "patch_task_viewer@test.com");
        await using var db = _factory.CreateDbContext();
        var board = await DatabaseSeeder.CreateBoardAsync(db, ownerId, UserRoleEnum.OWNER);
        await DatabaseSeeder.AddUserToBoardAsync(db, board.Id, viewerId, UserRoleEnum.VIEWER);
        var task = await DatabaseSeeder.CreateTaskAsync(db, board.Id, "Patch Protected Task", TaskStatusEnum.PENDING);

        var patchDoc = new[]
        {
            new { op = "replace", path = "/title", value = (object)"Blocked Title" },
            new { op = "replace", path = "/version", value = (object)task.Version }
        };

        var request = new System.Net.Http.StringContent(
            System.Text.Json.JsonSerializer.Serialize(patchDoc),
            System.Text.Encoding.UTF8,
            "application/json-patch+json"
        );

        // Act
        var response = await viewerClient.PatchAsync($"/api/boards/{board.Id}/tasks/{task.Id}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateBoardTask_WithStaleVersion_ReturnsConflict()
    {
        // Arrange
        var (userId, client) = await CreateUserWithClientAsync("conflict_task_user", "conflict_task_user@test.com");
        await using var db = _factory.CreateDbContext();
        var board = await DatabaseSeeder.CreateBoardAsync(db, userId, UserRoleEnum.EDITOR);
        var task = await DatabaseSeeder.CreateTaskAsync(db, board.Id, "Versioned Task", TaskStatusEnum.PENDING);

        var request = new BoardTaskUpdateRequest
        {
            Title = "Should Conflict",
            Description = "Conflict description",
            TaskStatus = TaskStatusEnum.IN_PROGRESS,
            Version = task.Version + 1
        };

        // Act
        var response = await client.PutAsJsonAsync($"/api/boards/{board.Id}/tasks/{task.Id}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
