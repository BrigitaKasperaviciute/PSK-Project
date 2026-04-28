using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using WorthBoards.Business.Dtos.Identity;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Common.Enums;
using WorthBoards.IntegrationTests.Infrastructure;

namespace WorthBoards.IntegrationTests.Controllers;

public class BoardOnUserControllerTests : IntegrationTestBase
{
    public BoardOnUserControllerTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task GetUsersLinkedToBoard_AsBoardMember_ReturnsOk()
    {
        var (token, _) = await RegisterAndLoginAsync();
        var client = CreateAuthenticatedClient(token);
        var board = await CreateBoardAsync(client);

        var response = await client.GetAsync($"/api/boards/{board.Id}/users");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetUsersLinkedToBoard_Unauthenticated_ReturnsUnauthorized()
    {
        var (token, _) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(CreateAuthenticatedClient(token));

        var response = await CreateAnonymousClient().GetAsync($"/api/boards/{board.Id}/users");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAllBoardToUserLinks_AsBoardMember_ReturnsOk()
    {
        var (token, _) = await RegisterAndLoginAsync();
        var client = CreateAuthenticatedClient(token);
        var board = await CreateBoardAsync(client);

        var response = await client.GetAsync($"/api/boards/{board.Id}/links");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetAllBoardToUserLinks_AsNonMember_ReturnsForbidden()
    {
        var (ownerToken, _) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(CreateAuthenticatedClient(ownerToken));

        var (otherToken, _) = await RegisterAndLoginAsync();
        var response = await CreateAuthenticatedClient(otherToken)
            .GetAsync($"/api/boards/{board.Id}/links");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetBoardToUserLink_OwnerLink_ReturnsOk()
    {
        var (token, userId) = await RegisterAndLoginAsync();
        var client = CreateAuthenticatedClient(token);
        var board = await CreateBoardAsync(client);

        var response = await client.GetAsync($"/api/boards/{board.Id}/link/{userId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<LinkUserToBoardResponse>();
        body!.UserId.Should().Be(userId);
    }

    [Fact]
    public async Task GetBoardToUserLink_NonExistingLink_ReturnsNotFound()
    {
        var (token, _) = await RegisterAndLoginAsync();
        var client = CreateAuthenticatedClient(token);
        var board = await CreateBoardAsync(client);

        var response = await client.GetAsync($"/api/boards/{board.Id}/link/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task LinkUserToBoard_AsAnonymous_ReturnsUnauthorized()
    {
        var (token, userId) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(CreateAuthenticatedClient(token));

        var request = new LinkUserToBoardRequest { UserRole = UserRoleEnum.VIEWER };
        var response = await CreateAnonymousClient()
            .PostAsJsonAsync($"/api/boards/{board.Id}/link/{userId}", request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LinkUserToBoard_SecondUser_AsOwner_ReturnsCreated()
    {
        var (ownerToken, _) = await RegisterAndLoginAsync();
        var ownerClient = CreateAuthenticatedClient(ownerToken);
        var board = await CreateBoardAsync(ownerClient);

        var (_, otherUserId) = await RegisterAndLoginAsync();
        var request = new LinkUserToBoardRequest { UserRole = UserRoleEnum.VIEWER };
        var response = await ownerClient
            .PostAsJsonAsync($"/api/boards/{board.Id}/link/{otherUserId}", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task UpdateUserOnBoard_AsOwner_ReturnsOk()
    {
        var (ownerToken, _) = await RegisterAndLoginAsync();
        var ownerClient = CreateAuthenticatedClient(ownerToken);
        var board = await CreateBoardAsync(ownerClient);

        var (_, otherUserId) = await RegisterAndLoginAsync();
        // First link the user
        var linkRequest = new LinkUserToBoardRequest { UserRole = UserRoleEnum.VIEWER };
        await ownerClient.PostAsJsonAsync($"/api/boards/{board.Id}/link/{otherUserId}", linkRequest);

        // Then update their role
        var updateRequest = new LinkUserToBoardRequest { UserRole = UserRoleEnum.EDITOR };
        var response = await ownerClient.PutAsJsonAsync($"/api/boards/{board.Id}/link/{otherUserId}", updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdateUserOnBoard_AsNonMember_ReturnsForbidden()
    {
        var (ownerToken, _) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(CreateAuthenticatedClient(ownerToken));

        var (otherToken, otherUserId) = await RegisterAndLoginAsync();
        var updateRequest = new LinkUserToBoardRequest { UserRole = UserRoleEnum.EDITOR };
        var response = await CreateAuthenticatedClient(otherToken)
            .PutAsJsonAsync($"/api/boards/{board.Id}/link/{otherUserId}", updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RemoveUser_SelfRemoval_ReturnsNoContent()
    {
        var (ownerToken, _) = await RegisterAndLoginAsync();
        var ownerClient = CreateAuthenticatedClient(ownerToken);
        var board = await CreateBoardAsync(ownerClient);

        var (otherToken, otherUserId) = await RegisterAndLoginAsync();
        var linkRequest = new LinkUserToBoardRequest { UserRole = UserRoleEnum.VIEWER };
        await ownerClient.PostAsJsonAsync($"/api/boards/{board.Id}/link/{otherUserId}", linkRequest);

        // Other user removes themselves
        var response = await CreateAuthenticatedClient(otherToken)
            .PostAsJsonAsync($"/api/boards/{board.Id}/remove/{otherUserId}", new { });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task RemoveUser_Unauthenticated_ReturnsUnauthorized()
    {
        var (token, _) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(CreateAuthenticatedClient(token));

        var response = await CreateAnonymousClient()
            .PostAsJsonAsync($"/api/boards/{board.Id}/remove/1", new { });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetCollaborators_AsOwner_ReturnsOk()
    {
        var (token, _) = await RegisterAndLoginAsync();
        var client = CreateAuthenticatedClient(token);
        var board = await CreateBoardAsync(client);

        var response = await client.GetAsync($"/api/boards/{board.Id}/collaborators?userName=test");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetCollaborators_AsNonMember_ReturnsForbidden()
    {
        var (ownerToken, _) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(CreateAuthenticatedClient(ownerToken));

        var (otherToken, _) = await RegisterAndLoginAsync();
        var response = await CreateAuthenticatedClient(otherToken)
            .GetAsync($"/api/boards/{board.Id}/collaborators?userName=test");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TransferOwnership_AsOwner_ReturnsNoContent()
    {
        var (ownerToken, _) = await RegisterAndLoginAsync();
        var ownerClient = CreateAuthenticatedClient(ownerToken);
        var board = await CreateBoardAsync(ownerClient);

        var (_, newOwnerId) = await RegisterAndLoginAsync();
        // Link new owner first
        var linkRequest = new LinkUserToBoardRequest { UserRole = UserRoleEnum.EDITOR };
        await ownerClient.PostAsJsonAsync($"/api/boards/{board.Id}/link/{newOwnerId}", linkRequest);

        var response = await ownerClient
            .PostAsJsonAsync($"/api/boards/{board.Id}/collaborators/{newOwnerId}", new { });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task TransferOwnership_AsNonOwner_ReturnsForbidden()
    {
        var (ownerToken, _) = await RegisterAndLoginAsync();
        var ownerClient = CreateAuthenticatedClient(ownerToken);
        var board = await CreateBoardAsync(ownerClient);

        var (otherToken, otherUserId) = await RegisterAndLoginAsync();
        var linkRequest = new LinkUserToBoardRequest { UserRole = UserRoleEnum.EDITOR };
        await ownerClient.PostAsJsonAsync($"/api/boards/{board.Id}/link/{otherUserId}", linkRequest);

        // Editor tries to transfer ownership
        var response = await CreateAuthenticatedClient(otherToken)
            .PostAsJsonAsync($"/api/boards/{board.Id}/collaborators/{otherUserId}", new { });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task InviteUser_AsOwner_ReturnsNoContent()
    {
        var (ownerToken, _) = await RegisterAndLoginAsync();
        var ownerClient = CreateAuthenticatedClient(ownerToken);
        var board = await CreateBoardAsync(ownerClient);

        var (_, guestUserId) = await RegisterAndLoginAsync();
        var inviteRequest = new InvitationRequest { UserId = guestUserId, Role = UserRoleEnum.VIEWER };
        var response = await ownerClient.PostAsJsonAsync($"/api/boards/{board.Id}/invite", inviteRequest);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task InviteUser_AsNonOwner_ReturnsForbidden()
    {
        var (ownerToken, _) = await RegisterAndLoginAsync();
        var ownerClient = CreateAuthenticatedClient(ownerToken);
        var board = await CreateBoardAsync(ownerClient);

        var (editorToken, editorUserId) = await RegisterAndLoginAsync();
        var linkRequest = new LinkUserToBoardRequest { UserRole = UserRoleEnum.EDITOR };
        await ownerClient.PostAsJsonAsync($"/api/boards/{board.Id}/link/{editorUserId}", linkRequest);

        var (_, guestUserId) = await RegisterAndLoginAsync();
        var inviteRequest = new InvitationRequest { UserId = guestUserId, Role = UserRoleEnum.VIEWER };
        var response = await CreateAuthenticatedClient(editorToken)
            .PostAsJsonAsync($"/api/boards/{board.Id}/invite", inviteRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PatchUserOnBoard_AsEditor_ReturnsOk()
    {
        var (ownerToken, _) = await RegisterAndLoginAsync();
        var ownerClient = CreateAuthenticatedClient(ownerToken);
        var board = await CreateBoardAsync(ownerClient);

        var (editorToken, editorUserId) = await RegisterAndLoginAsync();
        var (_, viewerUserId) = await RegisterAndLoginAsync();
        // Link both users
        await ownerClient.PostAsJsonAsync($"/api/boards/{board.Id}/link/{editorUserId}",
            new LinkUserToBoardRequest { UserRole = UserRoleEnum.EDITOR });
        await ownerClient.PostAsJsonAsync($"/api/boards/{board.Id}/link/{viewerUserId}",
            new LinkUserToBoardRequest { UserRole = UserRoleEnum.VIEWER });

        var patchDoc = new[] { new { op = "replace", path = "/userRole", value = (object)1 } }; // EDITOR=1
        var response = await CreateAuthenticatedClient(editorToken)
            .PatchAsJsonAsync($"/api/boards/{board.Id}/link/{viewerUserId}", patchDoc);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PatchUserOnBoard_AsViewer_ReturnsForbidden()
    {
        var (ownerToken, _) = await RegisterAndLoginAsync();
        var ownerClient = CreateAuthenticatedClient(ownerToken);
        var board = await CreateBoardAsync(ownerClient);

        var (viewerToken, viewerUserId) = await RegisterAndLoginAsync();
        await ownerClient.PostAsJsonAsync($"/api/boards/{board.Id}/link/{viewerUserId}",
            new LinkUserToBoardRequest { UserRole = UserRoleEnum.VIEWER });

        var patchDoc = new[] { new { op = "replace", path = "/userRole", value = (object)1 } };
        var response = await CreateAuthenticatedClient(viewerToken)
            .PatchAsJsonAsync($"/api/boards/{board.Id}/link/{viewerUserId}", patchDoc);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
