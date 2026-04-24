using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.JsonPatch;
using Xunit;
using Moq;
using WorthBoards.Business.Dtos.Identity;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Common.Enums;
using WorthBoards.Common.Exceptions.Custom;
using WorthBoards.IntegrationTests.Infrastructure;

namespace WorthBoards.IntegrationTests.Tests;

public class BoardOnUserControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public BoardOnUserControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetAllMocks();
    }

    private HttpClient AuthClient(int userId = 1) => _factory.CreateClient().WithAuth(userId);

    private static StringContent Json(object body) =>
        new(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

    private static StringContent JsonPatch(object body) =>
        new(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json-patch+json");

    private void SetupOwnerRole() =>
        _factory.BoardServiceMock
            .Setup(s => s.GetUserRoleByBoardIdAndUserIdAsync(It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(UserRoleEnum.OWNER);

    private void SetupNoRole() =>
        _factory.BoardServiceMock
            .Setup(s => s.GetUserRoleByBoardIdAndUserIdAsync(It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync((UserRoleEnum?)null);

    private static LinkUserToBoardResponse MakeLink() =>
        new() { BoardId = 1, UserId = 2, UserRole = UserRoleEnum.VIEWER, AddedAt = DateTime.UtcNow };

    // ── InviteUser ────────────────────────────────────────────────────────

    [Fact]
    public async Task InviteUser_OwnerRole_ReturnsNoContent()
    {
        SetupOwnerRole();
        _factory.NotificationServiceMock
            .Setup(s => s.NotifyBoardInvitation(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<UserRoleEnum>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await AuthClient().PostAsync("/api/boards/1/invite",
            Json(new { userId = 2, role = 2 }));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task InviteUser_InsufficientRole_ReturnsForbidden()
    {
        SetupNoRole();

        var response = await AuthClient().PostAsync("/api/boards/1/invite",
            Json(new { userId = 2, role = 2 }));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── RemoveUser ────────────────────────────────────────────────────────

    [Fact]
    public async Task RemoveUser_Authenticated_ReturnsNoContent()
    {
        _factory.BoardOnUserServiceMock
            .Setup(s => s.UnlinkUserFromBoard(1, 2, 1, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await AuthClient().PostAsync("/api/boards/1/remove/2", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task RemoveUser_NoToken_ReturnsUnauthorized()
    {
        var response = await _factory.CreateClient().PostAsync("/api/boards/1/remove/2", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── GetAllBoardToUserLinks ────────────────────────────────────────────

    [Fact]
    public async Task GetAllLinks_ViewerRole_ReturnsOk()
    {
        SetupOwnerRole();
        _factory.BoardOnUserServiceMock
            .Setup(s => s.GetAllBoardToUserLinks(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LinkUserToBoardResponse> { MakeLink() });

        var response = await AuthClient().GetAsync("/api/boards/1/links");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetAllLinks_NoToken_ReturnsUnauthorized()
    {
        var response = await _factory.CreateClient().GetAsync("/api/boards/1/links");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── GetBoardToUserLink ────────────────────────────────────────────────

    [Fact]
    public async Task GetLink_ExistingLink_ReturnsOk()
    {
        SetupOwnerRole();
        _factory.BoardOnUserServiceMock
            .Setup(s => s.GetBoardToUserLink(1, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeLink());

        var response = await AuthClient().GetAsync("/api/boards/1/link/2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetLink_NotFound_ReturnsNotFound()
    {
        SetupOwnerRole();
        _factory.BoardOnUserServiceMock
            .Setup(s => s.GetBoardToUserLink(1, 99, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Link not found."));

        var response = await AuthClient().GetAsync("/api/boards/1/link/99");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── LinkUserToBoard ───────────────────────────────────────────────────

    [Fact]
    public async Task LinkUser_Authenticated_ReturnsCreated()
    {
        _factory.BoardOnUserServiceMock
            .Setup(s => s.LinkUserToBoard(1, 2, It.IsAny<LinkUserToBoardRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeLink());

        var response = await AuthClient().PostAsync("/api/boards/1/link/2",
            Json(new { userRole = 2 }));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task LinkUser_NoToken_ReturnsUnauthorized()
    {
        var response = await _factory.CreateClient().PostAsync("/api/boards/1/link/2",
            Json(new { userRole = 2 }));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── UpdateUserOnBoard ─────────────────────────────────────────────────

    [Fact]
    public async Task UpdateLink_EditorRole_ReturnsOk()
    {
        SetupOwnerRole();
        _factory.BoardOnUserServiceMock
            .Setup(s => s.UpdateUserOnBoard(1, 2, It.IsAny<LinkUserToBoardRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeLink());

        var response = await AuthClient().PutAsync("/api/boards/1/link/2",
            Json(new { userRole = 1 }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UpdateLink_InsufficientRole_ReturnsForbidden()
    {
        SetupNoRole();

        var response = await AuthClient().PutAsync("/api/boards/1/link/2",
            Json(new { userRole = 1 }));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── PatchUserOnBoard ──────────────────────────────────────────────────

    [Fact]
    public async Task PatchLink_EditorRole_ReturnsOk()
    {
        SetupOwnerRole();
        _factory.BoardOnUserServiceMock
            .Setup(s => s.PatchUserOnBoard(1, 2, It.IsAny<JsonPatchDocument<LinkUserToBoardRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeLink());

        var patch = new[] { new { op = "replace", path = "/userRole", value = 1 } };
        var response = await AuthClient().PatchAsync("/api/boards/1/link/2", JsonPatch(patch));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PatchLink_InsufficientRole_ReturnsForbidden()
    {
        SetupNoRole();

        var patch = new[] { new { op = "replace", path = "/userRole", value = 1 } };
        var response = await AuthClient().PatchAsync("/api/boards/1/link/2", JsonPatch(patch));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── GetUsersLinkedToBoard ─────────────────────────────────────────────

    [Fact]
    public async Task GetUsersLinkedToBoard_ViewerRole_ReturnsOk()
    {
        SetupOwnerRole();
        _factory.BoardOnUserServiceMock
            .Setup(s => s.GetUsersLinkedToBoardAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LinkedUserToBoardResponse>());

        var response = await AuthClient().GetAsync("/api/boards/1/users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetUsersLinkedToBoard_NoToken_ReturnsUnauthorized()
    {
        var response = await _factory.CreateClient().GetAsync("/api/boards/1/users");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── GetUsersByUserName ────────────────────────────────────────────────

    [Fact]
    public async Task GetCollaborators_OwnerRole_ReturnsOk()
    {
        SetupOwnerRole();
        _factory.BoardOnUserServiceMock
            .Setup(s => s.GetUsersByUserNameAsync(1, "alice", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserResponse>());

        var response = await AuthClient().GetAsync("/api/boards/1/collaborators?userName=alice");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetCollaborators_InsufficientRole_ReturnsForbidden()
    {
        SetupNoRole();

        var response = await AuthClient().GetAsync("/api/boards/1/collaborators?userName=alice");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── TransferOwnership ─────────────────────────────────────────────────

    [Fact]
    public async Task TransferOwnership_OwnerRole_ReturnsNoContent()
    {
        SetupOwnerRole();
        _factory.BoardOnUserServiceMock
            .Setup(s => s.TransferOwnershipAsync(1, 1, 2, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await AuthClient().PostAsync("/api/boards/1/collaborators/2", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task TransferOwnership_InsufficientRole_ReturnsForbidden()
    {
        SetupNoRole();

        var response = await AuthClient().PostAsync("/api/boards/1/collaborators/2", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}