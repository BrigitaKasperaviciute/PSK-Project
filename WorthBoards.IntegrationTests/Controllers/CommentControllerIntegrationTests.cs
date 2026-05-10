using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using System.Text.Json;
using Xunit;
using WorthBoards.IntegrationTests.Infrastructure;

namespace WorthBoards.IntegrationTests.Controllers;

public class CommentControllerIntegrationTests : IntegrationTestBase
{
    [Fact]
    public async Task GetAllBoardTaskComments_ValidTask_ReturnsOkWithCommentsList()
    {
        // Arrange
        await ResetDatabaseAsync();
        var helper = new TestHelper(HttpClient, this);
        var (userId1, token1, _) = await helper.CreateAndLoginUserAsync();
        
        var board = await helper.CreateBoardAsync(userId1, token1);
        var task = await helper.CreateBoardTaskAsync(board.Id, token1);
        var comment = await helper.CreateCommentAsync(board.Id, task.Id, token1);
        
        SetBearerToken(token1);

        // Act
        var response = await HttpClient.GetAsync($"api/boards/{board.Id}/tasks/{task.Id}/comments");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("items");
    }

    [Fact]
    public async Task GetAllBoardTaskComments_NonExistentTask_ReturnsNotFound()
    {
        // Arrange
        await ResetDatabaseAsync();
        var helper = new TestHelper(HttpClient, this);
        var (userId1, token1, _) = await helper.CreateAndLoginUserAsync();
        
        var board = await helper.CreateBoardAsync(userId1, token1);
        SetBearerToken(token1);

        // Act
        var response = await HttpClient.GetAsync($"api/boards/{board.Id}/tasks/99999/comments");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        result.GetProperty("items").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task GetAllBoardTaskComments_WithPagination_ReturnsPagedResults()
    {
        // Arrange
        await ResetDatabaseAsync();
        var helper = new TestHelper(HttpClient, this);
        var (userId1, token1, _) = await helper.CreateAndLoginUserAsync();
        
        var board = await helper.CreateBoardAsync(userId1, token1);
        var task = await helper.CreateBoardTaskAsync(board.Id, token1);
        
        for (int i = 0; i < 15; i++)
        {
            await helper.CreateCommentAsync(board.Id, task.Id, token1);
        }
        
        SetBearerToken(token1);

        // Act
        var response = await HttpClient.GetAsync($"api/boards/{board.Id}/tasks/{task.Id}/comments?pageNum=0&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        result.GetProperty("pageSize").GetInt32().Should().Be(10);
    }

    [Fact]
    public async Task GetCommentById_ValidComment_ReturnsOkWithCommentData()
    {
        // Arrange
        await ResetDatabaseAsync();
        var helper = new TestHelper(HttpClient, this);
        var (userId1, token1, _) = await helper.CreateAndLoginUserAsync();
        
        var board = await helper.CreateBoardAsync(userId1, token1);
        var task = await helper.CreateBoardTaskAsync(board.Id, token1);
        var comment = await helper.CreateCommentAsync(board.Id, task.Id, token1);
        
        SetBearerToken(token1);

        // Act
        var response = await HttpClient.GetAsync($"api/boards/{board.Id}/tasks/{task.Id}/comments/{comment.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        result.GetProperty("id").GetInt32().Should().Be(comment.Id);
    }

    [Fact]
    public async Task GetCommentById_NonExistentComment_ReturnsNotFound()
    {
        // Arrange
        await ResetDatabaseAsync();
        var helper = new TestHelper(HttpClient, this);
        var (userId1, token1, _) = await helper.CreateAndLoginUserAsync();
        
        var board = await helper.CreateBoardAsync(userId1, token1);
        var task = await helper.CreateBoardTaskAsync(board.Id, token1);
        
        SetBearerToken(token1);

        // Act
        var response = await HttpClient.GetAsync($"api/boards/{board.Id}/tasks/{task.Id}/comments/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateComment_ValidRequest_ReturnsCreatedWithCommentData()
    {
        // Arrange
        await ResetDatabaseAsync();
        var helper = new TestHelper(HttpClient, this);
        var (userId1, token1, _) = await helper.CreateAndLoginUserAsync();
        
        var board = await helper.CreateBoardAsync(userId1, token1);
        var task = await helper.CreateBoardTaskAsync(board.Id, token1);
        
        var commentRequest = TestDataBuilder.CreateCommentRequest();
        SetBearerToken(token1);

        // Act
        var response = await HttpClient.PostAsJsonAsync(
            $"api/boards/{board.Id}/tasks/{task.Id}/comments",
            commentRequest
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        result.GetProperty("id").GetInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CreateComment_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        await ResetDatabaseAsync();
        var commentRequest = TestDataBuilder.CreateCommentRequest();

        // Act
        var response = await HttpClient.PostAsJsonAsync(
            "api/boards/1/tasks/1/comments",
            commentRequest
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteComment_ValidComment_ReturnsNoContent()
    {
        // Arrange
        await ResetDatabaseAsync();
        var helper = new TestHelper(HttpClient, this);
        var (userId1, token1, _) = await helper.CreateAndLoginUserAsync();
        
        var board = await helper.CreateBoardAsync(userId1, token1);
        var task = await helper.CreateBoardTaskAsync(board.Id, token1);
        var comment = await helper.CreateCommentAsync(board.Id, task.Id, token1);
        
        SetBearerToken(token1);

        // Act
        var response = await HttpClient.DeleteAsync(
            $"api/boards/{board.Id}/tasks/{task.Id}/comments/{comment.Id}"
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteComment_NonExistentComment_ReturnsNotFound()
    {
        // Arrange
        await ResetDatabaseAsync();
        var helper = new TestHelper(HttpClient, this);
        var (userId1, token1, _) = await helper.CreateAndLoginUserAsync();
        
        var board = await helper.CreateBoardAsync(userId1, token1);
        var task = await helper.CreateBoardTaskAsync(board.Id, token1);
        
        SetBearerToken(token1);

        // Act
        var response = await HttpClient.DeleteAsync(
            $"api/boards/{board.Id}/tasks/{task.Id}/comments/99999"
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateComment_ValidRequest_ReturnsOkWithUpdatedComment()
    {
        // Arrange
        await ResetDatabaseAsync();
        var helper = new TestHelper(HttpClient, this);
        var (userId1, token1, _) = await helper.CreateAndLoginUserAsync();
        
        var board = await helper.CreateBoardAsync(userId1, token1);
        var task = await helper.CreateBoardTaskAsync(board.Id, token1);
        var comment = await helper.CreateCommentAsync(board.Id, task.Id, token1);
        
        var updateRequest = TestDataBuilder.CreateCommentUpdateRequest("Updated comment content");
        SetBearerToken(token1);

        // Act
        var response = await HttpClient.PutAsJsonAsync(
            $"api/boards/{board.Id}/tasks/{task.Id}/comments/{comment.Id}",
            updateRequest
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PatchComment_ValidRequest_ReturnsOkWithPatchedComment()
    {
        // Arrange
        await ResetDatabaseAsync();
        var helper = new TestHelper(HttpClient, this);
        var (userId1, token1, _) = await helper.CreateAndLoginUserAsync();
        
        var board = await helper.CreateBoardAsync(userId1, token1);
        var task = await helper.CreateBoardTaskAsync(board.Id, token1);
        var comment = await helper.CreateCommentAsync(board.Id, task.Id, token1);
        
        var patchRequest = @"[
            { ""op"": ""replace"", ""path"": ""/content"", ""value"": ""Patched comment"" }
        ]";
        
        SetBearerToken(token1);

        // Act
        var request = new HttpRequestMessage(new HttpMethod("PATCH"), $"api/boards/{board.Id}/tasks/{task.Id}/comments/{comment.Id}")
        {
            Content = new StringContent(patchRequest, System.Text.Encoding.UTF8, "application/json")
        };
        var response = await HttpClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
