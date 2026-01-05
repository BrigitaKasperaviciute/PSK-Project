using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using WorthBoards.Api.IntegrationTests.Infrastructure;
using WorthBoards.Business.Dtos.Responses;
using Xunit;

namespace WorthBoards.Api.IntegrationTests;

public class BoardControllerTests : IClassFixture<IntegrationTestFactory>
{
    private readonly HttpClient _authorizedClient;

    public BoardControllerTests(IntegrationTestFactory factory)
    {
        _authorizedClient = factory.CreateAuthenticatedClient();
    }

    [Fact]
    public async Task GetBoardById_ReturnsBoard_WhenItExists()
    {
        var response = await _authorizedClient.GetAsync("/api/boards/1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var board = await response.Content.ReadFromJsonAsync<BoardResponse>();
        board.Should().NotBeNull();
        board!.Title.Should().Be("Board One");
    }

    [Fact]
    public async Task GetBoardById_ReturnsNotFound_ForUnknownBoard()
    {
        var response = await _authorizedClient.GetAsync("/api/boards/999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
