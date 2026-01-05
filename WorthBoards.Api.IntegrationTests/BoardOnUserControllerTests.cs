using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using WorthBoards.Api.IntegrationTests.Infrastructure;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Common.Enums;
using Xunit;

namespace WorthBoards.Api.IntegrationTests;

public class BoardOnUserControllerTests : IClassFixture<IntegrationTestFactory>
{
    private readonly HttpClient _authorizedClient;

    public BoardOnUserControllerTests(IntegrationTestFactory factory)
    {
        _authorizedClient = factory.CreateAuthenticatedClient();
    }

    [Fact]
    public async Task GetBoardToUserLink_ReturnsLink_WhenUserLinked()
    {
        var response = await _authorizedClient.GetAsync("/api/boards/1/link/1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var link = await response.Content.ReadFromJsonAsync<LinkUserToBoardResponse>();
        link.Should().NotBeNull();
        link!.BoardId.Should().Be(1);
        link.UserId.Should().Be(1);
        link.UserRole.Should().Be(UserRoleEnum.OWNER);
    }

    [Fact]
    public async Task GetBoardToUserLink_ReturnsNotFound_WhenLinkMissing()
    {
        var response = await _authorizedClient.GetAsync("/api/boards/1/link/999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
