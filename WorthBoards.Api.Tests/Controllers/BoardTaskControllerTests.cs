using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using WorthBoards.Api.Tests.Infrastructure;
using WorthBoards.Business.Dtos.Responses;

namespace WorthBoards.Api.Tests.Controllers;

public class BoardTaskControllerTests : IntegrationTestBase
{
    [Fact]
    public async Task GetBoardTaskById_WithExistingTask_ReturnsTask()
    {
        // Arrange
        await ClearDatabaseAsync();
        var user = await TestHelpers.CreateTestUserAsync(Factory.Services);
        var board = await TestHelpers.CreateTestBoardAsync(Factory.Services, user.Id);
        var task = await TestHelpers.CreateTestTaskAsync(Factory.Services, board.Id);

        var token = GetJwtToken(user.Id, user.Email!);
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await Client.GetAsync($"/api/boards/{board.Id}/tasks/{task.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var taskResponse = await response.Content.ReadFromJsonAsync<BoardTaskResponse>();
        Assert.NotNull(taskResponse);
        Assert.Equal(task.Id, taskResponse.Id);
        Assert.Equal("Test Task", taskResponse.Title);
    }

    [Fact]
    public async Task GetBoardTaskById_WithNonExistentTask_ReturnsNotFound()
    {
        // Arrange
        await ClearDatabaseAsync();
        var user = await TestHelpers.CreateTestUserAsync(Factory.Services);
        var board = await TestHelpers.CreateTestBoardAsync(Factory.Services, user.Id);

        var token = GetJwtToken(user.Id, user.Email!);
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var nonExistentTaskId = 99999;

        // Act
        var response = await Client.GetAsync($"/api/boards/{board.Id}/tasks/{nonExistentTaskId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
