using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using System.Text.Json;
using Xunit;
using WorthBoards.Common.Enums;
using WorthBoards.IntegrationTests.Infrastructure;

namespace WorthBoards.IntegrationTests.Controllers;

public class NotificationControllerIntegrationTests : IntegrationTestBase
{
    [Fact]
    public async Task GetNotificationsByUserId_WithAuthentication_ReturnsOkWithNotificationsList()
    {
        // Arrange
        await ResetDatabaseAsync();
        var helper = new TestHelper(HttpClient, this);
        var (userId1, token1, _) = await helper.CreateAndLoginUserAsync();
        var (userId2, token2, _) = await helper.CreateAndLoginUserAsync();
        
        var board = await helper.CreateBoardAsync(userId1, token1);
        var inviteRequest = TestDataBuilder.CreateInvitationRequest(userId2, UserRoleEnum.VIEWER);
        SetBearerToken(token1);
        await HttpClient.PostAsJsonAsync($"api/boards/{board.Id}/invite", inviteRequest);
        
        SetBearerToken(token2);

        // Act
        var response = await HttpClient.GetAsync("api/notifications");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetNotificationsByUserId_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        await ResetDatabaseAsync();

        // Act
        var response = await HttpClient.GetAsync("api/notifications");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AcceptInvitation_ValidNotificationId_ReturnsNoContent()
    {
        // Arrange
        await ResetDatabaseAsync();
        var helper = new TestHelper(HttpClient, this);
        var (userId1, token1, _) = await helper.CreateAndLoginUserAsync();
        var (userId2, token2, _) = await helper.CreateAndLoginUserAsync();
        
        var board = await helper.CreateBoardAsync(userId1, token1);
        var inviteRequest = TestDataBuilder.CreateInvitationRequest(userId2, UserRoleEnum.VIEWER);
        SetBearerToken(token1);
        await HttpClient.PostAsJsonAsync($"api/boards/{board.Id}/invite", inviteRequest);
        
        // Get notifications for userId2
        SetBearerToken(token2);
        var notificationsResponse = await HttpClient.GetAsync("api/notifications");
        var notifications = await notificationsResponse.Content.ReadFromJsonAsync<JsonElement>();

        var notificationId = notifications.EnumerateArray().First().GetProperty("id").GetInt32();

        // Act
        var response = await HttpClient.PostAsync($"api/notifications/{notificationId}/accept", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AcceptInvitation_NonExistentNotification_ReturnsNotFound()
    {
        // Arrange
        await ResetDatabaseAsync();
        var helper = new TestHelper(HttpClient, this);
        var (userId, token, _) = await helper.CreateAndLoginUserAsync();
        SetBearerToken(token);

        // Act
        var response = await HttpClient.PostAsync("api/notifications/99999/accept", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteNotification_ValidNotificationId_ReturnsOk()
    {
        // Arrange
        await ResetDatabaseAsync();
        var helper = new TestHelper(HttpClient, this);
        var (userId1, token1, _) = await helper.CreateAndLoginUserAsync();
        var (userId2, token2, _) = await helper.CreateAndLoginUserAsync();
        
        var board = await helper.CreateBoardAsync(userId1, token1);
        var inviteRequest = TestDataBuilder.CreateInvitationRequest(userId2, UserRoleEnum.VIEWER);
        SetBearerToken(token1);
        await HttpClient.PostAsJsonAsync($"api/boards/{board.Id}/invite", inviteRequest);
        
        // Get notifications for userId2
        SetBearerToken(token2);
        var notificationsResponse = await HttpClient.GetAsync("api/notifications");
        var notifications = await notificationsResponse.Content.ReadFromJsonAsync<JsonElement>();

        var notificationId = notifications.EnumerateArray().First().GetProperty("id").GetInt32();

        // Act
        var response = await HttpClient.DeleteAsync($"api/notifications/{notificationId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeleteNotification_NonExistentNotification_ReturnsNotFound()
    {
        // Arrange
        await ResetDatabaseAsync();
        var helper = new TestHelper(HttpClient, this);
        var (userId, token, _) = await helper.CreateAndLoginUserAsync();
        SetBearerToken(token);

        // Act
        var response = await HttpClient.DeleteAsync("api/notifications/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteAllNotifications_WithNotifications_ReturnsOk()
    {
        // Arrange
        await ResetDatabaseAsync();
        var helper = new TestHelper(HttpClient, this);
        var (userId1, token1, _) = await helper.CreateAndLoginUserAsync();
        var (userId2, token2, _) = await helper.CreateAndLoginUserAsync();
        
        var board = await helper.CreateBoardAsync(userId1, token1);
        var inviteRequest = TestDataBuilder.CreateInvitationRequest(userId2, UserRoleEnum.VIEWER);
        SetBearerToken(token1);
        await HttpClient.PostAsJsonAsync($"api/boards/{board.Id}/invite", inviteRequest);
        
        SetBearerToken(token2);

        // Act
        var response = await HttpClient.DeleteAsync("api/notifications");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
