using Microsoft.EntityFrameworkCore;

namespace WorthBoards.IntegrationTests.Controllers;

[Collection(nameof(ApiCollection))]
[Trait("Category", "Integration")]
public sealed class BoardOnUserControllerTests(CustomWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    // ── POST /api/boards/{boardId}/invite ─────────────────────────────────────

    [Fact]
    public async Task InviteUser_AsOwner_ReturnsNoContentAndCreatesNotification()
    {
        // Arrange
        var (ownerId, ownerToken) = await RegisterAndLoginAsync();
        SetAuthToken(ownerToken);
        var board = await CreateBoardAsync();

        var (inviteeId, _) = await RegisterAndLoginAsync();
        SetAuthToken(ownerToken);

        var request = new InvitationRequest { UserId = inviteeId, Role = UserRoleEnum.EDITOR };

        // Act
        var response = await Client.PostAsJsonAsync($"/api/boards/{board.Id}/invite", request);

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert – notification was created for the invitee
        await using var db = CreateDbContext();
        var notification = await db.Notifications.AsNoTracking()
            .FirstOrDefaultAsync(n => n.BoardId == board.Id
                                   && n.SubjectUserId == inviteeId
                                   && n.NotificationType == NotificationEventTypeEnum.INVITATION);
        notification.Should().NotBeNull();
        notification!.InvitationRole.Should().Be(UserRoleEnum.EDITOR);
    }

    [Fact]
    public async Task InviteUser_AsEditor_ReturnsForbidden()
    {
        // Arrange
        var (_, ownerToken) = await RegisterAndLoginAsync();
        SetAuthToken(ownerToken);
        var board = await CreateBoardAsync();

        var (editorId, editorToken) = await RegisterAndLoginAsync();
        await LinkUserToBoardDirectlyAsync(board.Id, editorId, UserRoleEnum.EDITOR);

        var (targetId, _) = await RegisterAndLoginAsync();
        SetAuthToken(editorToken);

        var request = new InvitationRequest { UserId = targetId, Role = UserRoleEnum.VIEWER };

        // Act
        var response = await Client.PostAsJsonAsync($"/api/boards/{board.Id}/invite", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── GET /api/boards/{boardId}/links ───────────────────────────────────────

    [Fact]
    public async Task GetAllBoardToUserLinks_AsViewer_ReturnsOkWithLinks()
    {
        // Arrange
        var (_, ownerToken) = await RegisterAndLoginAsync();
        SetAuthToken(ownerToken);
        var board = await CreateBoardAsync();

        var (viewerId, viewerToken) = await RegisterAndLoginAsync();
        await LinkUserToBoardDirectlyAsync(board.Id, viewerId, UserRoleEnum.VIEWER);
        SetAuthToken(viewerToken);

        // Act
        var response = await Client.GetAsync($"/api/boards/{board.Id}/links");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<List<LinkUserToBoardResponse>>();
        body.Should().NotBeNull();
        body!.Should().HaveCountGreaterThanOrEqualTo(2); // owner + viewer
    }

    [Fact]
    public async Task GetAllBoardToUserLinks_WithoutBoardMembership_ReturnsForbidden()
    {
        // Arrange
        var (_, ownerToken) = await RegisterAndLoginAsync();
        SetAuthToken(ownerToken);
        var board = await CreateBoardAsync();

        var (_, strangerToken) = await RegisterAndLoginAsync();
        SetAuthToken(strangerToken);

        // Act
        var response = await Client.GetAsync($"/api/boards/{board.Id}/links");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── GET /api/boards/{boardId}/link/{userId} ───────────────────────────────

    [Fact]
    public async Task GetBoardToUserLink_AsViewer_ReturnsOkWithLink()
    {
        // Arrange
        var (ownerId, ownerToken) = await RegisterAndLoginAsync();
        SetAuthToken(ownerToken);
        var board = await CreateBoardAsync();

        var (viewerId, viewerToken) = await RegisterAndLoginAsync();
        await LinkUserToBoardDirectlyAsync(board.Id, viewerId, UserRoleEnum.VIEWER);
        SetAuthToken(viewerToken);

        // Act
        var response = await Client.GetAsync($"/api/boards/{board.Id}/link/{ownerId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<LinkUserToBoardResponse>();
        body.Should().NotBeNull();
        body!.UserId.Should().Be(ownerId);
        body.UserRole.Should().Be(UserRoleEnum.OWNER);
    }

    [Fact]
    public async Task GetBoardToUserLink_WithNonexistentUserId_ReturnsNotFound()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);
        var board = await CreateBoardAsync();

        // Act
        var response = await Client.GetAsync($"/api/boards/{board.Id}/link/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── POST /api/boards/{boardId}/link/{userId} ──────────────────────────────

    [Fact]
    public async Task LinkUserToBoard_WhenAuthenticated_ReturnsCreatedAndPersistsLink()
    {
        // Arrange
        var (_, ownerToken) = await RegisterAndLoginAsync();
        SetAuthToken(ownerToken);
        var board = await CreateBoardAsync();

        var (newUserId, newUserToken) = await RegisterAndLoginAsync();
        SetAuthToken(newUserToken);

        var request = new LinkUserToBoardRequest { UserRole = UserRoleEnum.VIEWER };

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/api/boards/{board.Id}/link/{newUserId}", request);

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<LinkUserToBoardResponse>();
        body.Should().NotBeNull();
        body!.UserId.Should().Be(newUserId);
        body.UserRole.Should().Be(UserRoleEnum.VIEWER);

        // Assert – database state
        await using var db = CreateDbContext();
        var link = await db.BoardOnUsers.AsNoTracking()
            .SingleOrDefaultAsync(bou => bou.BoardId == board.Id && bou.UserId == newUserId);
        link.Should().NotBeNull();
    }

    [Fact]
    public async Task LinkUserToBoard_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var (_, ownerToken) = await RegisterAndLoginAsync();
        SetAuthToken(ownerToken);
        var board = await CreateBoardAsync();
        var (newUserId, _) = await RegisterAndLoginAsync();
        ClearAuthToken();

        var request = new LinkUserToBoardRequest { UserRole = UserRoleEnum.VIEWER };

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/api/boards/{board.Id}/link/{newUserId}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── POST /api/boards/{boardId}/remove/{userId} ────────────────────────────

    [Fact]
    public async Task RemoveUser_AsOwner_ReturnsNoContentAndUnlinksUser()
    {
        // Arrange
        var (_, ownerToken) = await RegisterAndLoginAsync();
        SetAuthToken(ownerToken);
        var board = await CreateBoardAsync();

        var (editorId, _) = await RegisterAndLoginAsync();
        await LinkUserToBoardDirectlyAsync(board.Id, editorId, UserRoleEnum.EDITOR);

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/api/boards/{board.Id}/remove/{editorId}", new { });

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert – user no longer linked
        await using var db = CreateDbContext();
        var link = await db.BoardOnUsers.AsNoTracking()
            .SingleOrDefaultAsync(bou => bou.BoardId == board.Id && bou.UserId == editorId);
        link.Should().BeNull();
    }

    [Fact]
    public async Task RemoveUser_AsEditorRemovingAnother_ReturnsBadRequest()
    {
        // Arrange – editor cannot remove another member
        var (_, ownerToken) = await RegisterAndLoginAsync();
        SetAuthToken(ownerToken);
        var board = await CreateBoardAsync();

        var (editorId, editorToken) = await RegisterAndLoginAsync();
        await LinkUserToBoardDirectlyAsync(board.Id, editorId, UserRoleEnum.EDITOR);

        var (viewerId, _) = await RegisterAndLoginAsync();
        await LinkUserToBoardDirectlyAsync(board.Id, viewerId, UserRoleEnum.VIEWER);

        SetAuthToken(editorToken);

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/api/boards/{board.Id}/remove/{viewerId}", new { });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── GET /api/boards/{boardId}/users ───────────────────────────────────────

    [Fact]
    public async Task GetUsersLinkedToBoard_AsViewer_ReturnsOkWithUsers()
    {
        // Arrange
        var (_, ownerToken) = await RegisterAndLoginAsync();
        SetAuthToken(ownerToken);
        var board = await CreateBoardAsync();

        var (viewerId, viewerToken) = await RegisterAndLoginAsync();
        await LinkUserToBoardDirectlyAsync(board.Id, viewerId, UserRoleEnum.VIEWER);
        SetAuthToken(viewerToken);

        // Act
        var response = await Client.GetAsync($"/api/boards/{board.Id}/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<List<LinkedUserToBoardResponse>>();
        body.Should().NotBeNull();
        body!.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task GetUsersLinkedToBoard_WithoutBoardMembership_ReturnsForbidden()
    {
        // Arrange
        var (_, ownerToken) = await RegisterAndLoginAsync();
        SetAuthToken(ownerToken);
        var board = await CreateBoardAsync();

        var (_, strangerToken) = await RegisterAndLoginAsync();
        SetAuthToken(strangerToken);

        // Act
        var response = await Client.GetAsync($"/api/boards/{board.Id}/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── GET /api/boards/{boardId}/collaborators ───────────────────────────────

    [Fact]
    public async Task GetUsersByUserName_AsOwner_ReturnsOkWithMatchingUsers()
    {
        // Arrange
        var (_, ownerToken) = await RegisterAndLoginAsync();
        SetAuthToken(ownerToken);
        var board = await CreateBoardAsync();

        // Register a user with a known username
        var suffix = Guid.NewGuid().ToString("N")[..8];
        await RegisterAndLoginAsync($"searchable{suffix}", $"searchable{suffix}@test.com");

        // Act – search by partial username
        var response = await Client.GetAsync(
            $"/api/boards/{board.Id}/collaborators?userName=searchable{suffix}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<List<UserResponse>>();
        body.Should().NotBeNull();
        body!.Should().Contain(u => u.UserName!.Contains($"searchable{suffix}"));
    }

    [Fact]
    public async Task GetUsersByUserName_AsViewer_ReturnsForbidden()
    {
        // Arrange
        var (_, ownerToken) = await RegisterAndLoginAsync();
        SetAuthToken(ownerToken);
        var board = await CreateBoardAsync();

        var (viewerId, viewerToken) = await RegisterAndLoginAsync();
        await LinkUserToBoardDirectlyAsync(board.Id, viewerId, UserRoleEnum.VIEWER);
        SetAuthToken(viewerToken);

        // Act
        var response = await Client.GetAsync(
            $"/api/boards/{board.Id}/collaborators?userName=test");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── POST /api/boards/{boardId}/collaborators/{newOwnerId} ─────────────────

    [Fact]
    public async Task TransferOwnership_AsOwner_ReturnsNoContentAndUpdatesRoles()
    {
        // Arrange
        var (ownerId, ownerToken) = await RegisterAndLoginAsync();
        SetAuthToken(ownerToken);
        var board = await CreateBoardAsync();

        var (newOwnerId, _) = await RegisterAndLoginAsync();
        await LinkUserToBoardDirectlyAsync(board.Id, newOwnerId, UserRoleEnum.EDITOR);

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/api/boards/{board.Id}/collaborators/{newOwnerId}", new { });

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert – new owner has OWNER role; old owner is demoted to VIEWER
        await using var db = CreateDbContext();
        var newOwnerLink = await db.BoardOnUsers.AsNoTracking()
            .SingleOrDefaultAsync(bou => bou.BoardId == board.Id && bou.UserId == newOwnerId);
        newOwnerLink!.UserRole.Should().Be(UserRoleEnum.OWNER);

        var oldOwnerLink = await db.BoardOnUsers.AsNoTracking()
            .SingleOrDefaultAsync(bou => bou.BoardId == board.Id && bou.UserId == ownerId);
        oldOwnerLink!.UserRole.Should().Be(UserRoleEnum.VIEWER);
    }

    [Fact]
    public async Task TransferOwnership_AsEditor_ReturnsForbidden()
    {
        // Arrange
        var (_, ownerToken) = await RegisterAndLoginAsync();
        SetAuthToken(ownerToken);
        var board = await CreateBoardAsync();

        var (editorId, editorToken) = await RegisterAndLoginAsync();
        await LinkUserToBoardDirectlyAsync(board.Id, editorId, UserRoleEnum.EDITOR);

        var (targetId, _) = await RegisterAndLoginAsync();
        await LinkUserToBoardDirectlyAsync(board.Id, targetId, UserRoleEnum.VIEWER);

        SetAuthToken(editorToken);

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/api/boards/{board.Id}/collaborators/{targetId}", new { });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── PUT /api/boards/{boardId}/link/{userId} ───────────────────────────────

    [Fact]
    public async Task UpdateUserOnBoard_AsEditor_ReturnsOkWithUpdatedRole()
    {
        // Arrange
        var (_, ownerToken) = await RegisterAndLoginAsync();
        SetAuthToken(ownerToken);
        var board = await CreateBoardAsync();

        var (editorId, editorToken) = await RegisterAndLoginAsync();
        await LinkUserToBoardDirectlyAsync(board.Id, editorId, UserRoleEnum.EDITOR);

        var (viewerId, _) = await RegisterAndLoginAsync();
        await LinkUserToBoardDirectlyAsync(board.Id, viewerId, UserRoleEnum.VIEWER);

        SetAuthToken(editorToken);

        var request = new LinkUserToBoardRequest { UserRole = UserRoleEnum.EDITOR };

        // Act
        var response = await Client.PutAsJsonAsync(
            $"/api/boards/{board.Id}/link/{viewerId}", request);

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<LinkUserToBoardResponse>();
        body.Should().NotBeNull();
        body!.UserRole.Should().Be(UserRoleEnum.EDITOR);

        // Assert – database state
        await using var db = CreateDbContext();
        var link = await db.BoardOnUsers.AsNoTracking()
            .SingleOrDefaultAsync(bou => bou.BoardId == board.Id && bou.UserId == viewerId);
        link!.UserRole.Should().Be(UserRoleEnum.EDITOR);
    }

    [Fact]
    public async Task UpdateUserOnBoard_AsViewer_ReturnsForbidden()
    {
        // Arrange
        var (_, ownerToken) = await RegisterAndLoginAsync();
        SetAuthToken(ownerToken);
        var board = await CreateBoardAsync();

        var (viewerId, viewerToken) = await RegisterAndLoginAsync();
        await LinkUserToBoardDirectlyAsync(board.Id, viewerId, UserRoleEnum.VIEWER);

        var (targetId, _) = await RegisterAndLoginAsync();
        await LinkUserToBoardDirectlyAsync(board.Id, targetId, UserRoleEnum.VIEWER);

        SetAuthToken(viewerToken);

        var request = new LinkUserToBoardRequest { UserRole = UserRoleEnum.EDITOR };

        // Act
        var response = await Client.PutAsJsonAsync(
            $"/api/boards/{board.Id}/link/{targetId}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
