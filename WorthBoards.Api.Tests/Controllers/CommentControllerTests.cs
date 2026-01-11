using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using WorthBoards.Api.Tests.Infrastructure;
using WorthBoards.Business.Dtos.Responses;

namespace WorthBoards.Api.Tests.Controllers;

public class CommentControllerTests : IntegrationTestBase
{
    [Fact]
    public async Task GetCommentById_WithExistingComment_ReturnsComment()
    {
        // Arrange
        await ClearDatabaseAsync();
        var user = await TestHelpers.CreateTestUserAsync(Factory.Services);
        var board = await TestHelpers.CreateTestBoardAsync(Factory.Services, user.Id);
        var task = await TestHelpers.CreateTestTaskAsync(Factory.Services, board.Id);
        var comment = await TestHelpers.CreateTestCommentAsync(Factory.Services, task.Id, user.Id);

        var token = GetJwtToken(user.Id, user.Email!);
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await Client.GetAsync($"/api/boards/{board.Id}/tasks/{task.Id}/comments/{comment.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var commentResponse = await response.Content.ReadFromJsonAsync<CommentResponse>();
        Assert.NotNull(commentResponse);
        Assert.Equal(comment.Id, commentResponse.Id);
        Assert.Equal("Test Comment", commentResponse.Content);
    }

    [Fact]
    public async Task GetCommentById_WithNonExistentComment_ReturnsNotFound()
    {
        // Arrange
        await ClearDatabaseAsync();
        var user = await TestHelpers.CreateTestUserAsync(Factory.Services);
        var board = await TestHelpers.CreateTestBoardAsync(Factory.Services, user.Id);
        var task = await TestHelpers.CreateTestTaskAsync(Factory.Services, board.Id);

        var token = GetJwtToken(user.Id, user.Email!);
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var nonExistentCommentId = 99999;

        // Act
        var response = await Client.GetAsync($"/api/boards/{board.Id}/tasks/{task.Id}/comments/{nonExistentCommentId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
