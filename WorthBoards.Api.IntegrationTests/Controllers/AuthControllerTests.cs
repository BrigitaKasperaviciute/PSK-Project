using System.Net;
using System.Net.Http.Json;
using WorthBoards.Business.Dtos.Identity;
using Xunit;

namespace WorthBoards.Api.IntegrationTests.Controllers;

public class AuthControllerTests : IntegrationTestBase
{
    public AuthControllerTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsToken()
    {
        // Arrange
        var client = CreateUnauthenticatedClient();

        // First register a user
        var registerRequest = new UserRegisterRequest(
            "Test",
            "User",
            "testuser",
            "testuser@example.com",
            "Test123!@#"
        );
        await client.PostAsJsonAsync("/register", registerRequest);

        var loginRequest = new UserLoginRequest(
            "testuser",
            "Test123!@#"
        );

        // Act
        var response = await client.PostAsJsonAsync("/login", loginRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<UserLoginResponse>();
        Assert.NotNull(result);
        Assert.NotNull(result.JwtToken);
        Assert.NotEmpty(result.JwtToken);
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ReturnsUnauthorized()
    {
        // Arrange
        var client = CreateUnauthenticatedClient();

        // First register a user
        var registerRequest = new UserRegisterRequest(
            "Test",
            "User",
            "testuser2",
            "testuser2@example.com",
            "Test123!@#"
        );
        await client.PostAsJsonAsync("/register", registerRequest);

        var loginRequest = new UserLoginRequest(
            "testuser2",
            "WrongPassword123"
        );

        // Act
        var response = await client.PostAsJsonAsync("/login", loginRequest);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
