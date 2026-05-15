using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WorthBoards.Business.Dtos.Identity;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Common.Exceptions;
using WorthBoards.Data.Identity;
using WorthBoards.IntegrationTests.Infrastructure;
using Xunit;

namespace WorthBoards.IntegrationTests.Controllers;

[Collection(nameof(ApiCollection))]
[Trait("Category", "Integration")]
public sealed class AuthControllerTests : IAsyncLifetime
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public AuthControllerTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await using var db = _factory.CreateDbContext();
        await DatabaseSeeder.CleanDatabaseAsync(db);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ---------------------------------------------------------------------------
    // POST /register
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task RegisterUser_WithValidPayload_ReturnsOkAndPersistsUser()
    {
        // Arrange
        var request = new UserRegisterRequest(
            FirstName: "Alice",
            LastName: "Johnson",
            UserName: "alice.johnson",
            Email: "alice.johnson@example.com",
            Password: "Alice@12345!");

        // Act
        var response = await _client.PostAsJsonAsync("/register", request);

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - response body
        var body = await response.Content.ReadFromJsonAsync<UserResponse>();
        body.Should().NotBeNull();
        body!.UserName.Should().Be(request.UserName);
        body.Email.Should().Be(request.Email);
        body.FirstName.Should().Be(request.FirstName);
        body.Id.Should().BeGreaterThan(0);

        // Assert - database state
        await using var db = _factory.CreateDbContext();
        var persisted = await db.Users.AsNoTracking()
            .SingleOrDefaultAsync(u => u.Email == request.Email);
        persisted.Should().NotBeNull();
        persisted!.UserName.Should().Be(request.UserName);
    }

    [Fact]
    public async Task RegisterUser_WithDuplicateEmail_ReturnsBadRequest()
    {
        // Arrange - seed a user with the same email
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var existing = await seeder.CreateUserAsync(email: "duplicate@example.com");

        var request = new UserRegisterRequest(
            FirstName: "Bob",
            LastName: "Smith",
            UserName: "bob.smith",
            Email: "duplicate@example.com",
            Password: "Bob@12345!");

        // Act
        var response = await _client.PostAsJsonAsync("/register", request);

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Assert - response body contains error details
        var error = await response.Content.ReadFromJsonAsync<ErrorDetails>();
        error.Should().NotBeNull();
        error!.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);

        // Assert - database state: only the original user exists
        await using var db = _factory.CreateDbContext();
        var count = await db.Users.CountAsync(u => u.Email == "duplicate@example.com");
        count.Should().Be(1);
    }

    // ---------------------------------------------------------------------------
    // POST /login
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task LoginUser_WithValidCredentials_ReturnsOkWithJwtToken()
    {
        // Arrange - register a user first
        var registerRequest = new UserRegisterRequest(
            FirstName: "Carol",
            LastName: "White",
            UserName: "carol.white",
            Email: "carol.white@example.com",
            Password: "Carol@12345!");
        await _client.PostAsJsonAsync("/register", registerRequest);

        var loginRequest = new UserLoginRequest(
            UserName: "carol.white",
            Password: "Carol@12345!");

        // Act
        var response = await _client.PostAsJsonAsync("/login", loginRequest);

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - response body
        var body = await response.Content.ReadFromJsonAsync<UserLoginResponse>();
        body.Should().NotBeNull();
        body!.UserName.Should().Be("carol.white");
        body.JwtToken.Should().NotBeNullOrWhiteSpace();
        body.Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task LoginUser_WithInvalidPassword_ReturnsUnauthorized()
    {
        // Arrange
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        await seeder.CreateUserAsync(userName: "dave.jones", email: "dave.jones@example.com");

        var loginRequest = new UserLoginRequest(
            UserName: "dave.jones",
            Password: "WrongPassword@999!");

        // Act
        var response = await _client.PostAsJsonAsync("/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LoginUser_WithNonExistentUsername_ReturnsUnauthorized()
    {
        // Arrange
        var loginRequest = new UserLoginRequest(
            UserName: "nobody",
            Password: "Any@12345!");

        // Act
        var response = await _client.PostAsJsonAsync("/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------------------------------------------------------------------------
    // POST /forgot-password
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ForgotPassword_WithRegisteredEmail_ReturnsOk()
    {
        // Arrange
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        await seeder.CreateUserAsync(email: "eve.brown@example.com");

        var request = new ForgotPasswordRequest { Email = "eve.brown@example.com" };

        // Act
        var response = await _client.PostAsJsonAsync("/forgot-password", request);

        // Assert - always returns OK to prevent email enumeration
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ForgotPassword_WithNonExistentEmail_StillReturnsOk()
    {
        // Arrange - no user with this email exists
        var request = new ForgotPasswordRequest { Email = "ghost@example.com" };

        // Act
        var response = await _client.PostAsJsonAsync("/forgot-password", request);

        // Assert - same OK response to prevent email enumeration
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ---------------------------------------------------------------------------
    // POST /change-password
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ChangePassword_WhenAuthenticated_WithValidOldPassword_ReturnsOk()
    {
        // Arrange - create user and get authenticated client
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var user = await seeder.CreateUserAsync(email: "frank.miller@example.com");

        var authenticatedClient = _factory.CreateAuthenticatedClient(
            user.Id, user.UserName!, user.Email!);

        var request = new ChangePasswordRequest
        {
            OldPassword = "Test@12345!",
            NewPassword = "NewTest@67890!"
        };

        // Act
        var response = await authenticatedClient.PostAsJsonAsync("/change-password", request);

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - new password works for login
        var loginResponse = await _client.PostAsJsonAsync("/login",
            new UserLoginRequest(user.UserName!, "NewTest@67890!"));
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ChangePassword_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var request = new ChangePasswordRequest
        {
            OldPassword = "Test@12345!",
            NewPassword = "NewTest@67890!"
        };

        // Act - using unauthenticated client (no JWT)
        var response = await _client.PostAsJsonAsync("/change-password", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------------------------------------------------------------------------
    // POST /reset-password
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ResetPassword_WithValidToken_ReturnsOkAndPasswordIsChanged()
    {
        // Arrange - create user and generate a real identity reset token
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var user = await seeder.CreateUserAsync(email: "grace.hopper@example.com");

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var rawToken = await userManager.GeneratePasswordResetTokenAsync(user);

        var request = new ResetPasswordRequest
        {
            Email = user.Email!,
            ResetCode = rawToken,
            NewPassword = "ResetPass@99999!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/reset-password", request);

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - new password works for login
        var loginResponse = await _client.PostAsJsonAsync("/login",
            new UserLoginRequest(user.UserName!, "ResetPass@99999!"));
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ResetPassword_WithInvalidToken_ReturnsBadRequest()
    {
        // Arrange - create user but use a garbage reset token
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var user = await seeder.CreateUserAsync(email: "henry.ford@example.com");

        var request = new ResetPasswordRequest
        {
            Email = user.Email!,
            ResetCode = "invalid-token-xyz",
            NewPassword = "NewPass@12345!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/reset-password", request);

        // Assert - Identity rejects the bad token
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Assert - original password still works
        var loginResponse = await _client.PostAsJsonAsync("/login",
            new UserLoginRequest(user.UserName!, "Test@12345!"));
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
