using System.Net;
using System.Net.Http.Headers;
using WorthBoards.Api.Tests.Infrastructure;

namespace WorthBoards.Api.Tests.Controllers;

public class NotificationControllerTests : IntegrationTestBase
{
    [Fact]
    public async Task DeleteNotification_WithExistingNotification_ReturnsOk()
    {
        // Arrange
        await ClearDatabaseAsync();
        var user = await TestHelpers.CreateTestUserAsync(Factory.Services);
        var board = await TestHelpers.CreateTestBoardAsync(Factory.Services, user.Id);
        var notification = await TestHelpers.CreateTestNotificationAsync(Factory.Services, user.Id, board.Id);

        var token = GetJwtToken(user.Id, user.Email!);
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await Client.DeleteAsync($"/api/notifications/{notification.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DeleteNotification_WithNonExistentNotification_ReturnsNotFound()
    {
        // Arrange
        await ClearDatabaseAsync();
        var user = await TestHelpers.CreateTestUserAsync(Factory.Services);

        var token = GetJwtToken(user.Id, user.Email!);
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var nonExistentNotificationId = 99999;

        // Act
        var response = await Client.DeleteAsync($"/api/notifications/{nonExistentNotificationId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
