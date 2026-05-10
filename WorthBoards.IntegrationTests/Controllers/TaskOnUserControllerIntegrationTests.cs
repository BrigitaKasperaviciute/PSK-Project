using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;
using WorthBoards.Common.Enums;
using WorthBoards.IntegrationTests.Infrastructure;

namespace WorthBoards.IntegrationTests.Controllers;

public class TaskOnUserControllerIntegrationTests : IntegrationTestBase
{
    [Fact]
    public async Task LinkUsersToTask_ValidRequest_ReturnsOkWithLinkedUsers()
    {
        // Arrange
        await ResetDatabaseAsync();
        var helper = new TestHelper(HttpClient, this);
        var (userId1, token1, _) = await helper.CreateAndLoginUserAsync();
        var (userId2, token2, _) = await helper.CreateAndLoginUserAsync();
        
        var board = await helper.CreateBoardAsync(userId1, token1);
        var task = await helper.CreateBoardTaskAsync(board.Id, token1);
        
        var linkRequest = new[] { TestDataBuilder.CreateLinkUserToTaskRequest(userId2) };
        SetBearerToken(token1);

        // Act
        var response = await HttpClient.PostAsJsonAsync(
            $"api/boards/{board.Id}/tasks/{task.Id}/users/link",
            linkRequest
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task LinkUsersToTask_WithoutEditorRole_ReturnsForbidden()
    {
        // Arrange
        await ResetDatabaseAsync();
        var helper = new TestHelper(HttpClient, this);
        var (userId1, token1, _) = await helper.CreateAndLoginUserAsync();
        var (userId2, token2, _) = await helper.CreateAndLoginUserAsync();
        var (userId3, token3, _) = await helper.CreateAndLoginUserAsync();
        
        var board = await helper.CreateBoardAsync(userId1, token1);
        await helper.LinkUserToBoardAsync(board.Id, userId2, token1, UserRoleEnum.VIEWER);
        
        var task = await helper.CreateBoardTaskAsync(board.Id, token1);
        
        var linkRequest = new[] { TestDataBuilder.CreateLinkUserToTaskRequest(userId3) };
        SetBearerToken(token2);

        // Act
        var response = await HttpClient.PostAsJsonAsync(
            $"api/boards/{board.Id}/tasks/{task.Id}/users/link",
            linkRequest
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UnlinkUsersFromTask_ValidRequest_ReturnsOkWithUnlinkedUsers()
    {
        // Arrange
        await ResetDatabaseAsync();
        var helper = new TestHelper(HttpClient, this);
        var (userId1, token1, _) = await helper.CreateAndLoginUserAsync();
        var (userId2, token2, _) = await helper.CreateAndLoginUserAsync();
        
        var board = await helper.CreateBoardAsync(userId1, token1);
        var task = await helper.CreateBoardTaskAsync(board.Id, token1);
        
        await helper.LinkUserToTaskAsync(board.Id, task.Id, userId2, token1);
        
        var unlinkRequest = new[] { TestDataBuilder.CreateLinkUserToTaskRequest(userId2) };
        SetBearerToken(token1);

        // Act
        var request = new HttpRequestMessage(HttpMethod.Delete, $"api/boards/{board.Id}/tasks/{task.Id}/users/unlink")
        {
            Content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(unlinkRequest),
                System.Text.Encoding.UTF8,
                "application/json"
            )
        };
        var response = await HttpClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UnlinkUsersFromTask_NonExistentLink_ReturnsNotFound()
    {
        // Arrange
        await ResetDatabaseAsync();
        var helper = new TestHelper(HttpClient, this);
        var (userId1, token1, _) = await helper.CreateAndLoginUserAsync();
        var (userId2, token2, _) = await helper.CreateAndLoginUserAsync();
        
        var board = await helper.CreateBoardAsync(userId1, token1);
        var task = await helper.CreateBoardTaskAsync(board.Id, token1);
        
        var unlinkRequest = new[] { TestDataBuilder.CreateLinkUserToTaskRequest(userId2) };
        SetBearerToken(token1);

        // Act
        var request = new HttpRequestMessage(HttpMethod.Delete, $"api/boards/{board.Id}/tasks/{task.Id}/users/unlink")
        {
            Content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(unlinkRequest),
                System.Text.Encoding.UTF8,
                "application/json"
            )
        };
        var response = await HttpClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetUsersLinkedToTask_ValidTask_ReturnsOkWithUsersList()
    {
        // Arrange
        await ResetDatabaseAsync();
        var helper = new TestHelper(HttpClient, this);
        var (userId1, token1, _) = await helper.CreateAndLoginUserAsync();
        var (userId2, token2, _) = await helper.CreateAndLoginUserAsync();
        
        var board = await helper.CreateBoardAsync(userId1, token1);
        var task = await helper.CreateBoardTaskAsync(board.Id, token1);
        
        await helper.LinkUserToTaskAsync(board.Id, task.Id, userId2, token1);
        
        SetBearerToken(token1);

        // Act
        var response = await HttpClient.GetAsync($"api/boards/{board.Id}/tasks/{task.Id}/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetUsersLinkedToTask_NonExistentTask_ReturnsNotFound()
    {
        // Arrange
        await ResetDatabaseAsync();
        var helper = new TestHelper(HttpClient, this);
        var (userId1, token1, _) = await helper.CreateAndLoginUserAsync();
        
        var board = await helper.CreateBoardAsync(userId1, token1);
        SetBearerToken(token1);

        // Act
        var response = await HttpClient.GetAsync($"api/boards/{board.Id}/tasks/99999/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
