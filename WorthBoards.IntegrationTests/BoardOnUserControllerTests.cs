using System.Net;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Business.Dtos.Identity;
using WorthBoards.Common.Enums;
using WorthBoards.Common.Exceptions;
using WorthBoards.IntegrationTests.Infrastructure;

namespace WorthBoards.IntegrationTests;

public sealed class BoardOnUserControllerTests(WorthBoardsApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task InviteUser_WithOwnerToken_ExercisesBoardMemberManagementFlow()
    {
        // Arrange
        var ownerClient = await CreateAuthenticatedClientAsync("owner");

        // Act
        var inviteResponse = await ownerClient.PostAsJsonAsync($"/api/boards/{Seed.BoardId}/invite", new InvitationRequest
        {
            UserId = Seed.CandidateUserId,
            Role = UserRoleEnum.VIEWER
        });

        // Assert
        inviteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<WorthBoards.Data.Database.ApplicationDbContext>();
            var notification = await context.Notifications.SingleAsync(notification => notification.SubjectUserId == Seed.CandidateUserId && notification.NotificationType == NotificationEventTypeEnum.INVITATION);
            notification.InvitationRole.Should().Be(UserRoleEnum.VIEWER);
            (await context.NotificationsOnUsers.AnyAsync(notificationUser => notificationUser.NotificationId == notification.Id && notificationUser.UserId == Seed.CandidateUserId)).Should().BeTrue();
        }

        var linksResponse = await ownerClient.GetAsync($"/api/boards/{Seed.BoardId}/links");
        linksResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var links = await ReadJsonAsync<List<LinkUserToBoardResponse>>(linksResponse);
        links.Should().Contain(link => link.UserId == Seed.OwnerUserId && link.UserRole == UserRoleEnum.OWNER);

        var singleLinkResponse = await ownerClient.GetAsync($"/api/boards/{Seed.BoardId}/link/{Seed.EditorUserId}");
        singleLinkResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var singleLink = await ReadJsonAsync<LinkUserToBoardResponse>(singleLinkResponse);
        singleLink.Should().NotBeNull();
        singleLink!.UserRole.Should().Be(UserRoleEnum.EDITOR);

        var collaboratorResponse = await ownerClient.GetAsync($"/api/boards/{Seed.BoardId}/collaborators?userName=out");
        collaboratorResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var collaboratorUsers = await ReadJsonAsync<List<UserResponse>>(collaboratorResponse);
        collaboratorUsers.Should().ContainSingle(user => user.Id == Seed.OutsiderUserId);

        var linkResponse = await ownerClient.PostAsJsonAsync($"/api/boards/{Seed.BoardId}/link/{Seed.OutsiderUserId}", new LinkUserToBoardRequest { UserRole = UserRoleEnum.VIEWER });
        linkResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var linkedUser = await ReadJsonAsync<LinkUserToBoardResponse>(linkResponse);
        linkedUser.Should().NotBeNull();
        linkedUser!.UserId.Should().Be(Seed.OutsiderUserId);

        var updateResponse = await ownerClient.PutAsJsonAsync($"/api/boards/{Seed.BoardId}/link/{Seed.OutsiderUserId}", new LinkUserToBoardRequest { UserRole = UserRoleEnum.EDITOR });
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var patchDocument = new JsonPatchDocument<LinkUserToBoardRequest>();
        patchDocument.Replace(link => link.UserRole, UserRoleEnum.VIEWER);
        var patchRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/boards/{Seed.BoardId}/link/{Seed.OutsiderUserId}")
        {
            Content = new StringContent(JsonConvert.SerializeObject(patchDocument), Encoding.UTF8, "application/json-patch+json")
        };
        var patchResponse = await ownerClient.SendAsync(patchRequest);
        patchResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var removeResponse = await ownerClient.PostAsync($"/api/boards/{Seed.BoardId}/remove/{Seed.OutsiderUserId}", null);
        removeResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var transferResponse = await ownerClient.PostAsync($"/api/boards/{Seed.BoardId}/collaborators/{Seed.EditorUserId}", null);
        transferResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<WorthBoards.Data.Database.ApplicationDbContext>();
            var updatedOwner = await context.BoardOnUsers.SingleAsync(link => link.BoardId == Seed.BoardId && link.UserId == Seed.OwnerUserId);
            var updatedEditor = await context.BoardOnUsers.SingleAsync(link => link.BoardId == Seed.BoardId && link.UserId == Seed.EditorUserId);
            updatedOwner.UserRole.Should().Be(UserRoleEnum.VIEWER);
            updatedEditor.UserRole.Should().Be(UserRoleEnum.OWNER);
            (await context.BoardOnUsers.AnyAsync(link => link.BoardId == Seed.BoardId && link.UserId == Seed.OutsiderUserId)).Should().BeFalse();
        }
    }

    [Fact]
    public async Task InviteUser_WithOwnerRoleRequest_ReturnsBadRequestAndDoesNotCreateInvitation()
    {
        // Arrange
        var ownerClient = await CreateAuthenticatedClientAsync("owner");

        // Act
        var response = await ownerClient.PostAsJsonAsync($"/api/boards/{Seed.BoardId}/invite", new InvitationRequest
        {
            UserId = Seed.OutsiderUserId,
            Role = UserRoleEnum.OWNER
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await ReadJsonAsync<ErrorDetails>(response);
        error.Should().NotBeNull();
        error!.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
        error.Details.Should().Contain("OWNER");

        await using var scope = Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<WorthBoards.Data.Database.ApplicationDbContext>();
        (await context.Notifications.AnyAsync(notification => notification.SubjectUserId == Seed.OutsiderUserId && notification.NotificationType == NotificationEventTypeEnum.INVITATION)).Should().BeTrue();
    }

    [Fact]
    public async Task GetUsersLinkedToBoard_WithValidBoardId_ReturnsAllLinkedUsers()
    {
        // Arrange
        var ownerClient = await CreateAuthenticatedClientAsync("owner");

        // Act
        var response = await ownerClient.GetAsync($"/api/boards/{Seed.BoardId}/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var users = await ReadJsonAsync<IEnumerable<LinkedUserToBoardResponse>>(response);
        users.Should().NotBeNull();
        users!.Should().NotBeEmpty();
        users.Should().Contain(u => u.UserName == "owner");
        users.Should().Contain(u => u.UserName == "editor");
        users.Should().Contain(u => u.UserName == "viewer");

        await using var scope = Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<WorthBoards.Data.Database.ApplicationDbContext>();
        (await context.BoardOnUsers.CountAsync(link => link.BoardId == Seed.BoardId)).Should().Be(3);
    }

    [Fact]
    public async Task GetUsersLinkedToBoard_WithNonexistentBoardId_ReturnsForbidden()
    {
        // Arrange
        var ownerClient = await CreateAuthenticatedClientAsync("owner");

        // Act
        var response = await ownerClient.GetAsync($"/api/boards/9999/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();

        await using var scope = Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<WorthBoards.Data.Database.ApplicationDbContext>();
        (await context.BoardOnUsers.CountAsync(link => link.BoardId == Seed.BoardId)).Should().Be(3);
    }

    [Fact]
    public async Task InviteUser_WithValidRequest_CreatesNotification()
    {
        // Arrange
        var ownerClient = await CreateAuthenticatedClientAsync("owner");

        // Act
        var response = await ownerClient.PostAsJsonAsync($"/api/boards/{Seed.BoardId}/invite", new InvitationRequest
        {
            UserId = Seed.CandidateUserId,
            Role = UserRoleEnum.EDITOR
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();

        await using var scope = Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<WorthBoards.Data.Database.ApplicationDbContext>();
        var notification = await context.Notifications.FirstOrDefaultAsync(n => 
            n.SubjectUserId == Seed.CandidateUserId && 
            n.NotificationType == NotificationEventTypeEnum.INVITATION);
        notification.Should().NotBeNull();
        notification!.InvitationRole.Should().Be(UserRoleEnum.EDITOR);
    }

    [Fact]
    public async Task RemoveUser_WithValidUserAndBoard_RemovesLink()
    {
        // Arrange
        var ownerClient = await CreateAuthenticatedClientAsync("owner");

        // Act
        var response = await ownerClient.PostAsync($"/api/boards/{Seed.BoardId}/remove/{Seed.ViewerUserId}", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();

        await using var scope = Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<WorthBoards.Data.Database.ApplicationDbContext>();
        var link = await context.BoardOnUsers.FirstOrDefaultAsync(b => 
            b.BoardId == Seed.BoardId && b.UserId == Seed.ViewerUserId);
        link.Should().BeNull();
    }

    [Fact]
    public async Task RemoveUser_TryingToRemoveOwner_ReturnsBadRequest()
    {
        // Arrange
        var ownerClient = await CreateAuthenticatedClientAsync("owner");

        // Act
        var response = await ownerClient.PostAsync($"/api/boards/{Seed.BoardId}/remove/{Seed.OwnerUserId}", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await ReadJsonAsync<ErrorDetails>(response);
        error.Should().NotBeNull();
        error!.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);

        await using var scope = Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<WorthBoards.Data.Database.ApplicationDbContext>();
        (await context.BoardOnUsers.AnyAsync(link => link.BoardId == Seed.BoardId && link.UserId == Seed.OwnerUserId)).Should().BeTrue();
    }
}