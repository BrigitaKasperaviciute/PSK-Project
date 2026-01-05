using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using WorthBoards.Api.IntegrationTests.Infrastructure;
using WorthBoards.Business.Dtos.Responses;
using Xunit;

namespace WorthBoards.Api.IntegrationTests;

public class BoardTaskControllerTests : IClassFixture<IntegrationTestFactory>
{
    private readonly HttpClient _authorizedClient;

    public BoardTaskControllerTests(IntegrationTestFactory factory)
    {
        _authorizedClient = factory.CreateAuthenticatedClient();
    }

    [Fact]
    public async Task GetBoardTaskById_ReturnsTask_WhenItExists()
    {
        var response = await _authorizedClient.GetAsync("/api/boards/1/tasks/1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var task = await response.Content.ReadFromJsonAsync<BoardTaskResponse>();
        task.Should().NotBeNull();
        task!.Title.Should().Be("Seed Task");
    }

    [Fact]
    public async Task GetBoardTaskById_ReturnsNotFound_ForUnknownTask()
    {
        var response = await _authorizedClient.GetAsync("/api/boards/1/tasks/999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
