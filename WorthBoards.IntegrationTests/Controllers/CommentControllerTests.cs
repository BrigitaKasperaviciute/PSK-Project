using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
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

    private async Task<(int UserId, HttpClient Client)> CreateUserWithClientAsync(string? username = null, string? email = null)
    {
        await using var db = _factory.CreateDbContext();
        var userManager = _factory.Services.CreateScope().ServiceProvider
            .GetRequiredService<UserManager<Data.Identity.ApplicationUser>>();
        var user = await DatabaseSeeder.CreateUserAsync(db, userManager, username, email);
        return (user.Id, _factory.CreateAuthenticatedClient(user.Id, user.UserName!, user.Email!));
    }

    // --- GET /api/boards/{boardId}/tasks/{taskId}/comments ---

    [Fact]
    public async Task GetAllBoardTaskComments_WhenAuthenticated_ReturnsOkWithComments()
    {
        // Arrange
        var (userId, client) = await CreateUserWithClientAsync();
        await using var db = _factory.CreateDbContext();
        var board = await DatabaseSeeder.CreateBoardAsync(db, userId);
        var task = await DatabaseSeeder.CreateTaskAsync(db, board.Id, "Task with Comments");
        await DatabaseSeeder.CreateCommentAsync(db, task.Id, userId, "First comment");
        await DatabaseSeeder.CreateCommentAsync(db, task.Id, userId, "Second comment");

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}/tasks/{task.Id}/comments");

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - response body contains items
        var body = await response.Content.ReadFromJsonAsync<PagedCommentsResponse>();
        body.Should().NotBeNull();
        body!.Items.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task GetAllBoardTaskComments_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/boards/1/tasks/1/comments");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --- GET /api/boards/{boardId}/tasks/{taskId}/comments/{commentId} ---

    [Fact]
    public async Task GetCommentById_WhenCommentExists_ReturnsOkWithComment()
    {
        // Arrange
        var (userId, client) = await CreateUserWithClientAsync();
        await using var db = _factory.CreateDbContext();
        var board = await DatabaseSeeder.CreateBoardAsync(db, userId);
        var task = await DatabaseSeeder.CreateTaskAsync(db, board.Id);
        var comment = await DatabaseSeeder.CreateCommentAsync(db, task.Id, userId, "My comment");

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}/tasks/{task.Id}/comments/{comment.Id}");

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - response body
        var body = await response.Content.ReadFromJsonAsync<CommentResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(comment.Id);
        body.Content.Should().Be("My comment");
        body.UserId.Should().Be(userId);

        // Assert - database state
        await using var verifyDb = _factory.CreateDbContext();
        var persisted = await verifyDb.Comments.AsNoTracking().SingleOrDefaultAsync(c => c.Id == comment.Id);
        persisted.Should().NotBeNull();
    }

    [Fact]
    public async Task GetCommentById_WhenCommentNotFound_ReturnsNotFound()
    {
        // Arrange
        var (userId, client) = await CreateUserWithClientAsync();
        await using var db = _factory.CreateDbContext();
        var board = await DatabaseSeeder.CreateBoardAsync(db, userId);
        var task = await DatabaseSeeder.CreateTaskAsync(db, board.Id);

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}/tasks/{task.Id}/comments/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- POST /api/boards/{boardId}/tasks/{taskId}/comments ---

    [Fact]
    public async Task CreateComment_WhenAuthenticated_ReturnsCreatedAndPersistsComment()
    {
        // Arrange
        var (userId, client) = await CreateUserWithClientAsync();
        await using var db = _factory.CreateDbContext();
        var board = await DatabaseSeeder.CreateBoardAsync(db, userId);
        var task = await DatabaseSeeder.CreateTaskAsync(db, board.Id);

        var request = new CommentRequest { Content = "New comment text" };

        // Act
        var response = await client.PostAsJsonAsync($"/api/boards/{board.Id}/tasks/{task.Id}/comments", request);

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        // Assert - response body
        var body = await response.Content.ReadFromJsonAsync<CommentResponse>();
        body.Should().NotBeNull();
        body!.Content.Should().Be("New comment text");
        body.UserId.Should().Be(userId);
        body.Edited.Should().BeFalse();

        // Assert - database state
        await using var verifyDb = _factory.CreateDbContext();
        var persisted = await verifyDb.Comments.AsNoTracking().SingleOrDefaultAsync(c => c.Id == body.Id);
        persisted.Should().NotBeNull();
        persisted!.Content.Should().Be("New comment text");
    }

    [Fact]
    public async Task CreateComment_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new CommentRequest { Content = "Sneaky comment" };

        // Act
        var response = await client.PostAsJsonAsync("/api/boards/1/tasks/1/comments", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --- DELETE /api/boards/{boardId}/tasks/{taskId}/comments/{commentId} ---

    [Fact]
    public async Task DeleteComment_WhenCommentExists_ReturnsNoContentAndRemovesComment()
    {
        // Arrange
        var (userId, client) = await CreateUserWithClientAsync();
        await using var db = _factory.CreateDbContext();
        var board = await DatabaseSeeder.CreateBoardAsync(db, userId);
        var task = await DatabaseSeeder.CreateTaskAsync(db, board.Id);
        var comment = await DatabaseSeeder.CreateCommentAsync(db, task.Id, userId, "To be deleted");

        // Act
        var response = await client.DeleteAsync($"/api/boards/{board.Id}/tasks/{task.Id}/comments/{comment.Id}");

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert - database state
        await using var verifyDb = _factory.CreateDbContext();
        var deleted = await verifyDb.Comments.AsNoTracking().SingleOrDefaultAsync(c => c.Id == comment.Id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteComment_WhenCommentNotFound_ReturnsNotFound()
    {
        // Arrange
        var (userId, client) = await CreateUserWithClientAsync();
        await using var db = _factory.CreateDbContext();
        var board = await DatabaseSeeder.CreateBoardAsync(db, userId);
        var task = await DatabaseSeeder.CreateTaskAsync(db, board.Id);

        // Act
        var response = await client.DeleteAsync($"/api/boards/{board.Id}/tasks/{task.Id}/comments/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- PUT /api/boards/{boardId}/tasks/{taskId}/comments/{commentId} ---

    [Fact]
    public async Task UpdateComment_WhenCommentExists_ReturnsOkWithUpdatedContent()
    {
        // Arrange
        var (userId, client) = await CreateUserWithClientAsync();
        await using var db = _factory.CreateDbContext();
        var board = await DatabaseSeeder.CreateBoardAsync(db, userId);
        var task = await DatabaseSeeder.CreateTaskAsync(db, board.Id);
        var comment = await DatabaseSeeder.CreateCommentAsync(db, task.Id, userId, "Original content");

        var request = new CommentUpdateRequest
        {
            Content = "Updated content",
            Version = comment.Version
        };

        // Act
        var response = await client.PutAsJsonAsync($"/api/boards/{board.Id}/tasks/{task.Id}/comments/{comment.Id}", request);

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - response body
        var body = await response.Content.ReadFromJsonAsync<CommentResponse>();
        body.Should().NotBeNull();
        body!.Content.Should().Be("Updated content");
        body.Edited.Should().BeTrue();

        // Assert - database state
        await using var verifyDb = _factory.CreateDbContext();
        var persisted = await verifyDb.Comments.AsNoTracking().SingleOrDefaultAsync(c => c.Id == comment.Id);
        persisted!.Content.Should().Be("Updated content");
        persisted.Edited.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateComment_WhenCommentNotFound_ReturnsNotFound()
    {
        // Arrange
        var (userId, client) = await CreateUserWithClientAsync();
        await using var db = _factory.CreateDbContext();
        var board = await DatabaseSeeder.CreateBoardAsync(db, userId);
        var task = await DatabaseSeeder.CreateTaskAsync(db, board.Id);

        var request = new CommentUpdateRequest { Content = "No comment", Version = 0 };

        // Act
        var response = await client.PutAsJsonAsync($"/api/boards/{board.Id}/tasks/{task.Id}/comments/99999", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- PATCH /api/boards/{boardId}/tasks/{taskId}/comments/{commentId} ---

    [Fact]
    public async Task PatchComment_WhenCommentExists_ReturnsOkWithPatchedContent()
    {
        // Arrange
        var (userId, client) = await CreateUserWithClientAsync();
        await using var db = _factory.CreateDbContext();
        var board = await DatabaseSeeder.CreateBoardAsync(db, userId);
        var task = await DatabaseSeeder.CreateTaskAsync(db, board.Id);
        var comment = await DatabaseSeeder.CreateCommentAsync(db, task.Id, userId, "Patch me");

        var patchDoc = new[]
        {
            new { op = "replace", path = "/content", value = (object)"Patched content" },
            new { op = "replace", path = "/version", value = (object)comment.Version }
        };

        var request = new System.Net.Http.StringContent(
            System.Text.Json.JsonSerializer.Serialize(patchDoc),
            System.Text.Encoding.UTF8,
            "application/json-patch+json"
        );

        // Act
        var response = await client.PatchAsync($"/api/boards/{board.Id}/tasks/{task.Id}/comments/{comment.Id}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<CommentResponse>();
        body.Should().NotBeNull();
        body!.Content.Should().Be("Patched content");
    }

    [Fact]
    public async Task PatchComment_WithStaleVersion_ReturnsConflict()
    {
        // Arrange
        var (userId, client) = await CreateUserWithClientAsync();
        await using var db = _factory.CreateDbContext();
        var board = await DatabaseSeeder.CreateBoardAsync(db, userId);
        var task = await DatabaseSeeder.CreateTaskAsync(db, board.Id, "Conflict Task");
        var comment = await DatabaseSeeder.CreateCommentAsync(db, task.Id, userId, "Conflict me");

        var patchDoc = new[]
        {
            new { op = "replace", path = "/content", value = (object)"Conflict content" },
            new { op = "replace", path = "/version", value = (object)(comment.Version + 1) }
        };

        var request = new System.Net.Http.StringContent(
            System.Text.Json.JsonSerializer.Serialize(patchDoc),
            System.Text.Encoding.UTF8,
            "application/json-patch+json"
        );

        // Act
        var response = await client.PatchAsync($"/api/boards/{board.Id}/tasks/{task.Id}/comments/{comment.Id}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // Helper record for paginated response deserialization
    private record PagedCommentsResponse(
        IEnumerable<CommentResponse> Items,
        int PageNumber,
        int PageCount,
        int PageSize
    );
}
