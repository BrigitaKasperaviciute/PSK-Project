using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using WorthBoards.Api.Tests.Infrastructure;
using WorthBoards.Business.Dtos.Responses;

namespace WorthBoards.Api.Tests.Controllers;

public class TaskOnUserControllerTests : IntegrationTestBase
{
    [Fact]
    public async Task GetUsersLinkedToTask_WithExistingTask_ReturnsUsers()
    {
        // Arrange
        await ClearDatabaseAsync();
        var user = await TestHelpers.CreateTestUserAsync(Factory.Services);
        var board = await TestHelpers.CreateTestBoardAsync(Factory.Services, user.Id);
        var task = await TestHelpers.CreateTestTaskAsync(Factory.Services, board.Id);
        await TestHelpers.CreateTestTaskOnUserAsync(Factory.Services, task.Id, user.Id);

        var token = GetJwtToken(user.Id, user.Email!);
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await Client.GetAsync($"/api/boards/{board.Id}/tasks/{task.Id}/users");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var usersResponse = await response.Content.ReadFromJsonAsync<IEnumerable<LinkedUserToTaskResponse>>();
        Assert.NotNull(usersResponse);
        Assert.NotEmpty(usersResponse);
    }

    [Fact]
    public async Task GetUsersLinkedToTask_WithNonExistentTask_ReturnsNotFound()
    {
        // Arrange
        await ClearDatabaseAsync();
        var user = await TestHelpers.CreateTestUserAsync(Factory.Services);
        var board = await TestHelpers.CreateTestBoardAsync(Factory.Services, user.Id);

        var token = GetJwtToken(user.Id, user.Email!);
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var nonExistentTaskId = 99999;

        // Act
        var response = await Client.GetAsync($"/api/boards/{board.Id}/tasks/{nonExistentTaskId}/users");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
