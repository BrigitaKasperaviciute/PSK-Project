using Microsoft.EntityFrameworkCore;
using WorthBoards.Domain.Entities;

namespace WorthBoards.IntegrationTests.Controllers;

[Collection(nameof(ApiCollection))]
[Trait("Category", "Integration")]
public sealed class NotificationControllerTests(CustomWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    // ── GET /api/notifications ────────────────────────────────────────────────

    [Fact]
    public async Task GetNotificationsByUserId_WhenAuthenticated_ReturnsOkWithNotifications()
    {
        // Arrange
        var (ownerId, ownerToken) = await RegisterAndLoginAsync();
        SetAuthToken(ownerToken);
        var board = await CreateBoardAsync();

        var (inviteeId, inviteeToken) = await RegisterAndLoginAsync();
        await CreateInvitationNotificationAsync(board.Id, inviteeId, ownerId);

        SetAuthToken(inviteeToken);

        // Act
        var response = await Client.GetAsync("/api/notifications");

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<List<NotificationResponse>>();
        body.Should().NotBeNull();
        body!.Should().HaveCountGreaterThanOrEqualTo(1);
        body.Should().Contain(n => n.Type == NotificationEventTypeEnum.INVITATION);
    }

    [Fact]
    public async Task GetNotificationsByUserId_WhenUnauthenticated_ReturnsUnauthorized()
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
    public async Task AcceptInvitation_WithValidInvitation_ReturnsNoContentAndCreatesLink()
    {
        // Arrange
        var (ownerId, ownerToken) = await RegisterAndLoginAsync();
        SetAuthToken(ownerToken);
        var board = await CreateBoardAsync();

        var (inviteeId, inviteeToken) = await RegisterAndLoginAsync();
        var notificationId = await CreateInvitationNotificationAsync(
            board.Id, inviteeId, ownerId, UserRoleEnum.EDITOR);

        SetAuthToken(inviteeToken);

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/api/notifications/{notificationId}/accept", new { });

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert – invitee is now linked to the board
        await using var db = CreateDbContext();
        var link = await db.BoardOnUsers.AsNoTracking()
            .SingleOrDefaultAsync(bou => bou.BoardId == board.Id && bou.UserId == inviteeId);
        link.Should().NotBeNull();
        link!.UserRole.Should().Be(UserRoleEnum.EDITOR);

        // Assert – invitation notification is deleted
        var invitation = await db.Notifications.AsNoTracking()
            .SingleOrDefaultAsync(n => n.Id == notificationId);
        invitation.Should().BeNull();
    }

    [Fact]
    public async Task AcceptInvitation_WithNonexistentNotificationId_ReturnsNotFound()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);

        // Act
        var response = await Client.PostAsJsonAsync("/api/notifications/999999/accept", new { });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AcceptInvitation_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        ClearAuthToken();

        // Act
        var response = await Client.PostAsJsonAsync("/api/notifications/1/accept", new { });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── DELETE /api/notifications/{notificationId} ────────────────────────────

    [Fact]
    public async Task DeleteNotification_WithExistingNotification_ReturnsOkAndRemovesLink()
    {
        // Arrange
        var (ownerId, ownerToken) = await RegisterAndLoginAsync();
        SetAuthToken(ownerToken);
        var board = await CreateBoardAsync();

        var (inviteeId, inviteeToken) = await RegisterAndLoginAsync();
        var notificationId = await CreateInvitationNotificationAsync(
            board.Id, inviteeId, ownerId);

        SetAuthToken(inviteeToken);

        // Act
        var response = await Client.DeleteAsync($"/api/notifications/{notificationId}");

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert – NotificationOnUser link removed
        await using var db = CreateDbContext();
        var link = await db.NotificationsOnUsers.AsNoTracking()
            .SingleOrDefaultAsync(nou => nou.NotificationId == notificationId
                                      && nou.UserId == inviteeId);
        link.Should().BeNull();
    }

    [Fact]
    public async Task DeleteNotification_WithNotFoundNotification_ReturnsNotFound()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);

        // Act
        var response = await Client.DeleteAsync("/api/notifications/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── DELETE /api/notifications (all) ───────────────────────────────────────

    [Fact]
    public async Task DeleteAllNotifications_WhenAuthenticated_ReturnsOkAndClearsAll()
    {
        // Arrange
        var (ownerId, ownerToken) = await RegisterAndLoginAsync();
        SetAuthToken(ownerToken);
        var board = await CreateBoardAsync();

        var (inviteeId, inviteeToken) = await RegisterAndLoginAsync();

        // Create two notifications for the invitee
        await CreateInvitationNotificationAsync(board.Id, inviteeId, ownerId);

        var board2 = await CreateBoardAsync("Board 2");
        await CreateInvitationNotificationAsync(board2.Id, inviteeId, ownerId);

        SetAuthToken(inviteeToken);

        // Act
        var response = await Client.DeleteAsync("/api/notifications");

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert – all NotificationOnUser links for invitee removed
        await using var db = CreateDbContext();
        var remaining = await db.NotificationsOnUsers.AsNoTracking()
            .Where(nou => nou.UserId == inviteeId)
            .ToListAsync();
        remaining.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteAllNotifications_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        ClearAuthToken();

        // Act
        var response = await Client.DeleteAsync("/api/notifications");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
