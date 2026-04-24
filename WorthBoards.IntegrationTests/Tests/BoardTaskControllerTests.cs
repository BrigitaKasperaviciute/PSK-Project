using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;
using Microsoft.AspNetCore.JsonPatch;
using Moq;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Common.Enums;
using WorthBoards.Common.Exceptions.Custom;
using WorthBoards.IntegrationTests.Infrastructure;

namespace WorthBoards.IntegrationTests.Tests;

public class BoardTaskControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public BoardTaskControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetAllMocks();
    }

    private HttpClient AuthClient(int userId = 1) => _factory.CreateClient().WithAuth(userId);

    private static StringContent Json(object body) =>
        new(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

    private static StringContent JsonPatch(object body) =>
        new(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json-patch+json");

    private static BoardTaskResponse MakeTask(int id = 1, TaskStatusEnum status = TaskStatusEnum.PENDING) =>
        new() { Id = id, BoardId = 1, Title = "Task", TaskStatus = status, CreationDate = DateTime.UtcNow, Version = 0 };

    private void SetupOwnerRole() =>
        _factory.BoardServiceMock
            .Setup(s => s.GetUserRoleByBoardIdAndUserIdAsync(It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(UserRoleEnum.OWNER);

    private void SetupNoRole() =>
        _factory.BoardServiceMock
            .Setup(s => s.GetUserRoleByBoardIdAndUserIdAsync(It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync((UserRoleEnum?)null);

    // ── GetAllActiveBoardTasks ─────────────────────────────────────────────

    [Fact]
    public async Task GetActiveTasks_ViewerRole_ReturnsOk()
    {
        SetupOwnerRole();
        _factory.BoardTaskServiceMock
            .Setup(s => s.GetBoardTasks(1, It.IsAny<IEnumerable<TaskStatusEnum>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BoardTaskResponse> { MakeTask() });

        var response = await AuthClient().GetAsync("/api/boards/1/tasks");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetActiveTasks_InsufficientRole_ReturnsForbidden()
    {
        SetupNoRole();

        var response = await AuthClient().GetAsync("/api/boards/1/tasks");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── GetAllArchivedBoardTasks ───────────────────────────────────────────

    [Fact]
    public async Task GetArchivedTasks_ViewerRole_ReturnsOk()
    {
        SetupOwnerRole();
        _factory.BoardTaskServiceMock
            .Setup(s => s.GetBoardTasks(1, It.IsAny<IEnumerable<TaskStatusEnum>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BoardTaskResponse> { MakeTask(1, TaskStatusEnum.ARCHIVED) });

        var response = await AuthClient().GetAsync("/api/boards/1/tasks/archived");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetArchivedTasks_InsufficientRole_ReturnsForbidden()
    {
        SetupNoRole();

        var response = await AuthClient().GetAsync("/api/boards/1/tasks/archived");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── GetBoardTaskById ──────────────────────────────────────────────────

    [Fact]
    public async Task GetTaskById_ExistingTask_ReturnsOk()
    {
        SetupOwnerRole();
        _factory.BoardTaskServiceMock
            .Setup(s => s.GetBoardTaskById(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeTask(1));

        var response = await AuthClient().GetAsync("/api/boards/1/tasks/1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetTaskById_NotFound_ReturnsNotFound()
    {
        SetupOwnerRole();
        _factory.BoardTaskServiceMock
            .Setup(s => s.GetBoardTaskById(1, 999, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Task not found."));

        var response = await AuthClient().GetAsync("/api/boards/1/tasks/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── CreateBoardTask ───────────────────────────────────────────────────

    [Fact]
    public async Task CreateTask_EditorRole_ReturnsCreated()
    {
        SetupOwnerRole();
        _factory.BoardTaskServiceMock
            .Setup(s => s.CreateBoardTask(1, It.IsAny<BoardTaskRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeTask(1));
        _factory.NotificationServiceMock
            .Setup(s => s.NotifyTaskCreated(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await AuthClient().PostAsync("/api/boards/1/tasks",
            Json(new { title = "Task", taskStatus = 0 }));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CreateTask_InsufficientRole_ReturnsForbidden()
    {
        SetupNoRole();

        var response = await AuthClient().PostAsync("/api/boards/1/tasks",
            Json(new { title = "Task", taskStatus = 0 }));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── DeleteBoardTask ───────────────────────────────────────────────────

    [Fact]
    public async Task DeleteTask_EditorRole_ReturnsNoContent()
    {
        SetupOwnerRole();
        _factory.BoardTaskServiceMock
            .Setup(s => s.DeleteBoardTask(1, 1, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await AuthClient().DeleteAsync("/api/boards/1/tasks/1");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteTask_NotFound_ReturnsNotFound()
    {
        SetupOwnerRole();
        _factory.BoardTaskServiceMock
            .Setup(s => s.DeleteBoardTask(1, 999, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Task not found."));

        var response = await AuthClient().DeleteAsync("/api/boards/1/tasks/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── DeleteArchivedBoardTasks ──────────────────────────────────────────

    [Fact]
    public async Task DeleteArchivedTasks_EditorRole_ReturnsNoContent()
    {
        SetupOwnerRole();
        _factory.BoardTaskServiceMock
            .Setup(s => s.DeleteArchivedBoardTasks(1, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await AuthClient().DeleteAsync("/api/boards/1/tasks/archived");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteArchivedTasks_InsufficientRole_ReturnsForbidden()
    {
        SetupNoRole();

        var response = await AuthClient().DeleteAsync("/api/boards/1/tasks/archived");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── UpdateBoardTask ───────────────────────────────────────────────────

    [Fact]
    public async Task UpdateTask_StatusChanges_NotifiesAndReturnsOk()
    {
        SetupOwnerRole();
        // Old task has PENDING; request changes to IN_PROGRESS → triggers notification
        _factory.BoardTaskServiceMock
            .Setup(s => s.GetBoardTaskById(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeTask(1, TaskStatusEnum.PENDING));
        _factory.BoardTaskServiceMock
            .Setup(s => s.UpdateBoardTask(1, 1, It.IsAny<BoardTaskUpdateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeTask(1, TaskStatusEnum.IN_PROGRESS));
        _factory.NotificationServiceMock
            .Setup(s => s.NotifyTaskStatusChange(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<TaskStatusEnum>(), It.IsAny<TaskStatusEnum>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await AuthClient().PutAsync("/api/boards/1/tasks/1",
            Json(new { title = "Task", taskStatus = 1, version = 0 }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UpdateTask_NotFound_ReturnsNotFound()
    {
        SetupOwnerRole();
        _factory.BoardTaskServiceMock
            .Setup(s => s.GetBoardTaskById(1, 999, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Task not found."));

        var response = await AuthClient().PutAsync("/api/boards/1/tasks/999",
            Json(new { title = "Task", taskStatus = 0, version = 0 }));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateTask_SameStatus_ReturnsOkWithoutNotification()
    {
        SetupOwnerRole();
        // Old and new status both PENDING → no status change → NotifyTaskStatusChange NOT called
        _factory.BoardTaskServiceMock
            .Setup(s => s.GetBoardTaskById(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeTask(1, TaskStatusEnum.PENDING));
        _factory.BoardTaskServiceMock
            .Setup(s => s.UpdateBoardTask(1, 1, It.IsAny<BoardTaskUpdateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeTask(1, TaskStatusEnum.PENDING));

        var response = await AuthClient().PutAsync("/api/boards/1/tasks/1",
            Json(new { title = "Task", taskStatus = 0, version = 0 }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        _factory.NotificationServiceMock.Verify(
            s => s.NotifyTaskStatusChange(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<TaskStatusEnum>(), It.IsAny<TaskStatusEnum>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── PatchBoardTask ────────────────────────────────────────────────────

    [Fact]
    public async Task PatchTask_EditorRole_ReturnsOk()
    {
        SetupOwnerRole();
        _factory.BoardTaskServiceMock
            .Setup(s => s.PatchBoardTask(1, 1, It.IsAny<JsonPatchDocument<BoardTaskUpdateRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeTask(1));

        var patch = new[] { new { op = "replace", path = "/title", value = "Patched" } };
        var response = await AuthClient().PatchAsync("/api/boards/1/tasks/1", JsonPatch(patch));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PatchTask_InsufficientRole_ReturnsForbidden()
    {
        SetupNoRole();

        var patch = new[] { new { op = "replace", path = "/title", value = "Patched" } };
        var response = await AuthClient().PatchAsync("/api/boards/1/tasks/1", JsonPatch(patch));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}