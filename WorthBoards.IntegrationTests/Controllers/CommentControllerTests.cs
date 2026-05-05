using Microsoft.EntityFrameworkCore;

namespace WorthBoards.IntegrationTests.Controllers;

[Collection(nameof(ApiCollection))]
[Trait("Category", "Integration")]
public sealed class CommentControllerTests(CustomWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    // Route: api/boards/{boardId}/tasks/{taskId}/comments
    // All endpoints require [Authorize] (not role-based, just authenticated).

    // ── GET /comments ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllBoardTaskComments_WhenAuthenticated_ReturnsOkWithComments()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);
        var board = await CreateBoardAsync();
        var task  = await CreateTaskAsync(board.Id);
        await CreateCommentAsync(board.Id, task.Id, "First comment");
        await CreateCommentAsync(board.Id, task.Id, "Second comment");

        // Act
        var response = await Client.GetAsync($"/api/boards/{board.Id}/tasks/{task.Id}/comments");

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<PagedComments>();
        body.Should().NotBeNull();
        body!.Items.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task GetAllBoardTaskComments_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);
        var board = await CreateBoardAsync();
        var task  = await CreateTaskAsync(board.Id);
        ClearAuthToken();

        // Act
        var response = await Client.GetAsync($"/api/boards/{board.Id}/tasks/{task.Id}/comments");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── GET /comments/{commentId} ─────────────────────────────────────────────

    [Fact]
    public async Task GetCommentById_WithExistingComment_ReturnsOkWithComment()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);
        var board   = await CreateBoardAsync();
        var task    = await CreateTaskAsync(board.Id);
        var comment = await CreateCommentAsync(board.Id, task.Id, "Findable comment");

        // Act
        var response = await Client.GetAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/comments/{comment.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<CommentResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(comment.Id);
        body.Content.Should().Be("Findable comment");
        body.Edited.Should().BeFalse();
    }

    [Fact]
    public async Task GetCommentById_WithNonexistentId_ReturnsNotFound()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);
        var board = await CreateBoardAsync();
        var task  = await CreateTaskAsync(board.Id);

        // Act
        var response = await Client.GetAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/comments/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── POST /comments ────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateComment_WhenAuthenticated_ReturnsCreatedAndPersistsComment()
    {
        // Arrange
        var (userId, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);
        var board = await CreateBoardAsync();
        var task  = await CreateTaskAsync(board.Id);

        var request = new CommentRequest { Content = "New comment content" };

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/comments", request);

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<CommentResponse>();
        body.Should().NotBeNull();
        body!.Content.Should().Be("New comment content");
        body.TaskId.Should().Be(task.Id);
        body.UserId.Should().Be(userId);
        body.Edited.Should().BeFalse();

        // Assert – database state
        await using var db = CreateDbContext();
        var persisted = await db.Comments.AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == body.Id);
        persisted.Should().NotBeNull();
        persisted!.Content.Should().Be("New comment content");
        persisted.UserId.Should().Be(userId);
    }

    [Fact]
    public async Task CreateComment_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);
        var board = await CreateBoardAsync();
        var task  = await CreateTaskAsync(board.Id);
        ClearAuthToken();

        var request = new CommentRequest { Content = "Should not be created" };

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/comments", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── DELETE /comments/{commentId} ──────────────────────────────────────────

    [Fact]
    public async Task DeleteComment_WithExistingComment_ReturnsNoContentAndRemovesComment()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);
        var board   = await CreateBoardAsync();
        var task    = await CreateTaskAsync(board.Id);
        var comment = await CreateCommentAsync(board.Id, task.Id, "Delete me");

        // Act
        var response = await Client.DeleteAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/comments/{comment.Id}");

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert – database state
        await using var db = CreateDbContext();
        var deleted = await db.Comments.AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == comment.Id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteComment_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);
        var board   = await CreateBoardAsync();
        var task    = await CreateTaskAsync(board.Id);
        var comment = await CreateCommentAsync(board.Id, task.Id, "Stay alive");
        ClearAuthToken();

        // Act
        var response = await Client.DeleteAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/comments/{comment.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Assert – comment still exists
        await using var db = CreateDbContext();
        var still = await db.Comments.AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == comment.Id);
        still.Should().NotBeNull();
    }

    // ── PUT /comments/{commentId} ─────────────────────────────────────────────

    [Fact]
    public async Task UpdateComment_WithValidRequest_ReturnsOkWithUpdatedContent()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);
        var board   = await CreateBoardAsync();
        var task    = await CreateTaskAsync(board.Id);
        var comment = await CreateCommentAsync(board.Id, task.Id, "Original content");

        var updateRequest = new CommentUpdateRequest
        {
            Content = "Updated content",
            Version = comment.Version
        };

        // Act
        var response = await Client.PutAsJsonAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/comments/{comment.Id}", updateRequest);

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<CommentResponse>();
        body.Should().NotBeNull();
        body!.Content.Should().Be("Updated content");

        // Assert – database state
        await using var db = CreateDbContext();
        var updated = await db.Comments.AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == comment.Id);
        updated!.Content.Should().Be("Updated content");
    }

    [Fact]
    public async Task UpdateComment_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);
        var board   = await CreateBoardAsync();
        var task    = await CreateTaskAsync(board.Id);
        var comment = await CreateCommentAsync(board.Id, task.Id, "Do not change");
        ClearAuthToken();

        var updateRequest = new CommentUpdateRequest
        {
            Content = "Hacked content",
            Version = comment.Version
        };

        // Act
        var response = await Client.PutAsJsonAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/comments/{comment.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── PATCH /comments/{commentId} ───────────────────────────────────────────

    [Fact]
    public async Task PatchComment_WithValidPatch_ReturnsOkWithPatchedContent()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);
        var board   = await CreateBoardAsync();
        var task    = await CreateTaskAsync(board.Id);
        var comment = await CreateCommentAsync(board.Id, task.Id, "Patch me");

        var patch = new object[]
        {
            new { op = "replace", path = "/content", value = (object)"Patched content" },
            new { op = "replace", path = "/version", value = (object)comment.Version }
        };

        using var content = JsonContent.Create(patch);
        content.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue("application/json-patch+json");

        // Act
        var response = await Client.PatchAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/comments/{comment.Id}", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<CommentResponse>();
        body!.Content.Should().Be("Patched content");
    }

    [Fact]
    public async Task PatchComment_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);
        var board   = await CreateBoardAsync();
        var task    = await CreateTaskAsync(board.Id);
        var comment = await CreateCommentAsync(board.Id, task.Id, "No patch");
        ClearAuthToken();

        var patch = new[] { new { op = "replace", path = "/content", value = "Hacked" } };
        using var content = JsonContent.Create(patch);
        content.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue("application/json-patch+json");

        // Act
        var response = await Client.PatchAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/comments/{comment.Id}", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}

file record PagedComments(List<CommentResponse> Items, int PageNumber, int PageCount, int PageSize);
