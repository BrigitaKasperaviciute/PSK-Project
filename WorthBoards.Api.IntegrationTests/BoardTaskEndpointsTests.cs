using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using WorthBoards.Api.IntegrationTests.Support;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Common.Enums;
using Xunit;

namespace WorthBoards.Api.IntegrationTests;

public class BoardTaskEndpointsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public BoardTaskEndpointsTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateTask_ReturnsCreated_ForEditor()
    {
        var request = new BoardTaskRequest
        {
            Title = "New Task",
            Description = "Created from integration test",
            TaskStatus = TaskStatusEnum.PENDING
        };

        var response = await _client.WithUser(1).PostAsJsonAsync("/api/boards/10/tasks", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<BoardTaskResponse>();
        body.Should().NotBeNull();
        body!.BoardId.Should().Be(10);
        body.Title.Should().Be(request.Title);
    }

    [Fact]
    public async Task CreateTask_ReturnsForbidden_ForViewer()
    {
        var request = new BoardTaskRequest
        {
            Title = "Blocked Task",
            Description = "Viewer cannot create",
            TaskStatus = TaskStatusEnum.PENDING
        };

        var response = await _client.WithUser(2).PostAsJsonAsync("/api/boards/30/tasks", request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetTaskById_ReturnsTask_ForAuthorizedUser()
    {
        var response = await _client.WithUser(1).GetAsync("/api/boards/10/tasks/100");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<BoardTaskResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(100);
        body.BoardId.Should().Be(10);
    }

    [Fact]
    public async Task GetTaskById_ReturnsForbidden_WhenUserNotOnBoard()
    {
        var response = await _client.WithUser(2).GetAsync("/api/boards/10/tasks/100");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
