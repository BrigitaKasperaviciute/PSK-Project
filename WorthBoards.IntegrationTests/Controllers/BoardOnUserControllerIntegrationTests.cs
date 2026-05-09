using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using WorthBoards.Business.Dtos.Identity;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Common.Enums;
using WorthBoards.Data.Database;
using WorthBoards.Data.Identity;
using Microsoft.Extensions.DependencyInjection;
using WorthBoards.IntegrationTests.Infrastructure;
using Xunit;

namespace WorthBoards.IntegrationTests.Controllers;

[Collection(nameof(ApiCollection))]
public class BoardOnUserControllerIntegrationTests
{
    private readonly ApiFactory _factory;

    public BoardOnUserControllerIntegrationTests(ApiFactory factory)
    {
        _factory = factory;
    }

    #region GetAllBoardToUserLinks Tests

    [Fact]
    public async Task GetAllBoardToUserLinks_WithViewerRole_ReturnsOkAndAllLinks()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1, "owner@test.com");
        var (viewer, _) = await CreateUserAsync(2, "viewer@test.com");
        var (editor, _) = await CreateUserAsync(3, "editor@test.com");

        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id, "Test Board", "Description");
        await helper.AddUserToBoardAsync(board.Id, viewer.Id, UserRoleEnum.VIEWER);
        await helper.AddUserToBoardAsync(board.Id, editor.Id, UserRoleEnum.EDITOR);

        var client = _factory.CreateClientForUser(viewer.Id, "VIEWER");

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}/links");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var links = JsonConvert.DeserializeObject<List<LinkedUserToBoardResponse>>(content);
        links.Should().NotBeNull();
        links!.Should().HaveCount(3); // Owner, viewer, editor

        dbContext.Dispose();
    }

    [Fact]
    public async Task GetAllBoardToUserLinks_WithUnauthorizedUser_ReturnsForbidden()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1, "owner@test.com");
        var (outsider, _) = await CreateUserAsync(2, "outsider@test.com");

        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id, "Private Board", "Description");

        var client = _factory.CreateClientForUser(outsider.Id, "VIEWER");

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}/links");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        dbContext.Dispose();
    }

    #endregion

    #region GetBoardToUserLink Tests

    [Fact]
    public async Task GetBoardToUserLink_WithValidIds_ReturnsOkAndLinkData()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1, "owner@test.com");
        var (viewer, _) = await CreateUserAsync(2, "viewer@test.com");

        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id, "Test Board", "Description");
        await helper.AddUserToBoardAsync(board.Id, viewer.Id, UserRoleEnum.VIEWER);

        var client = _factory.CreateClientForUser(viewer.Id, "VIEWER");

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}/link/{viewer.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var link = JsonConvert.DeserializeObject<LinkedUserToBoardResponse>(content);
        link.Should().NotBeNull();
        link!.Id.Should().Be(viewer.Id);
        link.UserName.Should().Be(viewer.UserName);
        link.UserRole.Should().Be(UserRoleEnum.VIEWER);

        dbContext.Dispose();
    }

    [Fact]
    public async Task GetBoardToUserLink_WithNonexistentLink_ReturnsNotFound()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1, "owner@test.com");
        var (viewer, _) = await CreateUserAsync(2, "viewer@test.com");
        var (nonlinked, _) = await CreateUserAsync(3, "nonlinked@test.com");

        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id, "Test Board", "Description");
        await helper.AddUserToBoardAsync(board.Id, viewer.Id, UserRoleEnum.VIEWER);

        var client = _factory.CreateClientForUser(viewer.Id, "VIEWER");

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}/link/{nonlinked.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        dbContext.Dispose();
    }

    #endregion

    #region LinkUserToBoard Tests

    [Fact]
    public async Task LinkUserToBoard_WithValidPayload_ReturnsCreatedAndLinksUser()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1, "owner@test.com");
        var (newUser, _) = await CreateUserAsync(2, "newuser@test.com");

        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id, "Test Board", "Description");

        var client = _factory.CreateClientForUser(newUser.Id, "VIEWER");

        var linkRequest = new LinkUserToBoardRequest { UserRole = UserRoleEnum.VIEWER };

        // Act
        var content = new StringContent(
            JsonConvert.SerializeObject(linkRequest),
            Encoding.UTF8,
            "application/json");
        var response = await client.PostAsync($"/api/boards/{board.Id}/link/{newUser.Id}", content);

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        // Assert - response body
        var responseContent = await response.Content.ReadAsStringAsync();
        var linkResponse = JsonConvert.DeserializeObject<LinkedUserToBoardResponse>(responseContent);
        linkResponse.Should().NotBeNull();
        linkResponse!.UserName.Should().Be(newUser.UserName);
        linkResponse.UserRole.Should().Be(UserRoleEnum.VIEWER);
        linkResponse.Id.Should().NotBe(0);
        linkResponse.AddedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        // Assert - database persistence
        var savedLink = await dbContext.BoardOnUsers
            .FirstOrDefaultAsync(b => b.BoardId == board.Id && b.UserId == newUser.Id);
        savedLink.Should().NotBeNull();
        savedLink!.UserRole.Should().Be(UserRoleEnum.VIEWER);

        dbContext.Dispose();
    }

    [Fact]
    public async Task LinkUserToBoard_WithAlreadyLinkedUser_ReturnsBadRequest()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1, "owner@test.com");
        var (user, _) = await CreateUserAsync(2, "user@test.com");

        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id, "Test Board", "Description");
        await helper.AddUserToBoardAsync(board.Id, user.Id, UserRoleEnum.VIEWER);

        var client = _factory.CreateClientForUser(user.Id, "VIEWER");

        var linkRequest = new LinkUserToBoardRequest { UserRole = UserRoleEnum.EDITOR };

        // Act
        var content = new StringContent(
            JsonConvert.SerializeObject(linkRequest),
            Encoding.UTF8,
            "application/json");
        var response = await client.PostAsync($"/api/boards/{board.Id}/link/{user.Id}", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        dbContext.Dispose();
    }

    [Fact]
    public async Task LinkUserToBoard_WithoutAuthorization_ReturnsUnauthorized()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var client = _factory.CreateClient();

        var linkRequest = new LinkUserToBoardRequest { UserRole = UserRoleEnum.VIEWER };

        // Act
        var content = new StringContent(
            JsonConvert.SerializeObject(linkRequest),
            Encoding.UTF8,
            "application/json");
        var response = await client.PostAsync($"/api/boards/1/link/1", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region UpdateUserOnBoard Tests

    [Fact]
    public async Task UpdateUserOnBoard_WithValidPayload_ReturnsOkAndUpdatesRole()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1, "owner@test.com");
        var (editor, _) = await CreateUserAsync(2, "editor@test.com");
        var (user, _) = await CreateUserAsync(3, "user@test.com");

        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id, "Test Board", "Description");
        await helper.AddUserToBoardAsync(board.Id, editor.Id, UserRoleEnum.EDITOR);
        await helper.AddUserToBoardAsync(board.Id, user.Id, UserRoleEnum.VIEWER);

        var client = _factory.CreateClientForUser(editor.Id, "EDITOR");

        var updateRequest = new LinkUserToBoardRequest { UserRole = UserRoleEnum.EDITOR };

        // Act
        var content = new StringContent(
            JsonConvert.SerializeObject(updateRequest),
            Encoding.UTF8,
            "application/json");
        var response = await client.PutAsync($"/api/boards/{board.Id}/link/{user.Id}", content);

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - response body
        var responseContent = await response.Content.ReadAsStringAsync();
        var linkResponse = JsonConvert.DeserializeObject<LinkedUserToBoardResponse>(responseContent);
        linkResponse.Should().NotBeNull();
        linkResponse!.UserRole.Should().Be(UserRoleEnum.EDITOR);

        // Assert - database state
        using var assertionContext = _factory.CreateDbContext();
        var updatedLink = await assertionContext.BoardOnUsers.AsNoTracking()
            .FirstOrDefaultAsync(b => b.BoardId == board.Id && b.UserId == user.Id);
        updatedLink!.UserRole.Should().Be(UserRoleEnum.EDITOR);

        dbContext.Dispose();
    }

    [Fact]
    public async Task UpdateUserOnBoard_WithViewerRole_ReturnsForbidden()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1, "owner@test.com");
        var (viewer, _) = await CreateUserAsync(2, "viewer@test.com");
        var (user, _) = await CreateUserAsync(3, "user@test.com");

        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id, "Test Board", "Description");
        await helper.AddUserToBoardAsync(board.Id, viewer.Id, UserRoleEnum.VIEWER);
        await helper.AddUserToBoardAsync(board.Id, user.Id, UserRoleEnum.VIEWER);

        var client = _factory.CreateClientForUser(viewer.Id, "VIEWER");

        var updateRequest = new LinkUserToBoardRequest { UserRole = UserRoleEnum.EDITOR };

        // Act
        var content = new StringContent(
            JsonConvert.SerializeObject(updateRequest),
            Encoding.UTF8,
            "application/json");
        var response = await client.PutAsync($"/api/boards/{board.Id}/link/{user.Id}", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        dbContext.Dispose();
    }

    [Fact]
    public async Task UpdateUserOnBoard_WithNonexistentLink_ReturnsNotFound()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1, "owner@test.com");
        var (editor, _) = await CreateUserAsync(2, "editor@test.com");
        var (nonexistent, _) = await CreateUserAsync(3, "nonexistent@test.com");

        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id, "Test Board", "Description");
        await helper.AddUserToBoardAsync(board.Id, editor.Id, UserRoleEnum.EDITOR);

        var client = _factory.CreateClientForUser(editor.Id, "EDITOR");

        var updateRequest = new LinkUserToBoardRequest { UserRole = UserRoleEnum.EDITOR };

        // Act
        var content = new StringContent(
            JsonConvert.SerializeObject(updateRequest),
            Encoding.UTF8,
            "application/json");
        var response = await client.PutAsync($"/api/boards/{board.Id}/link/{nonexistent.Id}", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        dbContext.Dispose();
    }

    #endregion

    #region PatchUserOnBoard Tests

    [Fact]
    public async Task PatchUserOnBoard_WithValidPatch_ReturnsOkAndAppliesChanges()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1, "owner@test.com");
        var (editor, _) = await CreateUserAsync(2, "editor@test.com");
        var (user, _) = await CreateUserAsync(3, "user@test.com");

        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id, "Test Board", "Description");
        await helper.AddUserToBoardAsync(board.Id, editor.Id, UserRoleEnum.EDITOR);
        await helper.AddUserToBoardAsync(board.Id, user.Id, UserRoleEnum.VIEWER);

        var client = _factory.CreateClientForUser(editor.Id, "EDITOR");

        var patchDoc = new JsonPatchDocument<LinkUserToBoardRequest>();
        patchDoc.Replace(x => x.UserRole, UserRoleEnum.EDITOR);

        // Act
        var content = new StringContent(
            JsonConvert.SerializeObject(patchDoc),
            Encoding.UTF8,
            "application/json-patch+json");
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json-patch+json");
        var response = await client.PatchAsync($"/api/boards/{board.Id}/link/{user.Id}", content);

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - database state
        using var assertionContext = _factory.CreateDbContext();
        var patchedLink = await assertionContext.BoardOnUsers.AsNoTracking()
            .FirstOrDefaultAsync(b => b.BoardId == board.Id && b.UserId == user.Id);
        patchedLink!.UserRole.Should().Be(UserRoleEnum.EDITOR);

        dbContext.Dispose();
    }

    [Fact]
    public async Task PatchUserOnBoard_OnOwnerUser_ReturnsBadRequest()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1, "owner@test.com");
        var (editor, _) = await CreateUserAsync(2, "editor@test.com");

        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id, "Test Board", "Description");
        await helper.AddUserToBoardAsync(board.Id, editor.Id, UserRoleEnum.EDITOR);

        var client = _factory.CreateClientForUser(editor.Id, "EDITOR");

        var patchDoc = new JsonPatchDocument<LinkUserToBoardRequest>();
        patchDoc.Replace(x => x.UserRole, UserRoleEnum.VIEWER);

        // Act
        var content = new StringContent(
            JsonConvert.SerializeObject(patchDoc),
            Encoding.UTF8,
            "application/json-patch+json");
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json-patch+json");
        var response = await client.PatchAsync($"/api/boards/{board.Id}/link/{owner.Id}", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        dbContext.Dispose();
    }

    #endregion

    #region GetUsersLinkedToBoard Tests

    [Fact]
    public async Task GetUsersLinkedToBoard_WithValidBoard_ReturnsOkAndUserList()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1, "owner@test.com");
        var (user1, _) = await CreateUserAsync(2, "user1@test.com");
        var (user2, _) = await CreateUserAsync(3, "user2@test.com");

        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id, "Test Board", "Description");
        await helper.AddUserToBoardAsync(board.Id, user1.Id, UserRoleEnum.VIEWER);
        await helper.AddUserToBoardAsync(board.Id, user2.Id, UserRoleEnum.EDITOR);

        var client = _factory.CreateClientForUser(user1.Id, "VIEWER");

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var users = JsonConvert.DeserializeObject<List<LinkedUserToBoardResponse>>(content);
        users.Should().NotBeNull();
        users!.Should().HaveCount(3); // Owner + 2 users

        dbContext.Dispose();
    }

    [Fact]
    public async Task GetUsersLinkedToBoard_WithNoUsers_ReturnsOkAndEmptyList()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1, "owner@test.com");

        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id, "Test Board", "Description");

        var client = _factory.CreateClientForUser(owner.Id, "OWNER");

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var users = JsonConvert.DeserializeObject<List<LinkedUserToBoardResponse>>(content);
        users.Should().NotBeNull();
        users!.Should().HaveCount(1); // Only owner

        dbContext.Dispose();
    }

    #endregion

    #region InviteUser Tests

    [Fact]
    public async Task InviteUser_WithValidPayload_ReturnsNoContent()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1, "owner@test.com");
        var (invitee, _) = await CreateUserAsync(2, "invitee@test.com");

        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id, "Test Board", "Description");

        var client = _factory.CreateClientForUser(owner.Id, "OWNER");

        var invitationRequest = new InvitationRequest
        {
            UserId = invitee.Id,
            Role = UserRoleEnum.VIEWER
        };

        // Act
        var content = new StringContent(
            JsonConvert.SerializeObject(invitationRequest),
            Encoding.UTF8,
            "application/json");
        var response = await client.PostAsync($"/api/boards/{board.Id}/invite", content);

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert - notification created in database
        var notification = await dbContext.Notifications
            .FirstOrDefaultAsync(n => n.SubjectUserId == invitee.Id);
        notification.Should().NotBeNull();

        dbContext.Dispose();
    }

    [Fact]
    public async Task InviteUser_WithoutOwnerRole_ReturnsForbidden()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1, "owner@test.com");
        var (editor, _) = await CreateUserAsync(2, "editor@test.com");
        var (invitee, _) = await CreateUserAsync(3, "invitee@test.com");

        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id, "Test Board", "Description");
        await helper.AddUserToBoardAsync(board.Id, editor.Id, UserRoleEnum.EDITOR);

        var client = _factory.CreateClientForUser(editor.Id, "EDITOR");

        var invitationRequest = new InvitationRequest
        {
            UserId = invitee.Id,
            Role = UserRoleEnum.VIEWER
        };

        // Act
        var content = new StringContent(
            JsonConvert.SerializeObject(invitationRequest),
            Encoding.UTF8,
            "application/json");
        var response = await client.PostAsync($"/api/boards/{board.Id}/invite", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        dbContext.Dispose();
    }

    #endregion

    #region RemoveUser Tests

    [Fact]
    public async Task RemoveUser_ByOwner_ReturnsNoContent()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1, "owner@test.com");
        var (user, _) = await CreateUserAsync(2, "user@test.com");

        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id, "Test Board", "Description");
        await helper.AddUserToBoardAsync(board.Id, user.Id, UserRoleEnum.VIEWER);

        var client = _factory.CreateClientForUser(owner.Id, "OWNER");

        // Act
        var response = await client.PostAsync($"/api/boards/{board.Id}/remove/{user.Id}", null);

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert - user removed from database
        var link = await dbContext.BoardOnUsers
            .FirstOrDefaultAsync(b => b.BoardId == board.Id && b.UserId == user.Id);
        link.Should().BeNull();

        dbContext.Dispose();
    }

    [Fact]
    public async Task RemoveUser_BySelf_ReturnsNoContent()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1, "owner@test.com");
        var (user, _) = await CreateUserAsync(2, "user@test.com");

        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id, "Test Board", "Description");
        await helper.AddUserToBoardAsync(board.Id, user.Id, UserRoleEnum.VIEWER);

        var client = _factory.CreateClientForUser(user.Id, "VIEWER");

        // Act
        var response = await client.PostAsync($"/api/boards/{board.Id}/remove/{user.Id}", null);

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert - user removed from database
        var link = await dbContext.BoardOnUsers
            .FirstOrDefaultAsync(b => b.BoardId == board.Id && b.UserId == user.Id);
        link.Should().BeNull();

        dbContext.Dispose();
    }

    [Fact]
    public async Task RemoveUser_WithoutPermission_ReturnsForbidden()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1, "owner@test.com");
        var (viewer, _) = await CreateUserAsync(2, "viewer@test.com");
        var (user, _) = await CreateUserAsync(3, "user@test.com");

        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id, "Test Board", "Description");
        await helper.AddUserToBoardAsync(board.Id, viewer.Id, UserRoleEnum.VIEWER);
        await helper.AddUserToBoardAsync(board.Id, user.Id, UserRoleEnum.VIEWER);

        var client = _factory.CreateClientForUser(viewer.Id, "VIEWER");

        // Act
        var response = await client.PostAsync($"/api/boards/{board.Id}/remove/{user.Id}", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        dbContext.Dispose();
    }

    [Fact]
    public async Task RemoveUser_TryingToRemoveOwner_ReturnsForbidden()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1, "owner@test.com");
        var (editor, _) = await CreateUserAsync(2, "editor@test.com");

        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id, "Test Board", "Description");
        await helper.AddUserToBoardAsync(board.Id, editor.Id, UserRoleEnum.EDITOR);

        var client = _factory.CreateClientForUser(editor.Id, "EDITOR");

        // Act
        var response = await client.PostAsync($"/api/boards/{board.Id}/remove/{owner.Id}", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        dbContext.Dispose();
    }

    [Fact]
    public async Task RemoveUser_WithoutAuthorization_ReturnsUnauthorized()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsync($"/api/boards/1/remove/1", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region TransferOwnership Tests

    [Fact]
    public async Task TransferOwnership_WithValidUser_ReturnsNoContent()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (currentOwner, _) = await CreateUserAsync(1, "owner@test.com");
        var (newOwner, _) = await CreateUserAsync(2, "newowner@test.com");

        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(currentOwner.Id, "Test Board", "Description");
        await helper.AddUserToBoardAsync(board.Id, newOwner.Id, UserRoleEnum.EDITOR);

        var client = _factory.CreateClientForUser(currentOwner.Id, "OWNER");

        // Act
        var response = await client.PostAsync(
            $"/api/boards/{board.Id}/collaborators/{newOwner.Id}", 
            new StringContent(""));

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert - ownership transferred in database
        using var assertionContext = _factory.CreateDbContext();
        var currentOwnerLink = await assertionContext.BoardOnUsers.AsNoTracking()
            .FirstOrDefaultAsync(b => b.BoardId == board.Id && b.UserId == currentOwner.Id);
        var newOwnerLink = await assertionContext.BoardOnUsers.AsNoTracking()
            .FirstOrDefaultAsync(b => b.BoardId == board.Id && b.UserId == newOwner.Id);

        currentOwnerLink!.UserRole.Should().Be(UserRoleEnum.VIEWER);
        newOwnerLink!.UserRole.Should().Be(UserRoleEnum.OWNER);

        dbContext.Dispose();
    }

    [Fact]
    public async Task TransferOwnership_ToSelf_ReturnsBadRequest()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1, "owner@test.com");

        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id, "Test Board", "Description");

        var client = _factory.CreateClientForUser(owner.Id, "OWNER");

        // Act
        var response = await client.PostAsync(
            $"/api/boards/{board.Id}/collaborators/{owner.Id}", 
            new StringContent(""));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        dbContext.Dispose();
    }

    [Fact]
    public async Task TransferOwnership_ToUserNotOnBoard_ReturnsNotFound()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (currentOwner, _) = await CreateUserAsync(1, "owner@test.com");
        var (outsider, _) = await CreateUserAsync(2, "outsider@test.com");

        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(currentOwner.Id, "Test Board", "Description");

        var client = _factory.CreateClientForUser(currentOwner.Id, "OWNER");

        // Act
        var response = await client.PostAsync(
            $"/api/boards/{board.Id}/collaborators/{outsider.Id}", 
            new StringContent(""));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        dbContext.Dispose();
    }

    [Fact]
    public async Task TransferOwnership_WithoutOwnerRole_ReturnsForbidden()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1, "owner@test.com");
        var (editor, _) = await CreateUserAsync(2, "editor@test.com");
        var (newOwner, _) = await CreateUserAsync(3, "newowner@test.com");

        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id, "Test Board", "Description");
        await helper.AddUserToBoardAsync(board.Id, editor.Id, UserRoleEnum.EDITOR);
        await helper.AddUserToBoardAsync(board.Id, newOwner.Id, UserRoleEnum.EDITOR);

        var client = _factory.CreateClientForUser(editor.Id, "EDITOR");

        // Act
        var response = await client.PostAsync(
            $"/api/boards/{board.Id}/collaborators/{newOwner.Id}", 
            new StringContent(""));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        dbContext.Dispose();
    }

    #endregion

    #region GetUsersByUserName Tests

    [Fact]
    public async Task GetUsersByUserName_WithValidUsername_ReturnsOkAndAvailableUsers()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1, "owner@test.com", "ownerusr");
        var (user1, _) = await CreateUserAsync(2, "user1@test.com", "johnsmith");
        var (user2, _) = await CreateUserAsync(3, "user2@test.com", "janedoe");
        var (alreadyLinked, _) = await CreateUserAsync(4, "linked@test.com", "linkedusr");

        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id, "Test Board", "Description");
        await helper.AddUserToBoardAsync(board.Id, alreadyLinked.Id, UserRoleEnum.VIEWER);

        var client = _factory.CreateClientForUser(owner.Id, "OWNER");

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}/collaborators?userName=john");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var users = JsonConvert.DeserializeObject<List<UserResponse>>(content);
        users.Should().NotBeNull();
        users!.Should().Contain(u => u.UserName == "johnsmith");
        users.Should().NotContain(u => u.Id == alreadyLinked.Id); // Already linked user should be excluded

        dbContext.Dispose();
    }

    [Fact]
    public async Task GetUsersByUserName_WithoutOwnerRole_ReturnsForbidden()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1, "owner@test.com");
        var (editor, _) = await CreateUserAsync(2, "editor@test.com");

        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id, "Test Board", "Description");
        await helper.AddUserToBoardAsync(board.Id, editor.Id, UserRoleEnum.EDITOR);

        var client = _factory.CreateClientForUser(editor.Id, "EDITOR");

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}/collaborators?userName=test");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        dbContext.Dispose();
    }

    #endregion

    #region Helper Methods

    private UserManager<ApplicationUser> GetUserManager(ApplicationDbContext dbContext)
    {
        return _factory.GetUserManager(dbContext);
    }

    private async Task<(ApplicationUser User, string Password)> CreateUserAsync(
        int userId,
        string email,
        string userName = null!,
        string password = "DefaultPassword123!")
    {
        userName ??= $"user{userId}";
        var truncatedUserName = userName.Length > 30 ? userName.Substring(0, 30) : userName;

        var dbContext = _factory.CreateDbContext();
        var userManager = GetUserManager(dbContext);

        var user = new ApplicationUser
        {
            Id = userId,
            FirstName = $"User",
            LastName = $"Number{userId}",
            UserName = truncatedUserName,
            Email = email,
            NormalizedEmail = email.ToUpper(),
            NormalizedUserName = truncatedUserName.ToUpper(),
            SecurityStamp = Guid.NewGuid().ToString(),
            CreationDate = DateTime.UtcNow
        };

        await userManager.CreateAsync(user, password);
        dbContext.Dispose();

        return (user, password);
    }

    #endregion
}
