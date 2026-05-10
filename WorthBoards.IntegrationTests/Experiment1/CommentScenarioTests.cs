using System.Net;
using System.Net.Http.Json;

namespace WorthBoards.IntegrationTests.Experiment1;

public class CommentScenarioTests(TestApplicationFactory factory) : IClassFixture<TestApplicationFactory>
{
    [Fact]
    public async Task Scenario_Comment_Happy_Create_Then_GetById_ReturnsComment()
    {
        var session = await TestAuthHelpers.RegisterAndLoginAsync(factory, "commenthappy");
        var boardId = await TestAuthHelpers.CreateBoardAsync(session.Client, "Comment Board");
        var taskId = await TestAuthHelpers.CreateTaskAsync(session.Client, boardId, "Comment Task");

        var createCommentResponse = await session.Client.PostAsJsonAsync($"/api/boards/{boardId}/tasks/{taskId}/comments", new
        {
            content = "This comment is created in a happy flow."
        });
        createCommentResponse.EnsureSuccessStatusCode();

        var createBody = await createCommentResponse.Content.ReadAsStringAsync();
        Assert.Contains("comment", createBody, StringComparison.OrdinalIgnoreCase);

        var getAllResponse = await session.Client.GetAsync($"/api/boards/{boardId}/tasks/{taskId}/comments");
        getAllResponse.EnsureSuccessStatusCode();
        var getAllBody = await getAllResponse.Content.ReadAsStringAsync();

        Assert.Contains("happy flow", getAllBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Scenario_Comment_Negative_Create_For_Missing_Task_ReturnsNotFound()
    {
        var session = await TestAuthHelpers.RegisterAndLoginAsync(factory, "commentnegative");

        var response = await session.Client.PostAsJsonAsync("/api/boards/99999/tasks/99999/comments", new
        {
            content = "This should fail for missing task"
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
