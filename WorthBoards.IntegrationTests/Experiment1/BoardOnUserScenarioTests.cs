using System.Net;

namespace WorthBoards.IntegrationTests.Experiment1;

public class BoardOnUserScenarioTests(TestApplicationFactory factory) : IClassFixture<TestApplicationFactory>
{
    [Fact]
    public async Task Scenario_BoardUsers_Happy_LinkUser_Then_ListUsers_ReturnsLinkedUser()
    {
        var ownerSession = await TestAuthHelpers.RegisterAndLoginAsync(factory, "boardusers_owner");
        var memberSession = await TestAuthHelpers.RegisterAndLoginAsync(factory, "boardusers_member");

        var boardId = await TestAuthHelpers.CreateBoardAsync(ownerSession.Client, "Board Users Happy");
        await TestAuthHelpers.LinkUserToBoardAsync(ownerSession.Client, boardId, memberSession.UserId, userRole: 2);

        var usersResponse = await ownerSession.Client.GetAsync($"/api/boards/{boardId}/users");
        usersResponse.EnsureSuccessStatusCode();

        var body = await usersResponse.Content.ReadAsStringAsync();
        Assert.Contains(memberSession.UserName, body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Scenario_BoardUsers_Negative_Viewer_Accesses_Owner_Endpoint_ReturnsForbidden()
    {
        var ownerSession = await TestAuthHelpers.RegisterAndLoginAsync(factory, "boardusers_owner_neg");
        var viewerSession = await TestAuthHelpers.RegisterAndLoginAsync(factory, "boardusers_viewer_neg");

        var boardId = await TestAuthHelpers.CreateBoardAsync(ownerSession.Client, "Board Users Negative");
        await TestAuthHelpers.LinkUserToBoardAsync(ownerSession.Client, boardId, viewerSession.UserId, userRole: 2);

        var response = await viewerSession.Client.GetAsync($"/api/boards/{boardId}/collaborators?userName=test");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
