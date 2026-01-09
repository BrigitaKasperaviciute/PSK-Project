using System.Net;
using WorthBoards.Data.Identity;
using WorthBoards.Domain.Entities;
using Xunit;

namespace WorthBoards.Api.IntegrationTests.Controllers;

public class NotificationControllerTests : IntegrationTestBase
{
    public NotificationControllerTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task DeleteNotification_WhenNotificationExists_ReturnsOk()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = 1,
            FirstName = "Test",
            LastName = "User",
            UserName = "testuser",
            Email = "test@example.com",
            CreationDate = DateTime.UtcNow
        };
        DbContext.Users.Add(user);

        var notification = new Notification
        {
            Id = 1,
            SenderId = 1,
            NotificationType = Common.Enums.NotificationEventTypeEnum.INVITATION,
            SendDate = DateTime.UtcNow,
            BoardId = 1
        };
        DbContext.Notifications.Add(notification);

        var notificationOnUser = new NotificationOnUser
        {
            NotificationId = 1,
            UserId = 1
        };
        DbContext.NotificationsOnUsers.Add(notificationOnUser);

        await DbContext.SaveChangesAsync();

        var client = CreateAuthenticatedClient("1", "test@example.com");

        // Act
        var response = await client.DeleteAsync("/api/notifications/1");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DeleteNotification_WhenNotificationDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = 1,
            FirstName = "Test",
            LastName = "User",
            UserName = "testuser",
            Email = "test@example.com",
            CreationDate = DateTime.UtcNow
        };
        DbContext.Users.Add(user);

        await DbContext.SaveChangesAsync();

        var client = CreateAuthenticatedClient("1", "test@example.com");

        // Act
        var response = await client.DeleteAsync("/api/notifications/999");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
