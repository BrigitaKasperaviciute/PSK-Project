using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.IntegrationTests.Infrastructure;

namespace WorthBoards.IntegrationTests.Controllers;

public class CommentControllerTests(CustomWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    // ── GET /api/boards/{boardId}/tasks/{taskId}/comments ─────────────────────

    [Fact]
    public async Task GetAllBoardTaskComments_AuthenticatedUser_ReturnsOkWithComments()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(token);
        var task = await CreateTaskAsync(board.Id, token);
        var comment = await CreateCommentAsync(board.Id, task.Id, token);
        SetAuthToken(token);

        // Act
        var response = await Client.GetAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/comments");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content
            .ReadFromJsonAsync<CommentPagedResult>();
        body.Should().NotBeNull();
        body!.Items.Should().Contain(c => c.Id == comment.Id);
    }

    [Fact]
    public async Task GetAllBoardTaskComments_UnauthenticatedUser_ReturnsUnauthorized()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(token);
        var task = await CreateTaskAsync(board.Id, token);
        ClearAuthToken();

        // Act
        var response = await Client.GetAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/comments");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── GET /api/boards/{boardId}/tasks/{taskId}/comments/{commentId} ─────────

    [Fact]
    public async Task GetCommentById_ExistingComment_ReturnsOkWithComment()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(token);
        var task = await CreateTaskAsync(board.Id, token);
        var comment = await CreateCommentAsync(board.Id, task.Id, token);
        SetAuthToken(token);

        // Act
        var response = await Client.GetAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/comments/{comment.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<CommentResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(comment.Id);
        body.Content.Should().Be(comment.Content);
    }

    [Fact]
    public async Task GetCommentById_NonExistingComment_ReturnsNotFound()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(token);
        var task = await CreateTaskAsync(board.Id, token);
        SetAuthToken(token);

        // Act
        var response = await Client.GetAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/comments/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── POST /api/boards/{boardId}/tasks/{taskId}/comments ────────────────────

    [Fact]
    public async Task CreateComment_AuthenticatedUser_ReturnsCreatedAndPersistsToDb()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(token);
        var task = await CreateTaskAsync(board.Id, token);
        SetAuthToken(token);

        var request = new CommentRequest { Content = "My test comment" };

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/comments", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<CommentResponse>();
        body.Should().NotBeNull();
        body!.Content.Should().Be("My test comment");
        body.TaskId.Should().Be(task.Id);

        var dbComment = await QueryAsync(db => db.Comments.FindAsync(body.Id).AsTask());
        dbComment.Should().NotBeNull();
        dbComment!.Content.Should().Be("My test comment");
    }

    [Fact]
    public async Task CreateComment_UnauthenticatedUser_ReturnsUnauthorized()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(token);
        var task = await CreateTaskAsync(board.Id, token);
        ClearAuthToken();

        var request = new CommentRequest { Content = "Should not be created" };

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/comments", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── PUT /api/boards/{boardId}/tasks/{taskId}/comments/{commentId} ─────────

    [Fact]
    public async Task UpdateComment_ExistingComment_ReturnsOkWithUpdatedContent()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(token);
        var task = await CreateTaskAsync(board.Id, token);
        var comment = await CreateCommentAsync(board.Id, task.Id, token);
        SetAuthToken(token);

        var updateRequest = new CommentUpdateRequest
        {
            Content = "Updated comment content",
            Version = comment.Version
        };

        // Act
        var response = await Client.PutAsJsonAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/comments/{comment.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<CommentResponse>();
        body.Should().NotBeNull();
        body!.Content.Should().Be("Updated comment content");
        body.Edited.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateComment_UnauthenticatedUser_ReturnsUnauthorized()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(token);
        var task = await CreateTaskAsync(board.Id, token);
        var comment = await CreateCommentAsync(board.Id, task.Id, token);
        ClearAuthToken();

        var updateRequest = new CommentUpdateRequest
        {
            Content = "Should fail",
            Version = comment.Version
        };

        // Act
        var response = await Client.PutAsJsonAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/comments/{comment.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── DELETE /api/boards/{boardId}/tasks/{taskId}/comments/{commentId} ──────

    [Fact]
    public async Task DeleteComment_ExistingComment_ReturnsNoContentAndRemovesFromDb()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(token);
        var task = await CreateTaskAsync(board.Id, token);
        var comment = await CreateCommentAsync(board.Id, task.Id, token);
        SetAuthToken(token);

        // Act
        var response = await Client.DeleteAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/comments/{comment.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var dbComment = await QueryAsync(db => db.Comments.FindAsync(comment.Id).AsTask());
        dbComment.Should().BeNull();
    }

    [Fact]
    public async Task DeleteComment_UnauthenticatedUser_ReturnsUnauthorized()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(token);
        var task = await CreateTaskAsync(board.Id, token);
        var comment = await CreateCommentAsync(board.Id, task.Id, token);
        ClearAuthToken();

        // Act
        var response = await Client.DeleteAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/comments/{comment.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}

file record CommentPagedResult(IEnumerable<CommentResponse> Items, int PageNumber, int PageCount, int PageSize);
