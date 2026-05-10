using System.Net;

namespace WorthBoards.IntegrationTests.Experiment1;

public class NotificationScenarioTests(TestApplicationFactory factory) : IClassFixture<TestApplicationFactory>
{
    [Fact]
    public async Task Scenario_Notification_Happy_TaskCreation_NotifiesBoardMember()
    {
        var ownerSession = await TestAuthHelpers.RegisterAndLoginAsync(factory, "notif_owner");
        var memberSession = await TestAuthHelpers.RegisterAndLoginAsync(factory, "notif_member");

        var boardId = await TestAuthHelpers.CreateBoardAsync(ownerSession.Client, "Notification Board");
        await TestAuthHelpers.LinkUserToBoardAsync(ownerSession.Client, boardId, memberSession.UserId, userRole: 2);

        await TestAuthHelpers.CreateTaskAsync(ownerSession.Client, boardId, "Notification Task");

        var notificationsResponse = await memberSession.Client.GetAsync("/api/notifications");
        notificationsResponse.EnsureSuccessStatusCode();

        var body = await notificationsResponse.Content.ReadAsStringAsync();
        Assert.Contains("task", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Scenario_Notification_Negative_GetWithoutAuth_ReturnsUnauthorized()
    {
        var anonymousClient = factory.CreateClient();

        var response = await anonymousClient.GetAsync("/api/notifications");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
