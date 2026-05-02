using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.Extensions.DependencyInjection;
using WorthBoards.Business.Dtos.Identity;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Data.Database;
using WorthBoards.Data.Identity;
using WorthBoards.IntegrationTests.Infrastructure;

namespace WorthBoards.IntegrationTests.Controllers;

public class AuthControllerTests(CustomWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    // ── Register ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Register_ValidRequest_ReturnsUserResponse()
    {
        // Arrange
        var id = Guid.NewGuid().ToString("N")[..10];
        var request = new UserRegisterRequest(
            FirstName: "Alice",
            LastName: "Smith",
            UserName: $"alice{id}",
            Email: $"alice{id}@example.com",
            Password: "Alice@12345"
        );
        var client = Factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var user = await response.Content.ReadFromJsonAsync<UserResponse>();
        user.Should().NotBeNull();
        user!.UserName.Should().Be(request.UserName);
        user.Email.Should().Be(request.Email);

        using var db = GetDbContext();
        db.Users.Any(u => u.UserName == request.UserName).Should().BeTrue();
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsBadRequest()
    {
        // Arrange
        var (_, _, existingClient) = await RegisterAndLoginAsync();
        var id = Guid.NewGuid().ToString("N")[..10];

        // Register same email with different username
        var (userId, _, _) = await RegisterAndLoginAsync();
        using var db = GetDbContext();
        var existingUser = db.Users.Find(userId)!;

        var duplicateRequest = new UserRegisterRequest(
            FirstName: "Bob",
            LastName: "Jones",
            UserName: $"bob{id}",
            Email: existingUser.Email!,
            Password: "Bob@12345"
        );
        var client = Factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/register", duplicateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Login ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_ValidCredentials_ReturnsJwtToken()
    {
        // Arrange
        var id = Guid.NewGuid().ToString("N")[..10];
        var registerRequest = new UserRegisterRequest(
            FirstName: "Carol",
            LastName: "White",
            UserName: $"carol{id}",
            Email: $"carol{id}@example.com",
            Password: "Carol@12345"
        );
        var client = Factory.CreateClient();
        await client.PostAsJsonAsync("/register", registerRequest);

        var loginRequest = new UserLoginRequest(registerRequest.UserName, registerRequest.Password);

        // Act
        var response = await client.PostAsJsonAsync("/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var loginResult = await response.Content.ReadFromJsonAsync<UserLoginResponse>();
        loginResult.Should().NotBeNull();
        loginResult!.JwtToken.Should().NotBeNullOrEmpty();
        loginResult.UserName.Should().Be(registerRequest.UserName);
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsUnauthorized()
    {
        // Arrange
        var id = Guid.NewGuid().ToString("N")[..10];
        var registerRequest = new UserRegisterRequest(
            FirstName: "Dave",
            LastName: "Brown",
            UserName: $"dave{id}",
            Email: $"dave{id}@example.com",
            Password: "Dave@12345"
        );
        var client = Factory.CreateClient();
        await client.PostAsJsonAsync("/register", registerRequest);

        var loginRequest = new UserLoginRequest(registerRequest.UserName, "WrongPass@1");

        // Act
        var response = await client.PostAsJsonAsync("/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_NonExistentUser_ReturnsUnauthorized()
    {
        // Arrange
        var client = Factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/login",
            new UserLoginRequest("nobody_xyz_not_registered", "Password@1"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Forgot Password ──────────────────────────────────────────────────────

    [Fact]
    public async Task ForgotPassword_RegisteredEmail_ReturnsOk()
    {
        // Arrange
        var (_, _, _) = await RegisterAndLoginAsync();
        var id = Guid.NewGuid().ToString("N")[..10];
        var registerRequest = new UserRegisterRequest(
            "Eve", "Green", $"eve{id}", $"eve{id}@example.com", "Eve@12345"
        );
        var anonClient = Factory.CreateClient();
        await anonClient.PostAsJsonAsync("/register", registerRequest);

        // Act
        var response = await anonClient.PostAsJsonAsync("/forgot-password",
            new ForgotPasswordRequest { Email = registerRequest.Email });

        // Assert — always returns 200 to avoid user enumeration
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ForgotPassword_UnknownEmail_StillReturnsOk()
    {
        // Arrange
        var client = Factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/forgot-password",
            new ForgotPasswordRequest { Email = "nobody@example.com" });

        // Assert — must not leak whether email exists
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── Change Password ──────────────────────────────────────────────────────

    [Fact]
    public async Task ChangePassword_ValidOldPassword_ReturnsOk()
    {
        // Arrange
        var id = Guid.NewGuid().ToString("N")[..10];
        var registerRequest = new UserRegisterRequest(
            "Frank", "Black", $"frank{id}", $"frank{id}@example.com", "Frank@12345"
        );
        var client = Factory.CreateClient();
        await client.PostAsJsonAsync("/register", registerRequest);
        var loginResult = (await (await client.PostAsJsonAsync("/login",
            new UserLoginRequest(registerRequest.UserName, registerRequest.Password)))
            .Content.ReadFromJsonAsync<UserLoginResponse>())!;

        var authClient = CreateClient(loginResult.JwtToken);
        var changePasswordRequest = new ChangePasswordRequest
        {
            OldPassword = registerRequest.Password,
            NewPassword = "NewFrank@99999"
        };

        // Act
        var response = await authClient.PostAsJsonAsync("/change-password", changePasswordRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ChangePassword_Unauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var client = Factory.CreateClient();
        var changePasswordRequest = new ChangePasswordRequest
        {
            OldPassword = "OldPass@1",
            NewPassword = "NewPass@1"
        };

        // Act
        var response = await client.PostAsJsonAsync("/change-password", changePasswordRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Reset Password ───────────────────────────────────────────────────────

    [Fact]
    public async Task ResetPassword_ValidToken_ReturnsOk()
    {
        // Arrange
        var id = Guid.NewGuid().ToString("N")[..10];
        var registerRequest = new UserRegisterRequest(
            "Reset", "User", $"reset{id}", $"reset{id}@example.com", "Reset@12345"
        );
        var client = Factory.CreateClient();
        await client.PostAsJsonAsync("/register", registerRequest);

        // Generate a valid reset token directly via UserManager
        using var scope = Factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(registerRequest.Email);
        var token = await userManager.GeneratePasswordResetTokenAsync(user!);

        // Act
        var response = await client.PostAsJsonAsync("/reset-password", new ResetPasswordRequest
        {
            Email = registerRequest.Email,
            ResetCode = token,
            NewPassword = "NewReset@12345"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ResetPassword_InvalidToken_ReturnsBadRequest()
    {
        // Arrange
        var id = Guid.NewGuid().ToString("N")[..10];
        var registerRequest = new UserRegisterRequest(
            "BadToken", "User", $"badtoken{id}", $"badtoken{id}@example.com", "BadToken@12345"
        );
        var client = Factory.CreateClient();
        await client.PostAsJsonAsync("/register", registerRequest);

        // Act
        var response = await client.PostAsJsonAsync("/reset-password", new ResetPasswordRequest
        {
            Email = registerRequest.Email,
            ResetCode = "completely-invalid-token",
            NewPassword = "NewBadToken@12345"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
