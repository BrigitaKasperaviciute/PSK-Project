using System.Net;

namespace WorthBoards.IntegrationTests.Experiment1;

public sealed class NotificationCleanupScenarioTests(TestApplicationFactory factory) : IClassFixture<TestApplicationFactory>
{
    [Fact]
    public async Task Happy_DeleteNotification_RemovesExistingNotification()
    {
        var ownerSession = await TestAuthHelpers.RegisterAndLoginAsync(factory, "notifowner");
        var memberSession = await TestAuthHelpers.RegisterAndLoginAsync(factory, "notifmember");

        var boardId = await TestAuthHelpers.CreateBoardAsync(ownerSession.Client, "Notif Board");
        await TestAuthHelpers.LinkUserToBoardAsync(ownerSession.Client, boardId, memberSession.UserId, 2);

        await TestAuthHelpers.CreateTaskAsync(ownerSession.Client, boardId, "Notification Task");

        var notificationId = await TestAuthHelpers.GetFirstNotificationIdAsync(memberSession.Client);
        var deleteResponse = await memberSession.Client.DeleteAsync($"/api/notifications/{notificationId}");

        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task Negative_DeleteMissingNotification_ReturnsNotFound()
    {
        var session = await TestAuthHelpers.RegisterAndLoginAsync(factory, "notifneg");

        var response = await session.Client.DeleteAsync("/api/notifications/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
