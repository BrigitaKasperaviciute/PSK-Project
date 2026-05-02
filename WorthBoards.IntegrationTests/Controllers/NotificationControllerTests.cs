using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Common.Enums;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.IntegrationTests.Infrastructure;

namespace WorthBoards.IntegrationTests.Controllers;

public class NotificationControllerTests(CustomWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    // ── GET /api/notifications ───────────────────────────────────────────────

    [Fact]
    public async Task GetNotificationsByUserId_AuthenticatedUser_ReturnsNotifications()
    {
        // Arrange
        var (_, _, client) = await RegisterAndLoginAsync();

        // Act
        var response = await client.GetAsync("/api/notifications");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var notifications = await response.Content.ReadFromJsonAsync<IEnumerable<NotificationResponse>>();
        notifications.Should().NotBeNull();
    }

    [Fact]
    public async Task GetNotificationsByUserId_Unauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var client = Factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/notifications");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── POST /api/notifications/{notificationId}/accept ──────────────────────

    [Fact]
    public async Task AcceptInvitation_ValidInvitation_ReturnsNoContentAndLinksUser()
    {
        // Arrange
        var (_, _, ownerClient) = await RegisterAndLoginAsync();
        var (inviteeId, _, inviteeClient) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(ownerClient);

        // Owner sends an invitation via the invite endpoint
        var inviteRequest = new InvitationRequest { UserId = inviteeId, Role = UserRoleEnum.EDITOR };
        var inviteResponse = await ownerClient.PostAsJsonAsync(
            $"/api/boards/{board.Id}/invite", inviteRequest);
        inviteResponse.EnsureSuccessStatusCode();

        // Fetch the notification for the invitee
        var notificationsResponse = await inviteeClient.GetAsync("/api/notifications");
        var notifications = (await notificationsResponse.Content
            .ReadFromJsonAsync<IEnumerable<NotificationResponse>>())!.ToList();
        var invitation = notifications.First();

        // Act
        var response = await inviteeClient.PostAsync(
            $"/api/notifications/{invitation.Id}/accept", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var db = GetDbContext();
        db.BoardOnUsers.Any(bou => bou.BoardId == board.Id && bou.UserId == inviteeId).Should().BeTrue();
    }

    [Fact]
    public async Task AcceptInvitation_NonExistentNotification_ReturnsNotFound()
    {
        // Arrange
        var (_, _, client) = await RegisterAndLoginAsync();

        // Act
        var response = await client.PostAsync("/api/notifications/999999/accept", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── DELETE /api/notifications/{notificationId} ───────────────────────────

    [Fact]
    public async Task DeleteNotification_ExistingNotification_ReturnsOkAndUnlinksIt()
    {
        // Arrange
        var (_, _, ownerClient) = await RegisterAndLoginAsync();
        var (inviteeId, _, inviteeClient) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(ownerClient);

        // Send invitation so invitee gets a notification
        await ownerClient.PostAsJsonAsync(
            $"/api/boards/{board.Id}/invite",
            new InvitationRequest { UserId = inviteeId, Role = UserRoleEnum.VIEWER });

        var notificationsResponse = await inviteeClient.GetAsync("/api/notifications");
        var notification = (await notificationsResponse.Content
            .ReadFromJsonAsync<IEnumerable<NotificationResponse>>())!.First();

        // Act
        var response = await inviteeClient.DeleteAsync($"/api/notifications/{notification.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var db = GetDbContext();
        db.NotificationsOnUsers
            .Any(nou => nou.NotificationId == notification.Id && nou.UserId == inviteeId)
            .Should().BeFalse();
    }

    [Fact]
    public async Task DeleteNotification_NotificationNotBelongingToUser_ReturnsNotFound()
    {
        // Arrange
        var (_, _, client) = await RegisterAndLoginAsync();

        // Act — notification id that doesn't belong to this user
        var response = await client.DeleteAsync("/api/notifications/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── DELETE /api/notifications ────────────────────────────────────────────

    [Fact]
    public async Task DeleteAllNotifications_AuthenticatedUser_ReturnsOkAndClearsAll()
    {
        // Arrange
        var (_, _, ownerClient) = await RegisterAndLoginAsync();
        var (inviteeId, _, inviteeClient) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(ownerClient);

        // Create a task so the owner (invitee here as another user) gets task-created notifications
        // Instead: invite the user so they get a notification
        await ownerClient.PostAsJsonAsync(
            $"/api/boards/{board.Id}/invite",
            new InvitationRequest { UserId = inviteeId, Role = UserRoleEnum.VIEWER });

        // Act
        var response = await inviteeClient.DeleteAsync("/api/notifications");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var notificationsResponse = await inviteeClient.GetAsync("/api/notifications");
        var remaining = (await notificationsResponse.Content
            .ReadFromJsonAsync<IEnumerable<NotificationResponse>>())!;
        remaining.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteAllNotifications_Unauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var client = Factory.CreateClient();

        // Act
        var response = await client.DeleteAsync("/api/notifications");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
