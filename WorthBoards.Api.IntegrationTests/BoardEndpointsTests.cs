using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using WorthBoards.Api.IntegrationTests.Support;
using WorthBoards.Business.Dtos.Responses;
using Xunit;

namespace WorthBoards.Api.IntegrationTests;

public class BoardEndpointsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public BoardEndpointsTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private sealed class BoardPageResponse
    {
        public List<BoardResponse> Items { get; set; } = new();
        public int PageNumber { get; set; }
        public int PageCount { get; set; }
        public int PageSize { get; set; }
    }

    [Fact]
    public async Task GetBoards_ReturnsBoards_ForAuthorizedUser()
    {
        var response = await _client.WithUser(1).GetAsync("/api/boards");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<BoardPageResponse>();
        body.Should().NotBeNull();
        body!.Items.Should().NotBeEmpty();
        body.Items.Should().ContainSingle(b => b.Id == 10 && b.Title.Contains("Alpha", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetBoards_ReturnsUnauthorized_WhenMissingAuth()
    {
        var response = await _client.GetAsync("/api/boards");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetBoardById_ReturnsBoard_WhenExists()
    {
        var response = await _client.WithUser(1).GetAsync("/api/boards/10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<BoardResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(10);
        body.Title.IndexOf("Alpha", StringComparison.OrdinalIgnoreCase).Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task GetBoardById_ReturnsNotFound_WhenMissing()
    {
        var response = await _client.WithUser(1).GetAsync("/api/boards/9999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteBoard_ReturnsNoContent_ForOwner()
    {
        var response = await _client.WithUser(1).DeleteAsync("/api/boards/20");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteBoard_ReturnsForbidden_ForNonMember()
    {
        var response = await _client.WithUser(2).DeleteAsync("/api/boards/20");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
