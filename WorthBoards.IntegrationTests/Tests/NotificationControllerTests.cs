using System.Net;
using Moq;
using Xunit;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Common.Enums;
using WorthBoards.IntegrationTests.Infrastructure;

namespace WorthBoards.IntegrationTests.Tests;

public class NotificationControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public NotificationControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetAllMocks();
    }

    private HttpClient AuthClient() => _factory.CreateClient().WithAuth(userId: 1);

    private static NotificationResponse MakeNotification(int id = 1) =>
        new() { Id = id, Title = "Invite", Description = "You were invited.", SendDate = DateTime.UtcNow, Type = NotificationEventTypeEnum.INVITATION };

    // ── AcceptInvitation ──────────────────────────────────────────────────

    [Fact]
    public async Task AcceptInvitation_Authenticated_ReturnsNoContent()
    {
        _factory.NotificationServiceMock
            .Setup(s => s.AcceptInvitation(1, 1, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await AuthClient().PostAsync("/api/notifications/1/accept", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task AcceptInvitation_NoToken_ReturnsUnauthorized()
    {
        var response = await _factory.CreateClient().PostAsync("/api/notifications/1/accept", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── GetNotificationsByUserId ──────────────────────────────────────────

    [Fact]
    public async Task GetNotifications_Authenticated_ReturnsOk()
    {
        _factory.NotificationServiceMock
            .Setup(s => s.GetNotificationsByUserId(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<NotificationResponse> { MakeNotification() });

        var response = await AuthClient().GetAsync("/api/notifications");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetNotifications_NoToken_ReturnsUnauthorized()
    {
        var response = await _factory.CreateClient().GetAsync("/api/notifications");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── DeleteNotification ────────────────────────────────────────────────

    [Fact]
    public async Task DeleteNotification_Authenticated_ReturnsOk()
    {
        _factory.NotificationServiceMock
            .Setup(s => s.UnlinkNotification(1, 1, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await AuthClient().DeleteAsync("/api/notifications/1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DeleteNotification_NoToken_ReturnsUnauthorized()
    {
        var response = await _factory.CreateClient().DeleteAsync("/api/notifications/1");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── DeleteAllNotifications ────────────────────────────────────────────

    [Fact]
    public async Task DeleteAllNotifications_Authenticated_ReturnsOk()
    {
        _factory.NotificationServiceMock
            .Setup(s => s.UnlinkAllNotifications(1, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await AuthClient().DeleteAsync("/api/notifications");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DeleteAllNotifications_NoToken_ReturnsUnauthorized()
    {
        var response = await _factory.CreateClient().DeleteAsync("/api/notifications");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}