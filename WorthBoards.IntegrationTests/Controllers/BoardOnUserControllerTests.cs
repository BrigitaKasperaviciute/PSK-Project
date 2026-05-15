using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
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

    // ---------------------------------------------------------------------------
    // GET /api/boards/{boardId}/links  [AuthorizeRole(VIEWER)]
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetAllBoardToUserLinks_AsViewer_ReturnsAllLinks()
    {
        // Arrange - owner and viewer both linked to the same board
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var viewer = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync();
        await seeder.LinkUserToBoardAsync(board.Id, owner.Id, UserRoleEnum.OWNER);
        await seeder.LinkUserToBoardAsync(board.Id, viewer.Id, UserRoleEnum.VIEWER);

        var client = _factory.CreateAuthenticatedClient(viewer.Id, viewer.UserName!, viewer.Email!);

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}/links");

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - both links returned
        var links = await response.Content.ReadFromJsonAsync<IEnumerable<LinkUserToBoardResponse>>();
        links.Should().NotBeNull();
        links!.Should().HaveCount(2);
        links.Should().Contain(l => l.UserId == owner.Id && l.UserRole == UserRoleEnum.OWNER);
        links.Should().Contain(l => l.UserId == viewer.Id && l.UserRole == UserRoleEnum.VIEWER);
    }

    [Fact]
    public async Task GetAllBoardToUserLinks_WhenNotBoardMember_ReturnsForbidden()
    {
        // Arrange - user has no board membership
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var nonMember = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync();

        var client = _factory.CreateAuthenticatedClient(nonMember.Id, nonMember.UserName!, nonMember.Email!);

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}/links");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---------------------------------------------------------------------------
    // GET /api/boards/{boardId}/users  [AuthorizeRole(VIEWER)]
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetUsersLinkedToBoard_AsViewer_ReturnsLinkedUsers()
    {
        // Arrange
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var viewer = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync();
        await seeder.LinkUserToBoardAsync(board.Id, owner.Id, UserRoleEnum.OWNER);
        await seeder.LinkUserToBoardAsync(board.Id, viewer.Id, UserRoleEnum.VIEWER);

        var client = _factory.CreateAuthenticatedClient(viewer.Id, viewer.UserName!, viewer.Email!);

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var users = await response.Content.ReadFromJsonAsync<IEnumerable<LinkedUserToBoardResponse>>();
        users.Should().NotBeNull();
        users!.Should().HaveCount(2);
    }

    // ---------------------------------------------------------------------------
    // POST /api/boards/{boardId}/link/{userId}  [Authorize]
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task LinkUserToBoard_WhenAuthenticated_ReturnsCreatedAndPersistsLink()
    {
        // Arrange
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var requester = await seeder.CreateUserAsync();
        var newMember = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync();
        await seeder.LinkUserToBoardAsync(board.Id, requester.Id, UserRoleEnum.OWNER);

        var client = _factory.CreateAuthenticatedClient(requester.Id, requester.UserName!, requester.Email!);

        var request = new LinkUserToBoardRequest { UserRole = UserRoleEnum.EDITOR };

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/boards/{board.Id}/link/{newMember.Id}", request);

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        // Assert - response body
        var body = await response.Content.ReadFromJsonAsync<LinkUserToBoardResponse>();
        body.Should().NotBeNull();
        body!.BoardId.Should().Be(board.Id);
        body.UserId.Should().Be(newMember.Id);
        body.UserRole.Should().Be(UserRoleEnum.EDITOR);

        // Assert - database state
        await using var db = _factory.CreateDbContext();
        var persisted = await db.BoardOnUsers.AsNoTracking()
            .SingleOrDefaultAsync(b => b.BoardId == board.Id && b.UserId == newMember.Id);
        persisted.Should().NotBeNull();
        persisted!.UserRole.Should().Be(UserRoleEnum.EDITOR);
    }

    [Fact]
    public async Task LinkUserToBoard_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var user = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync();
        var client = _factory.CreateClient();

        var request = new LinkUserToBoardRequest { UserRole = UserRoleEnum.VIEWER };

        // Act
        var response = await client.PostAsJsonAsync($"/api/boards/{board.Id}/link/{user.Id}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------------------------------------------------------------------------
    // POST /api/boards/{boardId}/remove/{userId}  [Authorize]
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task RemoveUser_AsOwner_ReturnsNoContentAndRemovesLink()
    {
        // Arrange
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var memberToRemove = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync();
        await seeder.LinkUserToBoardAsync(board.Id, owner.Id, UserRoleEnum.OWNER);
        await seeder.LinkUserToBoardAsync(board.Id, memberToRemove.Id, UserRoleEnum.VIEWER);

        var client = _factory.CreateAuthenticatedClient(owner.Id, owner.UserName!, owner.Email!);

        // Act
        var response = await client.PostAsync(
            $"/api/boards/{board.Id}/remove/{memberToRemove.Id}", null);

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert - link removed from database
        await using var db = _factory.CreateDbContext();
        var removed = await db.BoardOnUsers.AsNoTracking()
            .SingleOrDefaultAsync(b => b.BoardId == board.Id && b.UserId == memberToRemove.Id);
        removed.Should().BeNull();
    }

    // ---------------------------------------------------------------------------
    // POST /api/boards/{boardId}/collaborators/{newOwnerId}  [AuthorizeRole(OWNER)]
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task TransferOwnership_AsOwner_ReturnsNoContent()
    {
        // Arrange
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var currentOwner = await seeder.CreateUserAsync();
        var newOwner = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync();
        await seeder.LinkUserToBoardAsync(board.Id, currentOwner.Id, UserRoleEnum.OWNER);
        await seeder.LinkUserToBoardAsync(board.Id, newOwner.Id, UserRoleEnum.EDITOR);

        var client = _factory.CreateAuthenticatedClient(
            currentOwner.Id, currentOwner.UserName!, currentOwner.Email!);

        // Act
        var response = await client.PostAsync(
            $"/api/boards/{board.Id}/collaborators/{newOwner.Id}", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task TransferOwnership_AsEditor_ReturnsForbidden()
    {
        // Arrange
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var editor = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync();
        await seeder.LinkUserToBoardAsync(board.Id, owner.Id, UserRoleEnum.OWNER);
        await seeder.LinkUserToBoardAsync(board.Id, editor.Id, UserRoleEnum.EDITOR);

        var client = _factory.CreateAuthenticatedClient(editor.Id, editor.UserName!, editor.Email!);

        // Act - editor tries to transfer ownership
        var response = await client.PostAsync(
            $"/api/boards/{board.Id}/collaborators/{owner.Id}", null);

        // Assert - editor role (1) > required OWNER role (0)
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---------------------------------------------------------------------------
    // GET /api/boards/{boardId}/collaborators  [AuthorizeRole(OWNER)]
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetUsersByUserName_AsOwner_ReturnsMatchingUsers()
    {
        // Arrange
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync();
        await seeder.LinkUserToBoardAsync(board.Id, owner.Id, UserRoleEnum.OWNER);

        var client = _factory.CreateAuthenticatedClient(owner.Id, owner.UserName!, owner.Email!);

        // Act - search for the owner by partial username
        var response = await client.GetAsync(
            $"/api/boards/{board.Id}/collaborators?userName={owner.UserName![..4]}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ---------------------------------------------------------------------------
    // GET /api/boards/{boardId}/users — negative
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetUsersLinkedToBoard_WhenNotBoardMember_ReturnsForbidden()
    {
        // Arrange
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var nonMember = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync();

        var client = _factory.CreateAuthenticatedClient(nonMember.Id, nonMember.UserName!, nonMember.Email!);

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}/users");

        // Assert - PermissionHandler fails for non-members
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---------------------------------------------------------------------------
    // GET /api/boards/{boardId}/link/{userId}  [AuthorizeRole(VIEWER)]
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetBoardToUserLink_AsViewer_ReturnsLink()
    {
        // Arrange
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var viewer = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync();
        await seeder.LinkUserToBoardAsync(board.Id, owner.Id, UserRoleEnum.OWNER);
        await seeder.LinkUserToBoardAsync(board.Id, viewer.Id, UserRoleEnum.VIEWER);

        var client = _factory.CreateAuthenticatedClient(viewer.Id, viewer.UserName!, viewer.Email!);

        // Act - fetch the link for the owner
        var response = await client.GetAsync($"/api/boards/{board.Id}/link/{owner.Id}");

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - response body
        var body = await response.Content.ReadFromJsonAsync<LinkUserToBoardResponse>();
        body.Should().NotBeNull();
        body!.BoardId.Should().Be(board.Id);
        body.UserId.Should().Be(owner.Id);
        body.UserRole.Should().Be(UserRoleEnum.OWNER);
    }

    [Fact]
    public async Task GetBoardToUserLink_WhenNotBoardMember_ReturnsForbidden()
    {
        // Arrange
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var nonMember = await seeder.CreateUserAsync();
        var owner = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync();
        await seeder.LinkUserToBoardAsync(board.Id, owner.Id, UserRoleEnum.OWNER);

        var client = _factory.CreateAuthenticatedClient(nonMember.Id, nonMember.UserName!, nonMember.Email!);

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}/link/{owner.Id}");

        // Assert - non-member cannot access board links
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---------------------------------------------------------------------------
    // PUT /api/boards/{boardId}/link/{userId}  [AuthorizeRole(EDITOR)]
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task UpdateUserOnBoard_AsOwner_ReturnsOkWithUpdatedRole()
    {
        // Arrange
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var viewer = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync();
        await seeder.LinkUserToBoardAsync(board.Id, owner.Id, UserRoleEnum.OWNER);
        await seeder.LinkUserToBoardAsync(board.Id, viewer.Id, UserRoleEnum.VIEWER);

        var client = _factory.CreateAuthenticatedClient(owner.Id, owner.UserName!, owner.Email!);

        var request = new LinkUserToBoardRequest { UserRole = UserRoleEnum.EDITOR };

        // Act - owner promotes viewer to editor
        var response = await client.PutAsJsonAsync($"/api/boards/{board.Id}/link/{viewer.Id}", request);

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - response body
        var body = await response.Content.ReadFromJsonAsync<LinkUserToBoardResponse>();
        body.Should().NotBeNull();
        body!.UserRole.Should().Be(UserRoleEnum.EDITOR);

        // Assert - database state
        await using var db = _factory.CreateDbContext();
        var persisted = await db.BoardOnUsers.AsNoTracking()
            .SingleOrDefaultAsync(b => b.BoardId == board.Id && b.UserId == viewer.Id);
        persisted.Should().NotBeNull();
        persisted!.UserRole.Should().Be(UserRoleEnum.EDITOR);
    }

    [Fact]
    public async Task UpdateUserOnBoard_AsViewer_ReturnsForbidden()
    {
        // Arrange
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var viewer = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync();
        await seeder.LinkUserToBoardAsync(board.Id, owner.Id, UserRoleEnum.OWNER);
        await seeder.LinkUserToBoardAsync(board.Id, viewer.Id, UserRoleEnum.VIEWER);

        var client = _factory.CreateAuthenticatedClient(viewer.Id, viewer.UserName!, viewer.Email!);

        var request = new LinkUserToBoardRequest { UserRole = UserRoleEnum.EDITOR };

        // Act - viewer tries to update another user's role
        var response = await client.PutAsJsonAsync($"/api/boards/{board.Id}/link/{owner.Id}", request);

        // Assert - viewer role (2) > required EDITOR role (1)
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---------------------------------------------------------------------------
    // POST /api/boards/{boardId}/remove/{userId} — negative
    // ---------------------------------------------------------------------------

    // ---------------------------------------------------------------------------
    // POST /api/boards/{boardId}/invite  [AuthorizeRole(OWNER)]
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task InviteUser_AsOwner_ReturnsNoContent()
    {
        // Arrange
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var invitee = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync();
        await seeder.LinkUserToBoardAsync(board.Id, owner.Id, UserRoleEnum.OWNER);

        var client = _factory.CreateAuthenticatedClient(owner.Id, owner.UserName!, owner.Email!);

        var request = new InvitationRequest { UserId = invitee.Id, Role = UserRoleEnum.EDITOR };

        // Act
        var response = await client.PostAsJsonAsync($"/api/boards/{board.Id}/invite", request);

        // Assert - invitation notification created, no board membership yet
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task InviteUser_AsViewer_ReturnsForbidden()
    {
        // Arrange
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var viewer = await seeder.CreateUserAsync();
        var target = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync();
        await seeder.LinkUserToBoardAsync(board.Id, viewer.Id, UserRoleEnum.VIEWER);

        var client = _factory.CreateAuthenticatedClient(viewer.Id, viewer.UserName!, viewer.Email!);

        var request = new InvitationRequest { UserId = target.Id, Role = UserRoleEnum.EDITOR };

        // Act
        var response = await client.PostAsJsonAsync($"/api/boards/{board.Id}/invite", request);

        // Assert - viewer role (2) > required OWNER role (0)
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---------------------------------------------------------------------------
    // PATCH /api/boards/{boardId}/link/{userId}  [AuthorizeRole(EDITOR)]
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task PatchUserOnBoard_AsOwner_ReturnsOkWithPatchedRole()
    {
        // Arrange
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var editor = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync();
        await seeder.LinkUserToBoardAsync(board.Id, owner.Id, UserRoleEnum.OWNER);
        await seeder.LinkUserToBoardAsync(board.Id, editor.Id, UserRoleEnum.EDITOR);

        var client = _factory.CreateAuthenticatedClient(owner.Id, owner.UserName!, owner.Email!);

        var patchDoc = new[]
        {
            new { op = "replace", path = "/userRole", value = (int)UserRoleEnum.VIEWER }
        };
        var content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(patchDoc),
            System.Text.Encoding.UTF8,
            "application/json-patch+json");

        // Act
        var response = await client.PatchAsync($"/api/boards/{board.Id}/link/{editor.Id}", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<LinkUserToBoardResponse>();
        body!.UserRole.Should().Be(UserRoleEnum.VIEWER);
    }

    [Fact]
    public async Task RemoveUser_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var member = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync();
        await seeder.LinkUserToBoardAsync(board.Id, member.Id, UserRoleEnum.VIEWER);

        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsync($"/api/boards/{board.Id}/remove/{member.Id}", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Assert - link still exists
        await using var db = _factory.CreateDbContext();
        var stillExists = await db.BoardOnUsers.AsNoTracking()
            .AnyAsync(b => b.BoardId == board.Id && b.UserId == member.Id);
        stillExists.Should().BeTrue();
    }
}
