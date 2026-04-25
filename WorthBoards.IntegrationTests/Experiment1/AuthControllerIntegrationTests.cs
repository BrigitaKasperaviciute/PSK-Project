using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.EntityFrameworkCore;
using WorthBoards.Business.Dtos.Identity;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.IntegrationTests.Experiment1.TestInfrastructure;
using Xunit;

namespace WorthBoards.IntegrationTests.Experiment1;

public class AuthControllerIntegrationTests(IntegrationTestWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    [Fact]
    public async Task RegisterUser_ValidRequest_CreatesUserAndReturnsOk()
    {
        // Arrange
        var client = CreateAnonymousClient();
        var userName = $"auth_happy_{Guid.NewGuid():N}"[..26];
        var request = new UserRegisterRequest(
            FirstName: "Alice",
            LastName: "Tester",
            UserName: userName,
            Email: $"{userName}@example.com",
            Password: "Passw0rd!");

        // Act
        var response = await client.PostAsJsonAsync("/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<UserResponse>();
        body.Should().NotBeNull();
        body!.UserName.Should().Be(request.UserName);
        body.Email.Should().Be(request.Email);

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WorthBoards.Data.Database.ApplicationDbContext>();
        var persistedUser = await dbContext.Users.SingleAsync(u => u.Id == body.Id);
        persistedUser.UserName.Should().Be(request.UserName);
        persistedUser.Email.Should().Be(request.Email);
    }

    [Fact]
    public async Task LoginUser_InvalidPassword_ReturnsUnauthorizedAndDoesNotModifyUsers()
    {
        // Arrange
        var session = await CreateAuthenticatedUserAsync("auth_negative");
        var anonymousClient = CreateAnonymousClient();

        using var arrangeScope = Factory.Services.CreateScope();
        var arrangeDb = arrangeScope.ServiceProvider.GetRequiredService<WorthBoards.Data.Database.ApplicationDbContext>();
        var usersBefore = await arrangeDb.Users.CountAsync();

        // Act
        var response = await anonymousClient.PostAsJsonAsync("/login", new UserLoginRequest(session.UserName, "WrongPass1!"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var bodyText = await response.Content.ReadAsStringAsync();
        bodyText.Should().Contain("Unauthorized");

        using var assertScope = Factory.Services.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<WorthBoards.Data.Database.ApplicationDbContext>();
        var usersAfter = await assertDb.Users.CountAsync();
        usersAfter.Should().Be(usersBefore);
    }

    [Fact]
    public async Task ForgotPassword_AnyEmail_ReturnsOkWithMessage()
    {
        // Arrange
        var client = CreateAnonymousClient();

        // Act
        var response = await client.PostAsJsonAsync("/forgot-password", new ForgotPasswordRequest
        {
            Email = "unknown@example.com"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("password reset link");
    }

    [Fact]
    public async Task ResetPassword_InvalidToken_ReturnsBadRequest()
    {
        // Arrange
        var session = await CreateAuthenticatedUserAsync("auth_reset_negative");
        var client = CreateAnonymousClient();

        // Act
        var response = await client.PostAsJsonAsync("/reset-password", new ResetPasswordRequest
        {
            Email = session.Email,
            ResetCode = "invalid-token",
            NewPassword = "NewPassw0rd!"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("BadRequest");
    }

    [Fact]
    public async Task ChangePassword_ValidCredentials_ReturnsOkAndAllowsNewLogin()
    {
        // Arrange
        var session = await CreateAuthenticatedUserAsync("auth_change_happy");
        var request = new ChangePasswordRequest
        {
            OldPassword = "Passw0rd!",
            NewPassword = "ChangedPassw0rd!"
        };

        // Act
        var response = await session.Client.PostAsJsonAsync("/change-password", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Password change was successfull");

        var reloginClient = CreateAnonymousClient();
        var reloginResponse = await reloginClient.PostAsJsonAsync("/login", new UserLoginRequest(session.UserName, request.NewPassword));
        reloginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task LoginUser_UserDoesNotExist_ReturnsUnauthorized()
    {
        // Arrange
        var client = CreateAnonymousClient();

        // Act
        var response = await client.PostAsJsonAsync("/login", new UserLoginRequest("missing_user", "Passw0rd!"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
