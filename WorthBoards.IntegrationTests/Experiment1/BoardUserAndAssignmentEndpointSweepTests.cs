using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace WorthBoards.IntegrationTests.Experiment1;

public sealed class BoardUserAndAssignmentEndpointSweepTests(TestApplicationFactory factory) : IClassFixture<TestApplicationFactory>
{
    [Fact]
    public async Task Happy_BoardUserAndTaskAssignment_Endpoints_AreReachable()
    {
        var ownerSession = await TestAuthHelpers.RegisterAndLoginAsync(factory, "linksweepowner");
        var memberSession = await TestAuthHelpers.RegisterAndLoginAsync(factory, "linksweepmember");

        var boardId = await TestAuthHelpers.CreateBoardAsync(ownerSession.Client, "Link Sweep Board");

        var createLink = await ownerSession.Client.PostAsJsonAsync($"/api/boards/{boardId}/link/{memberSession.UserId}", new { userRole = 2 });
        Assert.Equal(HttpStatusCode.Created, createLink.StatusCode);

        var getLink = await ownerSession.Client.GetAsync($"/api/boards/{boardId}/link/{memberSession.UserId}");
        Assert.Equal(HttpStatusCode.OK, getLink.StatusCode);

        var updateLink = await ownerSession.Client.PutAsJsonAsync($"/api/boards/{boardId}/link/{memberSession.UserId}", new { userRole = 1 });
        Assert.Equal(HttpStatusCode.OK, updateLink.StatusCode);

        var patchLinkContent = TestAuthHelpers.BuildJsonPatchContent(
            "[{\"op\":\"replace\",\"path\":\"/userRole\",\"value\":2}]"
        );
        var patchLink = await ownerSession.Client.PatchAsync($"/api/boards/{boardId}/link/{memberSession.UserId}", patchLinkContent);
        Assert.Equal(HttpStatusCode.OK, patchLink.StatusCode);

        var taskId = await TestAuthHelpers.CreateTaskAsync(ownerSession.Client, boardId, "Assignment Sweep Task");

        var taskLink = await ownerSession.Client.PostAsJsonAsync($"/api/boards/{boardId}/tasks/{taskId}/users/link", new[]
        {
            new { userId = memberSession.UserId }
        });
        Assert.Equal(HttpStatusCode.OK, taskLink.StatusCode);

        var unlinkRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/boards/{boardId}/tasks/{taskId}/users/unlink")
        {
            Content = JsonContent.Create(new[]
            {
                new { userId = memberSession.UserId }
            })
        };
        var unlinkResponse = await ownerSession.Client.SendAsync(unlinkRequest);
        Assert.Equal(HttpStatusCode.OK, unlinkResponse.StatusCode);

        var removeUser = await ownerSession.Client.PostAsync($"/api/boards/{boardId}/remove/{memberSession.UserId}", null);
        Assert.Equal(HttpStatusCode.NoContent, removeUser.StatusCode);

        var relinkUser = await ownerSession.Client.PostAsJsonAsync($"/api/boards/{boardId}/link/{memberSession.UserId}", new { userRole = 2 });
        Assert.Equal(HttpStatusCode.Created, relinkUser.StatusCode);

        var collaborators = await ownerSession.Client.GetAsync($"/api/boards/{boardId}/collaborators?userName={memberSession.UserName}");
        Assert.Equal(HttpStatusCode.OK, collaborators.StatusCode);

        var transfer = await ownerSession.Client.PostAsync($"/api/boards/{boardId}/collaborators/{memberSession.UserId}", null);
        Assert.Equal(HttpStatusCode.NoContent, transfer.StatusCode);

        var linksResponse = await memberSession.Client.GetAsync($"/api/boards/{boardId}/links");
        Assert.Equal(HttpStatusCode.OK, linksResponse.StatusCode);

        var linksBody = await linksResponse.Content.ReadAsStringAsync();
        using var linksJson = JsonDocument.Parse(linksBody);
        Assert.Equal(JsonValueKind.Array, linksJson.RootElement.ValueKind);
    }

    [Fact]
    public async Task Negative_TransferOwnership_ToSelf_ReturnsBadRequest()
    {
        var ownerSession = await TestAuthHelpers.RegisterAndLoginAsync(factory, "linksweepneg");
        var boardId = await TestAuthHelpers.CreateBoardAsync(ownerSession.Client, "Transfer Neg Board");

        var transferToSelf = await ownerSession.Client.PostAsync($"/api/boards/{boardId}/collaborators/{ownerSession.UserId}", null);

        Assert.Equal(HttpStatusCode.BadRequest, transferToSelf.StatusCode);
    }
}
