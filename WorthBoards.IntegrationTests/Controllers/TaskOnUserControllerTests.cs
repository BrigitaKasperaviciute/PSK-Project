using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Common.Enums;
using WorthBoards.IntegrationTests.Infrastructure;

namespace WorthBoards.IntegrationTests.Controllers;

public class TaskOnUserControllerTests : IntegrationTestBase
{
    public TaskOnUserControllerTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task GetUsersLinkedToTask_AsBoardMember_ReturnsOk()
    {
        var (token, _) = await RegisterAndLoginAsync();
        var client = CreateAuthenticatedClient(token);
        var board = await CreateBoardAsync(client);
        var task = await CreateTaskAsync(client, board.Id);

        var response = await client.GetAsync($"/api/boards/{board.Id}/tasks/{task.Id}/users");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetUsersLinkedToTask_Unauthenticated_ReturnsUnauthorized()
    {
        var (token, _) = await RegisterAndLoginAsync();
        var client = CreateAuthenticatedClient(token);
        var board = await CreateBoardAsync(client);
        var task = await CreateTaskAsync(client, board.Id);

        var response = await CreateAnonymousClient()
            .GetAsync($"/api/boards/{board.Id}/tasks/{task.Id}/users");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LinkUsersToTask_AsOwner_ReturnsOk()
    {
        var (token, userId) = await RegisterAndLoginAsync();
        var client = CreateAuthenticatedClient(token);
        var board = await CreateBoardAsync(client);
        var task = await CreateTaskAsync(client, board.Id);

        var linkList = new[] { new LinkUserToTaskRequest { UserId = userId } };
        var response = await client
            .PostAsJsonAsync($"/api/boards/{board.Id}/tasks/{task.Id}/users/link", linkList);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task LinkUsersToTask_AsNonMember_ReturnsForbidden()
    {
        var (ownerToken, ownerUserId) = await RegisterAndLoginAsync();
        var ownerClient = CreateAuthenticatedClient(ownerToken);
        var board = await CreateBoardAsync(ownerClient);
        var task = await CreateTaskAsync(ownerClient, board.Id);

        var (otherToken, _) = await RegisterAndLoginAsync();
        var linkList = new[] { new LinkUserToTaskRequest { UserId = ownerUserId } };
        var response = await CreateAuthenticatedClient(otherToken)
            .PostAsJsonAsync($"/api/boards/{board.Id}/tasks/{task.Id}/users/link", linkList);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UnlinkUsersFromTask_AsOwner_ReturnsOk()
    {
        var (token, userId) = await RegisterAndLoginAsync();
        var client = CreateAuthenticatedClient(token);
        var board = await CreateBoardAsync(client);
        var task = await CreateTaskAsync(client, board.Id);

        // Link first, then unlink
        var linkList = new[] { new LinkUserToTaskRequest { UserId = userId } };
        await client.PostAsJsonAsync($"/api/boards/{board.Id}/tasks/{task.Id}/users/link", linkList);

        var response = await client
            .SendAsync(new HttpRequestMessage(HttpMethod.Delete,
                $"/api/boards/{board.Id}/tasks/{task.Id}/users/unlink")
            {
                Content = JsonContent.Create(linkList)
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UnlinkUsersFromTask_AsNonMember_ReturnsForbidden()
    {
        var (ownerToken, ownerUserId) = await RegisterAndLoginAsync();
        var ownerClient = CreateAuthenticatedClient(ownerToken);
        var board = await CreateBoardAsync(ownerClient);
        var task = await CreateTaskAsync(ownerClient, board.Id);

        var (otherToken, _) = await RegisterAndLoginAsync();
        var linkList = new[] { new LinkUserToTaskRequest { UserId = ownerUserId } };
        var response = await CreateAuthenticatedClient(otherToken)
            .SendAsync(new HttpRequestMessage(HttpMethod.Delete,
                $"/api/boards/{board.Id}/tasks/{task.Id}/users/unlink")
            {
                Content = JsonContent.Create(linkList)
            });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
