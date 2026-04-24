using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;
using Microsoft.AspNetCore.JsonPatch;
using Moq;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Common.Exceptions.Custom;
using WorthBoards.IntegrationTests.Infrastructure;

namespace WorthBoards.IntegrationTests.Tests;

public class CommentControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    // Route: api/boards/{boardId}/tasks/{taskId}/comments
    private const string BaseUrl = "/api/boards/1/tasks/1/comments";

    public CommentControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetAllMocks();
    }

    private HttpClient AuthClient() => _factory.CreateClient().WithAuth(userId: 1);

    private static StringContent Json(object body) =>
        new(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

    private static StringContent JsonPatch(object body) =>
        new(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json-patch+json");

    private static CommentResponse MakeComment(int id = 1) =>
        new() { Id = id, TaskId = 1, UserId = 1, Content = "Hello", CreationDate = DateTime.UtcNow, Version = 0 };

    // ── GetAllBoardTaskComments ───────────────────────────────────────────

    [Fact]
    public async Task GetAllComments_Authenticated_ReturnsOk()
    {
        _factory.CommentServiceMock
            .Setup(s => s.GetAllBoardTaskCommentsAsync(1, It.IsAny<CancellationToken>(), 0, 10))
            .ReturnsAsync((new List<CommentResponse> { MakeComment() }, 1));

        var response = await AuthClient().GetAsync(BaseUrl);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetAllComments_NoToken_ReturnsUnauthorized()
    {
        var response = await _factory.CreateClient().GetAsync(BaseUrl);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAllComments_EmptyResult_PageCountIsOne()
    {
        _factory.CommentServiceMock
            .Setup(s => s.GetAllBoardTaskCommentsAsync(1, It.IsAny<CancellationToken>(), 0, 10))
            .ReturnsAsync((new List<CommentResponse>(), 0));

        // totalCount==0 → pageCount=1 (ternary true branch)
        var response = await AuthClient().GetAsync(BaseUrl);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── GetCommentById ────────────────────────────────────────────────────

    [Fact]
    public async Task GetCommentById_ExistingComment_ReturnsOk()
    {
        _factory.CommentServiceMock
            .Setup(s => s.GetCommentByIdAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeComment(1));

        var response = await AuthClient().GetAsync($"{BaseUrl}/1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetCommentById_NotFound_ReturnsNotFound()
    {
        _factory.CommentServiceMock
            .Setup(s => s.GetCommentByIdAsync(1, 999, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Comment not found."));

        var response = await AuthClient().GetAsync($"{BaseUrl}/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── CreateComment ─────────────────────────────────────────────────────

    [Fact]
    public async Task CreateComment_Authenticated_ReturnsCreated()
    {
        _factory.CommentServiceMock
            .Setup(s => s.CreateCommentAsync(1, 1, It.IsAny<CommentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeComment(1));

        var response = await AuthClient().PostAsync(BaseUrl, Json(new { content = "Hello world" }));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CreateComment_NoToken_ReturnsUnauthorized()
    {
        var response = await _factory.CreateClient().PostAsync(BaseUrl, Json(new { content = "Hello world" }));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── DeleteComment ─────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteComment_ExistingComment_ReturnsNoContent()
    {
        _factory.CommentServiceMock
            .Setup(s => s.DeleteCommentAsync(1, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await AuthClient().DeleteAsync($"{BaseUrl}/1");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteComment_NotFound_ReturnsNotFound()
    {
        _factory.CommentServiceMock
            .Setup(s => s.DeleteCommentAsync(999, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Comment not found."));

        var response = await AuthClient().DeleteAsync($"{BaseUrl}/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── UpdateComment ─────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateComment_ValidRequest_ReturnsOk()
    {
        _factory.CommentServiceMock
            .Setup(s => s.UpdateCommentAsync(1, It.IsAny<CommentUpdateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeComment(1));

        var response = await AuthClient().PutAsync($"{BaseUrl}/1",
            Json(new { content = "Updated", version = 0 }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UpdateComment_NotFound_ReturnsNotFound()
    {
        _factory.CommentServiceMock
            .Setup(s => s.UpdateCommentAsync(999, It.IsAny<CommentUpdateRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Comment not found."));

        var response = await AuthClient().PutAsync($"{BaseUrl}/999",
            Json(new { content = "Updated", version = 0 }));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── PatchComment ──────────────────────────────────────────────────────

    [Fact]
    public async Task PatchComment_ValidRequest_ReturnsOk()
    {
        _factory.CommentServiceMock
            .Setup(s => s.PatchCommentAsync(1, It.IsAny<JsonPatchDocument<CommentUpdateRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeComment(1));

        var patch = new[] { new { op = "replace", path = "/content", value = "Patched" } };
        var response = await AuthClient().PatchAsync($"{BaseUrl}/1", JsonPatch(patch));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PatchComment_NoToken_ReturnsUnauthorized()
    {
        var patch = new[] { new { op = "replace", path = "/content", value = "Patched" } };
        var response = await _factory.CreateClient().PatchAsync($"{BaseUrl}/1", JsonPatch(patch));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}