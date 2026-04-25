using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Common.Enums;
using WorthBoards.IntegrationTests.Experiment1.TestInfrastructure;
using Xunit;

namespace WorthBoards.IntegrationTests.Experiment1;

public class NotificationControllerIntegrationTests(IntegrationTestWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    [Fact]
    public async Task GetNotificationsByUserId_AuthorizedRequest_ReturnsUserNotifications()
    {
        // Arrange
        var owner = await CreateAuthenticatedUserAsync("notif_owner");
        var invitee = await CreateAuthenticatedUserAsync("notif_invitee");
        var boardId = await CreateBoardAsync(owner.Client, "Notification Board");

        var inviteResponse = await owner.Client.PostAsJsonAsync($"/api/boards/{boardId}/invite", new InvitationRequest
        {
            UserId = invitee.UserId,
            Role = UserRoleEnum.VIEWER
        });
        inviteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Act
        var response = await invitee.Client.GetAsync("/api/notifications");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var responseBody = await response.Content.ReadAsStringAsync();
        responseBody.Should().Contain("Board invitation");
        responseBody.Should().Contain("\"type\":0");

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WorthBoards.Data.Database.ApplicationDbContext>();
        var notificationLinks = await dbContext.NotificationsOnUsers.Where(x => x.UserId == invitee.UserId).ToListAsync();
        notificationLinks.Should().NotBeEmpty();
    }

    [Fact]
    public async Task DeleteNotification_InvalidNotificationId_ReturnsNotFoundAndKeepsLinksUnchanged()
    {
        // Arrange
        var session = await CreateAuthenticatedUserAsync("notif_negative");

        using var arrangeScope = Factory.Services.CreateScope();
        var arrangeDb = arrangeScope.ServiceProvider.GetRequiredService<WorthBoards.Data.Database.ApplicationDbContext>();
        var linksBefore = await arrangeDb.NotificationsOnUsers.CountAsync(n => n.UserId == session.UserId);

        // Act
        var response = await session.Client.DeleteAsync("/api/notifications/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("NotFound");

        using var assertScope = Factory.Services.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<WorthBoards.Data.Database.ApplicationDbContext>();
        var linksAfter = await assertDb.NotificationsOnUsers.CountAsync(n => n.UserId == session.UserId);
        linksAfter.Should().Be(linksBefore);
    }

    [Fact]
    public async Task AcceptInvitation_ValidInvitation_LinksUserToBoard()
    {
        // Arrange
        var owner = await CreateAuthenticatedUserAsync("notif_accept_owner");
        var invitee = await CreateAuthenticatedUserAsync("notif_accept_invitee");
        var boardId = await CreateBoardAsync(owner.Client, "Accept invitation board");

        await owner.Client.PostAsJsonAsync($"/api/boards/{boardId}/invite", new InvitationRequest
        {
            UserId = invitee.UserId,
            Role = UserRoleEnum.VIEWER
        });

        var notifications = await invitee.Client.GetFromJsonAsync<List<NotificationResponse>>("/api/notifications");
        var invitationId = notifications!.Single().Id;

        // Act
        var response = await invitee.Client.PostAsync($"/api/notifications/{invitationId}/accept", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WorthBoards.Data.Database.ApplicationDbContext>();
        var linked = await db.BoardOnUsers.AnyAsync(x => x.BoardId == boardId && x.UserId == invitee.UserId);
        linked.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAllNotifications_ExistingNotifications_UnlinksAllForUser()
    {
        // Arrange
        var owner = await CreateAuthenticatedUserAsync("notif_deleteall_owner");
        var invitee = await CreateAuthenticatedUserAsync("notif_deleteall_invitee");
        var boardId = await CreateBoardAsync(owner.Client, "Delete all board");

        await owner.Client.PostAsJsonAsync($"/api/boards/{boardId}/invite", new InvitationRequest
        {
            UserId = invitee.UserId,
            Role = UserRoleEnum.VIEWER
        });

        // Act
        var response = await invitee.Client.DeleteAsync("/api/notifications");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WorthBoards.Data.Database.ApplicationDbContext>();
        var count = await db.NotificationsOnUsers.CountAsync(x => x.UserId == invitee.UserId);
        count.Should().Be(0);
    }

    [Fact]
    public async Task DeleteAllNotifications_NoNotifications_ReturnsOk()
    {
        // Arrange
        var user = await CreateAuthenticatedUserAsync("notif_deleteall_empty");

        // Act
        var response = await user.Client.DeleteAsync("/api/notifications");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
