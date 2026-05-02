using System.Net;
using System.Net.Http.Json;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.IntegrationTests.Infrastructure;

namespace WorthBoards.IntegrationTests.Controllers;

public class NotificationControllerTests : IntegrationTestBase
{
    public NotificationControllerTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task GetNotifications_Authenticated_ReturnsOk()
    {
        var (token, _) = await RegisterAndLoginAsync();
        var client = CreateAuthenticatedClient(token);

        var response = await client.GetAsync("/api/notifications");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<NotificationResponse>>();
        body.Should().NotBeNull();
    }

    [Fact]
    public async Task GetNotifications_Unauthenticated_ReturnsUnauthorized()
    {
        var response = await CreateAnonymousClient().GetAsync("/api/notifications");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AcceptInvitation_ValidNotification_ReturnsNoContent()
    {
        var (ownerToken, _) = await RegisterAndLoginAsync();
        var (guestToken, guestId) = await RegisterAndLoginAsync();
        var ownerClient = CreateAuthenticatedClient(ownerToken);
        var guestClient = CreateAuthenticatedClient(guestToken);

        var board = await CreateBoardAsync(ownerClient);
        var notification = await InviteUserAndGetNotificationAsync(ownerClient, guestClient, board.Id, guestId);

        var response = await guestClient.PostAsJsonAsync($"/api/notifications/{notification.Id}/accept", new { });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AcceptInvitation_Unauthenticated_ReturnsUnauthorized()
    {
        var response = await CreateAnonymousClient().PostAsJsonAsync("/api/notifications/1/accept", new { });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteNotification_Authenticated_ReturnsOk()
    {
        var (ownerToken, _) = await RegisterAndLoginAsync();
        var (guestToken, guestId) = await RegisterAndLoginAsync();
        var ownerClient = CreateAuthenticatedClient(ownerToken);
        var guestClient = CreateAuthenticatedClient(guestToken);

        var board = await CreateBoardAsync(ownerClient);
        var notification = await InviteUserAndGetNotificationAsync(ownerClient, guestClient, board.Id, guestId);

        var response = await guestClient.DeleteAsync($"/api/notifications/{notification.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeleteNotification_Unauthenticated_ReturnsUnauthorized()
    {
        var response = await CreateAnonymousClient().DeleteAsync("/api/notifications/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteAllNotifications_Authenticated_ReturnsOk()
    {
        var (ownerToken, _) = await RegisterAndLoginAsync();
        var (guestToken, guestId) = await RegisterAndLoginAsync();
        var ownerClient = CreateAuthenticatedClient(ownerToken);
        var guestClient = CreateAuthenticatedClient(guestToken);

        var board = await CreateBoardAsync(ownerClient);
        await InviteUserAndGetNotificationAsync(ownerClient, guestClient, board.Id, guestId);

        var response = await guestClient.DeleteAsync("/api/notifications");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeleteAllNotifications_Unauthenticated_ReturnsUnauthorized()
    {
        var response = await CreateAnonymousClient().DeleteAsync("/api/notifications");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
