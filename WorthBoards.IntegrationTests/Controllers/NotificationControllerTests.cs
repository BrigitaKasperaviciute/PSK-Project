using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Common.Enums;
using WorthBoards.IntegrationTests.Infrastructure;

namespace WorthBoards.IntegrationTests.Controllers;

public class NotificationControllerTests(CustomWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    // ── GET /api/notifications ────────────────────────────────────────────────

    [Fact]
    public async Task GetNotificationsByUserId_AuthenticatedUser_ReturnsOkWithNotifications()
    {
        // Arrange
        var (_, ownerToken) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(ownerToken);

        var (inviteeId, inviteeToken) = await RegisterAndLoginAsync();
        await InviteUserAndGetNotificationAsync(board.Id, inviteeId, UserRoleEnum.VIEWER, ownerToken);

        SetAuthToken(inviteeToken);

        // Act
        var response = await Client.GetAsync("/api/notifications");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<IEnumerable<NotificationResponse>>();
        body.Should().NotBeNull();
        body!.Should().Contain(n => n.BoardId == board.Id);
    }

    [Fact]
    public async Task GetNotificationsByUserId_UnauthenticatedUser_ReturnsUnauthorized()
    {
        // Arrange
        ClearAuthToken();

        // Act
        var response = await Client.GetAsync("/api/notifications");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── POST /api/notifications/{notificationId}/accept ───────────────────────

    [Fact]
    public async Task AcceptInvitation_ValidInvitation_ReturnsNoContentAndLinksBoardToUser()
    {
        // Arrange
        var (_, ownerToken) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(ownerToken);

        var (inviteeId, inviteeToken) = await RegisterAndLoginAsync();
        var notificationId = await InviteUserAndGetNotificationAsync(
            board.Id, inviteeId, UserRoleEnum.VIEWER, ownerToken);

        SetAuthToken(inviteeToken);

        // Act
        var response = await Client.PostAsync(
            $"/api/notifications/{notificationId}/accept", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var boardLink = await QueryAsync(db =>
            db.BoardOnUsers.FirstOrDefaultAsync(b => b.BoardId == board.Id && b.UserId == inviteeId));
        boardLink.Should().NotBeNull();
        boardLink!.UserRole.Should().Be(UserRoleEnum.VIEWER);
    }

    [Fact]
    public async Task AcceptInvitation_UnauthenticatedUser_ReturnsUnauthorized()
    {
        // Arrange
        ClearAuthToken();

        // Act
        var response = await Client.PostAsync("/api/notifications/1/accept", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── DELETE /api/notifications/{notificationId} ────────────────────────────

    [Fact]
    public async Task DeleteNotification_AuthenticatedUser_ReturnsOkAndUnlinksNotification()
    {
        // Arrange
        var (_, ownerToken) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(ownerToken);

        var (inviteeId, inviteeToken) = await RegisterAndLoginAsync();
        var notificationId = await InviteUserAndGetNotificationAsync(
            board.Id, inviteeId, UserRoleEnum.VIEWER, ownerToken);

        SetAuthToken(inviteeToken);

        // Act
        var response = await Client.DeleteAsync($"/api/notifications/{notificationId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var link = await QueryAsync(db =>
            db.NotificationsOnUsers.FirstOrDefaultAsync(
                n => n.NotificationId == notificationId && n.UserId == inviteeId));
        link.Should().BeNull();
    }

    [Fact]
    public async Task DeleteNotification_UnauthenticatedUser_ReturnsUnauthorized()
    {
        // Arrange
        ClearAuthToken();

        // Act
        var response = await Client.DeleteAsync("/api/notifications/1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── DELETE /api/notifications ─────────────────────────────────────────────

    [Fact]
    public async Task DeleteAllNotifications_AuthenticatedUser_ReturnsOkAndRemovesAllLinks()
    {
        // Arrange
        var (_, ownerToken) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(ownerToken);

        var (inviteeId, inviteeToken) = await RegisterAndLoginAsync();
        await InviteUserAndGetNotificationAsync(board.Id, inviteeId, UserRoleEnum.VIEWER, ownerToken);

        SetAuthToken(inviteeToken);

        // Act
        var response = await Client.DeleteAsync("/api/notifications");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var remaining = await QueryAsync(db =>
            db.NotificationsOnUsers
                .Where(n => n.UserId == inviteeId)
                .ToListAsync());
        remaining.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteAllNotifications_UnauthenticatedUser_ReturnsUnauthorized()
    {
        // Arrange
        ClearAuthToken();

        // Act
        var response = await Client.DeleteAsync("/api/notifications");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
