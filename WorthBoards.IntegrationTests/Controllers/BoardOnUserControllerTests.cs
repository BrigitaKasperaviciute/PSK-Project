using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WorthBoards.Business.Dtos.Identity;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Common.Enums;
using WorthBoards.IntegrationTests.Infrastructure;

namespace WorthBoards.IntegrationTests.Controllers;

public class BoardOnUserControllerTests(CustomWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    // ── GET /api/boards/{boardId}/links ───────────────────────────────────────

    [Fact]
    public async Task GetAllBoardToUserLinks_BoardMember_ReturnsOkWithLinks()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(token);
        SetAuthToken(token);

        // Act
        var response = await Client.GetAsync($"/api/boards/{board.Id}/links");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<IEnumerable<LinkUserToBoardResponse>>();
        body.Should().NotBeNull();
        body!.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAllBoardToUserLinks_UnauthenticatedUser_ReturnsUnauthorized()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(token);
        ClearAuthToken();

        // Act
        var response = await Client.GetAsync($"/api/boards/{board.Id}/links");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── GET /api/boards/{boardId}/link/{userId} ───────────────────────────────

    [Fact]
    public async Task GetBoardToUserLink_ExistingLink_ReturnsOkWithLink()
    {
        // Arrange
        var (userId, token) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(token);
        SetAuthToken(token);

        // Act
        var response = await Client.GetAsync($"/api/boards/{board.Id}/link/{userId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<LinkUserToBoardResponse>();
        body.Should().NotBeNull();
        body!.BoardId.Should().Be(board.Id);
        body.UserId.Should().Be(userId);
        body.UserRole.Should().Be(UserRoleEnum.OWNER);
    }

    [Fact]
    public async Task GetBoardToUserLink_NonMemberUser_ReturnsNotFound()
    {
        // Arrange
        var (_, ownerToken) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(ownerToken);

        var (nonMemberId, _) = await RegisterAndLoginAsync();
        SetAuthToken(ownerToken);

        // Act
        var response = await Client.GetAsync($"/api/boards/{board.Id}/link/{nonMemberId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── POST /api/boards/{boardId}/invite ─────────────────────────────────────

    [Fact]
    public async Task InviteUser_OwnerInvitesExistingUser_ReturnsNoContent()
    {
        // Arrange
        var (_, ownerToken) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(ownerToken);

        var (inviteeId, _) = await RegisterAndLoginAsync();
        SetAuthToken(ownerToken);

        var request = new InvitationRequest { UserId = inviteeId, Role = UserRoleEnum.VIEWER };

        // Act
        var response = await Client.PostAsJsonAsync($"/api/boards/{board.Id}/invite", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var notification = await QueryAsync(db =>
            db.NotificationsOnUsers.FirstOrDefaultAsync(n => n.UserId == inviteeId));
        notification.Should().NotBeNull();
    }

    [Fact]
    public async Task InviteUser_ViewerUser_ReturnsForbidden()
    {
        // Arrange
        var (_, ownerToken) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(ownerToken);

        var (viewerId, viewerToken) = await RegisterAndLoginAsync();
        await LinkUserToBoardDirectlyAsync(board.Id, viewerId, UserRoleEnum.VIEWER);

        var (targetId, _) = await RegisterAndLoginAsync();
        SetAuthToken(viewerToken);

        var request = new InvitationRequest { UserId = targetId, Role = UserRoleEnum.VIEWER };

        // Act
        var response = await Client.PostAsJsonAsync($"/api/boards/{board.Id}/invite", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── GET /api/boards/{boardId}/users ───────────────────────────────────────

    [Fact]
    public async Task GetUsersLinkedToBoard_BoardMember_ReturnsOkWithUsers()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(token);
        SetAuthToken(token);

        // Act
        var response = await Client.GetAsync($"/api/boards/{board.Id}/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<IEnumerable<LinkedUserToBoardResponse>>();
        body.Should().NotBeNull();
        body!.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetUsersLinkedToBoard_NonMember_ReturnsForbidden()
    {
        // Arrange
        var (_, ownerToken) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(ownerToken);

        var (_, outsiderToken) = await RegisterAndLoginAsync();
        SetAuthToken(outsiderToken);

        // Act
        var response = await Client.GetAsync($"/api/boards/{board.Id}/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── PUT /api/boards/{boardId}/link/{userId} ───────────────────────────────

    [Fact]
    public async Task UpdateUserOnBoard_OwnerUpdatesExistingLink_ReturnsOkWithUpdatedRole()
    {
        // Arrange
        var (_, ownerToken) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(ownerToken);

        var (editorId, _) = await RegisterAndLoginAsync();
        await LinkUserToBoardDirectlyAsync(board.Id, editorId, UserRoleEnum.VIEWER);

        SetAuthToken(ownerToken);
        var updateRequest = new LinkUserToBoardRequest { UserRole = UserRoleEnum.EDITOR };

        // Act
        var response = await Client.PutAsJsonAsync(
            $"/api/boards/{board.Id}/link/{editorId}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<LinkUserToBoardResponse>();
        body.Should().NotBeNull();
        body!.UserRole.Should().Be(UserRoleEnum.EDITOR);

        var dbLink = await QueryAsync(db =>
            db.BoardOnUsers.FirstOrDefaultAsync(b => b.BoardId == board.Id && b.UserId == editorId));
        dbLink!.UserRole.Should().Be(UserRoleEnum.EDITOR);
    }

    [Fact]
    public async Task UpdateUserOnBoard_ViewerUser_ReturnsForbidden()
    {
        // Arrange
        var (_, ownerToken) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(ownerToken);

        var (viewerId, viewerToken) = await RegisterAndLoginAsync();
        await LinkUserToBoardDirectlyAsync(board.Id, viewerId, UserRoleEnum.VIEWER);

        var (targetId, _) = await RegisterAndLoginAsync();
        await LinkUserToBoardDirectlyAsync(board.Id, targetId, UserRoleEnum.VIEWER);

        SetAuthToken(viewerToken);
        var updateRequest = new LinkUserToBoardRequest { UserRole = UserRoleEnum.EDITOR };

        // Act
        var response = await Client.PutAsJsonAsync(
            $"/api/boards/{board.Id}/link/{targetId}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── GET /api/boards/{boardId}/collaborators ───────────────────────────────

    [Fact]
    public async Task GetUsersByUserName_OwnerSearches_ReturnsMatchingUsers()
    {
        // Arrange
        var (_, ownerToken) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(ownerToken);

        // Register a user with a known username prefix
        var id = Guid.NewGuid().ToString("N")[..8];
        var suffix = $"search_{id}";
        var (searchUserId, _) = await RegisterAndLoginAsync(suffix);

        SetAuthToken(ownerToken);

        // Act
        var response = await Client.GetAsync(
            $"/api/boards/{board.Id}/collaborators?userName=search_{id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<IEnumerable<UserResponse>>();
        body.Should().NotBeNull();
        body!.Should().Contain(u => u.Id == searchUserId);
    }

    [Fact]
    public async Task GetUsersByUserName_ViewerUser_ReturnsForbidden()
    {
        // Arrange
        var (_, ownerToken) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(ownerToken);

        var (viewerId, viewerToken) = await RegisterAndLoginAsync();
        await LinkUserToBoardDirectlyAsync(board.Id, viewerId, UserRoleEnum.VIEWER);
        SetAuthToken(viewerToken);

        // Act
        var response = await Client.GetAsync($"/api/boards/{board.Id}/collaborators?userName=test");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── POST /api/boards/{boardId}/remove/{userId} ────────────────────────────

    [Fact]
    public async Task RemoveUser_OwnerRemovesMember_ReturnsNoContentAndUnlinksFromDb()
    {
        // Arrange
        var (_, ownerToken) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(ownerToken);

        var (memberId, _) = await RegisterAndLoginAsync();
        await LinkUserToBoardDirectlyAsync(board.Id, memberId, UserRoleEnum.VIEWER);

        SetAuthToken(ownerToken);

        // Act
        var response = await Client.PostAsync(
            $"/api/boards/{board.Id}/remove/{memberId}", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var dbLink = await QueryAsync(db =>
            db.BoardOnUsers.FirstOrDefaultAsync(b => b.BoardId == board.Id && b.UserId == memberId));
        dbLink.Should().BeNull();
    }

    [Fact]
    public async Task RemoveUser_UnauthenticatedUser_ReturnsUnauthorized()
    {
        // Arrange
        var (_, ownerToken) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(ownerToken);
        var (memberId, _) = await RegisterAndLoginAsync();
        await LinkUserToBoardDirectlyAsync(board.Id, memberId, UserRoleEnum.VIEWER);

        ClearAuthToken();

        // Act
        var response = await Client.PostAsync(
            $"/api/boards/{board.Id}/remove/{memberId}", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
