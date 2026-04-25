using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.Extensions.DependencyInjection;
using WorthBoards.Business.Dtos.Identity;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.IntegrationTests.Infrastructure;
using Xunit;

namespace WorthBoards.IntegrationTests;

public class AuthControllerTests(WorthBoardsWebApplicationFactory factory)
    : IClassFixture<WorthBoardsWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    // ── Register ────────────────────────────────────────────────────────────

    [Fact]
    public async Task RegisterUserAsync_ValidRequest_ReturnsOk()
    {
        // Arrange
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var request = new UserRegisterRequest(
            FirstName: "John",
            LastName: "Doe",
            UserName: $"john_{suffix}",
            Email: $"john_{suffix}@test.com",
            Password: "Test@12345!"
        );

        // Act
        var response = await _client.PostAsJsonAsync("/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UserResponse>();
        body.Should().NotBeNull();
        body!.Email.Should().Be(request.Email);

        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        // DB state – user can now login
        var loginResponse = await _client.PostAsJsonAsync("/login",
            new UserLoginRequest(request.UserName, request.Password));
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RegisterUserAsync_DuplicateEmail_ReturnsBadRequest()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var existingUser = await seeder.CreateUserAsync();

        var request = new UserRegisterRequest(
            FirstName: "Jane",
            LastName: "Doe",
            UserName: $"jane_{Guid.NewGuid():N}",
            Email: existingUser.Email!,   // duplicate email
            Password: "Test@12345!"
        );

        // Act
        var response = await _client.PostAsJsonAsync("/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Login ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task LoginUserAsync_ValidCredentials_ReturnsOkWithToken()
    {
        // Arrange
        var suffix = Guid.NewGuid().ToString("N")[..8];
        await _client.PostAsJsonAsync("/register", new UserRegisterRequest(
            "Alice", "Smith", $"alice_{suffix}", $"alice_{suffix}@test.com", "Test@12345!"));

        // Act
        var response = await _client.PostAsJsonAsync("/login",
            new UserLoginRequest($"alice_{suffix}", "Test@12345!"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UserLoginResponse>();
        body.Should().NotBeNull();
        body!.JwtToken.Should().NotBeNullOrEmpty();
        body.UserName.Should().Be($"alice_{suffix}");
    }

    [Fact]
    public async Task LoginUserAsync_WrongPassword_ReturnsUnauthorized()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var user = await seeder.CreateUserAsync();

        // Act
        var response = await _client.PostAsJsonAsync("/login",
            new UserLoginRequest(user.UserName!, "WrongPassword99!"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LoginUserAsync_NonExistentUser_ReturnsUnauthorized()
    {
        // Arrange – username that has never been registered (NotFoundException path in AuthController)

        // Act
        var response = await _client.PostAsJsonAsync("/login",
            new UserLoginRequest("ghost_user_that_does_not_exist", "Test@12345!"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Forgot Password ──────────────────────────────────────────────────────

    [Fact]
    public async Task ForgotPassword_RegisteredEmail_ReturnsOk()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var user = await seeder.CreateUserAsync();

        // Act
        var response = await _client.PostAsJsonAsync("/forgot-password",
            new ForgotPasswordRequest { Email = user.Email! });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ForgotPassword_UnknownEmail_ReturnsOk()
    {
        // Arrange – non-existent email (returns OK for security – no enumeration)

        // Act
        var response = await _client.PostAsJsonAsync("/forgot-password",
            new ForgotPasswordRequest { Email = "nobody@nowhere.com" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── Reset Password ──────────────────────────────────────────────────────

    [Fact]
    public async Task ResetPassword_ValidToken_ReturnsOk()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var user = await seeder.CreateUserAsync();

        // Generate a real Identity password-reset token via UserManager
        var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<WorthBoards.Data.Identity.ApplicationUser>>();
        var token = await userManager.GeneratePasswordResetTokenAsync(user);

        var request = new ResetPasswordRequest
        {
            Email = user.Email!,
            ResetCode = token,
            NewPassword = "NewPassword@1!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/reset-password", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── Change Password ──────────────────────────────────────────────────────

    [Fact]
    public async Task ChangePassword_AuthenticatedUserValidPasswords_ReturnsOk()
    {
        // Arrange
        var suffix = Guid.NewGuid().ToString("N")[..8];
        await _client.PostAsJsonAsync("/register", new UserRegisterRequest(
            "Bob", "Brown", $"bob_{suffix}", $"bob_{suffix}@test.com", "OldPass@1!"));

        var loginResp = await _client.PostAsJsonAsync("/login",
            new UserLoginRequest($"bob_{suffix}", "OldPass@1!"));
        var loginBody = await loginResp.Content.ReadFromJsonAsync<UserLoginResponse>();

        var authed = factory.CreateClient();
        authed.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", loginBody!.JwtToken);

        // Act
        var response = await authed.PostAsJsonAsync("/change-password",
            new ChangePasswordRequest { OldPassword = "OldPass@1!", NewPassword = "NewPass@2!" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ChangePassword_Unauthenticated_ReturnsUnauthorized()
    {
        // Arrange – no auth header

        // Act
        var response = await _client.PostAsJsonAsync("/change-password",
            new ChangePasswordRequest { OldPassword = "OldPass@1!", NewPassword = "NewPass@2!" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
