using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Common.Enums;
using WorthBoards.Common.Exceptions;
using WorthBoards.IntegrationTests.Infrastructure;
using Xunit;

namespace WorthBoards.IntegrationTests.Controllers;

[Collection(nameof(ApiCollection))]
[Trait("Category", "Integration")]
public sealed class CommentControllerTests : IAsyncLifetime
{
    private readonly ApiFactory _factory;

    public CommentControllerTests(ApiFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        await using var db = _factory.CreateDbContext();
        await DatabaseSeeder.CleanDatabaseAsync(db);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ---------------------------------------------------------------------------
    // GET /api/boards/{boardId}/tasks/{taskId}/comments  [Authorize]
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetAllBoardTaskComments_WhenAuthenticated_ReturnsPagedComments()
    {
        // Arrange - board, task, and two comments
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var user = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync();
        await seeder.LinkUserToBoardAsync(board.Id, user.Id, UserRoleEnum.VIEWER);
        var task = await seeder.CreateBoardTaskAsync(board.Id);
        await seeder.CreateCommentAsync(task.Id, user.Id, "First comment");
        await seeder.CreateCommentAsync(task.Id, user.Id, "Second comment");

        var client = _factory.CreateAuthenticatedClient(user.Id, user.UserName!, user.Email!);

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}/tasks/{task.Id}/comments");

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - response body
        var body = await response.Content.ReadFromJsonAsync<PagedResult<CommentResponse>>();
        body.Should().NotBeNull();
        body!.Items.Should().HaveCount(2);
        body.Items.Should().Contain(c => c.Content == "First comment");
        body.Items.Should().Contain(c => c.Content == "Second comment");
    }

    [Fact]
    public async Task GetAllBoardTaskComments_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var user = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync();
        var task = await seeder.CreateBoardTaskAsync(board.Id);
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}/tasks/{task.Id}/comments");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------------------------------------------------------------------------
    // GET /api/boards/{boardId}/tasks/{taskId}/comments/{commentId}  [Authorize]
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetCommentById_WithExistingComment_ReturnsComment()
    {
        // Arrange
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var user = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync();
        await seeder.LinkUserToBoardAsync(board.Id, user.Id, UserRoleEnum.VIEWER);
        var task = await seeder.CreateBoardTaskAsync(board.Id);
        var comment = await seeder.CreateCommentAsync(task.Id, user.Id, "My specific comment");

        var client = _factory.CreateAuthenticatedClient(user.Id, user.UserName!, user.Email!);

        // Act
        var response = await client.GetAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/comments/{comment.Id}");

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - response body
        var body = await response.Content.ReadFromJsonAsync<CommentResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(comment.Id);
        body.Content.Should().Be("My specific comment");
        body.UserId.Should().Be(user.Id);
        body.TaskId.Should().Be(task.Id);
    }

    [Fact]
    public async Task GetCommentById_WithNonExistentComment_ReturnsNotFound()
    {
        // Arrange
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var user = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync();
        var task = await seeder.CreateBoardTaskAsync(board.Id);

        var client = _factory.CreateAuthenticatedClient(user.Id, user.UserName!, user.Email!);

        // Act
        var response = await client.GetAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/comments/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------------------------------------------------------------------------
    // POST /api/boards/{boardId}/tasks/{taskId}/comments  [Authorize]
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task CreateComment_WhenAuthenticated_ReturnsCreatedAndPersistsComment()
    {
        // Arrange
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var user = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync();
        await seeder.LinkUserToBoardAsync(board.Id, user.Id, UserRoleEnum.VIEWER);
        var task = await seeder.CreateBoardTaskAsync(board.Id);

        var client = _factory.CreateAuthenticatedClient(user.Id, user.UserName!, user.Email!);

        var request = new CommentRequest { Content = "This is a new comment" };

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/comments", request);

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        // Assert - response body
        var body = await response.Content.ReadFromJsonAsync<CommentResponse>();
        body.Should().NotBeNull();
        body!.Content.Should().Be("This is a new comment");
        body.UserId.Should().Be(user.Id);
        body.TaskId.Should().Be(task.Id);
        body.Edited.Should().BeFalse();
        body.Id.Should().BeGreaterThan(0);

        // Assert - database state
        await using var db = _factory.CreateDbContext();
        var persisted = await db.Comments.AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == body.Id);
        persisted.Should().NotBeNull();
        persisted!.Content.Should().Be("This is a new comment");
        persisted.UserId.Should().Be(user.Id);
        persisted.BoardTaskId.Should().Be(task.Id);
    }

    [Fact]
    public async Task CreateComment_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var user = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync();
        var task = await seeder.CreateBoardTaskAsync(board.Id);

        var client = _factory.CreateClient();
        var request = new CommentRequest { Content = "Should fail" };

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/comments", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------------------------------------------------------------------------
    // DELETE /api/boards/{boardId}/tasks/{taskId}/comments/{commentId}  [Authorize]
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task DeleteComment_WhenAuthenticated_ReturnsNoContentAndRemovesComment()
    {
        // Arrange
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var user = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync();
        var task = await seeder.CreateBoardTaskAsync(board.Id);
        var comment = await seeder.CreateCommentAsync(task.Id, user.Id, "Comment to delete");

        var client = _factory.CreateAuthenticatedClient(user.Id, user.UserName!, user.Email!);

        // Act
        var response = await client.DeleteAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/comments/{comment.Id}");

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert - database state
        await using var db = _factory.CreateDbContext();
        var deleted = await db.Comments.AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == comment.Id);
        deleted.Should().BeNull();
    }

    // ---------------------------------------------------------------------------
    // PUT /api/boards/{boardId}/tasks/{taskId}/comments/{commentId}  [Authorize]
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task UpdateComment_WhenAuthenticated_ReturnsOkWithUpdatedContent()
    {
        // Arrange
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var user = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync();
        var task = await seeder.CreateBoardTaskAsync(board.Id);
        var comment = await seeder.CreateCommentAsync(task.Id, user.Id, "Original content");

        var client = _factory.CreateAuthenticatedClient(user.Id, user.UserName!, user.Email!);

        // Fetch current version for optimistic concurrency
        var getResponse = await client.GetAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/comments/{comment.Id}");
        var current = await getResponse.Content.ReadFromJsonAsync<CommentResponse>();

        var request = new CommentUpdateRequest
        {
            Content = "Updated content",
            Version = current!.Version
        };

        // Act
        var response = await client.PutAsJsonAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/comments/{comment.Id}", request);

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - response body
        var body = await response.Content.ReadFromJsonAsync<CommentResponse>();
        body.Should().NotBeNull();
        body!.Content.Should().Be("Updated content");
        body.Edited.Should().BeTrue();

        // Assert - database state
        await using var db = _factory.CreateDbContext();
        var persisted = await db.Comments.AsNoTracking().SingleAsync(c => c.Id == comment.Id);
        persisted.Content.Should().Be("Updated content");
        persisted.Edited.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateComment_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var user = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync();
        var task = await seeder.CreateBoardTaskAsync(board.Id);
        var comment = await seeder.CreateCommentAsync(task.Id, user.Id, "Original");

        var client = _factory.CreateClient();
        var request = new CommentUpdateRequest { Content = "Attempted update", Version = 0 };

        // Act
        var response = await client.PutAsJsonAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/comments/{comment.Id}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------------------------------------------------------------------------
    // DELETE /api/boards/{boardId}/tasks/{taskId}/comments/{commentId} — negative
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task DeleteComment_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var user = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync();
        var task = await seeder.CreateBoardTaskAsync(board.Id);
        var comment = await seeder.CreateCommentAsync(task.Id, user.Id, "Should not be deleted");

        var client = _factory.CreateClient();

        // Act
        var response = await client.DeleteAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/comments/{comment.Id}");

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Assert - comment still exists
        await using var db = _factory.CreateDbContext();
        var stillExists = await db.Comments.AsNoTracking()
            .AnyAsync(c => c.Id == comment.Id);
        stillExists.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteComment_WithNonExistentComment_ReturnsNotFound()
    {
        // Arrange
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var user = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync();
        var task = await seeder.CreateBoardTaskAsync(board.Id);

        var client = _factory.CreateAuthenticatedClient(user.Id, user.UserName!, user.Email!);

        // Act
        var response = await client.DeleteAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/comments/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------------------------------------------------------------------------
    // PATCH /api/boards/{boardId}/tasks/{taskId}/comments/{commentId}  [Authorize]
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task PatchComment_AsAuthor_ReturnsOkWithPatchedContent()
    {
        // Arrange
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var author = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync();
        await seeder.LinkUserToBoardAsync(board.Id, author.Id, UserRoleEnum.EDITOR);
        var task = await seeder.CreateBoardTaskAsync(board.Id);
        var comment = await seeder.CreateCommentAsync(task.Id, author.Id, "Original content");

        var client = _factory.CreateAuthenticatedClient(author.Id, author.UserName!, author.Email!);

        // Fetch comment to get version for optimistic concurrency
        var getResponse = await client.GetAsync($"/api/boards/{board.Id}/tasks/{task.Id}/comments/{comment.Id}");
        var current = await getResponse.Content.ReadFromJsonAsync<CommentResponse>();

        var patchDoc = new object[]
        {
            new { op = "replace", path = "/content", value = (object)"Patched content" },
            new { op = "replace", path = "/version", value = (object)current!.Version }
        };
        var content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(patchDoc),
            System.Text.Encoding.UTF8,
            "application/json-patch+json");

        // Act
        var response = await client.PatchAsync($"/api/boards/{board.Id}/tasks/{task.Id}/comments/{comment.Id}", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CommentResponse>();
        body!.Content.Should().Be("Patched content");
    }

    // ---------------------------------------------------------------------------
    // Helper record for deserializing paged responses
    // ---------------------------------------------------------------------------
    private record PagedResult<T>(IEnumerable<T> Items, int PageNumber, int PageCount, int PageSize);
}
