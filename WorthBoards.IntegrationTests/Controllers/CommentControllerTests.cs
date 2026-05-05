using Microsoft.EntityFrameworkCore;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.IntegrationTests.Infrastructure;

namespace WorthBoards.IntegrationTests.Controllers;

public sealed class CommentControllerTests(CustomWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    // ── GetAllBoardTaskComments ───────────────────────────────────────────────

    [Fact]
    public async Task GetAllBoardTaskComments_WithExistingComments_ReturnsOkWithComments()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);
        var board = await CreateBoardAsync();
        var task = await CreateTaskAsync(board.Id);
        await CreateCommentAsync(board.Id, task.Id, "First comment");
        await CreateCommentAsync(board.Id, task.Id, "Second comment");

        // Act
        var response = await Client.GetAsync($"/api/boards/{board.Id}/tasks/{task.Id}/comments");

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert – response body
        var body = await response.Content.ReadFromJsonAsync<CommentPagedResult>();
        body.Should().NotBeNull();
        body!.Items.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task GetAllBoardTaskComments_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);
        var board = await CreateBoardAsync();
        var task = await CreateTaskAsync(board.Id);
        ClearAuthToken();

        // Act
        var response = await Client.GetAsync($"/api/boards/{board.Id}/tasks/{task.Id}/comments");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── GetCommentById ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetCommentById_WithExistingComment_ReturnsOkWithComment()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);
        var board = await CreateBoardAsync();
        var task = await CreateTaskAsync(board.Id);
        var comment = await CreateCommentAsync(board.Id, task.Id, "Hello");

        // Act
        var response = await Client.GetAsync($"/api/boards/{board.Id}/tasks/{task.Id}/comments/{comment.Id}");

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert – response body
        var body = await response.Content.ReadFromJsonAsync<CommentResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(comment.Id);
        body.Content.Should().Be("Hello");
        body.Edited.Should().BeFalse();
    }

    [Fact]
    public async Task GetCommentById_WithNonExistentComment_ReturnsNotFound()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);
        var board = await CreateBoardAsync();
        var task = await CreateTaskAsync(board.Id);

        // Act
        var response = await Client.GetAsync($"/api/boards/{board.Id}/tasks/{task.Id}/comments/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── CreateComment ─────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateComment_WithValidRequest_ReturnsCreatedAndPersistsComment()
    {
        // Arrange
        var (userId, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);
        var board = await CreateBoardAsync();
        var task = await CreateTaskAsync(board.Id);
        var request = new CommentRequest { Content = "My new comment" };

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/comments", request);

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        // Assert – response body
        var body = await response.Content.ReadFromJsonAsync<CommentResponse>();
        body.Should().NotBeNull();
        body!.Content.Should().Be(request.Content);
        body.UserId.Should().Be(userId);
        body.TaskId.Should().Be(task.Id);
        body.Edited.Should().BeFalse();

        // Assert – database state
        await using var db = Factory.CreateDbContext();
        var persisted = await db.Comments.AsNoTracking().SingleOrDefaultAsync(c => c.Id == body.Id);
        persisted.Should().NotBeNull();
        persisted!.Content.Should().Be(request.Content);
    }

    [Fact]
    public async Task CreateComment_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);
        var board = await CreateBoardAsync();
        var task = await CreateTaskAsync(board.Id);
        ClearAuthToken();

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/comments",
            new CommentRequest { Content = "Unauthorized" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── DeleteComment ─────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteComment_WithExistingComment_ReturnsNoContentAndRemovesFromDb()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);
        var board = await CreateBoardAsync();
        var task = await CreateTaskAsync(board.Id);
        var comment = await CreateCommentAsync(board.Id, task.Id);

        // Act
        var response = await Client.DeleteAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/comments/{comment.Id}");

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert – database state
        await using var db = Factory.CreateDbContext();
        var persisted = await db.Comments.AsNoTracking().SingleOrDefaultAsync(c => c.Id == comment.Id);
        persisted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteComment_WithNonExistentComment_ReturnsNotFound()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);
        var board = await CreateBoardAsync();
        var task = await CreateTaskAsync(board.Id);

        // Act
        var response = await Client.DeleteAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/comments/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── UpdateComment ─────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateComment_WithValidRequest_ReturnsOkAndUpdatesDb()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);
        var board = await CreateBoardAsync();
        var task = await CreateTaskAsync(board.Id);
        var comment = await CreateCommentAsync(board.Id, task.Id, "Original content");

        var updateRequest = new CommentUpdateRequest { Content = "Updated content" };

        // Act
        var response = await Client.PutAsJsonAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/comments/{comment.Id}", updateRequest);

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert – response body
        var body = await response.Content.ReadFromJsonAsync<CommentResponse>();
        body!.Content.Should().Be(updateRequest.Content);
        body.Edited.Should().BeTrue();

        // Assert – database state
        await using var db = Factory.CreateDbContext();
        var persisted = await db.Comments.AsNoTracking().SingleOrDefaultAsync(c => c.Id == comment.Id);
        persisted!.Content.Should().Be(updateRequest.Content);
        persisted.Edited.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateComment_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);
        var board = await CreateBoardAsync();
        var task = await CreateTaskAsync(board.Id);
        var comment = await CreateCommentAsync(board.Id, task.Id);
        ClearAuthToken();

        // Act
        var response = await Client.PutAsJsonAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/comments/{comment.Id}",
            new CommentUpdateRequest { Content = "Hijacked" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── PatchComment ──────────────────────────────────────────────────────────

    [Fact]
    public async Task PatchComment_WithValidPatch_ReturnsOkAndUpdatesContent()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);
        var board = await CreateBoardAsync();
        var task = await CreateTaskAsync(board.Id);
        var comment = await CreateCommentAsync(board.Id, task.Id, "Before patch");

        var patch = new[] { new { op = "replace", path = "/content", value = "After patch" } };

        // Act
        var response = await Client.PatchAsJsonAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/comments/{comment.Id}", patch);

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert – database state
        await using var db = Factory.CreateDbContext();
        var persisted = await db.Comments.AsNoTracking().SingleOrDefaultAsync(c => c.Id == comment.Id);
        persisted!.Content.Should().Be("After patch");
    }

    [Fact]
    public async Task PatchComment_WithNonExistentComment_ReturnsNotFound()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);
        var board = await CreateBoardAsync();
        var task = await CreateTaskAsync(board.Id);

        var patch = new[] { new { op = "replace", path = "/content", value = "Ghost" } };

        // Act
        var response = await Client.PatchAsJsonAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/comments/99999", patch);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

file record CommentPagedResult(IEnumerable<CommentResponse> Items, int PageNumber, int PageCount, int PageSize);
