using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorthBoards.Business.Dtos.Identity;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Data.Identity;
using WorthBoards.Common.Exceptions;
using WorthBoards.IntegrationTests.Infrastructure;
using Xunit;

namespace WorthBoards.IntegrationTests.Controllers;

[Collection(nameof(IntegrationCollection))]
public sealed class AuthControllerTests(IntegrationTestAppFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Login_WithValidCredentials_ReturnsTokenAndKeepsUserPersisted()
    {
        // Arrange
        var user = await Factory.CreateUserAsync("Alice", "Walker", "auth-login");
        var client = Factory.CreateClient();
        var request = new UserLoginRequest(user.UserName!, IntegrationTestAppFactory.DefaultPassword);

        // Act
        var response = await client.PostAsJsonAsync("/login", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<UserLoginResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(user.Id);
        body.UserName.Should().Be(user.UserName);
        body.JwtToken.Should().NotBeNullOrWhiteSpace();

        await Factory.WithDbContextAsync(async db =>
        {
            var persisted = await db.Users.AsNoTracking().SingleAsync(item => item.Id == user.Id);
            persisted.Email.Should().Be(user.Email);
            persisted.UserName.Should().Be(user.UserName);
        });
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ReturnsUnauthorizedAndDoesNotChangeDatabase()
    {
        // Arrange
        var user = await Factory.CreateUserAsync("Bob", "Taylor", "auth-login-invalid");
        var client = Factory.CreateClient();
        var request = new UserLoginRequest(user.UserName!, "WrongPassword123!");

        // Act
        var response = await client.PostAsJsonAsync("/login", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be((int)HttpStatusCode.Unauthorized);

        await Factory.WithDbContextAsync(async db =>
        {
            var persisted = await db.Users.AsNoTracking().SingleAsync(item => item.Id == user.Id);
            persisted.Email.Should().Be(user.Email);
            persisted.UserName.Should().Be(user.UserName);
        });
    }

    [Fact]
    public async Task ChangePassword_WithValidCredentials_ReturnsOkAndRequiresNewPassword()
    {
        // Arrange
        var user = await Factory.CreateUserAsync("Clara", "Mills", "auth-change");
        var client = Factory.CreateAuthenticatedClient(user);
        var request = new ChangePasswordRequest
        {
            OldPassword = IntegrationTestAppFactory.DefaultPassword,
            NewPassword = "NewPassword123!"
        };

        // Act
        var response = await client.PostAsJsonAsync("/change-password", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ChangePasswordResponse>();
        body.Should().NotBeNull();
        body!.Message.Should().NotBeNullOrWhiteSpace();

        var loginWithNewPassword = await Factory.CreateClient().PostAsJsonAsync(
            "/login",
            new UserLoginRequest(user.UserName!, request.NewPassword));
        loginWithNewPassword.StatusCode.Should().Be(HttpStatusCode.OK);

        var loginWithOldPassword = await Factory.CreateClient().PostAsJsonAsync(
            "/login",
            new UserLoginRequest(user.UserName!, IntegrationTestAppFactory.DefaultPassword));
        loginWithOldPassword.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ChangePassword_WithWrongOldPassword_ReturnsBadRequestAndKeepsOldPasswordWorking()
    {
        // Arrange
        var user = await Factory.CreateUserAsync("Dylan", "Cross", "auth-change-invalid");
        var client = Factory.CreateAuthenticatedClient(user);
        var request = new ChangePasswordRequest
        {
            OldPassword = "WrongPassword123!",
            NewPassword = "NewPassword123!"
        };

        // Act
        var response = await client.PostAsJsonAsync("/change-password", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<ErrorDetails>();
        error.Should().NotBeNull();
        error!.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);

        var loginWithOldPassword = await Factory.CreateClient().PostAsJsonAsync(
            "/login",
            new UserLoginRequest(user.UserName!, IntegrationTestAppFactory.DefaultPassword));
        loginWithOldPassword.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}