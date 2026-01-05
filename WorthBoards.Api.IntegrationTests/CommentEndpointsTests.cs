using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using WorthBoards.Api.IntegrationTests.Support;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using Xunit;

namespace WorthBoards.Api.IntegrationTests;

public class CommentEndpointsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public CommentEndpointsTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateComment_ReturnsCreated_ForAuthorizedUser()
    {
        var request = new CommentRequest
        {
            Content = "Great progress!"
        };

        var response = await _client.WithUser(1).PostAsJsonAsync("/api/boards/10/tasks/100/comments", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<CommentResponse>();
        body.Should().NotBeNull();
        body!.TaskId.Should().Be(100);
        body.Content.Should().Be(request.Content);
        body.UserId.Should().Be(1);
    }

    [Fact]
    public async Task CreateComment_ReturnsNotFound_WhenTaskDoesNotExist()
    {
        var request = new CommentRequest
        {
            Content = "Won't work"
        };

        var response = await _client.WithUser(1).PostAsJsonAsync("/api/boards/10/tasks/999/comments", request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
