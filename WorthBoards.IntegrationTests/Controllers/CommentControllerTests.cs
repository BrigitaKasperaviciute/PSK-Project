using System.Net;
using System.Net.Http.Json;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.IntegrationTests.Infrastructure;

namespace WorthBoards.IntegrationTests.Controllers;

public class CommentControllerTests : IntegrationTestBase
{
    public CommentControllerTests(CustomWebApplicationFactory factory) : base(factory) { }

    private async Task<(HttpClient Client, int BoardId, int TaskId)> SetupBoardAndTaskAsync()
    {
        var (token, _) = await RegisterAndLoginAsync();
        var client = CreateAuthenticatedClient(token);
        var board = await CreateBoardAsync(client);
        var task = await CreateTaskAsync(client, board.Id);
        return (client, board.Id, task.Id);
    }

    [Fact]
    public async Task GetComments_Authenticated_ReturnsOk()
    {
        var (client, boardId, taskId) = await SetupBoardAndTaskAsync();

        var response = await client.GetAsync($"/api/boards/{boardId}/tasks/{taskId}/comments");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetComments_Unauthenticated_ReturnsUnauthorized()
    {
        var (client, boardId, taskId) = await SetupBoardAndTaskAsync();

        var response = await CreateAnonymousClient().GetAsync($"/api/boards/{boardId}/tasks/{taskId}/comments");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetCommentById_ExistingComment_ReturnsOk()
    {
        var (client, boardId, taskId) = await SetupBoardAndTaskAsync();
        var comment = await CreateCommentAsync(client, boardId, taskId);

        var response = await client.GetAsync($"/api/boards/{boardId}/tasks/{taskId}/comments/{comment.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CommentResponse>();
        body!.Id.Should().Be(comment.Id);
    }

    [Fact]
    public async Task GetCommentById_Unauthenticated_ReturnsUnauthorized()
    {
        var (client, boardId, taskId) = await SetupBoardAndTaskAsync();
        var comment = await CreateCommentAsync(client, boardId, taskId);

        var response = await CreateAnonymousClient()
            .GetAsync($"/api/boards/{boardId}/tasks/{taskId}/comments/{comment.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateComment_Authenticated_ReturnsCreated()
    {
        var (client, boardId, taskId) = await SetupBoardAndTaskAsync();

        var request = new CommentRequest { Content = "Hello from test!" };
        var response = await client.PostAsJsonAsync($"/api/boards/{boardId}/tasks/{taskId}/comments", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<CommentResponse>();
        body!.Content.Should().Be("Hello from test!");
    }

    [Fact]
    public async Task CreateComment_Unauthenticated_ReturnsUnauthorized()
    {
        var (client, boardId, taskId) = await SetupBoardAndTaskAsync();

        var request = new CommentRequest { Content = "Unauthorized comment" };
        var response = await CreateAnonymousClient()
            .PostAsJsonAsync($"/api/boards/{boardId}/tasks/{taskId}/comments", request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteComment_Authenticated_ReturnsNoContent()
    {
        var (client, boardId, taskId) = await SetupBoardAndTaskAsync();
        var comment = await CreateCommentAsync(client, boardId, taskId);

        var response = await client.DeleteAsync($"/api/boards/{boardId}/tasks/{taskId}/comments/{comment.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteComment_Unauthenticated_ReturnsUnauthorized()
    {
        var (client, boardId, taskId) = await SetupBoardAndTaskAsync();
        var comment = await CreateCommentAsync(client, boardId, taskId);

        var response = await CreateAnonymousClient()
            .DeleteAsync($"/api/boards/{boardId}/tasks/{taskId}/comments/{comment.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateComment_Authenticated_ReturnsOk()
    {
        var (client, boardId, taskId) = await SetupBoardAndTaskAsync();
        var comment = await CreateCommentAsync(client, boardId, taskId);

        var updateRequest = new CommentUpdateRequest { Content = "Updated comment", Version = comment.Version };
        var response = await client.PutAsJsonAsync(
            $"/api/boards/{boardId}/tasks/{taskId}/comments/{comment.Id}", updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CommentResponse>();
        body!.Content.Should().Be("Updated comment");
    }

    [Fact]
    public async Task UpdateComment_Unauthenticated_ReturnsUnauthorized()
    {
        var (client, boardId, taskId) = await SetupBoardAndTaskAsync();
        var comment = await CreateCommentAsync(client, boardId, taskId);

        var updateRequest = new CommentUpdateRequest { Content = "Hacked", Version = comment.Version };
        var response = await CreateAnonymousClient()
            .PutAsJsonAsync($"/api/boards/{boardId}/tasks/{taskId}/comments/{comment.Id}", updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PatchComment_Authenticated_ReturnsOk()
    {
        var (client, boardId, taskId) = await SetupBoardAndTaskAsync();
        var comment = await CreateCommentAsync(client, boardId, taskId);

        var patchDoc = new[]
        {
            new { op = "replace", path = "/content", value = (object)"Patched comment" },
            new { op = "replace", path = "/version", value = (object)comment.Version }
        };
        var response = await client.PatchAsJsonAsync(
            $"/api/boards/{boardId}/tasks/{taskId}/comments/{comment.Id}", patchDoc);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PatchComment_Unauthenticated_ReturnsUnauthorized()
    {
        var (client, boardId, taskId) = await SetupBoardAndTaskAsync();
        var comment = await CreateCommentAsync(client, boardId, taskId);

        var patchDoc = new[] { new { op = "replace", path = "/content", value = "Hacked" } };
        var response = await CreateAnonymousClient()
            .PatchAsJsonAsync($"/api/boards/{boardId}/tasks/{taskId}/comments/{comment.Id}", patchDoc);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
