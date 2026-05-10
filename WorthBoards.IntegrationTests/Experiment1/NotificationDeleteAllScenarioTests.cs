using System.Net;

namespace WorthBoards.IntegrationTests.Experiment1;

public sealed class NotificationDeleteAllScenarioTests(TestApplicationFactory factory) : IClassFixture<TestApplicationFactory>
{
    [Fact]
    public async Task Happy_DeleteAllNotifications_ReturnsOk()
    {
        var ownerSession = await TestAuthHelpers.RegisterAndLoginAsync(factory, "notifallsweepowner");
        var memberSession = await TestAuthHelpers.RegisterAndLoginAsync(factory, "notifallsweepmember");

        var boardId = await TestAuthHelpers.CreateBoardAsync(ownerSession.Client, "Notif All Board");
        await TestAuthHelpers.LinkUserToBoardAsync(ownerSession.Client, boardId, memberSession.UserId, 2);
        await TestAuthHelpers.CreateTaskAsync(ownerSession.Client, boardId, "Notif Source Task");

        var deleteAll = await memberSession.Client.DeleteAsync("/api/notifications");

        Assert.Equal(HttpStatusCode.OK, deleteAll.StatusCode);
    }

    [Fact]
    public async Task Negative_DeleteAllNotifications_WithoutToken_ReturnsUnauthorized()
    {
        var anonymousClient = factory.CreateClient();

        var response = await anonymousClient.DeleteAsync("/api/notifications");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
