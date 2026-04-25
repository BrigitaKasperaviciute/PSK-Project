using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Common.Enums;
using WorthBoards.Data.Database;
using WorthBoards.IntegrationTests.Infrastructure;
using Xunit;

namespace WorthBoards.IntegrationTests;

public class BoardOnUserControllerTests(WorthBoardsWebApplicationFactory factory)
    : IClassFixture<WorthBoardsWebApplicationFactory>
{
    private HttpClient AuthorizedClient(int userId, string userName, string email)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTokenHelper.GenerateToken(userId, userName, email));
        return client;
    }

    // ── GET links ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllBoardToUserLinks_AsViewer_ReturnsOk()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync(owner);
        var client = AuthorizedClient(owner.Id, owner.UserName!, owner.Email!);

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}/links");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IEnumerable<LinkUserToBoardResponse>>();
        body.Should().NotBeNull();
        body.Should().Contain(l => l.UserId == owner.Id);
    }

    [Fact]
    public async Task GetAllBoardToUserLinks_NotBoardMember_ReturnsForbidden()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var outsider = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync(owner);
        var client = AuthorizedClient(outsider.Id, outsider.UserName!, outsider.Email!);

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}/links");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── GET single link ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetBoardToUserLink_ExistingLink_ReturnsOk()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync(owner);
        var client = AuthorizedClient(owner.Id, owner.UserName!, owner.Email!);

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}/link/{owner.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<LinkUserToBoardResponse>();
        body!.UserId.Should().Be(owner.Id);
        body.UserRole.Should().Be(UserRoleEnum.OWNER);
    }

    [Fact]
    public async Task GetBoardToUserLink_NonExistingLink_ReturnsNotFound()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var outsider = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync(owner);
        var client = AuthorizedClient(owner.Id, owner.UserName!, owner.Email!);

        // Act – outsider is not linked to the board
        var response = await client.GetAsync($"/api/boards/{board.Id}/link/{outsider.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── POST link (accept without invitation flow) ────────────────────────────

    [Fact]
    public async Task LinkUserToBoard_ValidRequest_ReturnsCreated()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var newUser = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync(owner);
        var client = AuthorizedClient(owner.Id, owner.UserName!, owner.Email!);
        var request = new LinkUserToBoardRequest { UserRole = UserRoleEnum.EDITOR };

        // Act
        var response = await client.PostAsJsonAsync($"/api/boards/{board.Id}/link/{newUser.Id}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<LinkUserToBoardResponse>();
        body!.UserId.Should().Be(newUser.Id);
        body.UserRole.Should().Be(UserRoleEnum.EDITOR);

        using var dbScope = factory.Services.CreateScope();
        var db = dbScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.BoardOnUsers.Any(l => l.BoardId == board.Id && l.UserId == newUser.Id).Should().BeTrue();
    }

    [Fact]
    public async Task LinkUserToBoard_Unauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var client = factory.CreateClient();
        var request = new LinkUserToBoardRequest { UserRole = UserRoleEnum.VIEWER };

        // Act
        var response = await client.PostAsJsonAsync("/api/boards/1/link/2", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── PUT link ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateUserOnBoard_AsEditor_ReturnsOk()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var editor = await seeder.CreateUserAsync();
        var viewer = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync(owner);
        await seeder.LinkUserToBoardAsync(board, editor, UserRoleEnum.EDITOR);
        await seeder.LinkUserToBoardAsync(board, viewer, UserRoleEnum.VIEWER);
        var client = AuthorizedClient(editor.Id, editor.UserName!, editor.Email!);
        var request = new LinkUserToBoardRequest { UserRole = UserRoleEnum.EDITOR };

        // Act
        var response = await client.PutAsJsonAsync($"/api/boards/{board.Id}/link/{viewer.Id}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdateUserOnBoard_AsViewer_ReturnsForbidden()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var viewer = await seeder.CreateUserAsync();
        var target = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync(owner);
        await seeder.LinkUserToBoardAsync(board, viewer, UserRoleEnum.VIEWER);
        await seeder.LinkUserToBoardAsync(board, target, UserRoleEnum.VIEWER);
        var client = AuthorizedClient(viewer.Id, viewer.UserName!, viewer.Email!);
        var request = new LinkUserToBoardRequest { UserRole = UserRoleEnum.EDITOR };

        // Act
        var response = await client.PutAsJsonAsync($"/api/boards/{board.Id}/link/{target.Id}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── PATCH link ───────────────────────────────────────────────────────────

    [Fact]
    public async Task PatchUserOnBoard_AsEditor_ReturnsOk()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var viewer = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync(owner);
        await seeder.LinkUserToBoardAsync(board, viewer, UserRoleEnum.VIEWER);
        var client = AuthorizedClient(owner.Id, owner.UserName!, owner.Email!);

        var patchDoc = Newtonsoft.Json.JsonConvert.SerializeObject(new[]
        {
            new { op = "replace", path = "/userRole", value = (int)UserRoleEnum.EDITOR }
        });
        var content = new StringContent(patchDoc, Encoding.UTF8, "application/json-patch+json");

        // Act
        var response = await client.PatchAsync($"/api/boards/{board.Id}/link/{viewer.Id}", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PatchUserOnBoard_AsViewer_ReturnsForbidden()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var viewer = await seeder.CreateUserAsync();
        var target = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync(owner);
        await seeder.LinkUserToBoardAsync(board, viewer, UserRoleEnum.VIEWER);
        await seeder.LinkUserToBoardAsync(board, target, UserRoleEnum.VIEWER);
        var client = AuthorizedClient(viewer.Id, viewer.UserName!, viewer.Email!);

        var patchDoc = Newtonsoft.Json.JsonConvert.SerializeObject(new[]
        {
            new { op = "replace", path = "/userRole", value = (int)UserRoleEnum.EDITOR }
        });
        var content = new StringContent(patchDoc, Encoding.UTF8, "application/json-patch+json");

        // Act
        var response = await client.PatchAsync($"/api/boards/{board.Id}/link/{target.Id}", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── GET users on board ───────────────────────────────────────────────────

    [Fact]
    public async Task GetUsersLinkedToBoard_AsViewer_ReturnsOkWithUsers()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync(owner);
        var client = AuthorizedClient(owner.Id, owner.UserName!, owner.Email!);

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IEnumerable<LinkedUserToBoardResponse>>();
        body.Should().Contain(u => u.Id == owner.Id);
    }

    [Fact]
    public async Task GetUsersLinkedToBoard_NotBoardMember_ReturnsForbidden()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var outsider = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync(owner);
        var client = AuthorizedClient(outsider.Id, outsider.UserName!, outsider.Email!);

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── POST invite ──────────────────────────────────────────────────────────

    [Fact]
    public async Task InviteUser_AsOwner_ReturnsNoContent()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var invitee = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync(owner);
        var client = AuthorizedClient(owner.Id, owner.UserName!, owner.Email!);
        var request = new InvitationRequest { UserId = invitee.Id, Role = UserRoleEnum.VIEWER };

        // Act
        var response = await client.PostAsJsonAsync($"/api/boards/{board.Id}/invite", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var dbScope = factory.Services.CreateScope();
        var db = dbScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Notifications.Any(n =>
            n.BoardId == board.Id &&
            n.SubjectUserId == invitee.Id &&
            n.NotificationType == NotificationEventTypeEnum.INVITATION
        ).Should().BeTrue();
    }

    [Fact]
    public async Task InviteUser_AsViewer_ReturnsForbidden()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var viewer = await seeder.CreateUserAsync();
        var invitee = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync(owner);
        await seeder.LinkUserToBoardAsync(board, viewer, UserRoleEnum.VIEWER);
        var client = AuthorizedClient(viewer.Id, viewer.UserName!, viewer.Email!);
        var request = new InvitationRequest { UserId = invitee.Id, Role = UserRoleEnum.VIEWER };

        // Act
        var response = await client.PostAsJsonAsync($"/api/boards/{board.Id}/invite", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── POST remove ──────────────────────────────────────────────────────────

    [Fact]
    public async Task RemoveUser_OwnerRemovesViewer_ReturnsNoContent()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var viewer = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync(owner);
        await seeder.LinkUserToBoardAsync(board, viewer, UserRoleEnum.VIEWER);
        var client = AuthorizedClient(owner.Id, owner.UserName!, owner.Email!);

        // Act
        var response = await client.PostAsync($"/api/boards/{board.Id}/remove/{viewer.Id}", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var dbScope = factory.Services.CreateScope();
        var db = dbScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.BoardOnUsers.Any(l => l.BoardId == board.Id && l.UserId == viewer.Id).Should().BeFalse();
    }

    [Fact]
    public async Task RemoveUser_Unauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var client = factory.CreateClient();

        // Act
        var response = await client.PostAsync("/api/boards/1/remove/2", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── GET collaborators ────────────────────────────────────────────────────

    [Fact]
    public async Task GetCollaboratorsByUserName_AsOwner_ReturnsOk()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync(owner);
        var client = AuthorizedClient(owner.Id, owner.UserName!, owner.Email!);

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}/collaborators?userName=user");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetCollaboratorsByUserName_AsViewer_ReturnsForbidden()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var viewer = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync(owner);
        await seeder.LinkUserToBoardAsync(board, viewer, UserRoleEnum.VIEWER);
        var client = AuthorizedClient(viewer.Id, viewer.UserName!, viewer.Email!);

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}/collaborators?userName=user");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── POST transfer ownership ───────────────────────────────────────────────

    [Fact]
    public async Task TransferOwnership_AsOwner_ReturnsNoContent()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var newOwner = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync(owner);
        await seeder.LinkUserToBoardAsync(board, newOwner, UserRoleEnum.EDITOR);
        var client = AuthorizedClient(owner.Id, owner.UserName!, owner.Email!);

        // Act
        var response = await client.PostAsync($"/api/boards/{board.Id}/collaborators/{newOwner.Id}", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task TransferOwnership_AsViewer_ReturnsForbidden()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var viewer = await seeder.CreateUserAsync();
        var newOwner = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync(owner);
        await seeder.LinkUserToBoardAsync(board, viewer, UserRoleEnum.VIEWER);
        var client = AuthorizedClient(viewer.Id, viewer.UserName!, viewer.Email!);

        // Act
        var response = await client.PostAsync($"/api/boards/{board.Id}/collaborators/{newOwner.Id}", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
