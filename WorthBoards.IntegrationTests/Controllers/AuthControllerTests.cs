using WorthBoards.Business.Dtos.Identity;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace WorthBoards.IntegrationTests.Controllers;

public sealed class AuthControllerTests(CustomWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    // ── Register ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Register_WithValidRequest_ReturnsOkAndPersistsUser()
    {
        // Arrange
        var id = Guid.NewGuid().ToString("N")[..8];
        var request = new UserRegisterRequest(
            FirstName: "Alice",
            LastName: "Smith",
            UserName: $"alice{id}",
            Email: $"alice{id}@example.com",
            Password: "TestPass123!"
        );

        // Act
        var response = await Client.PostAsJsonAsync("/register", request);

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert – response body
        var body = await response.Content.ReadFromJsonAsync<UserResponse>();
        body.Should().NotBeNull();
        body!.UserName.Should().Be(request.UserName);
        body.Email.Should().Be(request.Email);

        // Assert – database state
        await using var db = Factory.CreateDbContext();
        var persisted = await db.Users.AsNoTracking()
            .SingleOrDefaultAsync(u => u.UserName == request.UserName);
        persisted.Should().NotBeNull();
        persisted!.Email.Should().Be(request.Email);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ReturnsBadRequest()
    {
        // Arrange – register a user first
        var id = Guid.NewGuid().ToString("N")[..8];
        var first = new UserRegisterRequest(
            FirstName: "Bob",
            LastName: "Jones",
            UserName: $"bob{id}",
            Email: $"bob{id}@example.com",
            Password: "TestPass123!"
        );
        await Client.PostAsJsonAsync("/register", first);

        var duplicate = new UserRegisterRequest(
            FirstName: "Bob2",
            LastName: "Jones2",
            UserName: $"bob2{id}",
            Email: first.Email,
            Password: "TestPass123!"
        );

        // Act
        var response = await Client.PostAsJsonAsync("/register", duplicate);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_WithWeakPassword_ReturnsBadRequest()
    {
        // Arrange
        var id = Guid.NewGuid().ToString("N")[..8];
        var request = new UserRegisterRequest(
            FirstName: "Charlie",
            LastName: "Brown",
            UserName: $"charlie{id}",
            Email: $"charlie{id}@example.com",
            Password: "weak"
        );

        // Act
        var response = await Client.PostAsJsonAsync("/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Login ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsOkWithToken()
    {
        // Arrange
        var (userId, _) = await RegisterAndLoginAsync();
        var id = Guid.NewGuid().ToString("N")[..8];
        var regRequest = new UserRegisterRequest(
            FirstName: "Dave",
            LastName: "Test",
            UserName: $"dave{id}",
            Email: $"dave{id}@example.com",
            Password: "TestPass123!"
        );
        await Client.PostAsJsonAsync("/register", regRequest);
        var loginRequest = new UserLoginRequest(UserName: regRequest.UserName, Password: regRequest.Password);

        // Act
        var response = await Client.PostAsJsonAsync("/login", loginRequest);

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert – response body
        var body = await response.Content.ReadFromJsonAsync<UserLoginResponse>();
        body.Should().NotBeNull();
        body!.UserName.Should().Be(regRequest.UserName);
        body.JwtToken.Should().NotBeNullOrWhiteSpace();
        body.Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        // Arrange
        var id = Guid.NewGuid().ToString("N")[..8];
        var regRequest = new UserRegisterRequest(
            FirstName: "Eve",
            LastName: "Test",
            UserName: $"eve{id}",
            Email: $"eve{id}@example.com",
            Password: "TestPass123!"
        );
        await Client.PostAsJsonAsync("/register", regRequest);

        // Act
        var response = await Client.PostAsJsonAsync("/login",
            new UserLoginRequest(UserName: regRequest.UserName, Password: "WrongPassword!"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithNonExistentUser_ReturnsUnauthorized()
    {
        // Arrange
        var request = new UserLoginRequest(UserName: "ghost_user_xyz", Password: "TestPass123!");

        // Act
        var response = await Client.PostAsJsonAsync("/login", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── ForgotPassword ────────────────────────────────────────────────────────

    [Fact]
    public async Task ForgotPassword_WithRegisteredEmail_ReturnsOk()
    {
        // Arrange
        var (_, _) = await RegisterAndLoginAsync();
        var id = Guid.NewGuid().ToString("N")[..8];
        var email = $"fp{id}@example.com";
        await Client.PostAsJsonAsync("/register", new UserRegisterRequest(
            FirstName: "Frank",
            LastName: "Test",
            UserName: $"frank{id}",
            Email: email,
            Password: "TestPass123!"));

        // Act
        var response = await Client.PostAsJsonAsync("/forgot-password", new { Email = email });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ForgotPassword_WithUnknownEmail_ReturnsOkWithoutError()
    {
        // Arrange – unknown email; API returns OK regardless (security: no email enumeration)

        // Act
        var response = await Client.PostAsJsonAsync("/forgot-password",
            new { Email = "nobody@example.com" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── ChangePassword ────────────────────────────────────────────────────────

    [Fact]
    public async Task ChangePassword_WithValidCredentials_ReturnsOk()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);

        // Act
        var response = await Client.PostAsJsonAsync("/change-password", new
        {
            OldPassword = "TestPass123!",
            NewPassword = "NewPass456!"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ChangePassword_WithWrongOldPassword_ReturnsBadRequest()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);

        // Act
        var response = await Client.PostAsJsonAsync("/change-password", new
        {
            OldPassword = "WrongOldPass!",
            NewPassword = "NewPass456!"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ChangePassword_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        ClearAuthToken();

        // Act
        var response = await Client.PostAsJsonAsync("/change-password", new
        {
            OldPassword = "TestPass123!",
            NewPassword = "NewPass456!"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
