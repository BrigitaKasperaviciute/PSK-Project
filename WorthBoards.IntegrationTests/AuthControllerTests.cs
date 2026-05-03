using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.EntityFrameworkCore;
using WorthBoards.Business.Dtos.Identity;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Common.Exceptions;
using WorthBoards.Data.Database;
using WorthBoards.Data.Identity;
using WorthBoards.IntegrationTests.Infrastructure;

namespace WorthBoards.IntegrationTests;

public sealed class AuthControllerTests(WorthBoardsApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Login_WithValidCredentials_ReturnsJwtAndAllowsPasswordChange()
    {
        // Arrange
        var client = CreateClient();

        // Act
        var loginResponse = await client.PostAsJsonAsync("/login", new UserLoginRequest("owner", IntegrationTestSeed.Password));

        // Assert
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var loginBody = await ReadJsonAsync<UserLoginResponse>(loginResponse);
        loginBody.Should().NotBeNull();
        loginBody!.UserName.Should().Be("owner");
        loginBody.JwtToken.Should().NotBeNullOrWhiteSpace();

        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginBody.JwtToken);
        var changeResponse = await client.PostAsJsonAsync("/change-password", new ChangePasswordRequest
        {
            OldPassword = IntegrationTestSeed.Password,
            NewPassword = "N3wP@ssword1!"
        });

        changeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var changeBody = await ReadJsonAsync<ChangePasswordResponse>(changeResponse);
        changeBody.Should().NotBeNull();
        changeBody!.Message.Should().ContainEquivalentOf("success");

        var newLoginResponse = await CreateClient().PostAsJsonAsync("/login", new UserLoginRequest("owner", "N3wP@ssword1!"));
        newLoginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var oldLoginResponse = await CreateClient().PostAsJsonAsync("/login", new UserLoginRequest("owner", IntegrationTestSeed.Password));
        oldLoginResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ChangePassword_WithWrongCurrentPassword_ReturnsBadRequestAndLeavesCredentialsUnchanged()
    {
        // Arrange
        var client = await CreateAuthenticatedClientAsync("owner");

        // Act
        var response = await client.PostAsJsonAsync("/change-password", new ChangePasswordRequest
        {
            OldPassword = "WrongP@ssword1!",
            NewPassword = "N3wP@ssword2!"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await ReadJsonAsync<ErrorDetails>(response);
        error.Should().NotBeNull();
        error!.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);

        var loginResponse = await CreateClient().PostAsJsonAsync("/login", new UserLoginRequest("owner", IntegrationTestSeed.Password));
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var newPasswordLogin = await CreateClient().PostAsJsonAsync("/login", new UserLoginRequest("owner", "N3wP@ssword2!"));
        newPasswordLogin.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ReturnsUnauthorized()
    {
        // Arrange
        var client = CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/login", new UserLoginRequest("owner", "WrongPassword123!"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync()).Should().Contain("\"status\":401");

        var validLoginResponse = await client.PostAsJsonAsync("/login", new UserLoginRequest("owner", IntegrationTestSeed.Password));
        validLoginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_WithNonexistentUser_ReturnsUnauthorized()
    {
        // Arrange
        var client = CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/login", new UserLoginRequest("nonexistent", "Password123!"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync()).Should().Contain("\"status\":401");

        var validLoginResponse = await client.PostAsJsonAsync("/login", new UserLoginRequest("owner", IntegrationTestSeed.Password));
        validLoginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ResetPassword_WithValidToken_UpdatesPassword()
    {
        // Arrange
        var client = CreateClient();
        var resetCode = await GenerateResetTokenAsync("owner");
        var request = new ResetPasswordRequest
        {
            Email = "owner@example.com",
            ResetCode = resetCode,
            NewPassword = "ResetP@ss1!"
        };

        // Act
        var response = await client.PostAsJsonAsync("/reset-password", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await ReadJsonAsync<PasswordRecoveryResponse>(response);
        body.Should().NotBeNull();
        body!.Message.Should().NotBeNullOrWhiteSpace();

        var newLoginResponse = await client.PostAsJsonAsync("/login", new UserLoginRequest("owner", request.NewPassword));
        newLoginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var oldLoginResponse = await client.PostAsJsonAsync("/login", new UserLoginRequest("owner", IntegrationTestSeed.Password));
        oldLoginResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
    [Fact]
    public async Task ResetPassword_WithInvalidToken_ReturnsBadRequestAndKeepsOldPassword()
    {
        // Arrange
        var client = CreateClient();
        var request = new ResetPasswordRequest
        {
            Email = "owner@example.com",
            ResetCode = "invalid-reset-code",
            NewPassword = "ResetP@ss2!"
        };

        // Act
        var response = await client.PostAsJsonAsync("/reset-password", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await ReadJsonAsync<ErrorDetails>(response);
        error.Should().NotBeNull();
        error!.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
        error.Details.Should().NotBeNullOrWhiteSpace();

        var oldLoginResponse = await client.PostAsJsonAsync("/login", new UserLoginRequest("owner", IntegrationTestSeed.Password));
        oldLoginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var newLoginResponse = await client.PostAsJsonAsync("/login", new UserLoginRequest("owner", request.NewPassword));
        newLoginResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<string> GenerateResetTokenAsync(string userName)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByNameAsync(userName);
        user.Should().NotBeNull();

        return await userManager.GeneratePasswordResetTokenAsync(user!);
    }
}