using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using WorthBoards.Business.Dtos.Identity;
using WorthBoards.Business.Dtos.Requests;
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

    // --- Register ---

    [Fact]
    public async Task Register_WithValidRequest_ReturnsOkAndPersistsUser()
    {
        // Arrange
        var request = new UserRegisterRequest(
            FirstName: "Alice",
            LastName: "Smith",
            UserName: "alice_smith",
            Email: "alice@example.com",
            Password: "Alice@12345!"
        );

        // Act
        var response = await _client.PostAsJsonAsync("/register", request);

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - response body
        var body = await response.Content.ReadFromJsonAsync<UserResponse>();
        body.Should().NotBeNull();
        body!.Email.Should().Be(request.Email);
        body.UserName.Should().Be(request.UserName);

        // Assert - database state
        await using var db = _factory.CreateDbContext();
        var persisted = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Email == request.Email);
        persisted.Should().NotBeNull();
        persisted!.UserName.Should().Be(request.UserName);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ReturnsBadRequest()
    {
        // Arrange - seed a user with the same email
        await using var seedDb = _factory.CreateDbContext();
        var userManager = _factory.Services.CreateScope().ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();
        await DatabaseSeeder.CreateUserAsync(seedDb, userManager, email: "duplicate@example.com");

        var request = new UserRegisterRequest(
            FirstName: "Bob",
            LastName: "Jones",
            UserName: "bob_jones",
            Email: "duplicate@example.com",
            Password: "Bob@12345!"
        );

        // Act
        var response = await _client.PostAsJsonAsync("/register", request);

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // --- Login ---

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsOkWithToken()
    {
        // Arrange
        await using var seedDb = _factory.CreateDbContext();
        var userManager = _factory.Services.CreateScope().ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();
        var user = await DatabaseSeeder.CreateUserAsync(seedDb, userManager, username: "loginuser", email: "loginuser@test.com");

        var request = new UserLoginRequest(UserName: "loginuser", Password: DatabaseSeeder.DefaultPassword);

        // Act
        var response = await _client.PostAsJsonAsync("/login", request);

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - response body contains JWT
        var body = await response.Content.ReadFromJsonAsync<UserLoginResponse>();
        body.Should().NotBeNull();
        body!.UserName.Should().Be("loginuser");
        body.JwtToken.Should().NotBeNullOrWhiteSpace();
        body.Id.Should().Be(user.Id);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        // Arrange
        await using var seedDb = _factory.CreateDbContext();
        var userManager = _factory.Services.CreateScope().ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();
        await DatabaseSeeder.CreateUserAsync(seedDb, userManager, username: "loginuser2", email: "loginuser2@test.com");

        var request = new UserLoginRequest(UserName: "loginuser2", Password: "WrongPassword@99!");

        // Act
        var response = await _client.PostAsJsonAsync("/login", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithNonExistentUser_ReturnsUnauthorized()
    {
        // Arrange
        var request = new UserLoginRequest(UserName: "ghost_user", Password: "Any@12345!");

        // Act
        var response = await _client.PostAsJsonAsync("/login", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --- ForgotPassword ---

    [Fact]
    public async Task ForgotPassword_WithExistingEmail_ReturnsOkAndSendsEmail()
    {
        // Arrange
        await using var seedDb = _factory.CreateDbContext();
        var userManager = _factory.Services.CreateScope().ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();
        await DatabaseSeeder.CreateUserAsync(seedDb, userManager, email: "forgot@test.com");

        var request = new { Email = "forgot@test.com" };

        // Act
        var response = await _client.PostAsJsonAsync("/forgot-password", request);

        // Assert - always returns 200 (security: doesn't reveal if email exists)
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _factory.EmailServiceMock.Verify(
            e => e.SendEmailAsync(It.IsAny<WorthBoards.Business.Utils.EmailService.SendEmailRequest>(), It.IsAny<CancellationToken>()),
            Moq.Times.Once
        );
    }

    [Fact]
    public async Task ForgotPassword_WithNonExistentEmail_ReturnsOkWithoutSendingEmail()
    {
        // Arrange - ensure database is clean (no user seeded)
        await using var seedDb = _factory.CreateDbContext();
        await DatabaseSeeder.CleanDatabaseAsync(seedDb);
        var request = new { Email = "nobody@example.com" };

        // Ensure the shared mock has no prior invocations
        _factory.EmailServiceMock.Reset();

        // Act
        var response = await _client.PostAsJsonAsync("/forgot-password", request);

        // Assert - still returns 200 (security: doesn't reveal absence of account)
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _factory.EmailServiceMock.Verify(
            e => e.SendEmailAsync(It.IsAny<WorthBoards.Business.Utils.EmailService.SendEmailRequest>(), It.IsAny<CancellationToken>()),
            Moq.Times.Never
        );
    }

    // --- ChangePassword ---

    [Fact]
    public async Task ChangePassword_WithValidOldPassword_ReturnsOk()
    {
        // Arrange
        await using var seedDb = _factory.CreateDbContext();
        var userManager = _factory.Services.CreateScope().ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();
        var user = await DatabaseSeeder.CreateUserAsync(seedDb, userManager, username: "changepwuser", email: "changepw@test.com");

        var authenticatedClient = _factory.CreateAuthenticatedClient(user.Id, user.UserName!, user.Email!);
        var request = new ChangePasswordRequest
        {
            OldPassword = DatabaseSeeder.DefaultPassword,
            NewPassword = "NewPass@12345!"
        };

        // Act
        var response = await authenticatedClient.PostAsJsonAsync("/change-password", request);

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ChangePassword_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var request = new ChangePasswordRequest
        {
            OldPassword = DatabaseSeeder.DefaultPassword,
            NewPassword = "NewPass@12345!"
        };

        // Act - no Authorization header
        var response = await _client.PostAsJsonAsync("/change-password", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
