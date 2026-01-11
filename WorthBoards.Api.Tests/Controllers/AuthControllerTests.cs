using System.Net;
using System.Net.Http.Json;
using WorthBoards.Api.Tests.Infrastructure;
using WorthBoards.Business.Dtos.Identity;

namespace WorthBoards.Api.Tests.Controllers;

public class AuthControllerTests : IntegrationTestBase
{
    [Fact]
    public async Task Login_WithValidCredentials_ReturnsToken()
    {
        // Arrange
        await ClearDatabaseAsync();
        var email = "testuser@example.com";
        var password = "TestPassword123!";

        await TestHelpers.CreateTestUserAsync(Factory.Services, email, password);

        var loginRequest = new UserLoginRequest(email, password);

        // Act
        var response = await Client.PostAsJsonAsync("/login", loginRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.NotNull(content);
        Assert.NotEmpty(content);
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ReturnsUnauthorized()
    {
        // Arrange
        await ClearDatabaseAsync();
        var email = "testuser@example.com";
        var password = "TestPassword123!";

        await TestHelpers.CreateTestUserAsync(Factory.Services, email, password);

        var loginRequest = new UserLoginRequest(email, "WrongPassword123!");

        // Act
        var response = await Client.PostAsJsonAsync("/login", loginRequest);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
