using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Common.Enums;
using WorthBoards.Data.Database;
using WorthBoards.IntegrationTests.Infrastructure;
using Xunit;

namespace WorthBoards.IntegrationTests;

public class NotificationControllerTests(WorthBoardsWebApplicationFactory factory)
    : IClassFixture<WorthBoardsWebApplicationFactory>
{
    private HttpClient AuthorizedClient(int userId, string userName, string email)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTokenHelper.GenerateToken(userId, userName, email));
        return client;
    }

    // ── GET notifications ────────────────────────────────────────────────────

    [Fact]
    public async Task GetNotificationsByUserId_Authenticated_ReturnsOkWithList()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var recipient = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync(owner);
        await seeder.CreateGenericNotificationAsync(board, owner, recipient);
        var client = AuthorizedClient(recipient.Id, recipient.UserName!, recipient.Email!);

        // Act
        var response = await client.GetAsync("/api/notifications");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IEnumerable<NotificationResponse>>();
        body.Should().NotBeNull();
        body.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetNotificationsByUserId_Unauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/notifications");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── POST accept invitation ───────────────────────────────────────────────

    [Fact]
    public async Task AcceptInvitation_ValidInvitation_ReturnsNoContent()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var invitee = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync(owner);
        var (notification, _) = await seeder.CreateInvitationAsync(board, owner, invitee, UserRoleEnum.VIEWER);
        var client = AuthorizedClient(invitee.Id, invitee.UserName!, invitee.Email!);

        // Act
        var response = await client.PostAsync($"/api/notifications/{notification.Id}/accept", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var dbScope = factory.Services.CreateScope();
        var db = dbScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.BoardOnUsers.Any(l => l.BoardId == board.Id && l.UserId == invitee.Id).Should().BeTrue();
    }

    [Fact]
    public async Task AcceptInvitation_NonExistingNotification_ReturnsNotFound()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var user = await seeder.CreateUserAsync();
        var client = AuthorizedClient(user.Id, user.UserName!, user.Email!);

        // Act
        var response = await client.PostAsync("/api/notifications/999999/accept", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── DELETE single notification ────────────────────────────────────────────

    [Fact]
    public async Task DeleteNotification_ExistingNotification_ReturnsOk()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var recipient = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync(owner);
        var (notification, _) = await seeder.CreateGenericNotificationAsync(board, owner, recipient);
        var client = AuthorizedClient(recipient.Id, recipient.UserName!, recipient.Email!);

        // Act
        var response = await client.DeleteAsync($"/api/notifications/{notification.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var dbScope = factory.Services.CreateScope();
        var db = dbScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.NotificationsOnUsers.Any(n => n.NotificationId == notification.Id && n.UserId == recipient.Id)
            .Should().BeFalse();
    }

    [Fact]
    public async Task DeleteNotification_NonExistingLink_ReturnsNotFound()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var user = await seeder.CreateUserAsync();
        var client = AuthorizedClient(user.Id, user.UserName!, user.Email!);

        // Act
        var response = await client.DeleteAsync("/api/notifications/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── DELETE all notifications ──────────────────────────────────────────────

    [Fact]
    public async Task DeleteAllNotifications_WithNotifications_ReturnsOk()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var recipient = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync(owner);
        await seeder.CreateGenericNotificationAsync(board, owner, recipient);
        var client = AuthorizedClient(recipient.Id, recipient.UserName!, recipient.Email!);

        // Act
        var response = await client.DeleteAsync("/api/notifications");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var dbScope = factory.Services.CreateScope();
        var db = dbScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.NotificationsOnUsers.Any(n => n.UserId == recipient.Id).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAllNotifications_Unauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var client = factory.CreateClient();

        // Act
        var response = await client.DeleteAsync("/api/notifications");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
