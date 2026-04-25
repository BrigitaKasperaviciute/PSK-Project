using System.Net;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Common.Enums;
using WorthBoards.IntegrationTests.Experiment1.TestInfrastructure;
using Xunit;

namespace WorthBoards.IntegrationTests.Experiment1;

public class BoardOnUserControllerIntegrationTests(IntegrationTestWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    [Fact]
    public async Task InviteUser_OwnerRequest_CreatesInvitationNotification()
    {
        // Arrange
        var owner = await CreateAuthenticatedUserAsync("invite_owner");
        var invitee = await CreateAuthenticatedUserAsync("invite_target");
        var boardId = await CreateBoardAsync(owner.Client, "Invitation Board");

        var request = new InvitationRequest
        {
            UserId = invitee.UserId,
            Role = UserRoleEnum.VIEWER
        };

        // Act
        var response = await owner.Client.PostAsJsonAsync($"/api/boards/{boardId}/invite", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var bodyText = await response.Content.ReadAsStringAsync();
        bodyText.Should().BeEmpty();

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WorthBoards.Data.Database.ApplicationDbContext>();
        var invitation = await dbContext.Notifications.SingleAsync(n =>
            n.BoardId == boardId &&
            n.NotificationType == NotificationEventTypeEnum.INVITATION &&
            n.SubjectUserId == invitee.UserId);

        invitation.InvitationRole.Should().Be(UserRoleEnum.VIEWER);

        var notificationLink = await dbContext.NotificationsOnUsers.SingleAsync(nou =>
            nou.NotificationId == invitation.Id && nou.UserId == invitee.UserId);
        notificationLink.Should().NotBeNull();
    }

    [Fact]
    public async Task InviteUser_OwnerRoleRequested_ReturnsBadRequestAndDoesNotCreateNotification()
    {
        // Arrange
        var owner = await CreateAuthenticatedUserAsync("invite_negative_owner");
        var invitee = await CreateAuthenticatedUserAsync("invite_negative_target");
        var boardId = await CreateBoardAsync(owner.Client, "Invitation Validation Board");

        using var arrangeScope = Factory.Services.CreateScope();
        var arrangeDb = arrangeScope.ServiceProvider.GetRequiredService<WorthBoards.Data.Database.ApplicationDbContext>();
        var notificationsBefore = await arrangeDb.Notifications.CountAsync();

        var request = new InvitationRequest
        {
            UserId = invitee.UserId,
            Role = UserRoleEnum.OWNER
        };

        // Act
        var response = await owner.Client.PostAsJsonAsync($"/api/boards/{boardId}/invite", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Cannot invite user as OWNER");

        using var assertScope = Factory.Services.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<WorthBoards.Data.Database.ApplicationDbContext>();
        var notificationsAfter = await assertDb.Notifications.CountAsync();
        notificationsAfter.Should().Be(notificationsBefore);
    }

    [Fact]
    public async Task GetAllBoardToUserLinks_LinkedUsersExist_ReturnsLinks()
    {
        // Arrange
        var owner = await CreateAuthenticatedUserAsync("links_owner");
        var user = await CreateAuthenticatedUserAsync("links_user");
        var boardId = await CreateBoardAsync(owner.Client, "Links Board");
        await owner.Client.PostAsJsonAsync($"/api/boards/{boardId}/link/{user.UserId}", new LinkUserToBoardRequest { UserRole = UserRoleEnum.VIEWER });

        // Act
        var response = await owner.Client.GetAsync($"/api/boards/{boardId}/links");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var links = await response.Content.ReadFromJsonAsync<List<LinkUserToBoardResponse>>();
        links.Should().NotBeNull();
        links!.Count.Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task UpdateUserOnBoard_ValidRequest_ChangesUserRole()
    {
        // Arrange
        var owner = await CreateAuthenticatedUserAsync("update_role_owner");
        var user = await CreateAuthenticatedUserAsync("update_role_user");
        var boardId = await CreateBoardAsync(owner.Client, "Update Role Board");
        await owner.Client.PostAsJsonAsync($"/api/boards/{boardId}/link/{user.UserId}", new LinkUserToBoardRequest { UserRole = UserRoleEnum.VIEWER });

        // Act
        var response = await owner.Client.PutAsJsonAsync($"/api/boards/{boardId}/link/{user.UserId}", new LinkUserToBoardRequest
        {
            UserRole = UserRoleEnum.EDITOR
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WorthBoards.Data.Database.ApplicationDbContext>();
        var link = await db.BoardOnUsers.SingleAsync(x => x.BoardId == boardId && x.UserId == user.UserId);
        link.UserRole.Should().Be(UserRoleEnum.EDITOR);
    }

    [Fact]
    public async Task PatchUserOnBoard_ValidPatch_ChangesUserRole()
    {
        // Arrange
        var owner = await CreateAuthenticatedUserAsync("patch_role_owner");
        var user = await CreateAuthenticatedUserAsync("patch_role_user");
        var boardId = await CreateBoardAsync(owner.Client, "Patch Role Board");
        await owner.Client.PostAsJsonAsync($"/api/boards/{boardId}/link/{user.UserId}", new LinkUserToBoardRequest { UserRole = UserRoleEnum.VIEWER });

        var patch = "[{\"op\":\"replace\",\"path\":\"/userRole\",\"value\":1}]";
        using var content = new StringContent(patch, Encoding.UTF8, "application/json-patch+json");

        // Act
        var response = await owner.Client.PatchAsync($"/api/boards/{boardId}/link/{user.UserId}", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WorthBoards.Data.Database.ApplicationDbContext>();
        var link = await db.BoardOnUsers.SingleAsync(x => x.BoardId == boardId && x.UserId == user.UserId);
        link.UserRole.Should().Be(UserRoleEnum.EDITOR);
    }

    [Fact]
    public async Task RemoveUser_OwnerRequest_UnlinksUserFromBoard()
    {
        // Arrange
        var owner = await CreateAuthenticatedUserAsync("remove_owner");
        var user = await CreateAuthenticatedUserAsync("remove_user");
        var boardId = await CreateBoardAsync(owner.Client, "Remove User Board");
        await owner.Client.PostAsJsonAsync($"/api/boards/{boardId}/link/{user.UserId}", new LinkUserToBoardRequest { UserRole = UserRoleEnum.VIEWER });

        // Act
        var response = await owner.Client.PostAsync($"/api/boards/{boardId}/remove/{user.UserId}", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WorthBoards.Data.Database.ApplicationDbContext>();
        var exists = await db.BoardOnUsers.AnyAsync(x => x.BoardId == boardId && x.UserId == user.UserId);
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task TransferOwnership_ValidTarget_ChangesOwnerRole()
    {
        // Arrange
        var owner = await CreateAuthenticatedUserAsync("owner_transfer");
        var user = await CreateAuthenticatedUserAsync("new_owner");
        var boardId = await CreateBoardAsync(owner.Client, "Ownership Board");
        await owner.Client.PostAsJsonAsync($"/api/boards/{boardId}/link/{user.UserId}", new LinkUserToBoardRequest { UserRole = UserRoleEnum.EDITOR });

        // Act
        var response = await owner.Client.PostAsync($"/api/boards/{boardId}/collaborators/{user.UserId}", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WorthBoards.Data.Database.ApplicationDbContext>();
        var oldOwnerLink = await db.BoardOnUsers.SingleAsync(x => x.BoardId == boardId && x.UserId == owner.UserId);
        var newOwnerLink = await db.BoardOnUsers.SingleAsync(x => x.BoardId == boardId && x.UserId == user.UserId);
        oldOwnerLink.UserRole.Should().Be(UserRoleEnum.VIEWER);
        newOwnerLink.UserRole.Should().Be(UserRoleEnum.OWNER);
    }

    [Fact]
    public async Task GetBoardToUserLink_ExistingLink_ReturnsLink()
    {
        // Arrange
        var owner = await CreateAuthenticatedUserAsync("getlink_owner");
        var user = await CreateAuthenticatedUserAsync("getlink_user");
        var boardId = await CreateBoardAsync(owner.Client, "Get Link Board");
        await owner.Client.PostAsJsonAsync($"/api/boards/{boardId}/link/{user.UserId}", new LinkUserToBoardRequest { UserRole = UserRoleEnum.VIEWER });

        // Act
        var response = await owner.Client.GetAsync($"/api/boards/{boardId}/link/{user.UserId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var link = await response.Content.ReadFromJsonAsync<LinkUserToBoardResponse>();
        link.Should().NotBeNull();
        link!.UserId.Should().Be(user.UserId);
    }

    [Fact]
    public async Task GetUsersLinkedToBoard_LinkedUsersExist_ReturnsUsers()
    {
        // Arrange
        var owner = await CreateAuthenticatedUserAsync("boardusers_owner");
        var user = await CreateAuthenticatedUserAsync("boardusers_user");
        var boardId = await CreateBoardAsync(owner.Client, "Users Board");
        await owner.Client.PostAsJsonAsync($"/api/boards/{boardId}/link/{user.UserId}", new LinkUserToBoardRequest { UserRole = UserRoleEnum.VIEWER });

        // Act
        var response = await owner.Client.GetAsync($"/api/boards/{boardId}/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain(user.UserName);
    }

    [Fact]
    public async Task GetUsersByUserName_ValidQuery_CurrentlyReturnsInternalServerError()
    {
        // Arrange
        var owner = await CreateAuthenticatedUserAsync("search_owner");
        var candidate = await CreateAuthenticatedUserAsync("search_target");
        var boardId = await CreateBoardAsync(owner.Client, "Collaborator Board");

        // Act
        var response = await owner.Client.GetAsync($"/api/boards/{boardId}/collaborators?userName={candidate.UserName}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().ContainEquivalentOf("error");
    }
}
