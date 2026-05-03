using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Common.Exceptions;
using WorthBoards.IntegrationTests.Infrastructure;

namespace WorthBoards.IntegrationTests;

public sealed class NotificationControllerTests(WorthBoardsApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task AcceptInvitation_WithTargetUserToken_FinalizesInvitationAndCreatesBoardMembership()
    {
        // Arrange
        var client = await CreateAuthenticatedClientAsync("outsider");

        // Act
        var notificationsResponse = await client.GetAsync("/api/notifications");
        notificationsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var notifications = await ReadJsonAsync<List<NotificationResponse>>(notificationsResponse);
        notifications.Should().ContainSingle(notification => notification.Id == Seed.InvitationNotificationId);

        var acceptResponse = await client.PostAsync($"/api/notifications/{Seed.InvitationNotificationId}/accept", null);

        // Assert
        acceptResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<WorthBoards.Data.Database.ApplicationDbContext>();
            (await context.BoardOnUsers.AnyAsync(link => link.BoardId == Seed.BoardId && link.UserId == Seed.OutsiderUserId)).Should().BeTrue();
            (await context.Notifications.AnyAsync(notification => notification.Id == Seed.InvitationNotificationId)).Should().BeFalse();
        }
    }

    [Fact]
    public async Task AcceptInvitation_WithUnknownNotificationId_ReturnsNotFoundAndKeepsInvitationSeeded()
    {
        // Arrange
        var client = await CreateAuthenticatedClientAsync("outsider");

        // Act
        var response = await client.PostAsync("/api/notifications/999999/accept", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await ReadJsonAsync<ErrorDetails>(response);
        error.Should().NotBeNull();
        error!.StatusCode.Should().Be((int)HttpStatusCode.NotFound);

        await using var scope = Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<WorthBoards.Data.Database.ApplicationDbContext>();
        (await context.Notifications.AnyAsync(notification => notification.Id == Seed.InvitationNotificationId)).Should().BeTrue();
    }
}