using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using WorthBoards.Api.IntegrationTests.Infrastructure;
using WorthBoards.Business.Dtos.Identity;
using Xunit;

namespace WorthBoards.Api.IntegrationTests;

public class AuthControllerTests : IClassFixture<IntegrationTestFactory>
{
    private readonly IntegrationTestFactory _factory;

    public AuthControllerTests(IntegrationTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Login_ReturnsToken_ForValidCredentials()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/login", new UserLoginRequest("owner", IntegrationTestFactory.DefaultPassword));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<UserLoginResponse>();
        payload.Should().NotBeNull();
        payload!.UserName.Should().Be("owner");
        payload.JwtToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Login_ReturnsUnauthorized_ForInvalidPassword()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/login", new UserLoginRequest("owner", "wrong-password"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
