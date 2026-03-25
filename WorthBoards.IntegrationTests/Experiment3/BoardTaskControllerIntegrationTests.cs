using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WorthBoards.Common.Enums;

namespace WorthBoards.IntegrationTests.Experiment3;

public sealed class BoardTaskControllerIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public BoardTaskControllerIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateBoardTask_AuthorizedUser_ReturnsCreatedAndPersistsTask()
    {
        // Arrange
        await _factory.ResetStateAsync();
        using var bootstrapClient = _factory.CreateClient();

        var token = await TestApiClientHelper.RegisterAndLoginAsync(bootstrapClient);
        using var authorizedClient = TestApiClientHelper.CreateAuthorizedClient(_factory, token);
        var boardId = await TestApiClientHelper.CreateBoardAsync(authorizedClient);

        var payload = new
        {
            title = "Experiment3 task",
            description = "Task created in happy flow",
            deadlineEnd = DateTime.UtcNow.AddDays(2),
            taskStatus = TaskStatusEnum.PENDING
        };

        // Act
        var response = await authorizedClient.PostAsJsonAsync($"/api/boards/{boardId}/tasks", payload);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(boardId, json.GetProperty("boardId").GetInt32());
        Assert.Equal("Experiment3 task", json.GetProperty("title").GetString());
        Assert.Equal((int)TaskStatusEnum.PENDING, json.GetProperty("taskStatus").GetInt32());

        var dbTask = await _factory.WithDbContextAsync(db =>
            db.BoardTasks.SingleOrDefaultAsync(task => task.Id == json.GetProperty("id").GetInt32()));
        Assert.NotNull(dbTask);
        Assert.Equal(boardId, dbTask.BoardId);
        Assert.Equal("Experiment3 task", dbTask.Title);
        Assert.Equal(TaskStatusEnum.PENDING, dbTask.TaskStatus);
    }

    [Fact]
    public async Task CreateBoardTask_MissingToken_ReturnsUnauthorizedAndDoesNotPersistTask()
    {
        // Arrange
        await _factory.ResetStateAsync();
        using var anonymousClient = _factory.CreateClient();

        var payload = new
        {
            title = "Unauthorized task",
            description = "Should not persist",
            deadlineEnd = DateTime.UtcNow.AddDays(1),
            taskStatus = TaskStatusEnum.PENDING
        };

        // Act
        var response = await anonymousClient.PostAsJsonAsync("/api/boards/999/tasks", payload);
        var responseBody = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True(string.IsNullOrWhiteSpace(responseBody));

        var taskCount = await _factory.WithDbContextAsync(db => db.BoardTasks.CountAsync());
        Assert.Equal(0, taskCount);
    }

    [Fact]
    public async Task UpdateBoardTask_ValidRequest_ReturnsOkAndPersistsUpdatedTask()
    {
        // Arrange
        await _factory.ResetStateAsync();
        using var bootstrapClient = _factory.CreateClient();

        var token = await TestApiClientHelper.RegisterAndLoginAsync(bootstrapClient);
        using var authorizedClient = TestApiClientHelper.CreateAuthorizedClient(_factory, token);
        var boardId = await TestApiClientHelper.CreateBoardAsync(authorizedClient);
        var (taskId, version) = await TestApiClientHelper.CreateTaskAsync(authorizedClient, boardId, "Update me");

        var payload = new
        {
            title = "Update me",
            description = "Updated description",
            deadlineEnd = DateTime.UtcNow.AddDays(3),
            taskStatus = TaskStatusEnum.IN_PROGRESS,
            version
        };

        // Act
        var response = await authorizedClient.PutAsJsonAsync($"/api/boards/{boardId}/tasks/{taskId}", payload);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(taskId, json.GetProperty("id").GetInt32());
        Assert.Equal("Updated description", json.GetProperty("description").GetString());
        Assert.Equal((int)TaskStatusEnum.IN_PROGRESS, json.GetProperty("taskStatus").GetInt32());

        var dbTask = await _factory.WithDbContextAsync(db =>
            db.BoardTasks.SingleAsync(task => task.Id == taskId));
        Assert.Equal("Updated description", dbTask.Description);
        Assert.Equal(TaskStatusEnum.IN_PROGRESS, dbTask.TaskStatus);
    }

    [Fact]
    public async Task UpdateBoardTask_StaleVersion_ReturnsConflictAndKeepsOriginalTaskState()
    {
        // Arrange
        await _factory.ResetStateAsync();
        using var bootstrapClient = _factory.CreateClient();

        var token = await TestApiClientHelper.RegisterAndLoginAsync(bootstrapClient);
        using var authorizedClient = TestApiClientHelper.CreateAuthorizedClient(_factory, token);
        var boardId = await TestApiClientHelper.CreateBoardAsync(authorizedClient);
        var (taskId, version) = await TestApiClientHelper.CreateTaskAsync(authorizedClient, boardId, "Concurrency task");

        var payload = new
        {
            title = "Concurrency task",
            description = "Should not be applied",
            deadlineEnd = DateTime.UtcNow.AddDays(4),
            taskStatus = TaskStatusEnum.COMPLETED,
            version = version + 1
        };

        // Act
        var response = await authorizedClient.PutAsJsonAsync($"/api/boards/{boardId}/tasks/{taskId}", payload);
        var responseBody = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("modified", responseBody, StringComparison.OrdinalIgnoreCase);

        var dbTask = await _factory.WithDbContextAsync(db =>
            db.BoardTasks.SingleAsync(task => task.Id == taskId));
        Assert.Equal(TaskStatusEnum.PENDING, dbTask.TaskStatus);
        Assert.Equal("Task seeded for integration testing", dbTask.Description);
    }
}
