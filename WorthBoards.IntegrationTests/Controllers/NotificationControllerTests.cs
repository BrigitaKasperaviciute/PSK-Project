using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Common.Enums;
using WorthBoards.IntegrationTests.Infrastructure;
using Xunit;

namespace WorthBoards.IntegrationTests.Controllers;

[Collection(nameof(ApiCollection))]
[Trait("Category", "Integration")]
public sealed class NotificationControllerTests : IAsyncLifetime
{
    private readonly ApiFactory _factory;

    public NotificationControllerTests(ApiFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        await using var db = _factory.CreateDbContext();
        await DatabaseSeeder.CleanDatabaseAsync(db);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ---------------------------------------------------------------------------
    // GET /api/notifications  [Authorize]
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetNotificationsByUserId_WhenAuthenticated_ReturnsUsersNotifications()
    {
        // Arrange - two users; recipient has two notifications, sender has none
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var sender = await seeder.CreateUserAsync();
        var recipient = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync();
        await seeder.LinkUserToBoardAsync(board.Id, sender.Id, UserRoleEnum.OWNER);
        var task = await seeder.CreateBoardTaskAsync(board.Id, "Notify Task");

        await seeder.CreateNotificationForUserAsync(board.Id, sender.Id, recipient.Id,
            NotificationEventTypeEnum.TASK_CREATED, taskId: task.Id);
        await seeder.CreateNotificationForUserAsync(board.Id, sender.Id, recipient.Id,
            NotificationEventTypeEnum.TASK_ASSIGNED, taskId: task.Id, subjectUserId: recipient.Id);

        var client = _factory.CreateAuthenticatedClient(recipient.Id, recipient.UserName!, recipient.Email!);

        // Act
        var response = await client.GetAsync("/api/notifications");

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - recipient sees exactly their two notifications
        var notifications = await response.Content.ReadFromJsonAsync<IEnumerable<NotificationResponse>>();
        notifications.Should().NotBeNull();
        notifications!.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetNotificationsByUserId_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/notifications");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------------------------------------------------------------------------
    // DELETE /api/notifications/{notificationId}  [Authorize]
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task DeleteNotification_WhenAuthenticated_ReturnsOkAndUnlinksNotification()
    {
        // Arrange
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var sender = await seeder.CreateUserAsync();
        var recipient = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync();
        await seeder.LinkUserToBoardAsync(board.Id, sender.Id, UserRoleEnum.OWNER);

        var notification = await seeder.CreateNotificationForUserAsync(
            board.Id, sender.Id, recipient.Id, NotificationEventTypeEnum.TASK_CREATED);

        var client = _factory.CreateAuthenticatedClient(recipient.Id, recipient.UserName!, recipient.Email!);

        // Act
        var response = await client.DeleteAsync($"/api/notifications/{notification.Id}");

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - NotificationOnUser link is removed
        await using var db = _factory.CreateDbContext();
        var link = await db.NotificationsOnUsers.AsNoTracking()
            .SingleOrDefaultAsync(n => n.NotificationId == notification.Id && n.UserId == recipient.Id);
        link.Should().BeNull();
    }

    // ---------------------------------------------------------------------------
    // DELETE /api/notifications  [Authorize]
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task DeleteAllNotifications_WhenAuthenticated_ReturnsOkAndClearsAllLinks()
    {
        // Arrange
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var sender = await seeder.CreateUserAsync();
        var recipient = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync();
        await seeder.LinkUserToBoardAsync(board.Id, sender.Id, UserRoleEnum.OWNER);

        await seeder.CreateNotificationForUserAsync(board.Id, sender.Id, recipient.Id, NotificationEventTypeEnum.TASK_CREATED);
        await seeder.CreateNotificationForUserAsync(board.Id, sender.Id, recipient.Id, NotificationEventTypeEnum.TASK_ASSIGNED);

        var client = _factory.CreateAuthenticatedClient(recipient.Id, recipient.UserName!, recipient.Email!);

        // Act
        var response = await client.DeleteAsync("/api/notifications");

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - all NotificationOnUser links for recipient are removed
        await using var db = _factory.CreateDbContext();
        var remaining = await db.NotificationsOnUsers.AsNoTracking()
            .CountAsync(n => n.UserId == recipient.Id);
        remaining.Should().Be(0);
    }

    [Fact]
    public async Task DeleteAllNotifications_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.DeleteAsync("/api/notifications");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------------------------------------------------------------------------
    // DELETE /api/notifications/{notificationId} — negative
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task DeleteNotification_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.DeleteAsync("/api/notifications/1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------------------------------------------------------------------------
    // POST /api/notifications/{notificationId}/accept  [Authorize]
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task AcceptInvitation_AsInvitee_ReturnsNoContentAndCreatesBoardMembership()
    {
        // Arrange - owner invites an outside user
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var invitee = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync();
        await seeder.LinkUserToBoardAsync(board.Id, owner.Id, UserRoleEnum.OWNER);

        // Create an INVITATION notification directly (NotificationEventTypeEnum.INVITATION)
        var invitation = await seeder.CreateInvitationNotificationAsync(
            board.Id, owner.Id, invitee.Id, UserRoleEnum.EDITOR);

        var client = _factory.CreateAuthenticatedClient(invitee.Id, invitee.UserName!, invitee.Email!);

        // Act
        var response = await client.PostAsync($"/api/notifications/{invitation.Id}/accept", null);

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert - BoardOnUser entry was created for invitee
        await using var db = _factory.CreateDbContext();
        var membership = await db.BoardOnUsers.AsNoTracking()
            .SingleOrDefaultAsync(b => b.BoardId == board.Id && b.UserId == invitee.Id);
        membership.Should().NotBeNull();
        membership!.UserRole.Should().Be(UserRoleEnum.EDITOR);

        // Assert - invitation notification was removed
        var deletedNotification = await db.Notifications.AsNoTracking()
            .SingleOrDefaultAsync(n => n.Id == invitation.Id);
        deletedNotification.Should().BeNull();
    }

    [Fact]
    public async Task AcceptInvitation_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsync("/api/notifications/1/accept", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AcceptInvitation_WithNonInvitationNotification_ReturnsNotFound()
    {
        // Arrange - create a non-invitation notification (TASK_CREATED type)
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var sender = await seeder.CreateUserAsync();
        var recipient = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync();
        await seeder.LinkUserToBoardAsync(board.Id, sender.Id, UserRoleEnum.OWNER);

        var taskNotification = await seeder.CreateNotificationForUserAsync(
            board.Id, sender.Id, recipient.Id, NotificationEventTypeEnum.TASK_CREATED);

        var client = _factory.CreateAuthenticatedClient(recipient.Id, recipient.UserName!, recipient.Email!);

        // Act - try to accept a non-invitation notification as if it were an invitation
        var response = await client.PostAsync($"/api/notifications/{taskNotification.Id}/accept", null);

        // Assert - service rejects non-INVITATION types with 404
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
