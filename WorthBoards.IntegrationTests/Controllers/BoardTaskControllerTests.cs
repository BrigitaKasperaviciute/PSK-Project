using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Common.Enums;
using WorthBoards.IntegrationTests.Infrastructure;

namespace WorthBoards.IntegrationTests.Controllers;

public class BoardTaskControllerTests : IntegrationTestBase
{
    public BoardTaskControllerTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task GetActiveTasks_AsBoardMember_ReturnsOk()
    {
        var (token, _) = await RegisterAndLoginAsync();
        var client = CreateAuthenticatedClient(token);
        var board = await CreateBoardAsync(client);

        var response = await client.GetAsync($"/api/boards/{board.Id}/tasks");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetActiveTasks_Unauthenticated_ReturnsUnauthorized()
    {
        var (token, _) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(CreateAuthenticatedClient(token));

        var response = await CreateAnonymousClient().GetAsync($"/api/boards/{board.Id}/tasks");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetArchivedTasks_AsBoardMember_ReturnsOk()
    {
        var (token, _) = await RegisterAndLoginAsync();
        var client = CreateAuthenticatedClient(token);
        var board = await CreateBoardAsync(client);

        var response = await client.GetAsync($"/api/boards/{board.Id}/tasks/archived");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetArchivedTasks_Unauthenticated_ReturnsUnauthorized()
    {
        var (token, _) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(CreateAuthenticatedClient(token));

        var response = await CreateAnonymousClient().GetAsync($"/api/boards/{board.Id}/tasks/archived");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetTaskById_ExistingTask_ReturnsOk()
    {
        var (token, _) = await RegisterAndLoginAsync();
        var client = CreateAuthenticatedClient(token);
        var board = await CreateBoardAsync(client);
        var task = await CreateTaskAsync(client, board.Id);

        var response = await client.GetAsync($"/api/boards/{board.Id}/tasks/{task.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<BoardTaskResponse>();
        body!.Id.Should().Be(task.Id);
    }

    [Fact]
    public async Task GetTaskById_NonExistingTask_ReturnsNotFound()
    {
        var (token, _) = await RegisterAndLoginAsync();
        var client = CreateAuthenticatedClient(token);
        var board = await CreateBoardAsync(client);

        var response = await client.GetAsync($"/api/boards/{board.Id}/tasks/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateTask_AsOwner_ReturnsCreated()
    {
        var (token, _) = await RegisterAndLoginAsync();
        var client = CreateAuthenticatedClient(token);
        var board = await CreateBoardAsync(client);

        var request = new BoardTaskRequest { Title = "New Task", TaskStatus = TaskStatusEnum.PENDING };
        var response = await client.PostAsJsonAsync($"/api/boards/{board.Id}/tasks", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<BoardTaskResponse>();
        body!.Title.Should().Be("New Task");
    }

    [Fact]
    public async Task CreateTask_AsNonMember_ReturnsForbidden()
    {
        var (ownerToken, _) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(CreateAuthenticatedClient(ownerToken));

        var (otherToken, _) = await RegisterAndLoginAsync();
        var request = new BoardTaskRequest { Title = "Hack Task", TaskStatus = TaskStatusEnum.PENDING };
        var response = await CreateAuthenticatedClient(otherToken)
            .PostAsJsonAsync($"/api/boards/{board.Id}/tasks", request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteTask_AsOwner_ReturnsNoContent()
    {
        var (token, _) = await RegisterAndLoginAsync();
        var client = CreateAuthenticatedClient(token);
        var board = await CreateBoardAsync(client);
        var task = await CreateTaskAsync(client, board.Id);

        var response = await client.DeleteAsync($"/api/boards/{board.Id}/tasks/{task.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteTask_NonExistingTask_ReturnsNotFound()
    {
        var (token, _) = await RegisterAndLoginAsync();
        var client = CreateAuthenticatedClient(token);
        var board = await CreateBoardAsync(client);

        var response = await client.DeleteAsync($"/api/boards/{board.Id}/tasks/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteArchivedTasks_AsOwner_ReturnsNoContent()
    {
        var (token, _) = await RegisterAndLoginAsync();
        var client = CreateAuthenticatedClient(token);
        var board = await CreateBoardAsync(client);

        var response = await client.DeleteAsync($"/api/boards/{board.Id}/tasks/archived");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteArchivedTasks_AsNonMember_ReturnsForbidden()
    {
        var (ownerToken, _) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(CreateAuthenticatedClient(ownerToken));

        var (otherToken, _) = await RegisterAndLoginAsync();
        var response = await CreateAuthenticatedClient(otherToken)
            .DeleteAsync($"/api/boards/{board.Id}/tasks/archived");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateTask_AsOwner_ReturnsOk()
    {
        var (token, _) = await RegisterAndLoginAsync();
        var client = CreateAuthenticatedClient(token);
        var board = await CreateBoardAsync(client);
        var task = await CreateTaskAsync(client, board.Id);

        var updateRequest = new BoardTaskUpdateRequest
        {
            Title = "Updated Task",
            TaskStatus = TaskStatusEnum.IN_PROGRESS,
            Version = task.Version
        };
        var response = await client.PutAsJsonAsync($"/api/boards/{board.Id}/tasks/{task.Id}", updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<BoardTaskResponse>();
        body!.Title.Should().Be("Updated Task");
    }

    [Fact]
    public async Task UpdateTask_VersionMismatch_ReturnsConflict()
    {
        var (token, _) = await RegisterAndLoginAsync();
        var client = CreateAuthenticatedClient(token);
        var board = await CreateBoardAsync(client);
        var task = await CreateTaskAsync(client, board.Id);

        var updateRequest = new BoardTaskUpdateRequest
        {
            Title = "Bad Version",
            TaskStatus = TaskStatusEnum.PENDING,
            Version = task.Version + 99
        };
        var response = await client.PutAsJsonAsync($"/api/boards/{board.Id}/tasks/{task.Id}", updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task PatchTask_AsOwner_ReturnsOk()
    {
        var (token, _) = await RegisterAndLoginAsync();
        var client = CreateAuthenticatedClient(token);
        var board = await CreateBoardAsync(client);
        var task = await CreateTaskAsync(client, board.Id);

        var patchDoc = new[]
        {
            new { op = "replace", path = "/title", value = (object)"Patched Task" },
            new { op = "replace", path = "/taskStatus", value = (object)0 },
            new { op = "replace", path = "/version", value = (object)(int)task.Version }
        };
        var response = await client.PatchAsJsonAsync($"/api/boards/{board.Id}/tasks/{task.Id}", patchDoc);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PatchTask_AsNonMember_ReturnsForbidden()
    {
        var (ownerToken, _) = await RegisterAndLoginAsync();
        var client = CreateAuthenticatedClient(ownerToken);
        var board = await CreateBoardAsync(client);
        var task = await CreateTaskAsync(client, board.Id);

        var (otherToken, _) = await RegisterAndLoginAsync();
        var patchDoc = new[] { new { op = "replace", path = "/title", value = "Hacked" } };
        var response = await CreateAuthenticatedClient(otherToken)
            .PatchAsJsonAsync($"/api/boards/{board.Id}/tasks/{task.Id}", patchDoc);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
