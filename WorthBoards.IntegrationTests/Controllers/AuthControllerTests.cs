using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WorthBoards.Business.Dtos.Identity;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.IntegrationTests.Infrastructure;

namespace WorthBoards.IntegrationTests.Controllers;

public class AuthControllerTests(CustomWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    // ── Register ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Register_ValidRequest_ReturnsOkWithUserResponse()
    {
        // Arrange
        var id = Guid.NewGuid().ToString("N")[..8];
        var request = new UserRegisterRequest(
            FirstName: "Jane",
            LastName: "Doe",
            UserName: $"jane_{id}",
            Email: $"jane_{id}@test.com",
            Password: "Password1!"
        );

        // Act
        var response = await Client.PostAsJsonAsync("/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<UserResponse>();
        body.Should().NotBeNull();
        body!.UserName.Should().Be($"jane_{id}");
        body.Email.Should().Be($"jane_{id}@test.com");

        var dbUser = await QueryAsync(db =>
            db.Users.FirstOrDefaultAsync(u => u.UserName == $"jane_{id}"));
        dbUser.Should().NotBeNull();
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsBadRequest()
    {
        // Arrange
        var id = Guid.NewGuid().ToString("N")[..8];
        var request = new UserRegisterRequest(
            FirstName: "Dup",
            LastName: "User",
            UserName: $"dup_{id}",
            Email: $"dup_{id}@test.com",
            Password: "Password1!"
        );
        await Client.PostAsJsonAsync("/register", request);

        var duplicate = request with { UserName = $"dup2_{id}" };

        // Act
        var response = await Client.PostAsJsonAsync("/register", duplicate);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Login ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_ValidCredentials_ReturnsOkWithJwtToken()
    {
        // Arrange
        var (_, _) = await RegisterAndLoginAsync();

        var id = Guid.NewGuid().ToString("N")[..8];
        var registerReq = new UserRegisterRequest(
            FirstName: "Login",
            LastName: "Test",
            UserName: $"logintest_{id}",
            Email: $"logintest_{id}@test.com",
            Password: "Password1!"
        );
        await Client.PostAsJsonAsync("/register", registerReq);
        var loginReq = new UserLoginRequest($"logintest_{id}", "Password1!");

        // Act
        var response = await Client.PostAsJsonAsync("/login", loginReq);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<UserLoginResponse>();
        body.Should().NotBeNull();
        body!.JwtToken.Should().NotBeNullOrWhiteSpace();
        body.UserName.Should().Be($"logintest_{id}");
        body.Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsUnauthorized()
    {
        // Arrange
        var id = Guid.NewGuid().ToString("N")[..8];
        var registerReq = new UserRegisterRequest(
            FirstName: "Wrong",
            LastName: "Pass",
            UserName: $"wrongpass_{id}",
            Email: $"wrongpass_{id}@test.com",
            Password: "Password1!"
        );
        await Client.PostAsJsonAsync("/register", registerReq);
        var loginReq = new UserLoginRequest($"wrongpass_{id}", "BadPassword9!");

        // Act
        var response = await Client.PostAsJsonAsync("/login", loginReq);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── ChangePassword ────────────────────────────────────────────────────────

    [Fact]
    public async Task ChangePassword_AuthenticatedUserWithCorrectOldPassword_ReturnsOk()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);
        var request = new ChangePasswordRequest
        {
            OldPassword = "Password1!",
            NewPassword = "NewPassword2@"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/change-password", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ChangePassword_UnauthenticatedUser_ReturnsUnauthorized()
    {
        // Arrange
        ClearAuthToken();
        var request = new ChangePasswordRequest
        {
            OldPassword = "Password1!",
            NewPassword = "NewPassword2@"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/change-password", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
