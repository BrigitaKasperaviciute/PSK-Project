using System.Net;
using FluentAssertions;
using WorthBoards.Api.IntegrationTests.Infrastructure;
using Xunit;

namespace WorthBoards.Api.IntegrationTests;

public class NotificationControllerTests : IClassFixture<IntegrationTestFactory>
{
    private readonly HttpClient _authorizedClient;

    public NotificationControllerTests(IntegrationTestFactory factory)
    {
        _authorizedClient = factory.CreateAuthenticatedClient();
    }

    [Fact]
    public async Task DeleteNotification_ReturnsOk_WhenNotificationExists()
    {
        var response = await _authorizedClient.DeleteAsync("/api/notifications/1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeleteNotification_ReturnsNotFound_ForMissingNotification()
    {
        var response = await _authorizedClient.DeleteAsync("/api/notifications/999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
