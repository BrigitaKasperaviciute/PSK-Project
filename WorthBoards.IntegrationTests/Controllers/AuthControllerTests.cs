using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using WorthBoards.Business.Dtos.Identity;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.IntegrationTests.Infrastructure;

namespace WorthBoards.IntegrationTests.Controllers;

public class AuthControllerTests : IntegrationTestBase
{
    public AuthControllerTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Register_ValidRequest_ReturnsOk()
    {
        var suffix = Guid.NewGuid().ToString("N")[..10];
        var request = new UserRegisterRequest("John", "Doe", $"john_{suffix}", $"john_{suffix}@test.com", "Test@1234!");

        var response = await CreateAnonymousClient().PostAsJsonAsync("/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UserResponse>();
        body!.UserName.Should().Be($"john_{suffix}");
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsBadRequest()
    {
        var suffix = Guid.NewGuid().ToString("N")[..10];
        var email = $"dup_{suffix}@test.com";
        var first = new UserRegisterRequest("A", "B", $"user1_{suffix}", email, "Test@1234!");
        var second = new UserRegisterRequest("C", "D", $"user2_{suffix}", email, "Test@1234!");

        var client = CreateAnonymousClient();
        await client.PostAsJsonAsync("/register", first);
        var response = await client.PostAsJsonAsync("/register", second);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsOkWithToken()
    {
        var (token, userId) = await RegisterAndLoginAsync();

        token.Should().NotBeNullOrEmpty();
        userId.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Login_InvalidPassword_ReturnsUnauthorized()
    {
        var suffix = Guid.NewGuid().ToString("N")[..10];
        var username = $"u_{suffix}";
        var registerRequest = new UserRegisterRequest("T", "U", username, $"{username}@test.com", "Test@1234!");
        await CreateAnonymousClient().PostAsJsonAsync("/register", registerRequest);

        var loginRequest = new UserLoginRequest(username, "WrongPass!999");
        var response = await CreateAnonymousClient().PostAsJsonAsync("/login", loginRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_NonExistentUser_ReturnsUnauthorized()
    {
        var loginRequest = new UserLoginRequest("nobody_xyz_9999", "Test@1234!");
        var response = await CreateAnonymousClient().PostAsJsonAsync("/login", loginRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ForgotPassword_AnyEmail_ReturnsOk()
    {
        var body = new { Email = "nonexistent@test.com" };
        var response = await CreateAnonymousClient().PostAsJsonAsync("/forgot-password", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ChangePassword_ValidOldPassword_ReturnsOk()
    {
        var (token, _) = await RegisterAndLoginAsync();
        var client = CreateAuthenticatedClient(token);

        var request = new ChangePasswordRequest { OldPassword = "Test@1234!", NewPassword = "NewPass@5678!" };
        var response = await client.PostAsJsonAsync("/change-password", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ChangePassword_WrongOldPassword_ReturnsBadRequest()
    {
        var (token, _) = await RegisterAndLoginAsync();
        var client = CreateAuthenticatedClient(token);

        var request = new ChangePasswordRequest { OldPassword = "WrongOld@1!", NewPassword = "NewPass@5678!" };
        var response = await client.PostAsJsonAsync("/change-password", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ChangePassword_Unauthenticated_ReturnsUnauthorized()
    {
        var request = new ChangePasswordRequest { OldPassword = "Test@1234!", NewPassword = "New@5678!" };
        var response = await CreateAnonymousClient().PostAsJsonAsync("/change-password", request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ResetPassword_ValidToken_ReturnsOk()
    {
        var suffix = Guid.NewGuid().ToString("N")[..10];
        var email = $"reset_{suffix}@test.com";
        var registerRequest = new UserRegisterRequest("Reset", "User", $"reset_{suffix}", email, "Test@1234!");
        await CreateAnonymousClient().PostAsJsonAsync("/register", registerRequest);

        var resetCode = await GetPasswordResetTokenAsync(email);

        var request = new { Email = email, ResetCode = resetCode, NewPassword = "NewPass@9999!" };
        var response = await CreateAnonymousClient().PostAsJsonAsync("/reset-password", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ResetPassword_InvalidToken_ReturnsBadRequest()
    {
        var suffix = Guid.NewGuid().ToString("N")[..10];
        var email = $"badreset_{suffix}@test.com";
        var registerRequest = new UserRegisterRequest("Bad", "Reset", $"badreset_{suffix}", email, "Test@1234!");
        await CreateAnonymousClient().PostAsJsonAsync("/register", registerRequest);

        var request = new { Email = email, ResetCode = "invalid-token-xyz", NewPassword = "NewPass@9999!" };
        var response = await CreateAnonymousClient().PostAsJsonAsync("/reset-password", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
