using Microsoft.EntityFrameworkCore;
using WorthBoards.Business.Dtos.Identity;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Common.Enums;
using WorthBoards.IntegrationTests.Infrastructure;

namespace WorthBoards.IntegrationTests.Controllers;

public sealed class BoardOnUserControllerTests(CustomWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    // ── GetAllBoardToUserLinks ────────────────────────────────────────────────

    [Fact]
    public async Task GetAllBoardToUserLinks_AsOwner_ReturnsOkWithLinks()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);
        var board = await CreateBoardAsync();

        var (memberId, _) = await RegisterAndLoginAsync();
        await LinkUserToBoardDirectlyAsync(board.Id, memberId, UserRoleEnum.VIEWER);

        // Act
        var response = await Client.GetAsync($"/api/boards/{board.Id}/links");

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert – response body
        var body = await response.Content.ReadFromJsonAsync<IEnumerable<LinkUserToBoardResponse>>();
        body.Should().NotBeNull();
        body!.Should().HaveCountGreaterThanOrEqualTo(2, "owner + member");
    }

    [Fact]
    public async Task GetAllBoardToUserLinks_AsNonMember_ReturnsForbidden()
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

    // ── GetBoardToUserLink ────────────────────────────────────────────────────

    [Fact]
    public async Task GetBoardToUserLink_WithExistingLink_ReturnsOkWithLink()
    {
        // Arrange
        var (ownerId, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);
        var board = await CreateBoardAsync();

        // Act
        var response = await Client.GetAsync($"/api/boards/{board.Id}/link/{ownerId}");

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert – response body
        var body = await response.Content.ReadFromJsonAsync<LinkUserToBoardResponse>();
        body.Should().NotBeNull();
        body!.UserId.Should().Be(ownerId);
        body.BoardId.Should().Be(board.Id);
        body.UserRole.Should().Be(UserRoleEnum.OWNER);
    }

    [Fact]
    public async Task GetBoardToUserLink_WithNonExistentLink_ReturnsNotFound()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);
        var board = await CreateBoardAsync();

        // Act
        var response = await Client.GetAsync($"/api/boards/{board.Id}/link/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── LinkUserToBoard ───────────────────────────────────────────────────────

    [Fact]
    public async Task LinkUserToBoard_WithValidRequest_ReturnsCreatedAndPersistsLink()
    {
        // Arrange
        var (_, ownerToken) = await RegisterAndLoginAsync();
        SetAuthToken(ownerToken);
        var board = await CreateBoardAsync();

        var (newUserId, _) = await RegisterAndLoginAsync();
        var request = new LinkUserToBoardRequest { UserRole = UserRoleEnum.VIEWER };

        // Act
        var response = await Client.PostAsJsonAsync($"/api/boards/{board.Id}/link/{newUserId}", request);

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        // Assert – response body
        var body = await response.Content.ReadFromJsonAsync<LinkUserToBoardResponse>();
        body.Should().NotBeNull();
        body!.UserId.Should().Be(newUserId);
        body.UserRole.Should().Be(UserRoleEnum.VIEWER);

        // Assert – database state
        await using var db = Factory.CreateDbContext();
        var persisted = await db.BoardOnUsers.AsNoTracking()
            .SingleOrDefaultAsync(bou => bou.BoardId == board.Id && bou.UserId == newUserId);
        persisted.Should().NotBeNull();
        persisted!.UserRole.Should().Be(UserRoleEnum.VIEWER);
    }

    [Fact]
    public async Task LinkUserToBoard_WhenUserAlreadyOwner_ReturnsBadRequest()
    {
        // Arrange
        var (ownerId, ownerToken) = await RegisterAndLoginAsync();
        SetAuthToken(ownerToken);
        var board = await CreateBoardAsync();

        // Act – try to link the already-owner user again
        var response = await Client.PostAsJsonAsync(
            $"/api/boards/{board.Id}/link/{ownerId}",
            new LinkUserToBoardRequest { UserRole = UserRoleEnum.VIEWER });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── RemoveUser ────────────────────────────────────────────────────────────

    [Fact]
    public async Task RemoveUser_AsOwner_ReturnsNoContentAndRemovesLink()
    {
        // Arrange
        var (_, ownerToken) = await RegisterAndLoginAsync();
        SetAuthToken(ownerToken);
        var board = await CreateBoardAsync();

        var (memberId, _) = await RegisterAndLoginAsync();
        await LinkUserToBoardDirectlyAsync(board.Id, memberId, UserRoleEnum.VIEWER);

        // Act
        var response = await Client.PostAsync($"/api/boards/{board.Id}/remove/{memberId}", null);

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert – database state
        await using var db = Factory.CreateDbContext();
        var link = await db.BoardOnUsers.AsNoTracking()
            .SingleOrDefaultAsync(bou => bou.BoardId == board.Id && bou.UserId == memberId);
        link.Should().BeNull();
    }

    [Fact]
    public async Task RemoveUser_AsViewer_ReturnsBadRequest()
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

        // Act – viewer tries to remove another user
        var response = await Client.PostAsync($"/api/boards/{board.Id}/remove/{targetId}", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── GetUsersLinkedToBoard ─────────────────────────────────────────────────

    [Fact]
    public async Task GetUsersLinkedToBoard_WithMembers_ReturnsOkWithUserList()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);
        var board = await CreateBoardAsync();

        var (memberId, _) = await RegisterAndLoginAsync();
        await LinkUserToBoardDirectlyAsync(board.Id, memberId, UserRoleEnum.EDITOR);

        // Act
        var response = await Client.GetAsync($"/api/boards/{board.Id}/users");

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert – response body
        var body = await response.Content.ReadFromJsonAsync<IEnumerable<LinkedUserToBoardResponse>>();
        body.Should().NotBeNull();
        body!.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task GetUsersLinkedToBoard_AsNonMember_ReturnsForbidden()
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

    // ── UpdateUserOnBoard ─────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateUserOnBoard_AsEditor_ReturnsOkAndUpdatesRole()
    {
        // Arrange
        var (_, ownerToken) = await RegisterAndLoginAsync();
        SetAuthToken(ownerToken);
        var board = await CreateBoardAsync();

        var (memberId, _) = await RegisterAndLoginAsync();
        await LinkUserToBoardDirectlyAsync(board.Id, memberId, UserRoleEnum.VIEWER);

        var request = new LinkUserToBoardRequest { UserRole = UserRoleEnum.EDITOR };

        // Act
        var response = await Client.PutAsJsonAsync($"/api/boards/{board.Id}/link/{memberId}", request);

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert – database state
        await using var db = Factory.CreateDbContext();
        var persisted = await db.BoardOnUsers.AsNoTracking()
            .SingleOrDefaultAsync(bou => bou.BoardId == board.Id && bou.UserId == memberId);
        persisted!.UserRole.Should().Be(UserRoleEnum.EDITOR);
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

        // Act
        var response = await Client.PutAsJsonAsync(
            $"/api/boards/{board.Id}/link/{targetId}",
            new LinkUserToBoardRequest { UserRole = UserRoleEnum.EDITOR });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── GetUsersByUserName (collaborators) ────────────────────────────────────

    [Fact]
    public async Task GetCollaborators_WithMatchingUsername_ReturnsOkWithUsers()
    {
        // Arrange
        var (_, ownerToken) = await RegisterAndLoginAsync();
        SetAuthToken(ownerToken);
        var board = await CreateBoardAsync();

        // Register a user with a known username pattern
        var id = Guid.NewGuid().ToString("N")[..8];
        await Client.PostAsJsonAsync("/register", new Business.Dtos.Identity.UserRegisterRequest(
            FirstName: "Searchable",
            LastName: "User",
            UserName: $"searchable{id}",
            Email: $"searchable{id}@example.com",
            Password: "TestPass123!"
        ));

        // Act
        var response = await Client.GetAsync($"/api/boards/{board.Id}/collaborators?userName=searchable{id}");

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert – at least one user returned
        var body = await response.Content.ReadFromJsonAsync<IEnumerable<UserResponse>>();
        body.Should().NotBeNull();
        body!.Should().Contain(u => u.UserName!.Contains($"searchable{id}"));
    }

    [Fact]
    public async Task GetCollaborators_AsViewer_ReturnsForbidden()
    {
        // Arrange
        var (_, ownerToken) = await RegisterAndLoginAsync();
        SetAuthToken(ownerToken);
        var board = await CreateBoardAsync();

        var (viewerId, viewerToken) = await RegisterAndLoginAsync();
        await LinkUserToBoardDirectlyAsync(board.Id, viewerId, UserRoleEnum.VIEWER);
        SetAuthToken(viewerToken);

        // Act
        var response = await Client.GetAsync($"/api/boards/{board.Id}/collaborators?userName=test");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── TransferOwnership ─────────────────────────────────────────────────────

    [Fact]
    public async Task TransferOwnership_AsOwner_ReturnsNoContentAndSwitchesRoles()
    {
        // Arrange
        var (ownerId, ownerToken) = await RegisterAndLoginAsync();
        SetAuthToken(ownerToken);
        var board = await CreateBoardAsync();

        var (newOwnerId, _) = await RegisterAndLoginAsync();
        await LinkUserToBoardDirectlyAsync(board.Id, newOwnerId, UserRoleEnum.EDITOR);

        // Act
        var response = await Client.PostAsync($"/api/boards/{board.Id}/collaborators/{newOwnerId}", null);

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert – database state
        await using var db = Factory.CreateDbContext();
        var oldOwner = await db.BoardOnUsers.AsNoTracking()
            .SingleOrDefaultAsync(bou => bou.BoardId == board.Id && bou.UserId == ownerId);
        var newOwner = await db.BoardOnUsers.AsNoTracking()
            .SingleOrDefaultAsync(bou => bou.BoardId == board.Id && bou.UserId == newOwnerId);

        oldOwner!.UserRole.Should().NotBe(UserRoleEnum.OWNER);
        newOwner!.UserRole.Should().Be(UserRoleEnum.OWNER);
    }

    [Fact]
    public async Task TransferOwnership_ToSelf_ReturnsBadRequest()
    {
        // Arrange
        var (ownerId, ownerToken) = await RegisterAndLoginAsync();
        SetAuthToken(ownerToken);
        var board = await CreateBoardAsync();

        // Act
        var response = await Client.PostAsync($"/api/boards/{board.Id}/collaborators/{ownerId}", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── InviteUser ────────────────────────────────────────────────────────────

    [Fact]
    public async Task InviteUser_AsOwner_ReturnsNoContentAndCreatesNotification()
    {
        // Arrange
        var (_, ownerToken) = await RegisterAndLoginAsync();
        SetAuthToken(ownerToken);
        var board = await CreateBoardAsync();

        var (inviteeId, _) = await RegisterAndLoginAsync();

        // Act
        var response = await Client.PostAsJsonAsync($"/api/boards/{board.Id}/invite",
            new { UserId = inviteeId, Role = UserRoleEnum.VIEWER });

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert – notification created for invitee
        await using var db = Factory.CreateDbContext();
        var notification = await db.Notifications.AsNoTracking()
            .SingleOrDefaultAsync(n => n.SubjectUserId == inviteeId && n.BoardId == board.Id);
        notification.Should().NotBeNull();
    }

    [Fact]
    public async Task InviteUser_AsViewer_ReturnsForbidden()
    {
        // Arrange
        var (_, ownerToken) = await RegisterAndLoginAsync();
        SetAuthToken(ownerToken);
        var board = await CreateBoardAsync();

        var (viewerId, viewerToken) = await RegisterAndLoginAsync();
        await LinkUserToBoardDirectlyAsync(board.Id, viewerId, UserRoleEnum.VIEWER);
        SetAuthToken(viewerToken);

        var (targetId, _) = await RegisterAndLoginAsync();

        // Act
        var response = await Client.PostAsJsonAsync($"/api/boards/{board.Id}/invite",
            new { UserId = targetId, Role = UserRoleEnum.VIEWER });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
