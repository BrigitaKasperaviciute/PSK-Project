using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.IntegrationTests.Infrastructure;

namespace WorthBoards.IntegrationTests.Controllers;

public class CommentControllerTests : IntegrationTestBase
{
    public CommentControllerTests(CustomWebApplicationFactory factory) : base(factory) { }

    private static string CommentsUrl(int boardId, int taskId) =>
        $"/api/boards/{boardId}/tasks/{taskId}/comments";

    private static string CommentUrl(int boardId, int taskId, int commentId) =>
        $"/api/boards/{boardId}/tasks/{taskId}/comments/{commentId}";

    [Fact]
    public async Task GetComments_Authenticated_ReturnsOk()
    {
        var (token, _) = await RegisterAndLoginAsync();
        var client = CreateAuthenticatedClient(token);
        var board = await CreateBoardAsync(client);
        var task = await CreateTaskAsync(client, board.Id);

        var response = await client.GetAsync(CommentsUrl(board.Id, task.Id));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetComments_Unauthenticated_ReturnsUnauthorized()
    {
        var (token, _) = await RegisterAndLoginAsync();
        var client = CreateAuthenticatedClient(token);
        var board = await CreateBoardAsync(client);
        var task = await CreateTaskAsync(client, board.Id);

        var response = await CreateAnonymousClient().GetAsync(CommentsUrl(board.Id, task.Id));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetCommentById_ExistingComment_ReturnsOk()
    {
        var (token, _) = await RegisterAndLoginAsync();
        var client = CreateAuthenticatedClient(token);
        var board = await CreateBoardAsync(client);
        var task = await CreateTaskAsync(client, board.Id);
        var comment = await CreateCommentAsync(client, board.Id, task.Id);

        var response = await client.GetAsync(CommentUrl(board.Id, task.Id, comment.Id));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CommentResponse>();
        body!.Id.Should().Be(comment.Id);
    }

    [Fact]
    public async Task GetCommentById_NonExistingComment_ReturnsNotFound()
    {
        var (token, _) = await RegisterAndLoginAsync();
        var client = CreateAuthenticatedClient(token);
        var board = await CreateBoardAsync(client);
        var task = await CreateTaskAsync(client, board.Id);

        var response = await client.GetAsync(CommentUrl(board.Id, task.Id, 999999));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateComment_Authenticated_ReturnsCreated()
    {
        var (token, _) = await RegisterAndLoginAsync();
        var client = CreateAuthenticatedClient(token);
        var board = await CreateBoardAsync(client);
        var task = await CreateTaskAsync(client, board.Id);

        var request = new CommentRequest { Content = "A new comment" };
        var response = await client.PostAsJsonAsync(CommentsUrl(board.Id, task.Id), request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<CommentResponse>();
        body!.Content.Should().Be("A new comment");
    }

    [Fact]
    public async Task CreateComment_Unauthenticated_ReturnsUnauthorized()
    {
        var (token, _) = await RegisterAndLoginAsync();
        var client = CreateAuthenticatedClient(token);
        var board = await CreateBoardAsync(client);
        var task = await CreateTaskAsync(client, board.Id);

        var request = new CommentRequest { Content = "Anon comment" };
        var response = await CreateAnonymousClient().PostAsJsonAsync(CommentsUrl(board.Id, task.Id), request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteComment_ExistingComment_ReturnsNoContent()
    {
        var (token, _) = await RegisterAndLoginAsync();
        var client = CreateAuthenticatedClient(token);
        var board = await CreateBoardAsync(client);
        var task = await CreateTaskAsync(client, board.Id);
        var comment = await CreateCommentAsync(client, board.Id, task.Id);

        var response = await client.DeleteAsync(CommentUrl(board.Id, task.Id, comment.Id));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteComment_NonExistingComment_ReturnsNotFound()
    {
        var (token, _) = await RegisterAndLoginAsync();
        var client = CreateAuthenticatedClient(token);
        var board = await CreateBoardAsync(client);
        var task = await CreateTaskAsync(client, board.Id);

        var response = await client.DeleteAsync(CommentUrl(board.Id, task.Id, 999999));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateComment_ValidVersion_ReturnsOk()
    {
        var (token, _) = await RegisterAndLoginAsync();
        var client = CreateAuthenticatedClient(token);
        var board = await CreateBoardAsync(client);
        var task = await CreateTaskAsync(client, board.Id);
        var comment = await CreateCommentAsync(client, board.Id, task.Id);

        var updateRequest = new CommentUpdateRequest { Content = "Updated content", Version = comment.Version };
        var response = await client.PutAsJsonAsync(CommentUrl(board.Id, task.Id, comment.Id), updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CommentResponse>();
        body!.Content.Should().Be("Updated content");
    }

    [Fact]
    public async Task UpdateComment_VersionMismatch_ReturnsConflict()
    {
        var (token, _) = await RegisterAndLoginAsync();
        var client = CreateAuthenticatedClient(token);
        var board = await CreateBoardAsync(client);
        var task = await CreateTaskAsync(client, board.Id);
        var comment = await CreateCommentAsync(client, board.Id, task.Id);

        var updateRequest = new CommentUpdateRequest { Content = "Bad version", Version = comment.Version + 99 };
        var response = await client.PutAsJsonAsync(CommentUrl(board.Id, task.Id, comment.Id), updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task UpdateComment_Unauthenticated_ReturnsUnauthorized()
    {
        var (token, _) = await RegisterAndLoginAsync();
        var client = CreateAuthenticatedClient(token);
        var board = await CreateBoardAsync(client);
        var task = await CreateTaskAsync(client, board.Id);
        var comment = await CreateCommentAsync(client, board.Id, task.Id);

        var updateRequest = new CommentUpdateRequest { Content = "Anon update", Version = comment.Version };
        var response = await CreateAnonymousClient()
            .PutAsJsonAsync(CommentUrl(board.Id, task.Id, comment.Id), updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PatchComment_ValidRequest_ReturnsOk()
    {
        var (token, _) = await RegisterAndLoginAsync();
        var client = CreateAuthenticatedClient(token);
        var board = await CreateBoardAsync(client);
        var task = await CreateTaskAsync(client, board.Id);
        var comment = await CreateCommentAsync(client, board.Id, task.Id);

        var patchDoc = new[]
        {
            new { op = "replace", path = "/content", value = (object)"Patched comment" },
            new { op = "replace", path = "/version", value = (object)(int)comment.Version }
        };
        var response = await client.PatchAsJsonAsync(CommentUrl(board.Id, task.Id, comment.Id), patchDoc);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PatchComment_Unauthenticated_ReturnsUnauthorized()
    {
        var (token, _) = await RegisterAndLoginAsync();
        var client = CreateAuthenticatedClient(token);
        var board = await CreateBoardAsync(client);
        var task = await CreateTaskAsync(client, board.Id);
        var comment = await CreateCommentAsync(client, board.Id, task.Id);

        var patchDoc = new[] { new { op = "replace", path = "/content", value = "Hacked" } };
        var response = await CreateAnonymousClient()
            .PatchAsJsonAsync(CommentUrl(board.Id, task.Id, comment.Id), patchDoc);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
