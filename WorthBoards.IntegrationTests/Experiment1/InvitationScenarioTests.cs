using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace WorthBoards.IntegrationTests.Experiment1;

public sealed class InvitationScenarioTests(TestApplicationFactory factory) : IClassFixture<TestApplicationFactory>
{
    [Fact]
    public async Task Happy_InviteAndAccept_AddsUserToBoard()
    {
        var ownerSession = await TestAuthHelpers.RegisterAndLoginAsync(factory, "ownerinv");
        var invitedSession = await TestAuthHelpers.RegisterAndLoginAsync(factory, "memberinv");

        var boardId = await TestAuthHelpers.CreateBoardAsync(ownerSession.Client, "Invite Board");

        var inviteResponse = await ownerSession.Client.PostAsJsonAsync($"/api/boards/{boardId}/invite", new
        {
            userId = invitedSession.UserId,
            role = 2
        });
        Assert.Equal(HttpStatusCode.NoContent, inviteResponse.StatusCode);

        var notificationId = await TestAuthHelpers.GetFirstNotificationIdAsync(invitedSession.Client);
        var acceptResponse = await invitedSession.Client.PostAsync($"/api/notifications/{notificationId}/accept", null);

        Assert.Equal(HttpStatusCode.NoContent, acceptResponse.StatusCode);

        var linksResponse = await invitedSession.Client.GetAsync($"/api/boards/{boardId}/links");
        Assert.Equal(HttpStatusCode.OK, linksResponse.StatusCode);

        var linksBody = await linksResponse.Content.ReadAsStringAsync();
        using var linksDoc = JsonDocument.Parse(linksBody);
        Assert.True(linksDoc.RootElement.ValueKind == JsonValueKind.Array && linksDoc.RootElement.GetArrayLength() > 0);
    }

    [Fact]
    public async Task Negative_InviteWithOwnerRole_ReturnsBadRequest()
    {
        var ownerSession = await TestAuthHelpers.RegisterAndLoginAsync(factory, "ownerinvneg");
        var invitedSession = await TestAuthHelpers.RegisterAndLoginAsync(factory, "memberinvneg");

        var boardId = await TestAuthHelpers.CreateBoardAsync(ownerSession.Client, "Invite Neg Board");

        var inviteResponse = await ownerSession.Client.PostAsJsonAsync($"/api/boards/{boardId}/invite", new
        {
            userId = invitedSession.UserId,
            role = 0
        });

        Assert.Equal(HttpStatusCode.BadRequest, inviteResponse.StatusCode);
    }
}
