using System.Net;
using System.Net.Http.Json;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Common.Enums;
using WorthBoards.IntegrationTests.Infrastructure;

namespace WorthBoards.IntegrationTests.Controllers;

public class BoardOnUserControllerTests : IntegrationTestBase
{
    public BoardOnUserControllerTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task InviteUser_AsOwner_ReturnsNoContent()
    {
        var (ownerToken, _) = await RegisterAndLoginAsync();
        var (_, guestId) = await RegisterAndLoginAsync();
        var ownerClient = CreateAuthenticatedClient(ownerToken);
        var board = await CreateBoardAsync(ownerClient);

        var inviteRequest = new InvitationRequest { UserId = guestId, Role = UserRoleEnum.VIEWER };
        var response = await ownerClient.PostAsJsonAsync($"/api/boards/{board.Id}/invite", inviteRequest);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task InviteUser_AsNonOwner_ReturnsForbidden()
    {
        var (ownerToken, _) = await RegisterAndLoginAsync();
        var (otherToken, otherId) = await RegisterAndLoginAsync();
        var ownerClient = CreateAuthenticatedClient(ownerToken);
        var board = await CreateBoardAsync(ownerClient);

        var inviteRequest = new InvitationRequest { UserId = otherId, Role = UserRoleEnum.VIEWER };
        var response = await CreateAuthenticatedClient(otherToken)
            .PostAsJsonAsync($"/api/boards/{board.Id}/invite", inviteRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RemoveUser_AsOwner_ReturnsNoContent()
    {
        var (ownerToken, _) = await RegisterAndLoginAsync();
        var (guestToken, guestId) = await RegisterAndLoginAsync();
        var ownerClient = CreateAuthenticatedClient(ownerToken);
        var guestClient = CreateAuthenticatedClient(guestToken);
        var board = await CreateBoardAsync(ownerClient);

        var notification = await InviteUserAndGetNotificationAsync(ownerClient, guestClient, board.Id, guestId);
        await guestClient.PostAsJsonAsync($"/api/notifications/{notification.Id}/accept", new { });

        var response = await ownerClient.PostAsJsonAsync($"/api/boards/{board.Id}/remove/{guestId}", new { });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task RemoveUser_Unauthenticated_ReturnsUnauthorized()
    {
        var response = await CreateAnonymousClient().PostAsJsonAsync("/api/boards/1/remove/1", new { });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetLinks_AsMember_ReturnsOk()
    {
        var (token, _) = await RegisterAndLoginAsync();
        var client = CreateAuthenticatedClient(token);
        var board = await CreateBoardAsync(client);

        var response = await client.GetAsync($"/api/boards/{board.Id}/links");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetLinks_AsNonMember_ReturnsForbidden()
    {
        var (ownerToken, _) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(CreateAuthenticatedClient(ownerToken));

        var (otherToken, _) = await RegisterAndLoginAsync();
        var response = await CreateAuthenticatedClient(otherToken).GetAsync($"/api/boards/{board.Id}/links");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetLink_AsMember_ReturnsOk()
    {
        var (token, ownerId) = await RegisterAndLoginAsync();
        var client = CreateAuthenticatedClient(token);
        var board = await CreateBoardAsync(client);

        var response = await client.GetAsync($"/api/boards/{board.Id}/link/{ownerId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<LinkUserToBoardResponse>();
        body!.UserId.Should().Be(ownerId);
    }

    [Fact]
    public async Task GetLink_AsNonMember_ReturnsForbidden()
    {
        var (ownerToken, ownerId) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(CreateAuthenticatedClient(ownerToken));

        var (otherToken, _) = await RegisterAndLoginAsync();
        var response = await CreateAuthenticatedClient(otherToken)
            .GetAsync($"/api/boards/{board.Id}/link/{ownerId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task LinkUserToBoard_AsOwner_ReturnsCreated()
    {
        var (ownerToken, _) = await RegisterAndLoginAsync();
        var (_, guestId) = await RegisterAndLoginAsync();
        var ownerClient = CreateAuthenticatedClient(ownerToken);
        var board = await CreateBoardAsync(ownerClient);

        var request = new LinkUserToBoardRequest { UserRole = UserRoleEnum.VIEWER };
        var response = await ownerClient.PostAsJsonAsync($"/api/boards/{board.Id}/link/{guestId}", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task LinkUserToBoard_Unauthenticated_ReturnsUnauthorized()
    {
        var request = new LinkUserToBoardRequest { UserRole = UserRoleEnum.VIEWER };
        var response = await CreateAnonymousClient().PostAsJsonAsync("/api/boards/1/link/1", request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateUserOnBoard_AsOwner_ReturnsOk()
    {
        var (ownerToken, _) = await RegisterAndLoginAsync();
        var (_, guestId) = await RegisterAndLoginAsync();
        var ownerClient = CreateAuthenticatedClient(ownerToken);
        var board = await CreateBoardAsync(ownerClient);
        await LinkUserToBoardDirectlyAsync(ownerClient, board.Id, guestId, UserRoleEnum.VIEWER);

        var updateRequest = new LinkUserToBoardRequest { UserRole = UserRoleEnum.EDITOR };
        var response = await ownerClient.PutAsJsonAsync($"/api/boards/{board.Id}/link/{guestId}", updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<LinkUserToBoardResponse>();
        body!.UserRole.Should().Be(UserRoleEnum.EDITOR);
    }

    [Fact]
    public async Task UpdateUserOnBoard_AsNonMember_ReturnsForbidden()
    {
        var (ownerToken, _) = await RegisterAndLoginAsync();
        var (_, guestId) = await RegisterAndLoginAsync();
        var ownerClient = CreateAuthenticatedClient(ownerToken);
        var board = await CreateBoardAsync(ownerClient);

        var (otherToken, _) = await RegisterAndLoginAsync();
        var updateRequest = new LinkUserToBoardRequest { UserRole = UserRoleEnum.EDITOR };
        var response = await CreateAuthenticatedClient(otherToken)
            .PutAsJsonAsync($"/api/boards/{board.Id}/link/{guestId}", updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PatchUserOnBoard_AsOwner_ReturnsOk()
    {
        var (ownerToken, _) = await RegisterAndLoginAsync();
        var (_, guestId) = await RegisterAndLoginAsync();
        var ownerClient = CreateAuthenticatedClient(ownerToken);
        var board = await CreateBoardAsync(ownerClient);
        await LinkUserToBoardDirectlyAsync(ownerClient, board.Id, guestId, UserRoleEnum.VIEWER);

        var patchDoc = new[] { new { op = "replace", path = "/userRole", value = (object)UserRoleEnum.EDITOR } };
        var response = await ownerClient.PatchAsJsonAsync($"/api/boards/{board.Id}/link/{guestId}", patchDoc);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PatchUserOnBoard_AsNonMember_ReturnsForbidden()
    {
        var (ownerToken, _) = await RegisterAndLoginAsync();
        var (_, guestId) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(CreateAuthenticatedClient(ownerToken));

        var (otherToken, _) = await RegisterAndLoginAsync();
        var patchDoc = new[] { new { op = "replace", path = "/userRole", value = (object)UserRoleEnum.EDITOR } };
        var response = await CreateAuthenticatedClient(otherToken)
            .PatchAsJsonAsync($"/api/boards/{board.Id}/link/{guestId}", patchDoc);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetUsersLinkedToBoard_AsMember_ReturnsOk()
    {
        var (token, _) = await RegisterAndLoginAsync();
        var client = CreateAuthenticatedClient(token);
        var board = await CreateBoardAsync(client);

        var response = await client.GetAsync($"/api/boards/{board.Id}/users");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetUsersLinkedToBoard_AsNonMember_ReturnsForbidden()
    {
        var (ownerToken, _) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(CreateAuthenticatedClient(ownerToken));

        var (otherToken, _) = await RegisterAndLoginAsync();
        var response = await CreateAuthenticatedClient(otherToken).GetAsync($"/api/boards/{board.Id}/users");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }


    [Fact]
    public async Task TransferOwnership_AsOwner_ReturnsNoContent()
    {
        var (ownerToken, _) = await RegisterAndLoginAsync();
        var (_, guestId) = await RegisterAndLoginAsync();
        var ownerClient = CreateAuthenticatedClient(ownerToken);
        var board = await CreateBoardAsync(ownerClient);
        await LinkUserToBoardDirectlyAsync(ownerClient, board.Id, guestId, UserRoleEnum.VIEWER);

        var response = await ownerClient
            .PostAsJsonAsync($"/api/boards/{board.Id}/collaborators/{guestId}", new { });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task TransferOwnership_AsNonOwner_ReturnsForbidden()
    {
        var (ownerToken, ownerId) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(CreateAuthenticatedClient(ownerToken));

        var (otherToken, _) = await RegisterAndLoginAsync();
        var response = await CreateAuthenticatedClient(otherToken)
            .PostAsJsonAsync($"/api/boards/{board.Id}/collaborators/{ownerId}", new { });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
