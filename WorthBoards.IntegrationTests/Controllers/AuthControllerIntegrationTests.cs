using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using WorthBoards.Business.Dtos.Identity;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.IntegrationTests.Infrastructure;

namespace WorthBoards.IntegrationTests.Controllers;

public class AuthControllerIntegrationTests : IntegrationTestBase
{
    [Fact]
    public async Task RegisterUserAsync_ValidRequest_ReturnsOkWithUserData()
    {
        // Arrange
        await ResetDatabaseAsync();
        var registerRequest = TestDataBuilder.CreateUserRegisterRequest();

        // Act
        var response = await HttpClient.PostAsJsonAsync("/register", registerRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<UserResponse>();
        result.Should().NotBeNull();
        result!.Id.Should().BeGreaterThan(0);
        result.UserName.Should().Be(registerRequest.UserName);
        result.Email.Should().Be(registerRequest.Email);
    }

    [Fact]
    public async Task RegisterUserAsync_InvalidEmail_ReturnsBadRequest()
    {
        // Arrange
        await ResetDatabaseAsync();
        var registerRequest = TestDataBuilder.CreateUserRegisterRequest(email: "invalid-email");

        // Act
        var response = await HttpClient.PostAsJsonAsync("/register", registerRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RegisterUserAsync_DuplicateUserName_ReturnsBadRequest()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userName = "uniqueuser123";
        var request1 = TestDataBuilder.CreateUserRegisterRequest(userName: userName);
        var request2 = TestDataBuilder.CreateUserRegisterRequest(userName: userName);

        // Act
        var response1 = await HttpClient.PostAsJsonAsync("/register", request1);
        var response2 = await HttpClient.PostAsJsonAsync("/register", request2);

        // Assert
        response1.StatusCode.Should().Be(HttpStatusCode.OK);
        response2.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RegisterUserAsync_WeakPassword_ReturnsBadRequest()
    {
        // Arrange
        await ResetDatabaseAsync();
        var registerRequest = new UserRegisterRequest(
            FirstName: "Test",
            LastName: "User",
            UserName: "testuser",
            Email: "test@example.com",
            Password: "weak"
        );

        // Act
        var response = await HttpClient.PostAsJsonAsync("/register", registerRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task LoginUserAsync_ValidCredentials_ReturnsOkWithToken()
    {
        // Arrange
        await ResetDatabaseAsync();
        var registerRequest = TestDataBuilder.CreateUserRegisterRequest();
        await HttpClient.PostAsJsonAsync("/register", registerRequest);

        var loginRequest = TestDataBuilder.CreateUserLoginRequest(
            registerRequest.UserName,
            registerRequest.Password
        );

        // Act
        var response = await HttpClient.PostAsJsonAsync("/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<WorthBoards.Business.Dtos.Identity.UserLoginResponse>();
        result.Should().NotBeNull();
        result!.Id.Should().BeGreaterThan(0);
        result.JwtToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task LoginUserAsync_InvalidPassword_ReturnsUnauthorized()
    {
        // Arrange
        await ResetDatabaseAsync();
        var registerRequest = TestDataBuilder.CreateUserRegisterRequest();
        await HttpClient.PostAsJsonAsync("/register", registerRequest);

        var loginRequest = TestDataBuilder.CreateUserLoginRequest(
            registerRequest.UserName,
            "WrongPassword123!"
        );

        // Act
        var response = await HttpClient.PostAsJsonAsync("/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LoginUserAsync_NonExistentUser_ReturnsUnauthorized()
    {
        // Arrange
        await ResetDatabaseAsync();
        var loginRequest = TestDataBuilder.CreateUserLoginRequest("nonexistent", "Password123!");

        // Act
        var response = await HttpClient.PostAsJsonAsync("/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ChangePassword_ValidRequest_ReturnsOk()
    {
        // Arrange
        await ResetDatabaseAsync();
        var registerRequest = TestDataBuilder.CreateUserRegisterRequest();
        var helper = new TestHelper(HttpClient, this);
        var (_, token, _) = await helper.CreateAndLoginUserAsync();

        SetBearerToken(token);

        var changePasswordRequest = new ChangePasswordRequest(registerRequest.Password, "NewPassword123!");

        // Act
        var response = await HttpClient.PostAsJsonAsync("/change-password", changePasswordRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ChangePassword_IncorrectOldPassword_ReturnsBadRequest()
    {
        // Arrange
        await ResetDatabaseAsync();
        var registerRequest = TestDataBuilder.CreateUserRegisterRequest();
        var helper = new TestHelper(HttpClient, this);
        var (_, token, _) = await helper.CreateAndLoginUserAsync();

        SetBearerToken(token);

        var changePasswordRequest = new ChangePasswordRequest("WrongPassword123!", "NewPassword123!");

        // Act
        var response = await HttpClient.PostAsJsonAsync("/change-password", changePasswordRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ChangePassword_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        await ResetDatabaseAsync();
        var changePasswordRequest = new ChangePasswordRequest("OldPassword123!", "NewPassword123!");

        // Act
        var response = await HttpClient.PostAsJsonAsync("/change-password", changePasswordRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ForgotPassword_ValidEmail_ReturnsOk()
    {
        // Arrange
        await ResetDatabaseAsync();
        var registerRequest = TestDataBuilder.CreateUserRegisterRequest();
        await HttpClient.PostAsJsonAsync("/register", registerRequest);

        var forgotPasswordRequest = new ForgotPasswordRequest(registerRequest.Email);

        // Act
        var response = await HttpClient.PostAsJsonAsync("/forgot-password", forgotPasswordRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ForgotPassword_InvalidEmail_ReturnsBadRequest()
    {
        // Arrange
        await ResetDatabaseAsync();
        var forgotPasswordRequest = new { Email = (string?)null };

        // Act
        var response = await HttpClient.PostAsJsonAsync("/forgot-password", forgotPasswordRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ResetPassword_ValidToken_ReturnsOk()
    {
        // Arrange
        await ResetDatabaseAsync();
        var registerRequest = TestDataBuilder.CreateUserRegisterRequest();
        await HttpClient.PostAsJsonAsync("/register", registerRequest);

        using var scope = Factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<WorthBoards.Data.Identity.ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(registerRequest.Email);
        var resetToken = await userManager.GeneratePasswordResetTokenAsync(user!);

        var resetPasswordRequest = new ResetPasswordRequest(registerRequest.Email, resetToken, "NewResetPassword123!");

        // Act
        var response = await HttpClient.PostAsJsonAsync("/reset-password", resetPasswordRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ResetPassword_InvalidToken_ReturnsBadRequest()
    {
        // Arrange
        await ResetDatabaseAsync();
        var registerRequest = TestDataBuilder.CreateUserRegisterRequest();
        await HttpClient.PostAsJsonAsync("/register", registerRequest);

        var resetPasswordRequest = new ResetPasswordRequest(registerRequest.Email, "invalid-token", "NewResetPassword123!");

        // Act
        var response = await HttpClient.PostAsJsonAsync("/reset-password", resetPasswordRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}

public record ChangePasswordRequest(string OldPassword, string NewPassword);
public record ForgotPasswordRequest(string Email);
public record ResetPasswordRequest(string Email, string ResetCode, string NewPassword);
