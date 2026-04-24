using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.JsonPatch;
using Xunit;
using Moq;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Common.Enums;
using WorthBoards.Common.Exceptions.Custom;
using WorthBoards.IntegrationTests.Infrastructure;

namespace WorthBoards.IntegrationTests.Tests;

public class BoardControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public BoardControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetAllMocks();
    }

    private HttpClient AuthClient(int userId = 1) => _factory.CreateClient().WithAuth(userId);

    private static StringContent Json(object body) =>
        new(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

    private static StringContent JsonPatch(object body) =>
        new(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json-patch+json");

    private static BoardResponse MakeBoard(int id = 1) =>
        new() { Id = id, Title = "Board", Description = "Desc", ImageURL = "", CreationDate = DateTime.UtcNow, Version = 0 };

    private void SetupOwnerRole() =>
        _factory.BoardServiceMock
            .Setup(s => s.GetUserRoleByBoardIdAndUserIdAsync(It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(UserRoleEnum.OWNER);

    private void SetupNoRole() =>
        _factory.BoardServiceMock
            .Setup(s => s.GetUserRoleByBoardIdAndUserIdAsync(It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync((UserRoleEnum?)null);

    // ── GetAllCurrentUserBoards ───────────────────────────────────────────

    [Fact]
    public async Task GetAllBoards_Authenticated_ReturnsOk()
    {
        _factory.BoardServiceMock
            .Setup(s => s.GetUserBoardsAsync(1, 0, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<BoardResponse> { MakeBoard() }, 1));

        var response = await AuthClient().GetAsync("/api/boards");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetAllBoards_NoToken_ReturnsUnauthorized()
    {
        var response = await _factory.CreateClient().GetAsync("/api/boards");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAllBoards_WithUserIdQueryParam_ReturnsOk()
    {
        _factory.BoardServiceMock
            .Setup(s => s.GetUserBoardsAsync(2, 0, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<BoardResponse>(), 0));

        // userId provided in query → skips null branch; totalCount=0 → pageCount=1 branch
        var response = await AuthClient(userId: 1).GetAsync("/api/boards?userId=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── GetBoardById ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetBoardById_ExistingBoard_ReturnsOk()
    {
        _factory.BoardServiceMock
            .Setup(s => s.GetBoardByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeBoard(1));

        var response = await AuthClient().GetAsync("/api/boards/1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetBoardById_NotFound_ReturnsNotFound()
    {
        _factory.BoardServiceMock
            .Setup(s => s.GetBoardByIdAsync(999, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Board not found."));

        var response = await AuthClient().GetAsync("/api/boards/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── CreateBoard ───────────────────────────────────────────────────────

    [Fact]
    public async Task CreateBoard_ValidRequest_ReturnsCreated()
    {
        _factory.BoardServiceMock
            .Setup(s => s.CreateBoardAsync(1, It.IsAny<BoardRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeBoard(1));

        var response = await AuthClient().PostAsync("/api/boards",
            Json(new { title = "My Board", description = "Desc", imageName = "" }));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CreateBoard_NoToken_ReturnsUnauthorized()
    {
        var response = await _factory.CreateClient().PostAsync("/api/boards",
            Json(new { title = "My Board", description = "Desc", imageName = "" }));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateBoard_WithUserIdQueryParam_ReturnsCreated()
    {
        _factory.BoardServiceMock
            .Setup(s => s.CreateBoardAsync(2, It.IsAny<BoardRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeBoard(1));

        // userId provided in query → skips null branch
        var response = await AuthClient(userId: 1).PostAsync("/api/boards?userId=2",
            Json(new { title = "My Board", description = "Desc", imageName = "" }));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // ── DeleteBoard ───────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteBoard_OwnerRole_ReturnsNoContent()
    {
        SetupOwnerRole();
        _factory.BoardServiceMock
            .Setup(s => s.DeleteBoardAsync(1, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await AuthClient().DeleteAsync("/api/boards/1");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteBoard_InsufficientRole_ReturnsForbidden()
    {
        SetupNoRole();

        var response = await AuthClient().DeleteAsync("/api/boards/1");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── UpdateBoard ───────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateBoard_EditorRole_ReturnsOk()
    {
        SetupOwnerRole();
        _factory.BoardServiceMock
            .Setup(s => s.UpdateBoardAsync(1, It.IsAny<BoardUpdateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeBoard(1));

        var response = await AuthClient().PutAsync("/api/boards/1",
            Json(new { title = "Updated", description = "Desc", imageName = "", version = 0 }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UpdateBoard_NotFound_ReturnsNotFound()
    {
        SetupOwnerRole();
        _factory.BoardServiceMock
            .Setup(s => s.UpdateBoardAsync(999, It.IsAny<BoardUpdateRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Board not found."));

        var response = await AuthClient().PutAsync("/api/boards/999",
            Json(new { title = "Updated", description = "Desc", imageName = "", version = 0 }));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── PatchBoard ────────────────────────────────────────────────────────

    [Fact]
    public async Task PatchBoard_EditorRole_ReturnsOk()
    {
        SetupOwnerRole();
        _factory.BoardServiceMock
            .Setup(s => s.PatchBoardAsync(1, It.IsAny<JsonPatchDocument<BoardUpdateRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeBoard(1));

        var patch = new[] { new { op = "replace", path = "/title", value = "Patched" } };
        var response = await AuthClient().PatchAsync("/api/boards/1", JsonPatch(patch));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PatchBoard_InsufficientRole_ReturnsForbidden()
    {
        SetupNoRole();

        var patch = new[] { new { op = "replace", path = "/title", value = "Patched" } };
        var response = await AuthClient().PatchAsync("/api/boards/1", JsonPatch(patch));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}