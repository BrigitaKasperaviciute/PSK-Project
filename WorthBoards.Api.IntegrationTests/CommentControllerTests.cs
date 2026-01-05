using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using WorthBoards.Api.IntegrationTests.Infrastructure;
using WorthBoards.Business.Dtos.Responses;
using Xunit;

namespace WorthBoards.Api.IntegrationTests;

public class CommentControllerTests : IClassFixture<IntegrationTestFactory>
{
    private readonly HttpClient _authorizedClient;

    public CommentControllerTests(IntegrationTestFactory factory)
    {
        _authorizedClient = factory.CreateAuthenticatedClient();
    }

    [Fact]
    public async Task GetCommentById_ReturnsComment_WhenItExists()
    {
        var response = await _authorizedClient.GetAsync("/api/boards/1/tasks/1/comments/1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var comment = await response.Content.ReadFromJsonAsync<CommentResponse>();
        comment.Should().NotBeNull();
        comment!.Content.Should().Be("Seed comment");
    }

    [Fact]
    public async Task GetCommentById_ReturnsNotFound_ForUnknownComment()
    {
        var response = await _authorizedClient.GetAsync("/api/boards/1/tasks/1/comments/999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
