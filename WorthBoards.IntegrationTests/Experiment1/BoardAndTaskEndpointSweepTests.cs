using System.Net;
using System.Net.Http.Json;

namespace WorthBoards.IntegrationTests.Experiment1;

public sealed class BoardAndTaskEndpointSweepTests(TestApplicationFactory factory) : IClassFixture<TestApplicationFactory>
{
    [Fact]
    public async Task Happy_BoardAndTask_Endpoints_AreReachable()
    {
        var session = await TestAuthHelpers.RegisterAndLoginAsync(factory, "boardsweep");

        var boardId = await TestAuthHelpers.CreateBoardAsync(session.Client, "Sweep Board");
        var boardVersion = await TestAuthHelpers.GetBoardVersionAsync(session.Client, boardId);

        var boardPut = await session.Client.PutAsJsonAsync($"/api/boards/{boardId}", new
        {
            title = "Sweep Board Updated",
            description = "Updated description",
            imageName = "updated.png",
            version = boardVersion
        });
        Assert.Equal(HttpStatusCode.OK, boardPut.StatusCode);

        var boardPatchContent = TestAuthHelpers.BuildJsonPatchContent(
            "[{\"op\":\"replace\",\"path\":\"/title\",\"value\":\"Sweep Board Patched\"}]"
        );
        var boardPatch = await session.Client.PatchAsync($"/api/boards/{boardId}", boardPatchContent);
        Assert.Equal(HttpStatusCode.OK, boardPatch.StatusCode);

        var taskId = await TestAuthHelpers.CreateTaskAsync(session.Client, boardId, "Sweep Task");

        var activeTasks = await session.Client.GetAsync($"/api/boards/{boardId}/tasks");
        Assert.Equal(HttpStatusCode.OK, activeTasks.StatusCode);

        var taskVersion = await TestAuthHelpers.GetTaskVersionAsync(session.Client, boardId, taskId);

        var taskPatchContent = TestAuthHelpers.BuildJsonPatchContent(
            "[{\"op\":\"replace\",\"path\":\"/title\",\"value\":\"Sweep Task Patched\"},{\"op\":\"replace\",\"path\":\"/version\",\"value\":" + taskVersion + "}]"
        );
        var taskPatch = await session.Client.PatchAsync($"/api/boards/{boardId}/tasks/{taskId}", taskPatchContent);
        Assert.Equal(HttpStatusCode.OK, taskPatch.StatusCode);

        var archiveTaskId = await TestAuthHelpers.CreateTaskAsync(session.Client, boardId, "Archive Task");
        var archiveTaskVersion = await TestAuthHelpers.GetTaskVersionAsync(session.Client, boardId, archiveTaskId);
        var archiveUpdate = await session.Client.PutAsJsonAsync($"/api/boards/{boardId}/tasks/{archiveTaskId}", new
        {
            title = "Archive Task",
            description = "Archive me",
            deadlineEnd = DateTime.UtcNow.AddDays(3),
            taskStatus = 3,
            version = archiveTaskVersion
        });
        Assert.Equal(HttpStatusCode.OK, archiveUpdate.StatusCode);

        var archivedList = await session.Client.GetAsync($"/api/boards/{boardId}/tasks/archived");
        Assert.Equal(HttpStatusCode.OK, archivedList.StatusCode);

        var deleteArchived = await session.Client.DeleteAsync($"/api/boards/{boardId}/tasks/archived");
        Assert.Equal(HttpStatusCode.NoContent, deleteArchived.StatusCode);

        var deleteTaskId = await TestAuthHelpers.CreateTaskAsync(session.Client, boardId, "Delete Task");
        var deleteTask = await session.Client.DeleteAsync($"/api/boards/{boardId}/tasks/{deleteTaskId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteTask.StatusCode);
    }

    [Fact]
    public async Task Negative_BoardPatch_WithoutAuthorization_ReturnsUnauthorized()
    {
        var ownerSession = await TestAuthHelpers.RegisterAndLoginAsync(factory, "boardsweepneg");
        var boardId = await TestAuthHelpers.CreateBoardAsync(ownerSession.Client, "Neg Sweep Board");

        var anonymousClient = factory.CreateClient();
        var boardPatchContent = TestAuthHelpers.BuildJsonPatchContent(
            "[{\"op\":\"replace\",\"path\":\"/title\",\"value\":\"Unauthorized Patch\"}]"
        );

        var patchResponse = await anonymousClient.PatchAsync($"/api/boards/{boardId}", boardPatchContent);

        Assert.Equal(HttpStatusCode.Unauthorized, patchResponse.StatusCode);
    }
}
