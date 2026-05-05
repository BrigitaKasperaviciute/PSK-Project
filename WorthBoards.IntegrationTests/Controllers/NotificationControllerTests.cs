using Microsoft.EntityFrameworkCore;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Common.Enums;
using WorthBoards.IntegrationTests.Infrastructure;

namespace WorthBoards.IntegrationTests.Controllers;

public sealed class NotificationControllerTests(CustomWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    // ── GetNotificationsByUserId ──────────────────────────────────────────────

    [Fact]
    public async Task GetNotificationsByUserId_AfterTaskCreated_ReturnsOkWithNotification()
    {
        // Arrange – owner creates a board, a second member joins, then owner creates a task
        var (_, ownerToken) = await RegisterAndLoginAsync();
        SetAuthToken(ownerToken);
        var board = await CreateBoardAsync();

        var (memberId, memberToken) = await RegisterAndLoginAsync();
        await LinkUserToBoardDirectlyAsync(board.Id, memberId, UserRoleEnum.VIEWER);

        // Owner creates a task — triggers TASK_CREATED notification for all board members
        await CreateTaskAsync(board.Id, "Notification Task");

        // Switch to member and fetch notifications
        SetAuthToken(memberToken);

        // Act
        var response = await Client.GetAsync("/api/notifications");

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert – response body contains at least one notification
        var body = await response.Content.ReadFromJsonAsync<IEnumerable<NotificationResponse>>();
        body.Should().NotBeNull();
        body!.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetNotificationsByUserId_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        ClearAuthToken();

        // Act
        var response = await Client.GetAsync("/api/notifications");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── AcceptInvitation ──────────────────────────────────────────────────────

    [Fact]
    public async Task AcceptInvitation_WithValidInvitation_ReturnsNoContentAndLinksUserToBoard()
    {
        // Arrange – owner invites a second user
        var (_, ownerToken) = await RegisterAndLoginAsync();
        SetAuthToken(ownerToken);
        var board = await CreateBoardAsync();

        var (inviteeId, inviteeToken) = await RegisterAndLoginAsync();
        var (notificationId, _) = await InviteUserAndGetNotificationAsync(board.Id, inviteeId, UserRoleEnum.EDITOR);

        // Switch to invitee
        SetAuthToken(inviteeToken);

        // Act
        var response = await Client.PostAsync($"/api/notifications/{notificationId}/accept", null);

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert – database state: user is now linked to the board
        await using var db = Factory.CreateDbContext();
        var link = await db.BoardOnUsers.AsNoTracking()
            .SingleOrDefaultAsync(bou => bou.BoardId == board.Id && bou.UserId == inviteeId);
        link.Should().NotBeNull();
        link!.UserRole.Should().Be(UserRoleEnum.EDITOR);
    }

    [Fact]
    public async Task AcceptInvitation_WithNonExistentNotification_ReturnsNotFound()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);

        // Act
        var response = await Client.PostAsync("/api/notifications/99999/accept", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── DeleteNotification ────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteNotification_WithOwnedNotification_ReturnsOkAndRemovesNotification()
    {
        // Arrange – owner creates a task to trigger a notification for member
        var (_, ownerToken) = await RegisterAndLoginAsync();
        SetAuthToken(ownerToken);
        var board = await CreateBoardAsync();

        var (memberId, memberToken) = await RegisterAndLoginAsync();
        await LinkUserToBoardDirectlyAsync(board.Id, memberId, UserRoleEnum.VIEWER);
        await CreateTaskAsync(board.Id);

        SetAuthToken(memberToken);
        var notificationsResponse = await Client.GetAsync("/api/notifications");
        var notifications = await notificationsResponse.Content.ReadFromJsonAsync<IEnumerable<NotificationResponse>>();
        var notificationId = notifications!.First().Id;

        // Act
        var response = await Client.DeleteAsync($"/api/notifications/{notificationId}");

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert – database state: the notification-on-user link is removed
        await using var db = Factory.CreateDbContext();
        var notificationOnUser = await db.NotificationsOnUsers.AsNoTracking()
            .SingleOrDefaultAsync(nou => nou.NotificationId == notificationId && nou.UserId == memberId);
        notificationOnUser.Should().BeNull();
    }

    [Fact]
    public async Task DeleteNotification_WithNonExistentNotification_ReturnsNotFound()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);

        // Act
        var response = await Client.DeleteAsync("/api/notifications/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── DeleteAllNotifications ────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAllNotifications_WhenUserHasNotifications_ReturnsOkAndClearsAll()
    {
        // Arrange – member receives two notifications (one per task creation)
        var (_, ownerToken) = await RegisterAndLoginAsync();
        SetAuthToken(ownerToken);
        var board = await CreateBoardAsync();

        var (memberId, memberToken) = await RegisterAndLoginAsync();
        await LinkUserToBoardDirectlyAsync(board.Id, memberId, UserRoleEnum.VIEWER);
        await CreateTaskAsync(board.Id, "Task Alpha");
        await CreateTaskAsync(board.Id, "Task Beta");

        SetAuthToken(memberToken);

        // Act
        var response = await Client.DeleteAsync("/api/notifications");

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert – database state: no notification links remain for the member
        await using var db = Factory.CreateDbContext();
        var remaining = await db.NotificationsOnUsers.AsNoTracking()
            .Where(nou => nou.UserId == memberId)
            .ToListAsync();
        remaining.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteAllNotifications_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        ClearAuthToken();

        // Act
        var response = await Client.DeleteAsync("/api/notifications");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
