using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Data.Database;
using WorthBoards.IntegrationTests.Infrastructure;
using Xunit;

namespace WorthBoards.IntegrationTests;

public class CommentControllerTests(WorthBoardsWebApplicationFactory factory)
    : IClassFixture<WorthBoardsWebApplicationFactory>
{
    private HttpClient AuthorizedClient(int userId, string userName, string email)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTokenHelper.GenerateToken(userId, userName, email));
        return client;
    }

    private static string CommentsUrl(int boardId, int taskId) =>
        $"/api/boards/{boardId}/tasks/{taskId}/comments";

    // ── GET all comments ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllBoardTaskComments_Authenticated_ReturnsOkWithComments()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync(owner);
        var task = await seeder.CreateTaskAsync(board);
        var comment = await seeder.CreateCommentAsync(task, owner, "Hello world");
        var client = AuthorizedClient(owner.Id, owner.UserName!, owner.Email!);

        // Act
        var response = await client.GetAsync(CommentsUrl(board.Id, task.Id));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedCommentResult>();
        body!.Items.Should().Contain(c => c.Id == comment.Id);
    }

    [Fact]
    public async Task GetAllBoardTaskComments_Unauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/boards/1/tasks/1/comments");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── GET comment by id ────────────────────────────────────────────────────

    [Fact]
    public async Task GetCommentById_ExistingComment_ReturnsOk()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync(owner);
        var task = await seeder.CreateTaskAsync(board);
        var comment = await seeder.CreateCommentAsync(task, owner, "Specific comment");
        var client = AuthorizedClient(owner.Id, owner.UserName!, owner.Email!);

        // Act
        var response = await client.GetAsync($"{CommentsUrl(board.Id, task.Id)}/{comment.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CommentResponse>();
        body!.Content.Should().Be("Specific comment");
    }

    [Fact]
    public async Task GetCommentById_NonExistingComment_ReturnsNotFound()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync(owner);
        var task = await seeder.CreateTaskAsync(board);
        var client = AuthorizedClient(owner.Id, owner.UserName!, owner.Email!);

        // Act
        var response = await client.GetAsync($"{CommentsUrl(board.Id, task.Id)}/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── POST comment ─────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateComment_ValidRequest_ReturnsCreated()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync(owner);
        var task = await seeder.CreateTaskAsync(board);
        var client = AuthorizedClient(owner.Id, owner.UserName!, owner.Email!);
        var request = new CommentRequest { Content = "Created comment" };

        // Act
        var response = await client.PostAsJsonAsync(CommentsUrl(board.Id, task.Id), request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<CommentResponse>();
        body!.Content.Should().Be("Created comment");
        body.UserId.Should().Be(owner.Id);

        using var dbScope = factory.Services.CreateScope();
        var db = dbScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Comments.Any(c => c.Content == "Created comment" && c.BoardTaskId == task.Id).Should().BeTrue();
    }

    [Fact]
    public async Task CreateComment_Unauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var client = factory.CreateClient();
        var request = new CommentRequest { Content = "Unauthorized" };

        // Act
        var response = await client.PostAsJsonAsync("/api/boards/1/tasks/1/comments", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── DELETE comment ───────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteComment_ExistingComment_ReturnsNoContent()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync(owner);
        var task = await seeder.CreateTaskAsync(board);
        var comment = await seeder.CreateCommentAsync(task, owner);
        var client = AuthorizedClient(owner.Id, owner.UserName!, owner.Email!);

        // Act
        var response = await client.DeleteAsync($"{CommentsUrl(board.Id, task.Id)}/{comment.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var dbScope = factory.Services.CreateScope();
        var db = dbScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Comments.Any(c => c.Id == comment.Id).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteComment_NonExistingComment_ReturnsNotFound()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync(owner);
        var task = await seeder.CreateTaskAsync(board);
        var client = AuthorizedClient(owner.Id, owner.UserName!, owner.Email!);

        // Act
        var response = await client.DeleteAsync($"{CommentsUrl(board.Id, task.Id)}/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── PUT comment ──────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateComment_ExistingComment_ReturnsOk()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync(owner);
        var task = await seeder.CreateTaskAsync(board);
        var comment = await seeder.CreateCommentAsync(task, owner, "Original");
        var client = AuthorizedClient(owner.Id, owner.UserName!, owner.Email!);
        var request = new CommentUpdateRequest { Content = "Updated content", Version = 0 };

        // Act
        var response = await client.PutAsJsonAsync($"{CommentsUrl(board.Id, task.Id)}/{comment.Id}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CommentResponse>();
        body!.Content.Should().Be("Updated content");
    }

    [Fact]
    public async Task UpdateComment_NonExistingComment_ReturnsNotFound()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync(owner);
        var task = await seeder.CreateTaskAsync(board);
        var client = AuthorizedClient(owner.Id, owner.UserName!, owner.Email!);
        var request = new CommentUpdateRequest { Content = "Doesn't matter", Version = 0 };

        // Act
        var response = await client.PutAsJsonAsync($"{CommentsUrl(board.Id, task.Id)}/999999", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── PATCH comment ────────────────────────────────────────────────────────

    [Fact]
    public async Task PatchComment_ExistingComment_ReturnsOk()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync(owner);
        var task = await seeder.CreateTaskAsync(board);
        var comment = await seeder.CreateCommentAsync(task, owner, "Before patch");
        var client = AuthorizedClient(owner.Id, owner.UserName!, owner.Email!);

        var patchDoc = Newtonsoft.Json.JsonConvert.SerializeObject(new[]
        {
            new { op = "replace", path = "/content", value = "After patch" }
        });
        var content = new StringContent(patchDoc, Encoding.UTF8, "application/json-patch+json");

        // Act
        var response = await client.PatchAsync($"{CommentsUrl(board.Id, task.Id)}/{comment.Id}", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CommentResponse>();
        body!.Content.Should().Be("After patch");
    }

    [Fact]
    public async Task PatchComment_NonExistingComment_ReturnsNotFound()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync(owner);
        var task = await seeder.CreateTaskAsync(board);
        var client = AuthorizedClient(owner.Id, owner.UserName!, owner.Email!);

        var patchDoc = Newtonsoft.Json.JsonConvert.SerializeObject(new[]
        {
            new { op = "replace", path = "/content", value = "Doesn't matter" }
        });
        var content = new StringContent(patchDoc, Encoding.UTF8, "application/json-patch+json");

        // Act
        var response = await client.PatchAsync($"{CommentsUrl(board.Id, task.Id)}/999999", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

file record PagedCommentResult(IEnumerable<CommentResponse> Items, int PageNumber, int PageCount, int PageSize);
