using System.Net;
using System.Net.Http.Json;

namespace WorthBoards.IntegrationTests.Experiment1;

public sealed class CommentMutationScenarioTests(TestApplicationFactory factory) : IClassFixture<TestApplicationFactory>
{
    [Fact]
    public async Task Happy_UpdateComment_WithCurrentVersion_ReturnsOk()
    {
        var session = await TestAuthHelpers.RegisterAndLoginAsync(factory, "cmtupd");

        var boardId = await TestAuthHelpers.CreateBoardAsync(session.Client, "Comment Board");
        var taskId = await TestAuthHelpers.CreateTaskAsync(session.Client, boardId, "Task");
        var (commentId, version) = await TestAuthHelpers.CreateCommentAsync(session.Client, boardId, taskId, "Original");

        var updateResponse = await session.Client.PutAsJsonAsync($"/api/boards/{boardId}/tasks/{taskId}/comments/{commentId}", new
        {
            content = "Updated content",
            version
        });

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
    }

    [Fact]
    public async Task Negative_UpdateMissingComment_ReturnsNotFound()
    {
        var session = await TestAuthHelpers.RegisterAndLoginAsync(factory, "cmtconf");

        var boardId = await TestAuthHelpers.CreateBoardAsync(session.Client, "Comment Conflict Board");
        var taskId = await TestAuthHelpers.CreateTaskAsync(session.Client, boardId, "Task");

        var updateMissingResponse = await session.Client.PutAsJsonAsync($"/api/boards/{boardId}/tasks/{taskId}/comments/999999", new
        {
            content = "Missing comment",
            version = 0
        });

        Assert.Equal(HttpStatusCode.NotFound, updateMissingResponse.StatusCode);
    }
}
