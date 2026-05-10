using System.Net;
using System.Net.Http.Json;

namespace WorthBoards.IntegrationTests.Experiment1;

public class BoardTaskScenarioTests(TestApplicationFactory factory) : IClassFixture<TestApplicationFactory>
{
    [Fact]
    public async Task Scenario_Task_Happy_Create_Then_GetById_ReturnsTask()
    {
        var ownerSession = await TestAuthHelpers.RegisterAndLoginAsync(factory, "taskowner");
        var boardId = await TestAuthHelpers.CreateBoardAsync(ownerSession.Client, "Task Happy Board");
        var taskId = await TestAuthHelpers.CreateTaskAsync(ownerSession.Client, boardId, "Task Happy");

        var getByIdResponse = await ownerSession.Client.GetAsync($"/api/boards/{boardId}/tasks/{taskId}");
        getByIdResponse.EnsureSuccessStatusCode();

        var body = await getByIdResponse.Content.ReadAsStringAsync();
        Assert.Contains("Task Happy", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Scenario_Task_Negative_Create_By_NonMember_ReturnsForbidden()
    {
        var ownerSession = await TestAuthHelpers.RegisterAndLoginAsync(factory, "taskownerneg");
        var outsiderSession = await TestAuthHelpers.RegisterAndLoginAsync(factory, "taskoutsider");

        var boardId = await TestAuthHelpers.CreateBoardAsync(ownerSession.Client, "Task Negative Board");

        var response = await outsiderSession.Client.PostAsJsonAsync($"/api/boards/{boardId}/tasks", new
        {
            title = "Unauthorized create",
            description = "Outsider should not be allowed",
            deadlineEnd = DateTime.UtcNow.AddDays(1),
            taskStatus = 0
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
