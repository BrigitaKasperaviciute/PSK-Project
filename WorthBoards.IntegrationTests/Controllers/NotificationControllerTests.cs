using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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

    private async Task<(int UserId, HttpClient Client)> CreateUserWithClientAsync(string? username = null, string? email = null)
    {
        await using var db = _factory.CreateDbContext();
        var userManager = _factory.Services.CreateScope().ServiceProvider
            .GetRequiredService<UserManager<Data.Identity.ApplicationUser>>();
        var user = await DatabaseSeeder.CreateUserAsync(db, userManager, username, email);
        return (user.Id, _factory.CreateAuthenticatedClient(user.Id, user.UserName!, user.Email!));
    }

    // --- GET /api/notifications ---

    [Fact]
    public async Task GetNotificationsByUserId_WhenAuthenticated_ReturnsOkWithNotifications()
    {
        // Arrange
        var (senderId, _) = await CreateUserWithClientAsync("notif_sender", "notif_sender@test.com");
        var (recipientId, recipientClient) = await CreateUserWithClientAsync("notif_recipient", "notif_recipient@test.com");
        await using var db = _factory.CreateDbContext();
        var board = await DatabaseSeeder.CreateBoardAsync(db, senderId, UserRoleEnum.OWNER);
        await DatabaseSeeder.CreateInvitationNotificationAsync(db, board.Id, senderId, recipientId);

        // Act
        var response = await recipientClient.GetAsync("/api/notifications");

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - response body contains the notification
        var body = await response.Content.ReadFromJsonAsync<IEnumerable<NotificationResponse>>();
        body.Should().NotBeNull();
        body.Should().HaveCountGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task GetNotificationsByUserId_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/notifications");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --- POST /api/notifications/{notificationId}/accept ---

    [Fact]
    public async Task AcceptInvitation_WhenInvitationExists_ReturnsNoContentAndLinksUserToBoard()
    {
        // Arrange
        var (senderId, _) = await CreateUserWithClientAsync("accept_sender", "accept_sender@test.com");
        var (recipientId, recipientClient) = await CreateUserWithClientAsync("accept_recipient", "accept_recipient@test.com");
        await using var db = _factory.CreateDbContext();
        var board = await DatabaseSeeder.CreateBoardAsync(db, senderId, UserRoleEnum.OWNER);
        var (notification, _) = await DatabaseSeeder.CreateInvitationNotificationAsync(
            db, board.Id, senderId, recipientId, UserRoleEnum.VIEWER
        );

        // Act
        var response = await recipientClient.PostAsync($"/api/notifications/{notification.Id}/accept", null);

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert - database state: recipient is now linked to board
        await using var verifyDb = _factory.CreateDbContext();
        var boardLink = await verifyDb.BoardOnUsers.AsNoTracking()
            .SingleOrDefaultAsync(bou => bou.BoardId == board.Id && bou.UserId == recipientId);
        boardLink.Should().NotBeNull();
        boardLink!.UserRole.Should().Be(UserRoleEnum.VIEWER);

        // Assert - invitation notification removed
        var removedNotification = await verifyDb.Notifications.AsNoTracking()
            .SingleOrDefaultAsync(n => n.Id == notification.Id);
        removedNotification.Should().BeNull();
    }

    [Fact]
    public async Task AcceptInvitation_WhenNotificationNotFound_ReturnsNotFound()
    {
        // Arrange
        var (userId, client) = await CreateUserWithClientAsync();

        // Act
        var response = await client.PostAsync("/api/notifications/99999/accept", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AcceptInvitation_WhenCalledByWrongUser_ReturnsUnauthorized()
    {
        // Arrange
        var (senderId, _) = await CreateUserWithClientAsync("accept_wrong_sender", "accept_wrong_sender@test.com");
        var (recipientId, _) = await CreateUserWithClientAsync("accept_wrong_recipient", "accept_wrong_recipient@test.com");
        var (wrongUserId, wrongUserClient) = await CreateUserWithClientAsync("accept_wrong_user", "accept_wrong_user@test.com");
        await using var db = _factory.CreateDbContext();
        var board = await DatabaseSeeder.CreateBoardAsync(db, senderId, UserRoleEnum.OWNER);
        var (notification, _) = await DatabaseSeeder.CreateInvitationNotificationAsync(db, board.Id, senderId, recipientId, UserRoleEnum.VIEWER);

        // Act
        var response = await wrongUserClient.PostAsync($"/api/notifications/{notification.Id}/accept", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Assert - database state unchanged
        await using var verifyDb = _factory.CreateDbContext();
        var boardLink = await verifyDb.BoardOnUsers.AsNoTracking()
            .SingleOrDefaultAsync(bou => bou.BoardId == board.Id && bou.UserId == wrongUserId);
        boardLink.Should().BeNull();
    }

    // --- DELETE /api/notifications/{notificationId} ---

    [Fact]
    public async Task DeleteNotification_WhenNotificationExists_ReturnsOkAndRemovesNotification()
    {
        // Arrange
        var (senderId, _) = await CreateUserWithClientAsync("del_sender", "del_sender@test.com");
        var (recipientId, recipientClient) = await CreateUserWithClientAsync("del_recipient", "del_recipient@test.com");
        await using var db = _factory.CreateDbContext();
        var board = await DatabaseSeeder.CreateBoardAsync(db, senderId, UserRoleEnum.OWNER);
        var (notification, _) = await DatabaseSeeder.CreateInvitationNotificationAsync(db, board.Id, senderId, recipientId);

        // Act
        var response = await recipientClient.DeleteAsync($"/api/notifications/{notification.Id}");

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - database state: notification link removed
        await using var verifyDb = _factory.CreateDbContext();
        var link = await verifyDb.NotificationsOnUsers.AsNoTracking()
            .SingleOrDefaultAsync(nou => nou.NotificationId == notification.Id && nou.UserId == recipientId);
        link.Should().BeNull();
    }

    [Fact]
    public async Task DeleteNotification_WhenNotificationNotFound_ReturnsNotFound()
    {
        // Arrange
        var (userId, client) = await CreateUserWithClientAsync();

        // Act
        var response = await client.DeleteAsync("/api/notifications/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- DELETE /api/notifications ---

    [Fact]
    public async Task DeleteAllNotifications_WhenAuthenticated_ReturnsOkAndRemovesAllNotifications()
    {
        // Arrange
        var (senderId, _) = await CreateUserWithClientAsync("delall_sender", "delall_sender@test.com");
        var (recipientId, recipientClient) = await CreateUserWithClientAsync("delall_recipient", "delall_recipient@test.com");
        await using var db = _factory.CreateDbContext();
        var board = await DatabaseSeeder.CreateBoardAsync(db, senderId, UserRoleEnum.OWNER);
        await DatabaseSeeder.CreateInvitationNotificationAsync(db, board.Id, senderId, recipientId);

        // Act
        var response = await recipientClient.DeleteAsync("/api/notifications");

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - database state: all notification links for recipient removed
        await using var verifyDb = _factory.CreateDbContext();
        var links = await verifyDb.NotificationsOnUsers.AsNoTracking()
            .Where(nou => nou.UserId == recipientId)
            .ToListAsync();
        links.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteAllNotifications_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.DeleteAsync("/api/notifications");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
