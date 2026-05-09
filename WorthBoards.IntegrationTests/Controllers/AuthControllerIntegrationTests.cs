using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Newtonsoft.Json;
using WorthBoards.Business.Dtos.Identity;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Data.Database;
using WorthBoards.Data.Identity;
using Microsoft.Extensions.DependencyInjection;
using WorthBoards.IntegrationTests.Infrastructure;
using Xunit;

namespace WorthBoards.IntegrationTests.Controllers;

[Collection(nameof(ApiCollection))]
public class AuthControllerIntegrationTests
{
    private readonly ApiFactory _factory;

    public AuthControllerIntegrationTests(ApiFactory factory)
    {
        _factory = factory;
    }

    #region Register Tests

    [Fact]
    public async Task RegisterUserAsync_WithValidPayload_ReturnsOkAndCreatesUser()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var client = _factory.CreateClient();

        var registerRequest = new UserRegisterRequest(
            FirstName: "John",
            LastName: "Doe",
            UserName: "johndoe",
            Email: "john.doe@example.com",
            Password: "SecurePassword123!"
        );

        // Act
        var content = new StringContent(
            JsonConvert.SerializeObject(registerRequest),
            Encoding.UTF8,
            "application/json");
        var response = await client.PostAsync("/register", content);

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - response body
        var responseContent = await response.Content.ReadAsStringAsync();
        var userResponse = JsonConvert.DeserializeObject<UserResponse>(responseContent);
        userResponse.Should().NotBeNull();
        userResponse!.FirstName.Should().Be("John");
        userResponse.LastName.Should().Be("Doe");
        userResponse.UserName.Should().Be("johndoe");
        userResponse.Email.Should().Be("john.doe@example.com");

        // Assert - database persistence
        await using var db = _factory.CreateDbContext();
        var createdUser = await db.Users.FindAsync(userResponse.Id);
        createdUser.Should().NotBeNull();
        createdUser!.Email.Should().Be("john.doe@example.com");
        createdUser.UserName.Should().Be("johndoe");
    }

    [Fact]
    public async Task RegisterUserAsync_WithDuplicateEmail_ReturnsBadRequest()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var client = _factory.CreateClient();
        var dbContext = _factory.CreateDbContext();
        var userManager = GetUserManager(dbContext);

        // Create existing user
        var existingUser = new ApplicationUser
        {
            Id = 1,
            FirstName = "Existing",
            LastName = "User",
            UserName = "existinguser",
            Email = "existing@example.com",
            NormalizedEmail = "EXISTING@EXAMPLE.COM",
            NormalizedUserName = "EXISTINGUSER",
            ConcurrencyStamp = Guid.NewGuid().ToString(),
            CreationDate = DateTime.UtcNow,
            EmailConfirmed = true,
        };
        existingUser.PasswordHash = new PasswordHasher<ApplicationUser>().HashPassword(existingUser, "Password123!");
        dbContext.Users.Add(existingUser);
        await dbContext.SaveChangesAsync();

        var registerRequest = new UserRegisterRequest(
            FirstName: "Different",
            LastName: "Person",
            UserName: "differentperson",
            Email: "existing@example.com",
            Password: "Password123!"
        );

        // Act
        var content = new StringContent(
            JsonConvert.SerializeObject(registerRequest),
            Encoding.UTF8,
            "application/json");
        var response = await client.PostAsync("/register", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        dbContext.Dispose();
    }

    [Fact]
    public async Task RegisterUserAsync_WithMissingFirstName_ReturnsBadRequest()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var client = _factory.CreateClient();

        var invalidRequest = new
        {
            LastName = "Doe",
            UserName = "johndoe",
            Email = "john@example.com",
            Password = "SecurePassword123!"
        };

        // Act
        var content = new StringContent(
            JsonConvert.SerializeObject(invalidRequest),
            Encoding.UTF8,
            "application/json");
        var response = await client.PostAsync("/register", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RegisterUserAsync_WithInvalidEmail_ReturnsBadRequest()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var client = _factory.CreateClient();

        var registerRequest = new UserRegisterRequest(
            FirstName: "John",
            LastName: "Doe",
            UserName: "johndoe",
            Email: "invalid-email-format",
            Password: "SecurePassword123!"
        );

        // Act
        var content = new StringContent(
            JsonConvert.SerializeObject(registerRequest),
            Encoding.UTF8,
            "application/json");
        var response = await client.PostAsync("/register", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RegisterUserAsync_WithWeakPassword_ReturnsBadRequest()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var client = _factory.CreateClient();

        var registerRequest = new UserRegisterRequest(
            FirstName: "John",
            LastName: "Doe",
            UserName: "johndoe",
            Email: "john@example.com",
            Password: "weak"
        );

        // Act
        var content = new StringContent(
            JsonConvert.SerializeObject(registerRequest),
            Encoding.UTF8,
            "application/json");
        var response = await client.PostAsync("/register", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Login Tests

    [Fact]
    public async Task LoginUserAsync_WithValidCredentials_ReturnsOkAndJwtToken()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (user, _) = await CreateUserAsync(1, "logintest@example.com", "testuser", "TestPassword123!");

        var client = _factory.CreateClient();
        var loginRequest = new UserLoginRequest(
            UserName: "testuser",
            Password: "TestPassword123!"
        );

        // Act
        var content = new StringContent(
            JsonConvert.SerializeObject(loginRequest),
            Encoding.UTF8,
            "application/json");
        var response = await client.PostAsync("/login", content);

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - response body contains JWT token
        var responseContent = await response.Content.ReadAsStringAsync();
        var loginResponse = JsonConvert.DeserializeObject<UserLoginResponse>(responseContent);
        loginResponse.Should().NotBeNull();
        loginResponse!.UserName.Should().Be("testuser");
        loginResponse.JwtToken.Should().NotBeNullOrEmpty();
        loginResponse.Id.Should().Be(user.Id);
    }

    [Fact]
    public async Task LoginUserAsync_WithIncorrectPassword_ReturnsUnauthorized()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        await CreateUserAsync(2, "wrongpass@example.com", "testuser2", "CorrectPassword123!");

        var client = _factory.CreateClient();
        var loginRequest = new UserLoginRequest(
            UserName: "testuser2",
            Password: "WrongPassword123!"
        );

        // Act
        var content = new StringContent(
            JsonConvert.SerializeObject(loginRequest),
            Encoding.UTF8,
            "application/json");
        var response = await client.PostAsync("/login", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LoginUserAsync_WithNonexistentUser_ReturnsUnauthorized()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var client = _factory.CreateClient();

        var loginRequest = new UserLoginRequest(
            UserName: "nonexistentuser",
            Password: "SomePassword123!"
        );

        // Act
        var content = new StringContent(
            JsonConvert.SerializeObject(loginRequest),
            Encoding.UTF8,
            "application/json");
        var response = await client.PostAsync("/login", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LoginUserAsync_WithEmptyUsername_ReturnsBadRequest()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var client = _factory.CreateClient();

        var loginRequest = new { UserName = "", Password = "SomePassword123!" };

        // Act
        var content = new StringContent(
            JsonConvert.SerializeObject(loginRequest),
            Encoding.UTF8,
            "application/json");
        var response = await client.PostAsync("/login", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task LoginUserAsync_WithEmptyPassword_ReturnsBadRequest()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var client = _factory.CreateClient();

        var loginRequest = new { UserName = "testuser", Password = "" };

        // Act
        var content = new StringContent(
            JsonConvert.SerializeObject(loginRequest),
            Encoding.UTF8,
            "application/json");
        var response = await client.PostAsync("/login", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Reset Password Tests

    [Fact]
    public async Task ResetPassword_WithValidResetCode_ReturnsOkAndAllowsLogin()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (user, _) = await CreateUserAsync(8, "reset@example.com", "resetuser", "OldResetPassword123!");
        var client = _factory.CreateClient();

        var dbContext = _factory.CreateDbContext();
        var userManager = GetUserManager(dbContext);
        var persistedUser = await userManager.FindByEmailAsync(user.Email);
        persistedUser.Should().NotBeNull();

        var resetCode = await userManager.GeneratePasswordResetTokenAsync(persistedUser!);

        var request = new ResetPasswordRequest
        {
            Email = user.Email,
            ResetCode = resetCode,
            NewPassword = "NewResetPassword123!"
        };

        // Act
        var content = new StringContent(
            JsonConvert.SerializeObject(request),
            Encoding.UTF8,
            "application/json");
        var response = await client.PostAsync("/reset-password", content);

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - response body
        var responseContent = await response.Content.ReadAsStringAsync();
        var resetPasswordResponse = JsonConvert.DeserializeObject<PasswordRecoveryResponse>(responseContent);
        resetPasswordResponse.Should().NotBeNull();
        resetPasswordResponse!.Message.Should().Be("Password reset was successfull.");

        // Assert - database state remains intact and new password works
        await using var assertionContext = _factory.CreateDbContext();
        var updatedUser = await assertionContext.Users.FindAsync(user.Id);
        updatedUser.Should().NotBeNull();
        updatedUser!.UserName.Should().Be("resetuser");

        var loginClient = _factory.CreateClient();
        var loginRequest = new UserLoginRequest(
            UserName: "resetuser",
            Password: "NewResetPassword123!");
        var loginContent = new StringContent(
            JsonConvert.SerializeObject(loginRequest),
            Encoding.UTF8,
            "application/json");
        var loginResponse = await loginClient.PostAsync("/login", loginContent);
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ResetPassword_WithInvalidResetCode_ReturnsBadRequest()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (user, _) = await CreateUserAsync(9, "invalidreset@example.com", "invalidresetuser", "OldResetPassword123!");
        var client = _factory.CreateClient();

        var request = new ResetPasswordRequest
        {
            Email = user.Email,
            ResetCode = "invalid-reset-code",
            NewPassword = "NewResetPassword123!"
        };

        // Act
        var content = new StringContent(
            JsonConvert.SerializeObject(request),
            Encoding.UTF8,
            "application/json");
        var response = await client.PostAsync("/reset-password", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Change Password Tests

    [Fact]
    public async Task ChangePassword_WithValidOldPassword_ReturnsOkAndChangesPassword()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (user, _) = await CreateUserAsync(3, "changepass@example.com", "changeuser", "OldPassword123!");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer",
            TestJwtTokenGenerator.GenerateToken(user.Id, "OWNER", user.Email));

        var changePasswordRequest = new ChangePasswordRequest
        {
            OldPassword = "OldPassword123!",
            NewPassword = "NewPassword123!"
        };

        // Act
        var content = new StringContent(
            JsonConvert.SerializeObject(changePasswordRequest),
            Encoding.UTF8,
            "application/json");
        var response = await client.PostAsync("/change-password", content);

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - response body
        var responseContent = await response.Content.ReadAsStringAsync();
        var changePasswordResponse = JsonConvert.DeserializeObject<ChangePasswordResponse>(responseContent);
        changePasswordResponse.Should().NotBeNull();
        changePasswordResponse!.Message.Should().NotBeNullOrEmpty();

        // Assert - new password works in login
        var loginRequest = new UserLoginRequest(
            UserName: "changeuser",
            Password: "NewPassword123!"
        );
        var loginClient = _factory.CreateClient();
        var loginContent = new StringContent(
            JsonConvert.SerializeObject(loginRequest),
            Encoding.UTF8,
            "application/json");
        var loginResponse = await loginClient.PostAsync("/login", loginContent);
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ChangePassword_WithIncorrectOldPassword_ReturnsBadRequest()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (user, _) = await CreateUserAsync(4, "incorrectold@example.com", "incorrectolduser", "CorrectOldPassword123!");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer",
            TestJwtTokenGenerator.GenerateToken(user.Id, "OWNER", user.Email));

        var changePasswordRequest = new ChangePasswordRequest
        {
            OldPassword = "WrongOldPassword123!",
            NewPassword = "NewPassword123!"
        };

        // Act
        var content = new StringContent(
            JsonConvert.SerializeObject(changePasswordRequest),
            Encoding.UTF8,
            "application/json");
        var response = await client.PostAsync("/change-password", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ChangePassword_WithoutAuthorization_ReturnsUnauthorized()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var client = _factory.CreateClient();

        var changePasswordRequest = new ChangePasswordRequest
        {
            OldPassword = "OldPassword123!",
            NewPassword = "NewPassword123!"
        };

        // Act
        var content = new StringContent(
            JsonConvert.SerializeObject(changePasswordRequest),
            Encoding.UTF8,
            "application/json");
        var response = await client.PostAsync("/change-password", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ChangePassword_WithWeakNewPassword_ReturnsBadRequest()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (user, _) = await CreateUserAsync(5, "weaknew@example.com", "weaknewuser", "ValidOldPassword123!");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer",
            TestJwtTokenGenerator.GenerateToken(user.Id, "OWNER", user.Email));

        var changePasswordRequest = new ChangePasswordRequest
        {
            OldPassword = "ValidOldPassword123!",
            NewPassword = "weak"
        };

        // Act
        var content = new StringContent(
            JsonConvert.SerializeObject(changePasswordRequest),
            Encoding.UTF8,
            "application/json");
        var response = await client.PostAsync("/change-password", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ChangePassword_WithSameOldAndNewPassword_ReturnsBadRequest()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (user, _) = await CreateUserAsync(6, "samepass@example.com", "samepassuser", "SamePassword123!");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer",
            TestJwtTokenGenerator.GenerateToken(user.Id, "OWNER", user.Email));

        var changePasswordRequest = new ChangePasswordRequest
        {
            OldPassword = "SamePassword123!",
            NewPassword = "SamePassword123!"
        };

        // Act
        var content = new StringContent(
            JsonConvert.SerializeObject(changePasswordRequest),
            Encoding.UTF8,
            "application/json");
        var response = await client.PostAsync("/change-password", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Helper Methods

    private UserManager<ApplicationUser> GetUserManager(ApplicationDbContext dbContext)
    {
        return _factory.GetUserManager(dbContext);
    }

    private async Task<(ApplicationUser User, string Password)> CreateUserAsync(
        int userId, 
        string email, 
        string userName = null!, 
        string password = "DefaultPassword123!")
    {
        userName ??= $"user{userId}";
        
        var dbContext = _factory.CreateDbContext();

        var user = new ApplicationUser
        {
            Id = userId,
            FirstName = $"User",
            LastName = $"Number{userId}",
            UserName = userName.Length > 30 ? userName.Substring(0, 30) : userName,
            Email = email,
            NormalizedEmail = email.ToUpper(),
            NormalizedUserName = userName.ToUpper(),
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString(),
            CreationDate = DateTime.UtcNow
        };

        user.PasswordHash = new PasswordHasher<ApplicationUser>().HashPassword(user, password);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        dbContext.Dispose();

        return (user, password);
    }

    #endregion
}
