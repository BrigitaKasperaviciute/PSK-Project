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
    private record PatchOp(string op, string path, object value);

    // ── GET active tasks ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllActiveBoardTasks_AsMember_ReturnsActiveTasks()
    {
        // Arrange
        var (_, _, client) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(client);
        var task = await CreateTaskAsync(client, board.Id, "Active Task");

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}/tasks");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var tasks = await response.Content.ReadFromJsonAsync<IEnumerable<BoardTaskResponse>>();
        tasks.Should().Contain(t => t.Id == task.Id);
    }

    [Fact]
    public async Task GetAllActiveBoardTasks_AsNonMember_ReturnsForbidden()
    {
        // Arrange
        var (_, _, ownerClient) = await RegisterAndLoginAsync();
        var (_, _, nonMemberClient) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(ownerClient);

        // Act
        var response = await nonMemberClient.GetAsync($"/api/boards/{board.Id}/tasks");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── GET archived tasks ───────────────────────────────────────────────────

    [Fact]
    public async Task GetAllArchivedBoardTasks_AsMember_ReturnsArchivedTasks()
    {
        // Arrange
        var (_, _, client) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(client);
        var task = await CreateTaskAsync(client, board.Id, "To Archive");

        // Archive it via update
        await client.PutAsJsonAsync($"/api/boards/{board.Id}/tasks/{task.Id}",
            new BoardTaskUpdateRequest
            {
                Title = task.Title,
                Description = task.Description,
                DeadlineEnd = task.DeadlineEnd,
                TaskStatus = TaskStatusEnum.ARCHIVED,
                Version = task.Version
            });

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}/tasks/archived");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var tasks = await response.Content.ReadFromJsonAsync<IEnumerable<BoardTaskResponse>>();
        tasks.Should().Contain(t => t.Id == task.Id);
    }

    [Fact]
    public async Task GetAllArchivedBoardTasks_AsNonMember_ReturnsForbidden()
    {
        // Arrange
        var (_, _, ownerClient) = await RegisterAndLoginAsync();
        var (_, _, nonMemberClient) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(ownerClient);

        // Act
        var response = await nonMemberClient.GetAsync($"/api/boards/{board.Id}/tasks/archived");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── GET task by id ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetBoardTaskById_ExistingTask_ReturnsTaskResponse()
    {
        // Arrange
        var (_, _, client) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(client);
        var task = await CreateTaskAsync(client, board.Id, "Find Me");

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}/tasks/{task.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<BoardTaskResponse>();
        result!.Id.Should().Be(task.Id);
        result.Title.Should().Be("Find Me");
    }

    [Fact]
    public async Task GetBoardTaskById_NonExistentTask_ReturnsNotFound()
    {
        // Arrange
        var (_, _, client) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(client);

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}/tasks/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── POST (create) task ───────────────────────────────────────────────────

    [Fact]
    public async Task CreateBoardTask_AsEditor_ReturnsCreatedTaskAndPersists()
    {
        // Arrange
        var (ownerId, _, ownerClient) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(ownerClient);
        var request = new BoardTaskRequest
        {
            Title = "New Task",
            Description = "Desc",
            DeadlineEnd = DateTime.UtcNow.AddDays(5),
            TaskStatus = TaskStatusEnum.PENDING
        };

        // Act
        var response = await ownerClient.PostAsJsonAsync($"/api/boards/{board.Id}/tasks", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var task = await response.Content.ReadFromJsonAsync<BoardTaskResponse>();
        task!.Title.Should().Be("New Task");
        task.TaskStatus.Should().Be(TaskStatusEnum.PENDING);

        using var db = GetDbContext();
        db.BoardTasks.Any(t => t.Id == task.Id && t.BoardId == board.Id).Should().BeTrue();
    }

    [Fact]
    public async Task CreateBoardTask_AsViewer_ReturnsForbidden()
    {
        // Arrange
        var (_, _, ownerClient) = await RegisterAndLoginAsync();
        var (viewerId, _, viewerClient) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(ownerClient);
        await LinkUserToBoardDirectlyAsync(board.Id, viewerId, UserRoleEnum.VIEWER);

        var request = new BoardTaskRequest
        {
            Title = "Not Allowed",
            Description = "",
            TaskStatus = TaskStatusEnum.PENDING
        };

        // Act
        var response = await viewerClient.PostAsJsonAsync($"/api/boards/{board.Id}/tasks", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── DELETE task ──────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteBoardTask_AsEditor_ReturnsNoContentAndRemovesTask()
    {
        // Arrange
        var (_, _, client) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(client);
        var task = await CreateTaskAsync(client, board.Id);

        // Act
        var response = await client.DeleteAsync($"/api/boards/{board.Id}/tasks/{task.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var db = GetDbContext();
        db.BoardTasks.Any(t => t.Id == task.Id).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteBoardTask_AsViewer_ReturnsForbidden()
    {
        // Arrange
        var (_, _, ownerClient) = await RegisterAndLoginAsync();
        var (viewerId, _, viewerClient) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(ownerClient);
        var task = await CreateTaskAsync(ownerClient, board.Id);
        await LinkUserToBoardDirectlyAsync(board.Id, viewerId, UserRoleEnum.VIEWER);

        // Act
        var response = await viewerClient.DeleteAsync($"/api/boards/{board.Id}/tasks/{task.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── DELETE archived tasks ────────────────────────────────────────────────

    [Fact]
    public async Task DeleteArchivedBoardTasks_AsEditor_ReturnsNoContent()
    {
        // Arrange
        var (_, _, client) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(client);
        var task = await CreateTaskAsync(client, board.Id);

        await client.PutAsJsonAsync($"/api/boards/{board.Id}/tasks/{task.Id}",
            new BoardTaskUpdateRequest
            {
                Title = task.Title,
                Description = task.Description,
                DeadlineEnd = task.DeadlineEnd,
                TaskStatus = TaskStatusEnum.ARCHIVED,
                Version = task.Version
            });

        // Act
        var response = await client.DeleteAsync($"/api/boards/{board.Id}/tasks/archived");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var db = GetDbContext();
        db.BoardTasks.Where(t => t.BoardId == board.Id && t.TaskStatus == TaskStatusEnum.ARCHIVED)
            .Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteArchivedBoardTasks_AsViewer_ReturnsForbidden()
    {
        // Arrange
        var (_, _, ownerClient) = await RegisterAndLoginAsync();
        var (viewerId, _, viewerClient) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(ownerClient);
        await LinkUserToBoardDirectlyAsync(board.Id, viewerId, UserRoleEnum.VIEWER);

        // Act
        var response = await viewerClient.DeleteAsync($"/api/boards/{board.Id}/tasks/archived");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── PUT (update) task ────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateBoardTask_ValidRequest_ReturnsUpdatedTask()
    {
        // Arrange
        var (_, _, client) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(client);
        var task = await CreateTaskAsync(client, board.Id, "Old Title");

        var updateRequest = new BoardTaskUpdateRequest
        {
            Title = "New Title",
            Description = "New Desc",
            DeadlineEnd = task.DeadlineEnd,
            TaskStatus = TaskStatusEnum.IN_PROGRESS,
            Version = task.Version
        };

        // Act
        var response = await client.PutAsJsonAsync($"/api/boards/{board.Id}/tasks/{task.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<BoardTaskResponse>();
        updated!.Title.Should().Be("New Title");
        updated.TaskStatus.Should().Be(TaskStatusEnum.IN_PROGRESS);
    }

    [Fact]
    public async Task UpdateBoardTask_StaleVersion_ReturnsConflict()
    {
        // Arrange
        var (_, _, client) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(client);
        var task = await CreateTaskAsync(client, board.Id);

        var updateRequest = new BoardTaskUpdateRequest
        {
            Title = task.Title,
            Description = task.Description,
            DeadlineEnd = task.DeadlineEnd,
            TaskStatus = task.TaskStatus,
            Version = task.Version + 999
        };

        // Act
        var response = await client.PutAsJsonAsync($"/api/boards/{board.Id}/tasks/{task.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ── PATCH task ───────────────────────────────────────────────────────────

    [Fact]
    public async Task PatchBoardTask_ValidPatch_ReturnsUpdatedTask()
    {
        // Arrange
        var (_, _, client) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(client);
        var task = await CreateTaskAsync(client, board.Id, "Patchable");

        var patchDoc = new PatchOp[]
        {
            new("replace", "/title", "Patched Task"),
            new("replace", "/version", task.Version)
        };

        // Act
        var response = await client.PatchAsJsonAsync($"/api/boards/{board.Id}/tasks/{task.Id}", patchDoc);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var patched = await response.Content.ReadFromJsonAsync<BoardTaskResponse>();
        patched!.Title.Should().Be("Patched Task");
    }

    [Fact]
    public async Task PatchBoardTask_AsViewer_ReturnsForbidden()
    {
        // Arrange
        var (_, _, ownerClient) = await RegisterAndLoginAsync();
        var (viewerId, _, viewerClient) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(ownerClient);
        var task = await CreateTaskAsync(ownerClient, board.Id);
        await LinkUserToBoardDirectlyAsync(board.Id, viewerId, UserRoleEnum.VIEWER);

        var patchDoc = new PatchOp[] { new("replace", "/title", "Forbidden") };

        // Act
        var response = await viewerClient.PatchAsJsonAsync($"/api/boards/{board.Id}/tasks/{task.Id}", patchDoc);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
