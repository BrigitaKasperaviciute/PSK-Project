using System.Net;
using System.Net.Http.Json;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.IntegrationTests.Infrastructure;

namespace WorthBoards.IntegrationTests.Controllers;

public class TaskOnUserControllerTests : IntegrationTestBase
{
    public TaskOnUserControllerTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task LinkUsersToTask_AsOwner_ReturnsOk()
    {
        var (token, ownerId) = await RegisterAndLoginAsync();
        var client = CreateAuthenticatedClient(token);
        var board = await CreateBoardAsync(client);
        var task = await CreateTaskAsync(client, board.Id);

        var linkList = new[] { new LinkUserToTaskRequest { UserId = ownerId } };
        var response = await client.PostAsJsonAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/users/link", linkList);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task LinkUsersToTask_AsNonMember_ReturnsForbidden()
    {
        var (ownerToken, ownerId) = await RegisterAndLoginAsync();
        var ownerClient = CreateAuthenticatedClient(ownerToken);
        var board = await CreateBoardAsync(ownerClient);
        var task = await CreateTaskAsync(ownerClient, board.Id);

        var (otherToken, _) = await RegisterAndLoginAsync();
        var linkList = new[] { new LinkUserToTaskRequest { UserId = ownerId } };
        var response = await CreateAuthenticatedClient(otherToken)
            .PostAsJsonAsync($"/api/boards/{board.Id}/tasks/{task.Id}/users/link", linkList);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UnlinkUsersFromTask_AsOwner_ReturnsOk()
    {
        var (token, ownerId) = await RegisterAndLoginAsync();
        var client = CreateAuthenticatedClient(token);
        var board = await CreateBoardAsync(client);
        var task = await CreateTaskAsync(client, board.Id);

        var linkList = new[] { new LinkUserToTaskRequest { UserId = ownerId } };
        await client.PostAsJsonAsync($"/api/boards/{board.Id}/tasks/{task.Id}/users/link", linkList);

        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete,
            $"/api/boards/{board.Id}/tasks/{task.Id}/users/unlink")
        {
            Content = JsonContent.Create(linkList)
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UnlinkUsersFromTask_AsNonMember_ReturnsForbidden()
    {
        var (ownerToken, ownerId) = await RegisterAndLoginAsync();
        var ownerClient = CreateAuthenticatedClient(ownerToken);
        var board = await CreateBoardAsync(ownerClient);
        var task = await CreateTaskAsync(ownerClient, board.Id);

        var (otherToken, _) = await RegisterAndLoginAsync();
        var linkList = new[] { new LinkUserToTaskRequest { UserId = ownerId } };
        var response = await CreateAuthenticatedClient(otherToken).SendAsync(
            new HttpRequestMessage(HttpMethod.Delete,
                $"/api/boards/{board.Id}/tasks/{task.Id}/users/unlink")
            {
                Content = JsonContent.Create(linkList)
            });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetUsersLinkedToTask_AsMember_ReturnsOk()
    {
        var (token, _) = await RegisterAndLoginAsync();
        var client = CreateAuthenticatedClient(token);
        var board = await CreateBoardAsync(client);
        var task = await CreateTaskAsync(client, board.Id);

        var response = await client.GetAsync($"/api/boards/{board.Id}/tasks/{task.Id}/users");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetUsersLinkedToTask_AsNonMember_ReturnsForbidden()
    {
        var (ownerToken, _) = await RegisterAndLoginAsync();
        var ownerClient = CreateAuthenticatedClient(ownerToken);
        var board = await CreateBoardAsync(ownerClient);
        var task = await CreateTaskAsync(ownerClient, board.Id);

        var (otherToken, _) = await RegisterAndLoginAsync();
        var response = await CreateAuthenticatedClient(otherToken)
            .GetAsync($"/api/boards/{board.Id}/tasks/{task.Id}/users");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
