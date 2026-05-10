using System.Net;
using System.Net.Http.Json;

namespace WorthBoards.IntegrationTests.Experiment1;

public class TaskOnUserScenarioTests(TestApplicationFactory factory) : IClassFixture<TestApplicationFactory>
{
    [Fact]
    public async Task Scenario_TaskUsers_Happy_LinkUserToTask_Then_ListUsers_ReturnsLinkedUser()
    {
        var ownerSession = await TestAuthHelpers.RegisterAndLoginAsync(factory, "taskusers_owner");
        var memberSession = await TestAuthHelpers.RegisterAndLoginAsync(factory, "taskusers_member");

        var boardId = await TestAuthHelpers.CreateBoardAsync(ownerSession.Client, "Task Users Happy");
        await TestAuthHelpers.LinkUserToBoardAsync(ownerSession.Client, boardId, memberSession.UserId, userRole: 2);
        var taskId = await TestAuthHelpers.CreateTaskAsync(ownerSession.Client, boardId, "Task Users Happy Task");

        var linkResponse = await ownerSession.Client.PostAsJsonAsync($"/api/boards/{boardId}/tasks/{taskId}/users/link", new[]
        {
            new { userId = memberSession.UserId }
        });
        linkResponse.EnsureSuccessStatusCode();

        var usersResponse = await ownerSession.Client.GetAsync($"/api/boards/{boardId}/tasks/{taskId}/users");
        usersResponse.EnsureSuccessStatusCode();

        var usersBody = await usersResponse.Content.ReadAsStringAsync();
        Assert.Contains(memberSession.UserName, usersBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Scenario_TaskUsers_Negative_Outsider_Links_Users_ReturnsForbidden()
    {
        var ownerSession = await TestAuthHelpers.RegisterAndLoginAsync(factory, "taskusers_owner_neg");
        var outsiderSession = await TestAuthHelpers.RegisterAndLoginAsync(factory, "taskusers_outsider_neg");
        var memberSession = await TestAuthHelpers.RegisterAndLoginAsync(factory, "taskusers_member_neg");

        var boardId = await TestAuthHelpers.CreateBoardAsync(ownerSession.Client, "Task Users Negative");
        var taskId = await TestAuthHelpers.CreateTaskAsync(ownerSession.Client, boardId, "Task Users Negative Task");

        var response = await outsiderSession.Client.PostAsJsonAsync($"/api/boards/{boardId}/tasks/{taskId}/users/link", new[]
        {
            new { userId = memberSession.UserId }
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
