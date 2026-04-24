using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity.Data;
using Xunit;
using Moq;
using WorthBoards.Business.Dtos.Identity;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Common.Exceptions.Custom;
using WorthBoards.IntegrationTests.Infrastructure;

namespace WorthBoards.IntegrationTests.Tests;

public class AuthControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AuthControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetAllMocks();
    }

    private HttpClient CreateClient() => _factory.CreateClient();

    private static StringContent Json(object body) =>
        new(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

    // ── Register ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Register_ValidRequest_ReturnsOk()
    {
        var expected = new UserResponse { Id = 1, UserName = "alice", Email = "alice@test.com", FirstName = "Alice", LastName = "Smith", CreationDate = DateTime.UtcNow };
        _factory.AuthServiceMock
            .Setup(s => s.RegisterUserAsync(It.IsAny<UserRegisterRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var response = await CreateClient().PostAsync("/register",
            Json(new { firstName = "Alice", lastName = "Smith", userName = "alice", email = "alice@test.com", password = "Pass@123" }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsBadRequest()
    {
        _factory.AuthServiceMock
            .Setup(s => s.RegisterUserAsync(It.IsAny<UserRegisterRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BadRequestException("Email already registered."));

        var response = await CreateClient().PostAsync("/register",
            Json(new { firstName = "Alice", lastName = "Smith", userName = "alice", email = "alice@test.com", password = "Pass@123" }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── Login ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_ValidCredentials_ReturnsOk()
    {
        _factory.AuthServiceMock
            .Setup(s => s.LoginUserAsync(It.IsAny<UserLoginRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserLoginResponse(1, "alice", "jwt-token"));

        var response = await CreateClient().PostAsync("/login",
            Json(new { userName = "alice", password = "Pass@123" }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_InvalidCredentials_ReturnsUnauthorized()
    {
        _factory.AuthServiceMock
            .Setup(s => s.LoginUserAsync(It.IsAny<UserLoginRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedException("Invalid credentials."));

        var response = await CreateClient().PostAsync("/login",
            Json(new { userName = "alice", password = "wrong" }));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_UserNotFound_ReturnsUnauthorized()
    {
        _factory.AuthServiceMock
            .Setup(s => s.LoginUserAsync(It.IsAny<UserLoginRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("User not found."));

        var response = await CreateClient().PostAsync("/login",
            Json(new { userName = "ghost", password = "Pass@123" }));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_UnhandledException_ReturnsProblem()
    {
        _factory.AuthServiceMock
            .Setup(s => s.LoginUserAsync(It.IsAny<UserLoginRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Unexpected error."));

        var response = await CreateClient().PostAsync("/login",
            Json(new { userName = "alice", password = "Pass@123" }));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }


    // ── ForgotPassword ────────────────────────────────────────────────────

    [Fact]
    public async Task ForgotPassword_KnownEmail_ReturnsOk()
    {
        _factory.AuthServiceMock
            .Setup(s => s.ForgotPasswordAsync(It.IsAny<ForgotPasswordRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PasswordRecoveryResponse("Reset email sent."));

        var response = await CreateClient().PostAsync("/forgot-password",
            Json(new { email = "alice@test.com" }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ForgotPassword_ServiceThrows_ReturnsInternalServerError()
    {
        _factory.AuthServiceMock
            .Setup(s => s.ForgotPasswordAsync(It.IsAny<ForgotPasswordRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Unexpected error"));

        var response = await CreateClient().PostAsync("/forgot-password",
            Json(new { email = "alice@test.com" }));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    // ── ResetPassword ─────────────────────────────────────────────────────

    [Fact]
    public async Task ResetPassword_ValidToken_ReturnsOk()
    {
        _factory.AuthServiceMock
            .Setup(s => s.ResetPasswordAsync(It.IsAny<ResetPasswordRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PasswordRecoveryResponse("Password reset successfully."));

        var response = await CreateClient().PostAsync("/reset-password",
            Json(new { email = "alice@test.com", resetCode = "valid-token", newPassword = "NewPass@123" }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_InvalidToken_ReturnsBadRequest()
    {
        _factory.AuthServiceMock
            .Setup(s => s.ResetPasswordAsync(It.IsAny<ResetPasswordRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BadRequestException("Invalid reset credentials."));

        var response = await CreateClient().PostAsync("/reset-password",
            Json(new { email = "alice@test.com", resetCode = "bad-token", newPassword = "NewPass@123" }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── ChangePassword ────────────────────────────────────────────────────

    [Fact]
    public async Task ChangePassword_Authenticated_ReturnsOk()
    {
        _factory.AuthServiceMock
            .Setup(s => s.ChangePasswordAsync(It.IsAny<ChangePasswordRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChangePasswordResponse("Password changed."));

        var client = CreateClient().WithAuth();
        var response = await client.PostAsync("/change-password",
            Json(new { oldPassword = "Old@123", newPassword = "New@123" }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_NoToken_ReturnsUnauthorized()
    {
        var response = await CreateClient().PostAsync("/change-password",
            Json(new { oldPassword = "Old@123", newPassword = "New@123" }));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}