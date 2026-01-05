using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using WorthBoards.Api.IntegrationTests.Infrastructure;
using WorthBoards.Business.Dtos.Identity;
using Xunit;

namespace WorthBoards.Api.IntegrationTests;

public class UserControllerTests : IClassFixture<IntegrationTestFactory>
{
    private readonly HttpClient _authorizedClient;

    public UserControllerTests(IntegrationTestFactory factory)
    {
        _authorizedClient = factory.CreateAuthenticatedClient();
    }

    [Fact]
    public async Task GetUserById_ReturnsUser_WhenUserExists()
    {
        var response = await _authorizedClient.GetAsync("/api/users/1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var user = await response.Content.ReadFromJsonAsync<UserResponse>();
        user.Should().NotBeNull();
        user!.Id.Should().Be(1);
        user.UserName.Should().Be("owner");
    }

    [Fact]
    public async Task GetUserById_ReturnsNotFound_ForUnknownUser()
    {
        var response = await _authorizedClient.GetAsync("/api/users/999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
