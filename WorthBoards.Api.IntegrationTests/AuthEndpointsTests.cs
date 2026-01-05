using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using WorthBoards.Api.IntegrationTests.Support;
using WorthBoards.Business.Dtos.Identity;
using Xunit;

namespace WorthBoards.Api.IntegrationTests;

public class AuthEndpointsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AuthEndpointsTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task RegisterUser_ReturnsOk_WhenPayloadIsValid()
    {
        var request = new
        {
            FirstName = "New",
            LastName = "User",
            UserName = "newuser",
            Email = "newuser@test.local",
            Password = "Str0ngP@ssword!"
        };

        var response = await _client.PostAsJsonAsync("/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UserResponse>();
        body.Should().NotBeNull();
        body!.Email.Should().Be(request.Email);
        body.UserName.Should().Be(request.UserName);
    }

    [Fact]
    public async Task RegisterUser_ReturnsBadRequest_WhenEmailAlreadyExists()
    {
        var request = new
        {
            FirstName = "Owner",
            LastName = "One",
            UserName = "owner",
            Email = "owner@test.local",
            Password = "Str0ngP@ssword!"
        };

        var response = await _client.PostAsJsonAsync("/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task LoginUser_ReturnsToken_WhenCredentialsAreValid()
    {
        var request = new UserLoginRequest("owner", "Str0ngP@ssword!");

        var response = await _client.PostAsJsonAsync("/login", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UserLoginResponse>();
        body.Should().NotBeNull();
        body!.JwtToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task LoginUser_ReturnsUnauthorized_WhenPasswordIsWrong()
    {
        var request = new UserLoginRequest("owner", "WrongP@ss!1");

        var response = await _client.PostAsJsonAsync("/login", request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
