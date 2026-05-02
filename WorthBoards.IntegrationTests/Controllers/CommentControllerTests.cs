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
    private record PatchOp(string op, string path, object value);

    // ── GET all comments ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllBoardTaskComments_AsMember_ReturnsPaginatedComments()
    {
        // Arrange
        var (_, _, client) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(client);
        var task = await CreateTaskAsync(client, board.Id);
        var comment = await CreateCommentAsync(client, board.Id, task.Id, "Hello comment");

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}/tasks/{task.Id}/comments");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PaginatedResponse<CommentResponse>>();
        body!.Items.Should().Contain(c => c.Id == comment.Id);
    }

    [Fact]
    public async Task GetAllBoardTaskComments_Unauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var (_, _, ownerClient) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(ownerClient);
        var task = await CreateTaskAsync(ownerClient, board.Id);
        var anonClient = Factory.CreateClient();

        // Act
        var response = await anonClient.GetAsync($"/api/boards/{board.Id}/tasks/{task.Id}/comments");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── GET comment by id ────────────────────────────────────────────────────

    [Fact]
    public async Task GetCommentById_ExistingComment_ReturnsCommentResponse()
    {
        // Arrange
        var (_, _, client) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(client);
        var task = await CreateTaskAsync(client, board.Id);
        var comment = await CreateCommentAsync(client, board.Id, task.Id, "Find me");

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}/tasks/{task.Id}/comments/{comment.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<CommentResponse>();
        result!.Id.Should().Be(comment.Id);
        result.Content.Should().Be("Find me");
    }

    [Fact]
    public async Task GetCommentById_NonExistentComment_ReturnsNotFound()
    {
        // Arrange
        var (_, _, client) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(client);
        var task = await CreateTaskAsync(client, board.Id);

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}/tasks/{task.Id}/comments/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── POST (create) comment ────────────────────────────────────────────────

    [Fact]
    public async Task CreateComment_ValidRequest_ReturnsCreatedCommentAndPersists()
    {
        // Arrange
        var (userId, _, client) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(client);
        var task = await CreateTaskAsync(client, board.Id);
        var request = new CommentRequest { Content = "My first comment" };

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/comments", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var comment = await response.Content.ReadFromJsonAsync<CommentResponse>();
        comment!.Content.Should().Be("My first comment");
        comment.UserId.Should().Be(userId);

        using var db = GetDbContext();
        db.Comments.Any(c => c.Id == comment.Id && c.BoardTaskId == task.Id).Should().BeTrue();
    }

    [Fact]
    public async Task CreateComment_Unauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var (_, _, ownerClient) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(ownerClient);
        var task = await CreateTaskAsync(ownerClient, board.Id);
        var anonClient = Factory.CreateClient();

        // Act
        var response = await anonClient.PostAsJsonAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/comments",
            new CommentRequest { Content = "Ghost comment" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── DELETE comment ───────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteComment_ExistingComment_ReturnsNoContentAndRemoves()
    {
        // Arrange
        var (_, _, client) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(client);
        var task = await CreateTaskAsync(client, board.Id);
        var comment = await CreateCommentAsync(client, board.Id, task.Id);

        // Act
        var response = await client.DeleteAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/comments/{comment.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var db = GetDbContext();
        db.Comments.Any(c => c.Id == comment.Id).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteComment_Unauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var (_, _, ownerClient) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(ownerClient);
        var task = await CreateTaskAsync(ownerClient, board.Id);
        var comment = await CreateCommentAsync(ownerClient, board.Id, task.Id);
        var anonClient = Factory.CreateClient();

        // Act
        var response = await anonClient.DeleteAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/comments/{comment.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── PUT (update) comment ─────────────────────────────────────────────────

    [Fact]
    public async Task UpdateComment_ValidRequest_ReturnsUpdatedComment()
    {
        // Arrange
        var (_, _, client) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(client);
        var task = await CreateTaskAsync(client, board.Id);
        var comment = await CreateCommentAsync(client, board.Id, task.Id, "Original");

        var updateRequest = new CommentUpdateRequest
        {
            Content = "Updated content",
            Version = comment.Version
        };

        // Act
        var response = await client.PutAsJsonAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/comments/{comment.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<CommentResponse>();
        updated!.Content.Should().Be("Updated content");
        updated.Edited.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateComment_StaleVersion_ReturnsConflict()
    {
        // Arrange
        var (_, _, client) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(client);
        var task = await CreateTaskAsync(client, board.Id);
        var comment = await CreateCommentAsync(client, board.Id, task.Id);

        var updateRequest = new CommentUpdateRequest
        {
            Content = "Stale update",
            Version = comment.Version + 999
        };

        // Act
        var response = await client.PutAsJsonAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/comments/{comment.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ── PATCH comment ────────────────────────────────────────────────────────

    [Fact]
    public async Task PatchComment_ValidPatch_ReturnsUpdatedComment()
    {
        // Arrange
        var (_, _, client) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(client);
        var task = await CreateTaskAsync(client, board.Id);
        var comment = await CreateCommentAsync(client, board.Id, task.Id, "Before patch");

        var patchDoc = new PatchOp[]
        {
            new("replace", "/content", "After patch"),
            new("replace", "/version", comment.Version)
        };

        // Act
        var response = await client.PatchAsJsonAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/comments/{comment.Id}", patchDoc);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var patched = await response.Content.ReadFromJsonAsync<CommentResponse>();
        patched!.Content.Should().Be("After patch");
    }

    [Fact]
    public async Task PatchComment_Unauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var (_, _, ownerClient) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(ownerClient);
        var task = await CreateTaskAsync(ownerClient, board.Id);
        var comment = await CreateCommentAsync(ownerClient, board.Id, task.Id);
        var anonClient = Factory.CreateClient();

        var patchDoc = new PatchOp[] { new("replace", "/content", "Forbidden") };

        // Act
        var response = await anonClient.PatchAsJsonAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/comments/{comment.Id}", patchDoc);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private record PaginatedResponse<T>(IEnumerable<T> Items, int PageNumber, int PageCount, int PageSize);
}
