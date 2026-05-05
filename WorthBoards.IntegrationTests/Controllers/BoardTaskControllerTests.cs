using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Common.Enums;
using WorthBoards.Common.Exceptions;
using WorthBoards.Data.Database;
using WorthBoards.IntegrationTests.Infrastructure;
using Xunit;

namespace WorthBoards.IntegrationTests.Controllers;

[Collection(nameof(IntegrationCollection))]
public sealed class BoardTaskControllerTests(IntegrationTestAppFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task CreateAndUpdateTask_WithEditorRole_PersistsTaskAndStatusChangeNotification()
    {
        // Arrange
        var owner = await Factory.CreateUserAsync("Karl", "Frost", "task-owner");
        var viewer = await Factory.CreateUserAsync("Lia", "Moss", "task-viewer");
        var board = await Factory.SeedBoardAsync(owner.Id, "Release board", "Task flow");
        await Factory.SeedBoardUserAsync(board.Id, viewer.Id, UserRoleEnum.VIEWER);
        var client = Factory.CreateAuthenticatedClient(owner);

        var createRequest = new BoardTaskRequest
        {
            Title = "Write release notes",
            Description = "Draft the final changelog",
            TaskStatus = TaskStatusEnum.PENDING
        };

        // Act
        var createResponse = await client.PostAsJsonAsync($"/api/boards/{board.Id}/tasks", createRequest);

        // Assert - create
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdTask = await createResponse.Content.ReadFromJsonAsync<BoardTaskResponse>();
        createdTask.Should().NotBeNull();
        createdTask!.Title.Should().Be(createRequest.Title);
        createdTask.TaskStatus.Should().Be(TaskStatusEnum.PENDING);

        var persistedTask = await Factory.WithDbContextAsync(async db =>
            await db.BoardTasks.AsNoTracking().SingleAsync(item => item.Id == createdTask.Id));

        var updateRequest = new BoardTaskUpdateRequest
        {
            Title = persistedTask.Title,
            Description = persistedTask.Description,
            DeadlineEnd = persistedTask.DeadlineEnd,
            TaskStatus = TaskStatusEnum.COMPLETED,
            Version = persistedTask.Version
        };

        var updateResponse = await client.PutAsJsonAsync($"/api/boards/{board.Id}/tasks/{createdTask.Id}", updateRequest);

        // Assert - update
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedTask = await updateResponse.Content.ReadFromJsonAsync<BoardTaskResponse>();
        updatedTask.Should().NotBeNull();
        updatedTask!.TaskStatus.Should().Be(TaskStatusEnum.COMPLETED);

        await Factory.WithDbContextAsync(async db =>
        {
            var persisted = await db.BoardTasks.AsNoTracking().SingleAsync(item => item.Id == createdTask.Id);
            persisted.TaskStatus.Should().Be(TaskStatusEnum.COMPLETED);

            var notifications = await db.Notifications.AsNoTracking()
                .Where(item => item.BoardId == board.Id && item.TaskId == createdTask.Id)
                .ToListAsync();
            notifications.Should().Contain(item => item.NotificationType == NotificationEventTypeEnum.TASK_CREATED);
            notifications.Should().Contain(item => item.NotificationType == NotificationEventTypeEnum.TASK_STATUS_CHANGE);

            var recipients = await db.NotificationsOnUsers.AsNoTracking().ToListAsync();
            recipients.Should().Contain(item => item.UserId == viewer.Id);
        });
    }

    [Fact]
    public async Task Create_WithViewerRole_ReturnsForbiddenAndDoesNotPersistTask()
    {
        // Arrange
        var owner = await Factory.CreateUserAsync("Mona", "Hart", "task-owner-negative");
        var viewer = await Factory.CreateUserAsync("Ned", "Ivy", "task-viewer-negative");
        var board = await Factory.SeedBoardAsync(owner.Id, "Support board", "Negative flow");
        await Factory.SeedBoardUserAsync(board.Id, viewer.Id, UserRoleEnum.VIEWER);
        var client = Factory.CreateAuthenticatedClient(viewer);

        var request = new BoardTaskRequest
        {
            Title = "Should not be created",
            Description = "Forbidden user",
            TaskStatus = TaskStatusEnum.PENDING
        };

        // Act
        var response = await client.PostAsJsonAsync($"/api/boards/{board.Id}/tasks", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await Factory.WithDbContextAsync(async db =>
        {
            var tasks = await db.BoardTasks.AsNoTracking().Where(item => item.BoardId == board.Id).ToListAsync();
            tasks.Should().BeEmpty();
        });
    }

    [Fact]
    public async Task GetDeleteAndArchive_WithEditorRole_ExercisesTaskLifecycle()
    {
        // Arrange
        var owner = await Factory.CreateUserAsync("Owen", "Price", "task-lifecycle-owner");
        var board = await Factory.SeedBoardAsync(owner.Id, "Lifecycle tasks", "Task reads and deletes");
        var client = Factory.CreateAuthenticatedClient(owner);

        var activeTask = await Factory.SeedTaskAsync(board.Id, "Active work", "Still active", TaskStatusEnum.IN_PROGRESS);
        var archivedTask = await Factory.SeedTaskAsync(board.Id, "Archived work", "Already archived", TaskStatusEnum.ARCHIVED);

        // Act - get active
        var activeResponse = await client.GetAsync($"/api/boards/{board.Id}/tasks");

        // Assert - get active
        activeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var activeBody = await activeResponse.Content.ReadFromJsonAsync<List<BoardTaskResponse>>();
        activeBody.Should().ContainSingle(item => item.Id == activeTask.Id);

        // Act - get archived
        var archivedResponse = await client.GetAsync($"/api/boards/{board.Id}/tasks/archived");

        // Assert - get archived
        archivedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var archivedBody = await archivedResponse.Content.ReadFromJsonAsync<List<BoardTaskResponse>>();
        archivedBody.Should().ContainSingle(item => item.Id == archivedTask.Id);

        // Act - update active task using the current version
        var updateResponse = await client.PutAsJsonAsync($"/api/boards/{board.Id}/tasks/{activeTask.Id}", new BoardTaskUpdateRequest
        {
            Title = activeTask.Title,
            Description = activeTask.Description,
            DeadlineEnd = activeTask.DeadlineEnd,
            TaskStatus = TaskStatusEnum.COMPLETED,
            Version = activeTask.Version
        });

        // Assert - update
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedTask = await updateResponse.Content.ReadFromJsonAsync<BoardTaskResponse>();
        updatedTask.Should().NotBeNull();

        // Act - patch active task title
        var patchOperations = new[]
        {
            new Dictionary<string, object?> { ["op"] = "replace", ["path"] = "/title", ["value"] = "Active work patched" },
            new Dictionary<string, object?> { ["op"] = "replace", ["path"] = "/version", ["value"] = updatedTask!.Version }
        };
        var patchContent = new StringContent(JsonSerializer.Serialize(patchOperations), Encoding.UTF8, "application/json-patch+json");
        var patchResponse = await client.PatchAsync($"/api/boards/{board.Id}/tasks/{activeTask.Id}", patchContent);

        // Assert - patch
        patchResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var patchedTask = await patchResponse.Content.ReadFromJsonAsync<BoardTaskResponse>();
        patchedTask.Should().NotBeNull();
        patchedTask!.Title.Should().Be("Active work patched");

        // Act - delete active task
        var deleteResponse = await client.DeleteAsync($"/api/boards/{board.Id}/tasks/{activeTask.Id}");

        // Assert - delete
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Act - delete archived tasks
        var deleteArchivedResponse = await client.DeleteAsync($"/api/boards/{board.Id}/tasks/archived");

        // Assert - delete archived
        deleteArchivedResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await Factory.WithDbContextAsync(async db =>
        {
            var tasks = await db.BoardTasks.AsNoTracking().Where(item => item.BoardId == board.Id).ToListAsync();
            tasks.Should().BeEmpty();
        });
    }
}