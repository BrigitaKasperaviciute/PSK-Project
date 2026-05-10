using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using System.Text.Json;
using Xunit;
using WorthBoards.Common.Enums;
using WorthBoards.IntegrationTests.Infrastructure;

namespace WorthBoards.IntegrationTests.Controllers;

public class BoardTaskControllerIntegrationTests : IntegrationTestBase
{
    [Fact]
    public async Task GetAllActiveBoardTasks_ValidBoard_ReturnsOkWithActiveTasksList()
    {
        // Arrange
        await ResetDatabaseAsync();
        var helper = new TestHelper(HttpClient, this);
        var (userId1, token1, _) = await helper.CreateAndLoginUserAsync();
        
        var board = await helper.CreateBoardAsync(userId1, token1);
        var task = await helper.CreateBoardTaskAsync(board.Id, token1);
        
        SetBearerToken(token1);

        // Act
        var response = await HttpClient.GetAsync($"api/boards/{board.Id}/tasks");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain(task.Title);
    }

    [Fact]
    public async Task GetAllActiveBoardTasks_NonExistentBoard_ReturnsNotFound()
    {
        // Arrange
        await ResetDatabaseAsync();
        var helper = new TestHelper(HttpClient, this);
        var (userId, token, _) = await helper.CreateAndLoginUserAsync();
        SetBearerToken(token);

        // Act
        var response = await HttpClient.GetAsync("api/boards/99999/tasks");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetAllArchivedBoardTasks_WithArchivedTasks_ReturnsOkWithArchiveTasksList()
    {
        // Arrange
        await ResetDatabaseAsync();
        var helper = new TestHelper(HttpClient, this);
        var (userId1, token1, _) = await helper.CreateAndLoginUserAsync();
        
        var board = await helper.CreateBoardAsync(userId1, token1);
        await helper.CreateBoardTaskAsync(board.Id, token1, status: TaskStatusEnum.ARCHIVED);
        
        SetBearerToken(token1);

        // Act
        var response = await HttpClient.GetAsync($"api/boards/{board.Id}/tasks/archived");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetBoardTaskById_ValidTask_ReturnsOkWithTaskData()
    {
        // Arrange
        await ResetDatabaseAsync();
        var helper = new TestHelper(HttpClient, this);
        var (userId1, token1, _) = await helper.CreateAndLoginUserAsync();
        
        var board = await helper.CreateBoardAsync(userId1, token1);
        var task = await helper.CreateBoardTaskAsync(board.Id, token1);
        
        SetBearerToken(token1);

        // Act
        var response = await HttpClient.GetAsync($"api/boards/{board.Id}/tasks/{task.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        result.GetProperty("id").GetInt32().Should().Be(task.Id);
    }

    [Fact]
    public async Task GetBoardTaskById_NonExistentTask_ReturnsNotFound()
    {
        // Arrange
        await ResetDatabaseAsync();
        var helper = new TestHelper(HttpClient, this);
        var (userId1, token1, _) = await helper.CreateAndLoginUserAsync();
        
        var board = await helper.CreateBoardAsync(userId1, token1);
        SetBearerToken(token1);

        // Act
        var response = await HttpClient.GetAsync($"api/boards/{board.Id}/tasks/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateBoardTask_ValidRequest_ReturnsCreatedWithTaskData()
    {
        // Arrange
        await ResetDatabaseAsync();
        var helper = new TestHelper(HttpClient, this);
        var (userId1, token1, _) = await helper.CreateAndLoginUserAsync();
        
        var board = await helper.CreateBoardAsync(userId1, token1);
        
        var taskRequest = TestDataBuilder.CreateBoardTaskRequest();
        SetBearerToken(token1);

        // Act
        var response = await HttpClient.PostAsJsonAsync($"api/boards/{board.Id}/tasks", taskRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        result.GetProperty("id").GetInt32().Should().BeGreaterThan(0);
        result.GetProperty("boardId").GetInt32().Should().Be(board.Id);
    }

    [Fact]
    public async Task CreateBoardTask_WithoutEditorRole_ReturnsForbidden()
    {
        // Arrange
        await ResetDatabaseAsync();
        var helper = new TestHelper(HttpClient, this);
        var (userId1, token1, _) = await helper.CreateAndLoginUserAsync();
        var (userId2, token2, _) = await helper.CreateAndLoginUserAsync();
        
        var board = await helper.CreateBoardAsync(userId1, token1);
        await helper.LinkUserToBoardAsync(board.Id, userId2, token1, UserRoleEnum.VIEWER);
        
        var taskRequest = TestDataBuilder.CreateBoardTaskRequest();
        SetBearerToken(token2);

        // Act
        var response = await HttpClient.PostAsJsonAsync($"api/boards/{board.Id}/tasks", taskRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteBoardTask_WithEditorRole_ReturnsNoContent()
    {
        // Arrange
        await ResetDatabaseAsync();
        var helper = new TestHelper(HttpClient, this);
        var (userId1, token1, _) = await helper.CreateAndLoginUserAsync();
        
        var board = await helper.CreateBoardAsync(userId1, token1);
        var task = await helper.CreateBoardTaskAsync(board.Id, token1);
        
        SetBearerToken(token1);

        // Act
        var response = await HttpClient.DeleteAsync($"api/boards/{board.Id}/tasks/{task.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteBoardTask_NonExistentTask_ReturnsNotFound()
    {
        // Arrange
        await ResetDatabaseAsync();
        var helper = new TestHelper(HttpClient, this);
        var (userId1, token1, _) = await helper.CreateAndLoginUserAsync();
        
        var board = await helper.CreateBoardAsync(userId1, token1);
        SetBearerToken(token1);

        // Act
        var response = await HttpClient.DeleteAsync($"api/boards/{board.Id}/tasks/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteArchivedBoardTasks_WithArchivedTasks_ReturnsNoContent()
    {
        // Arrange
        await ResetDatabaseAsync();
        var helper = new TestHelper(HttpClient, this);
        var (userId1, token1, _) = await helper.CreateAndLoginUserAsync();
        
        var board = await helper.CreateBoardAsync(userId1, token1);
        await helper.CreateBoardTaskAsync(board.Id, token1, status: TaskStatusEnum.ARCHIVED);
        
        SetBearerToken(token1);

        // Act
        var response = await HttpClient.DeleteAsync($"api/boards/{board.Id}/tasks/archived");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UpdateBoardTask_ValidRequest_ReturnsOkWithUpdatedTask()
    {
        // Arrange
        await ResetDatabaseAsync();
        var helper = new TestHelper(HttpClient, this);
        var (userId1, token1, _) = await helper.CreateAndLoginUserAsync();
        
        var board = await helper.CreateBoardAsync(userId1, token1);
        var task = await helper.CreateBoardTaskAsync(board.Id, token1);
        
        var updateRequest = TestDataBuilder.CreateBoardTaskUpdateRequest();
        SetBearerToken(token1);

        // Act
        var response = await HttpClient.PutAsJsonAsync(
            $"api/boards/{board.Id}/tasks/{task.Id}",
            updateRequest
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdateBoardTask_ChangeStatus_ReturnsOkWithNotification()
    {
        // Arrange
        await ResetDatabaseAsync();
        var helper = new TestHelper(HttpClient, this);
        var (userId1, token1, _) = await helper.CreateAndLoginUserAsync();
        
        var board = await helper.CreateBoardAsync(userId1, token1);
        var task = await helper.CreateBoardTaskAsync(board.Id, token1, status: TaskStatusEnum.PENDING);
        
        var updateRequest = TestDataBuilder.CreateBoardTaskUpdateRequest(status: TaskStatusEnum.IN_PROGRESS);
        SetBearerToken(token1);

        // Act
        var response = await HttpClient.PutAsJsonAsync(
            $"api/boards/{board.Id}/tasks/{task.Id}",
            updateRequest
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PatchBoardTask_ValidRequest_ReturnsOkWithPatchedTask()
    {
        // Arrange
        await ResetDatabaseAsync();
        var helper = new TestHelper(HttpClient, this);
        var (userId1, token1, _) = await helper.CreateAndLoginUserAsync();
        
        var board = await helper.CreateBoardAsync(userId1, token1);
        var task = await helper.CreateBoardTaskAsync(board.Id, token1);
        
        var patchRequest = @"[
            { ""op"": ""replace"", ""path"": ""/title"", ""value"": ""Patched Task"" }
        ]";
        
        SetBearerToken(token1);

        // Act
        var request = new HttpRequestMessage(new HttpMethod("PATCH"), $"api/boards/{board.Id}/tasks/{task.Id}")
        {
            Content = new StringContent(patchRequest, System.Text.Encoding.UTF8, "application/json")
        };
        var response = await HttpClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
