using System.Net;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Common.Enums;
using WorthBoards.Common.Exceptions;
using WorthBoards.IntegrationTests.Infrastructure;

namespace WorthBoards.IntegrationTests;

public sealed class BoardTaskControllerTests(WorthBoardsApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task CreateBoardTask_WithEditorToken_ManagesTaskAndNotificationLifecycle()
    {
        // Arrange
        var client = await CreateAuthenticatedClientAsync("editor");
        var createRequest = new BoardTaskRequest
        {
            Title = "Integration task",
            Description = "Task created through the API",
            DeadlineEnd = DateTime.UtcNow.AddDays(2),
            TaskStatus = TaskStatusEnum.PENDING
        };

        // Act
        var createResponse = await client.PostAsJsonAsync($"/api/boards/{Seed.BoardId}/tasks", createRequest);

        // Assert
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createdTask = await ReadJsonAsync<BoardTaskResponse>(createResponse);
        createdTask.Should().NotBeNull();
        createdTask!.BoardId.Should().Be(Seed.BoardId);
        createdTask.Title.Should().Be(createRequest.Title);

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<WorthBoards.Data.Database.ApplicationDbContext>();
            (await context.BoardTasks.CountAsync(task => task.BoardId == Seed.BoardId && task.Title == createRequest.Title)).Should().Be(1);
            (await context.Notifications.AnyAsync(notification => notification.NotificationType == NotificationEventTypeEnum.TASK_CREATED && notification.TaskId == createdTask.Id)).Should().BeTrue();
        }

        var activeResponse = await client.GetAsync($"/api/boards/{Seed.BoardId}/tasks");
        activeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var activeBody = await ReadJsonAsync<List<BoardTaskResponse>>(activeResponse);
        activeBody.Should().Contain(task => task.Id == createdTask.Id);

        var archivedTaskResponse = await client.PostAsJsonAsync($"/api/boards/{Seed.BoardId}/tasks", new BoardTaskRequest
        {
            Title = "Archived integration task",
            Description = "Archived task",
            DeadlineEnd = DateTime.UtcNow.AddDays(1),
            TaskStatus = TaskStatusEnum.ARCHIVED
        });
        archivedTaskResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var archivedTask = await ReadJsonAsync<BoardTaskResponse>(archivedTaskResponse);
        archivedTask.Should().NotBeNull();

        var archivedResponse = await client.GetAsync($"/api/boards/{Seed.BoardId}/tasks/archived");
        archivedResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var archivedBody = await ReadJsonAsync<List<BoardTaskResponse>>(archivedResponse);
        archivedBody.Should().Contain(task => task.Id == archivedTask!.Id);

        var getByIdResponse = await client.GetAsync($"/api/boards/{Seed.BoardId}/tasks/{createdTask.Id}");
        getByIdResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var updateRequest = new BoardTaskUpdateRequest
        {
            Title = "Updated integration task",
            Description = "Updated task description",
            DeadlineEnd = DateTime.UtcNow.AddDays(4),
            TaskStatus = TaskStatusEnum.COMPLETED,
            Version = createdTask.Version
        };
        var updateResponse = await client.PutAsJsonAsync($"/api/boards/{Seed.BoardId}/tasks/{createdTask.Id}", updateRequest);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var patchDocument = new JsonPatchDocument<BoardTaskUpdateRequest>();
        patchDocument.Replace(task => task.Title, "Patched integration task");
        var patchRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/boards/{Seed.BoardId}/tasks/{createdTask.Id}")
        {
            Content = new StringContent(JsonConvert.SerializeObject(patchDocument), Encoding.UTF8, "application/json-patch+json")
        };
        var patchResponse = await client.SendAsync(patchRequest);
        patchResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var deleteTaskResponse = await client.DeleteAsync($"/api/boards/{Seed.BoardId}/tasks/{createdTask.Id}");
        deleteTaskResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var deleteArchivedResponse = await client.DeleteAsync($"/api/boards/{Seed.BoardId}/tasks/archived");
        deleteArchivedResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<WorthBoards.Data.Database.ApplicationDbContext>();
            (await context.BoardTasks.AnyAsync(task => task.Id == createdTask.Id)).Should().BeFalse();
            (await context.BoardTasks.AnyAsync(task => task.Id == archivedTask!.Id)).Should().BeFalse();
        }
    }

    [Fact]
    public async Task UpdateBoardTask_WithUnknownTaskId_ReturnsNotFoundAndLeavesSeedDataUntouched()
    {
        // Arrange
        var client = await CreateAuthenticatedClientAsync("editor");

        // Act
        var response = await client.PutAsJsonAsync($"/api/boards/{Seed.BoardId}/tasks/999999", new BoardTaskUpdateRequest
        {
            Title = "Missing task",
            Description = "Missing",
            DeadlineEnd = DateTime.UtcNow.AddDays(1),
            TaskStatus = TaskStatusEnum.PENDING,
            Version = 0
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await ReadJsonAsync<ErrorDetails>(response);
        error.Should().NotBeNull();
        error!.StatusCode.Should().Be((int)HttpStatusCode.NotFound);

        await using var scope = Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<WorthBoards.Data.Database.ApplicationDbContext>();
        (await context.BoardTasks.AnyAsync(task => task.Id == Seed.TaskId)).Should().BeTrue();
    }
}