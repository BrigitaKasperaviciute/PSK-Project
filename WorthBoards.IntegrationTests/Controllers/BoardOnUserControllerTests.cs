using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Common.Enums;
using WorthBoards.IntegrationTests.Infrastructure;
using Xunit;

namespace WorthBoards.IntegrationTests.Controllers;

[Collection(nameof(ApiCollection))]
[Trait("Category", "Integration")]
public sealed class BoardOnUserControllerTests : IAsyncLifetime
{
    private readonly ApiFactory _factory;

    public BoardOnUserControllerTests(ApiFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        await using var db = _factory.CreateDbContext();
        await DatabaseSeeder.CleanDatabaseAsync(db);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<(int UserId, HttpClient Client, Data.Identity.ApplicationUser User)> CreateUserWithClientAsync(string? username = null, string? email = null)
    {
        await using var db = _factory.CreateDbContext();
        var userManager = _factory.Services.CreateScope().ServiceProvider
            .GetRequiredService<UserManager<Data.Identity.ApplicationUser>>();
        var user = await DatabaseSeeder.CreateUserAsync(db, userManager, username, email);
        return (user.Id, _factory.CreateAuthenticatedClient(user.Id, user.UserName!, user.Email!), user);
    }

    // --- POST /api/boards/{boardId}/invite ---

    [Fact]
    public async Task InviteUser_AsOwner_ReturnsNoContent()
    {
        // Arrange
        var (ownerId, ownerClient, _) = await CreateUserWithClientAsync("owner_inv", "owner_inv@test.com");
        var (inviteeId, _, _) = await CreateUserWithClientAsync("invitee", "invitee@test.com");
        await using var db = _factory.CreateDbContext();
        var board = await DatabaseSeeder.CreateBoardAsync(db, ownerId, UserRoleEnum.OWNER);

        var request = new InvitationRequest { UserId = inviteeId, Role = UserRoleEnum.VIEWER };

        // Act
        var response = await ownerClient.PostAsJsonAsync($"/api/boards/{board.Id}/invite", request);

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert - database state: invitation notification created
        await using var verifyDb = _factory.CreateDbContext();
        var invitation = await verifyDb.Notifications.AsNoTracking()
            .SingleOrDefaultAsync(n => n.BoardId == board.Id && n.SubjectUserId == inviteeId);
        invitation.Should().NotBeNull();
    }

    [Fact]
    public async Task InviteUser_AsEditor_ReturnsForbidden()
    {
        // Arrange
        var (ownerId, _, _) = await CreateUserWithClientAsync("owner_inv2", "owner_inv2@test.com");
        var (editorId, editorClient, _) = await CreateUserWithClientAsync("editor_inv", "editor_inv@test.com");
        var (inviteeId, _, _) = await CreateUserWithClientAsync("invitee2", "invitee2@test.com");
        await using var db = _factory.CreateDbContext();
        var board = await DatabaseSeeder.CreateBoardAsync(db, ownerId, UserRoleEnum.OWNER);
        await DatabaseSeeder.AddUserToBoardAsync(db, board.Id, editorId, UserRoleEnum.EDITOR);

        var request = new InvitationRequest { UserId = inviteeId, Role = UserRoleEnum.VIEWER };

        // Act
        var response = await editorClient.PostAsJsonAsync($"/api/boards/{board.Id}/invite", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // --- GET /api/boards/{boardId}/links ---

    [Fact]
    public async Task GetAllBoardToUserLinks_AsViewer_ReturnsOkWithLinks()
    {
        // Arrange
        var (userId, client, _) = await CreateUserWithClientAsync();
        await using var db = _factory.CreateDbContext();
        var board = await DatabaseSeeder.CreateBoardAsync(db, userId, UserRoleEnum.VIEWER);

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}/links");

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - response body
        var body = await response.Content.ReadFromJsonAsync<IEnumerable<LinkUserToBoardResponse>>();
        body.Should().NotBeNull();
        body.Should().HaveCountGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task GetAllBoardToUserLinks_WhenNotOnBoard_ReturnsForbidden()
    {
        // Arrange
        var (ownerId, _, _) = await CreateUserWithClientAsync("owner_lnk", "owner_lnk@test.com");
        var (otherId, otherClient, _) = await CreateUserWithClientAsync("other_lnk", "other_lnk@test.com");
        await using var db = _factory.CreateDbContext();
        var board = await DatabaseSeeder.CreateBoardAsync(db, ownerId, UserRoleEnum.OWNER);

        // Act - user not on the board
        var response = await otherClient.GetAsync($"/api/boards/{board.Id}/links");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // --- GET /api/boards/{boardId}/link/{userId} ---

    [Fact]
    public async Task GetBoardToUserLink_WhenLinkExists_ReturnsOkWithLink()
    {
        // Arrange
        var (userId, client, _) = await CreateUserWithClientAsync();
        await using var db = _factory.CreateDbContext();
        var board = await DatabaseSeeder.CreateBoardAsync(db, userId, UserRoleEnum.OWNER);

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}/link/{userId}");

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - response body
        var body = await response.Content.ReadFromJsonAsync<LinkUserToBoardResponse>();
        body.Should().NotBeNull();
        body!.UserId.Should().Be(userId);
        body.BoardId.Should().Be(board.Id);
        body.UserRole.Should().Be(UserRoleEnum.OWNER);
    }

    [Fact]
    public async Task GetBoardToUserLink_WhenLinkNotFound_ReturnsNotFound()
    {
        // Arrange
        var (userId, client, _) = await CreateUserWithClientAsync();
        await using var db = _factory.CreateDbContext();
        var board = await DatabaseSeeder.CreateBoardAsync(db, userId, UserRoleEnum.OWNER);

        // Act - look for a user not on the board
        var response = await client.GetAsync($"/api/boards/{board.Id}/link/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- POST /api/boards/{boardId}/link/{userId} ---

    [Fact]
    public async Task LinkUserToBoard_WhenAuthenticated_ReturnsCreatedAndPersistsLink()
    {
        // Arrange
        var (ownerId, ownerClient, _) = await CreateUserWithClientAsync("link_owner", "link_owner@test.com");
        var (newUserId, _, _) = await CreateUserWithClientAsync("link_user", "link_user@test.com");
        await using var db = _factory.CreateDbContext();
        var board = await DatabaseSeeder.CreateBoardAsync(db, ownerId, UserRoleEnum.OWNER);

        var request = new LinkUserToBoardRequest { UserRole = UserRoleEnum.VIEWER };

        // Act
        var response = await ownerClient.PostAsJsonAsync($"/api/boards/{board.Id}/link/{newUserId}", request);

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        // Assert - response body
        var body = await response.Content.ReadFromJsonAsync<LinkUserToBoardResponse>();
        body.Should().NotBeNull();
        body!.UserId.Should().Be(newUserId);
        body.BoardId.Should().Be(board.Id);
        body.UserRole.Should().Be(UserRoleEnum.VIEWER);

        // Assert - database state
        await using var verifyDb = _factory.CreateDbContext();
        var link = await verifyDb.BoardOnUsers.AsNoTracking()
            .SingleOrDefaultAsync(bou => bou.BoardId == board.Id && bou.UserId == newUserId);
        link.Should().NotBeNull();
        link!.UserRole.Should().Be(UserRoleEnum.VIEWER);
    }

    [Fact]
    public async Task LinkUserToBoard_WhenOwnerAlreadyLinked_ReturnsBadRequest()
    {
        // Arrange
        var (ownerId, ownerClient, _) = await CreateUserWithClientAsync("link_owner_bad", "link_owner_bad@test.com");
        await using var db = _factory.CreateDbContext();
        var board = await DatabaseSeeder.CreateBoardAsync(db, ownerId, UserRoleEnum.OWNER);

        var request = new LinkUserToBoardRequest { UserRole = UserRoleEnum.VIEWER };

        // Act
        var response = await ownerClient.PostAsJsonAsync($"/api/boards/{board.Id}/link/{ownerId}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // --- PUT /api/boards/{boardId}/link/{userId} ---

    [Fact]
    public async Task UpdateUserOnBoard_AsOwner_ReturnsOkWithUpdatedRole()
    {
        // Arrange
        var (ownerId, ownerClient, _) = await CreateUserWithClientAsync("upd_owner", "upd_owner@test.com");
        var (memberId, _, _) = await CreateUserWithClientAsync("upd_member", "upd_member@test.com");
        await using var db = _factory.CreateDbContext();
        var board = await DatabaseSeeder.CreateBoardAsync(db, ownerId, UserRoleEnum.OWNER);
        await DatabaseSeeder.AddUserToBoardAsync(db, board.Id, memberId, UserRoleEnum.VIEWER);

        var request = new LinkUserToBoardRequest { UserRole = UserRoleEnum.EDITOR };

        // Act
        var response = await ownerClient.PutAsJsonAsync($"/api/boards/{board.Id}/link/{memberId}", request);

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - response body
        var body = await response.Content.ReadFromJsonAsync<LinkUserToBoardResponse>();
        body.Should().NotBeNull();
        body!.UserRole.Should().Be(UserRoleEnum.EDITOR);

        // Assert - database state
        await using var verifyDb = _factory.CreateDbContext();
        var link = await verifyDb.BoardOnUsers.AsNoTracking()
            .SingleOrDefaultAsync(bou => bou.BoardId == board.Id && bou.UserId == memberId);
        link!.UserRole.Should().Be(UserRoleEnum.EDITOR);
    }

    [Fact]
    public async Task UpdateUserOnBoard_AsViewer_ReturnsForbidden()
    {
        // Arrange
        var (ownerId, _, _) = await CreateUserWithClientAsync("upd_own2", "upd_own2@test.com");
        var (viewerId, viewerClient, _) = await CreateUserWithClientAsync("upd_viewer", "upd_viewer@test.com");
        await using var db = _factory.CreateDbContext();
        var board = await DatabaseSeeder.CreateBoardAsync(db, ownerId, UserRoleEnum.OWNER);
        await DatabaseSeeder.AddUserToBoardAsync(db, board.Id, viewerId, UserRoleEnum.VIEWER);

        var request = new LinkUserToBoardRequest { UserRole = UserRoleEnum.EDITOR };

        // Act
        var response = await viewerClient.PutAsJsonAsync($"/api/boards/{board.Id}/link/{viewerId}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PatchUserOnBoard_AsOwner_ReturnsBadRequest()
    {
        // Arrange
        var (ownerId, ownerClient, _) = await CreateUserWithClientAsync("patch_owner_board", "patch_owner_board@test.com");
        await using var db = _factory.CreateDbContext();
        var board = await DatabaseSeeder.CreateBoardAsync(db, ownerId, UserRoleEnum.OWNER);

        var patchDoc = new[]
        {
            new { op = "replace", path = "/userRole", value = (object)UserRoleEnum.EDITOR }
        };

        var request = new System.Net.Http.StringContent(
            System.Text.Json.JsonSerializer.Serialize(patchDoc),
            System.Text.Encoding.UTF8,
            "application/json-patch+json"
        );

        // Act
        var response = await ownerClient.PatchAsync($"/api/boards/{board.Id}/link/{ownerId}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // --- GET /api/boards/{boardId}/users ---

    [Fact]
    public async Task GetUsersLinkedToBoard_AsViewer_ReturnsOkWithUsers()
    {
        // Arrange
        var (userId, client, _) = await CreateUserWithClientAsync();
        await using var db = _factory.CreateDbContext();
        var board = await DatabaseSeeder.CreateBoardAsync(db, userId, UserRoleEnum.VIEWER);

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}/users");

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - response body contains at least the owner
        var body = await response.Content.ReadFromJsonAsync<IEnumerable<Business.Dtos.Identity.UserResponse>>();
        body.Should().NotBeNull();
        body.Should().HaveCountGreaterThanOrEqualTo(1);
    }

    // --- GET /api/boards/{boardId}/collaborators ---

    [Fact]
    public async Task GetUsersByUserName_AsOwner_ReturnsOkWithMatchingUsers()
    {
        // Arrange
        var (ownerId, ownerClient, _) = await CreateUserWithClientAsync("collab_owner", "collab_owner@test.com");
        await CreateUserWithClientAsync("searchable_user", "searchable@test.com");
        await using var db = _factory.CreateDbContext();
        var board = await DatabaseSeeder.CreateBoardAsync(db, ownerId, UserRoleEnum.OWNER);

        // Act
        var response = await ownerClient.GetAsync($"/api/boards/{board.Id}/collaborators?userName=searchable");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetUsersByUserName_AsViewer_ReturnsForbidden()
    {
        // Arrange
        var (ownerId, _, _) = await CreateUserWithClientAsync("collab_owner_forbidden", "collab_owner_forbidden@test.com");
        var (viewerId, viewerClient, _) = await CreateUserWithClientAsync("collab_viewer", "collab_viewer@test.com");
        await using var db = _factory.CreateDbContext();
        var board = await DatabaseSeeder.CreateBoardAsync(db, ownerId, UserRoleEnum.OWNER);
        await DatabaseSeeder.AddUserToBoardAsync(db, board.Id, viewerId, UserRoleEnum.VIEWER);

        // Act
        var response = await viewerClient.GetAsync($"/api/boards/{board.Id}/collaborators?userName=collab");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // --- POST /api/boards/{boardId}/collaborators/{newOwnerId} ---

    [Fact]
    public async Task TransferOwnership_AsOwner_ReturnsNoContentAndUpdatesRoles()
    {
        // Arrange
        var (ownerId, ownerClient, _) = await CreateUserWithClientAsync("transfer_owner", "transfer_owner@test.com");
        var (newOwnerId, _, _) = await CreateUserWithClientAsync("new_owner", "new_owner@test.com");
        await using var db = _factory.CreateDbContext();
        var board = await DatabaseSeeder.CreateBoardAsync(db, ownerId, UserRoleEnum.OWNER);
        await DatabaseSeeder.AddUserToBoardAsync(db, board.Id, newOwnerId, UserRoleEnum.EDITOR);

        // Act
        var response = await ownerClient.PostAsync($"/api/boards/{board.Id}/collaborators/{newOwnerId}", null);

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert - database state: new user is now owner
        await using var verifyDb = _factory.CreateDbContext();
        var newOwnerLink = await verifyDb.BoardOnUsers.AsNoTracking()
            .SingleOrDefaultAsync(bou => bou.BoardId == board.Id && bou.UserId == newOwnerId);
        newOwnerLink.Should().NotBeNull();
        newOwnerLink!.UserRole.Should().Be(UserRoleEnum.OWNER);
    }

    [Fact]
    public async Task TransferOwnership_AsEditor_ReturnsForbidden()
    {
        // Arrange
        var (ownerId, _, _) = await CreateUserWithClientAsync("trans_own", "trans_own@test.com");
        var (editorId, editorClient, _) = await CreateUserWithClientAsync("trans_editor", "trans_editor@test.com");
        var (targetId, _, _) = await CreateUserWithClientAsync("trans_target", "trans_target@test.com");
        await using var db = _factory.CreateDbContext();
        var board = await DatabaseSeeder.CreateBoardAsync(db, ownerId, UserRoleEnum.OWNER);
        await DatabaseSeeder.AddUserToBoardAsync(db, board.Id, editorId, UserRoleEnum.EDITOR);

        // Act
        var response = await editorClient.PostAsync($"/api/boards/{board.Id}/collaborators/{targetId}", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // --- POST /api/boards/{boardId}/remove/{userId} ---

    [Fact]
    public async Task RemoveUser_AsOwner_ReturnsNoContentAndRemovesLink()
    {
        // Arrange
        var (ownerId, ownerClient, _) = await CreateUserWithClientAsync("rem_owner", "rem_owner@test.com");
        var (memberId, _, _) = await CreateUserWithClientAsync("rem_member", "rem_member@test.com");
        await using var db = _factory.CreateDbContext();
        var board = await DatabaseSeeder.CreateBoardAsync(db, ownerId, UserRoleEnum.OWNER);
        await DatabaseSeeder.AddUserToBoardAsync(db, board.Id, memberId, UserRoleEnum.VIEWER);

        // Act
        var response = await ownerClient.PostAsync($"/api/boards/{board.Id}/remove/{memberId}", null);

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert - database state: member removed
        await using var verifyDb = _factory.CreateDbContext();
        var link = await verifyDb.BoardOnUsers.AsNoTracking()
            .SingleOrDefaultAsync(bou => bou.BoardId == board.Id && bou.UserId == memberId);
        link.Should().BeNull();
    }

    [Fact]
    public async Task RemoveUser_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsync("/api/boards/1/remove/2", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
