using Microsoft.AspNetCore.Identity.Data;
using Microsoft.EntityFrameworkCore;
using WorthBoards.Business.Dtos.Requests;

namespace WorthBoards.IntegrationTests.Controllers;

[Collection(nameof(ApiCollection))]
[Trait("Category", "Integration")]
public sealed class AuthControllerTests(CustomWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    // ── POST /register ────────────────────────────────────────────────────────

    [Fact]
    public async Task Register_WithValidRequest_ReturnsOkAndPersistsUser()
    {
        // Arrange
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var request = new UserRegisterRequest(
            FirstName: "Alice",
            LastName: "Smith",
            UserName: $"alice{suffix}",
            Email: $"alice{suffix}@test.com",
            Password: "Alice@1234!");

        // Act
        var response = await Client.PostAsJsonAsync("/register", request);

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<UserResponse>();
        body.Should().NotBeNull();
        body!.Email.Should().Be(request.Email);
        body.UserName.Should().Be(request.UserName);

        // Assert – database state
        await using var db = CreateDbContext();
        var persisted = await db.Users.AsNoTracking()
            .SingleOrDefaultAsync(u => u.Email == request.Email);
        persisted.Should().NotBeNull();
        persisted!.FirstName.Should().Be("Alice");
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ReturnsBadRequest()
    {
        // Arrange – register the same email twice
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var request = new UserRegisterRequest(
            FirstName: "Bob",
            LastName: "Jones",
            UserName: $"bob{suffix}",
            Email: $"bob{suffix}@test.com",
            Password: "Bob@5678!");

        await Client.PostAsJsonAsync("/register", request);

        var duplicate = request with { UserName = $"bob2{suffix}" };

        // Act
        var response = await Client.PostAsJsonAsync("/register", duplicate);

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Assert – database state (only one user with that email)
        await using var db = CreateDbContext();
        var count = await db.Users.CountAsync(u => u.Email == request.Email);
        count.Should().Be(1);
    }

    // ── POST /login ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsOkWithJwtToken()
    {
        // Arrange
        var (_, _) = await RegisterAndLoginAsync("loginuser", "loginuser@test.com", "Login@1234!");
        ClearAuthToken();

        var loginRequest = new UserLoginRequest(UserName: "loginuser", Password: "Login@1234!");

        // Act
        var response = await Client.PostAsJsonAsync("/login", loginRequest);

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<UserLoginResponse>();
        body.Should().NotBeNull();
        body!.JwtToken.Should().NotBeNullOrWhiteSpace();
        body.UserName.Should().Be("loginuser");
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        // Arrange
        await RegisterAndLoginAsync("wrongpwuser", "wrongpw@test.com", "Right@1234!");
        ClearAuthToken();

        var loginRequest = new UserLoginRequest(UserName: "wrongpwuser", Password: "Wrong@0000!");

        // Act
        var response = await Client.PostAsJsonAsync("/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithNonexistentUsername_ReturnsUnauthorized()
    {
        // Arrange
        var loginRequest = new UserLoginRequest(UserName: "ghost_user_xyz", Password: "Ghost@1234!");

        // Act
        var response = await Client.PostAsJsonAsync("/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── POST /forgot-password ─────────────────────────────────────────────────

    [Fact]
    public async Task ForgotPassword_WithExistingEmail_ReturnsOkWithMessage()
    {
        // Arrange
        await RegisterAndLoginAsync("fpuser", "fpuser@test.com");
        ClearAuthToken();

        var request = new ForgotPasswordRequest { Email = "fpuser@test.com" };

        // Act
        var response = await Client.PostAsJsonAsync("/forgot-password", request);

        // Assert – always returns 200 regardless (no email leak)
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PasswordRecoveryResponse>();
        body.Should().NotBeNull();
        body!.Message.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ForgotPassword_WithUnknownEmail_ReturnsOkWithMessage()
    {
        // Arrange – email that doesn't exist; endpoint should not leak user existence
        var request = new ForgotPasswordRequest { Email = "nobody@nowhere.com" };

        // Act
        var response = await Client.PostAsJsonAsync("/forgot-password", request);

        // Assert – still 200 (intentional: no enumeration)
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── POST /change-password ─────────────────────────────────────────────────

    [Fact]
    public async Task ChangePassword_WhenAuthenticated_ReturnsOkWithMessage()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync(
            "chpwuser", "chpwuser@test.com", "OldPw@1234!");
        SetAuthToken(token);

        var request = new ChangePasswordRequest
        {
            OldPassword = "OldPw@1234!",
            NewPassword = "NewPw@5678!"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/change-password", request);

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ChangePasswordResponse>();
        body.Should().NotBeNull();
        body!.Message.Should().NotBeNullOrWhiteSpace();

        // Assert – new password works
        ClearAuthToken();
        var loginResponse = await Client.PostAsJsonAsync("/login",
            new UserLoginRequest("chpwuser", "NewPw@5678!"));
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ChangePassword_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        ClearAuthToken();

        var request = new ChangePasswordRequest
        {
            OldPassword = "OldPw@1234!",
            NewPassword = "NewPw@5678!"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/change-password", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ChangePassword_WithWrongOldPassword_ReturnsBadRequest()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync(
            "wrongoldpw", "wrongoldpw@test.com", "Correct@1234!");
        SetAuthToken(token);

        var request = new ChangePasswordRequest
        {
            OldPassword = "Wrong@1234!",
            NewPassword = "NewPw@5678!"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/change-password", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
