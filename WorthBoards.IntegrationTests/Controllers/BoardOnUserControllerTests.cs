using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Common.Enums;
using WorthBoards.IntegrationTests.Infrastructure;

namespace WorthBoards.IntegrationTests.Controllers;

public class BoardOnUserControllerTests(CustomWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    private record PatchOp(string op, string path, object value);

    // ── GET /api/boards/{boardId}/links ──────────────────────────────────────

    [Fact]
    public async Task GetAllBoardToUserLinks_AsMember_ReturnsLinks()
    {
        // Arrange
        var (_, _, client) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(client);

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}/links");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var links = await response.Content.ReadFromJsonAsync<IEnumerable<LinkUserToBoardResponse>>();
        links.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAllBoardToUserLinks_AsNonMember_ReturnsForbidden()
    {
        // Arrange
        var (_, _, ownerClient) = await RegisterAndLoginAsync();
        var (_, _, nonMemberClient) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(ownerClient);

        // Act
        var response = await nonMemberClient.GetAsync($"/api/boards/{board.Id}/links");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── GET /api/boards/{boardId}/link/{userId} ──────────────────────────────

    [Fact]
    public async Task GetBoardToUserLink_ExistingLink_ReturnsLinkResponse()
    {
        // Arrange
        var (ownerId, _, client) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(client);

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}/link/{ownerId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var link = await response.Content.ReadFromJsonAsync<LinkUserToBoardResponse>();
        link!.UserId.Should().Be(ownerId);
        link.UserRole.Should().Be(UserRoleEnum.OWNER);
    }

    [Fact]
    public async Task GetBoardToUserLink_NonExistentLink_ReturnsNotFound()
    {
        // Arrange
        var (_, _, client) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(client);

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}/link/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── POST /api/boards/{boardId}/link/{userId} ─────────────────────────────

    [Fact]
    public async Task LinkUserToBoard_ValidRequest_ReturnsCreatedLinkAndPersists()
    {
        // Arrange
        var (_, _, ownerClient) = await RegisterAndLoginAsync();
        var (newUserId, _, _) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(ownerClient);

        var request = new LinkUserToBoardRequest { UserRole = UserRoleEnum.EDITOR };

        // Act
        var response = await ownerClient.PostAsJsonAsync(
            $"/api/boards/{board.Id}/link/{newUserId}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var link = await response.Content.ReadFromJsonAsync<LinkUserToBoardResponse>();
        link!.UserId.Should().Be(newUserId);
        link.UserRole.Should().Be(UserRoleEnum.EDITOR);

        using var db = GetDbContext();
        db.BoardOnUsers.Any(bou => bou.BoardId == board.Id && bou.UserId == newUserId).Should().BeTrue();
    }

    [Fact]
    public async Task LinkUserToBoard_AlreadyOwner_ReturnsBadRequest()
    {
        // Arrange
        var (ownerId, _, ownerClient) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(ownerClient);

        var request = new LinkUserToBoardRequest { UserRole = UserRoleEnum.EDITOR };

        // Act — try to link the owner again
        var response = await ownerClient.PostAsJsonAsync(
            $"/api/boards/{board.Id}/link/{ownerId}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── POST /api/boards/{boardId}/remove/{userId} ───────────────────────────

    [Fact]
    public async Task RemoveUser_AsOwnerRemovingEditor_ReturnsNoContent()
    {
        // Arrange
        var (_, _, ownerClient) = await RegisterAndLoginAsync();
        var (editorId, _, _) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(ownerClient);
        await LinkUserToBoardDirectlyAsync(board.Id, editorId, UserRoleEnum.EDITOR);

        // Act
        var response = await ownerClient.PostAsync(
            $"/api/boards/{board.Id}/remove/{editorId}", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var db = GetDbContext();
        db.BoardOnUsers.Any(bou => bou.BoardId == board.Id && bou.UserId == editorId).Should().BeFalse();
    }

    [Fact]
    public async Task RemoveUser_AsViewerRemovingOtherUser_ReturnsBadRequest()
    {
        // Arrange
        var (_, _, ownerClient) = await RegisterAndLoginAsync();
        var (viewerId, _, viewerClient) = await RegisterAndLoginAsync();
        var (editorId, _, _) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(ownerClient);
        await LinkUserToBoardDirectlyAsync(board.Id, viewerId, UserRoleEnum.VIEWER);
        await LinkUserToBoardDirectlyAsync(board.Id, editorId, UserRoleEnum.EDITOR);

        // Act
        var response = await viewerClient.PostAsync(
            $"/api/boards/{board.Id}/remove/{editorId}", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── GET /api/boards/{boardId}/users ──────────────────────────────────────

    [Fact]
    public async Task GetUsersLinkedToBoard_AsMember_ReturnsLinkedUsers()
    {
        // Arrange
        var (_, _, client) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(client);

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var users = await response.Content.ReadFromJsonAsync<IEnumerable<LinkedUserToBoardResponse>>();
        users.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetUsersLinkedToBoard_AsNonMember_ReturnsForbidden()
    {
        // Arrange
        var (_, _, ownerClient) = await RegisterAndLoginAsync();
        var (_, _, nonMemberClient) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(ownerClient);

        // Act
        var response = await nonMemberClient.GetAsync($"/api/boards/{board.Id}/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── GET /api/boards/{boardId}/collaborators ──────────────────────────────

    [Fact]
    public async Task GetUsersByUserName_AsEditor_ReturnsForbidden()
    {
        // Arrange
        var (_, _, ownerClient) = await RegisterAndLoginAsync();
        var (editorId, _, editorClient) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(ownerClient);
        await LinkUserToBoardDirectlyAsync(board.Id, editorId, UserRoleEnum.EDITOR);

        // Act
        var response = await editorClient.GetAsync($"/api/boards/{board.Id}/collaborators?userName=any");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── PATCH /api/boards/{boardId}/link/{userId} ───────────────────────────

    [Fact]
    public async Task PatchUserOnBoard_ValidPatch_ReturnsUpdatedLink()
    {
        // Arrange
        var (_, _, ownerClient) = await RegisterAndLoginAsync();
        var (editorId, _, _) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(ownerClient);
        await LinkUserToBoardDirectlyAsync(board.Id, editorId, UserRoleEnum.EDITOR);

        var patchDoc = new PatchOp[] { new("replace", "/userRole", UserRoleEnum.VIEWER) };

        // Act
        var response = await ownerClient.PatchAsJsonAsync(
            $"/api/boards/{board.Id}/link/{editorId}", patchDoc);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var link = await response.Content.ReadFromJsonAsync<LinkUserToBoardResponse>();
        link!.UserRole.Should().Be(UserRoleEnum.VIEWER);
    }

    [Fact]
    public async Task PatchUserOnBoard_AsViewer_ReturnsForbidden()
    {
        // Arrange
        var (_, _, ownerClient) = await RegisterAndLoginAsync();
        var (viewerId, _, viewerClient) = await RegisterAndLoginAsync();
        var (targetId, _, _) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(ownerClient);
        await LinkUserToBoardDirectlyAsync(board.Id, viewerId, UserRoleEnum.VIEWER);
        await LinkUserToBoardDirectlyAsync(board.Id, targetId, UserRoleEnum.EDITOR);

        var patchDoc = new PatchOp[] { new("replace", "/userRole", UserRoleEnum.VIEWER) };

        // Act
        var response = await viewerClient.PatchAsJsonAsync(
            $"/api/boards/{board.Id}/link/{targetId}", patchDoc);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── PUT /api/boards/{boardId}/link/{userId} ──────────────────────────────

    [Fact]
    public async Task UpdateUserOnBoard_AsOwner_ReturnsUpdatedLink()
    {
        // Arrange
        var (_, _, ownerClient) = await RegisterAndLoginAsync();
        var (editorId, _, _) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(ownerClient);
        await LinkUserToBoardDirectlyAsync(board.Id, editorId, UserRoleEnum.EDITOR);

        var updateRequest = new LinkUserToBoardRequest { UserRole = UserRoleEnum.VIEWER };

        // Act
        var response = await ownerClient.PutAsJsonAsync(
            $"/api/boards/{board.Id}/link/{editorId}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var link = await response.Content.ReadFromJsonAsync<LinkUserToBoardResponse>();
        link!.UserRole.Should().Be(UserRoleEnum.VIEWER);
    }

    [Fact]
    public async Task UpdateUserOnBoard_AsViewer_ReturnsForbidden()
    {
        // Arrange
        var (_, _, ownerClient) = await RegisterAndLoginAsync();
        var (viewerId, _, viewerClient) = await RegisterAndLoginAsync();
        var (targetId, _, _) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(ownerClient);
        await LinkUserToBoardDirectlyAsync(board.Id, viewerId, UserRoleEnum.VIEWER);
        await LinkUserToBoardDirectlyAsync(board.Id, targetId, UserRoleEnum.EDITOR);

        var updateRequest = new LinkUserToBoardRequest { UserRole = UserRoleEnum.VIEWER };

        // Act
        var response = await viewerClient.PutAsJsonAsync(
            $"/api/boards/{board.Id}/link/{targetId}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── POST /api/boards/{boardId}/collaborators/{newOwnerId} ────────────────

    [Fact]
    public async Task TransferOwnership_AsOwner_ReturnsNoContentAndSwitchesRoles()
    {
        // Arrange
        var (ownerId, _, ownerClient) = await RegisterAndLoginAsync();
        var (newOwnerId, _, _) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(ownerClient);
        await LinkUserToBoardDirectlyAsync(board.Id, newOwnerId, UserRoleEnum.EDITOR);

        // Act
        var response = await ownerClient.PostAsync(
            $"/api/boards/{board.Id}/collaborators/{newOwnerId}", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var db = GetDbContext();
        db.BoardOnUsers.First(bou => bou.BoardId == board.Id && bou.UserId == newOwnerId)
            .UserRole.Should().Be(UserRoleEnum.OWNER);
        db.BoardOnUsers.First(bou => bou.BoardId == board.Id && bou.UserId == ownerId)
            .UserRole.Should().Be(UserRoleEnum.VIEWER);
    }

    [Fact]
    public async Task TransferOwnership_ToSelf_ReturnsBadRequest()
    {
        // Arrange
        var (ownerId, _, ownerClient) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(ownerClient);

        // Act
        var response = await ownerClient.PostAsync(
            $"/api/boards/{board.Id}/collaborators/{ownerId}", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
