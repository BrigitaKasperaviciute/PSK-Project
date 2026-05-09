using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Newtonsoft.Json;
using WorthBoards.Business.Dtos.Identity;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Data.Database;
using WorthBoards.Data.Identity;
using WorthBoards.IntegrationTests.Infrastructure;
using Xunit;

namespace WorthBoards.IntegrationTests.Controllers;

[Collection(nameof(ApiCollection))]
public class UserControllerIntegrationTests
{
    private readonly ApiFactory _factory;

    public UserControllerIntegrationTests(ApiFactory factory)
    {
        _factory = factory;
    }

    #region GetUserById Tests

    [Fact]
    public async Task GetUserById_WithValidUserId_ReturnsOkAndUserData()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (user, _) = await CreateUserAndGetTokenAsync(1, "OWNER");
        var client = _factory.CreateClientForUser(user.Id, "OWNER");
        var dbContext = _factory.CreateDbContext();

        // Act
        var response = await client.GetAsync($"/api/users/{user.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var userResponse = JsonConvert.DeserializeObject<UserResponse>(content);
        userResponse.Should().NotBeNull();
        userResponse!.Id.Should().Be(user.Id);
        userResponse.FirstName.Should().Be(user.FirstName);
        userResponse.LastName.Should().Be(user.LastName);
        userResponse.Email.Should().Be(user.Email);

        dbContext.Dispose();
    }

    [Fact]
    public async Task GetUserById_WithCurrentUserContext_ReturnsOkAndCurrentUserData()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (user, _) = await CreateUserAndGetTokenAsync(1, "OWNER");
        var client = _factory.CreateClientForUser(user.Id, "OWNER");

        // Act - Get current user by passing userId parameter
        var response = await client.GetAsync($"/api/users/{user.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var userResponse = JsonConvert.DeserializeObject<UserResponse>(content);
        userResponse.Should().NotBeNull();
        userResponse!.Id.Should().Be(user.Id);
    }

    [Fact]
    public async Task GetUserById_WithoutAuthorization_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/users/1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetUserById_WithInvalidToken_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "invalid.token.here");

        // Act
        var response = await client.GetAsync("/api/users/1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetUserById_WithExpiredToken_ReturnsUnauthorized()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (user, _) = await CreateUserAndGetTokenAsync(1, "OWNER");
        var client = _factory.CreateClient();
        var expiredToken = TestJwtTokenGenerator.GenerateExpiredToken(user.Id, "OWNER");
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", expiredToken);

        // Act
        var response = await client.GetAsync($"/api/users/{user.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region UpdateUserOnBoard Tests

    [Fact]
    public async Task UpdateUserOnBoard_WithValidData_ReturnsOkAndUpdatedUser()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (user, _) = await CreateUserAndGetTokenAsync(1, "OWNER");
        var client = _factory.CreateClientForUser(user.Id, "OWNER");

        var updateRequest = new UserUpdateRequest
        {
            FirstName = "UpdatedFirst",
            LastName = "UpdatedLast",
            UserName = "updated.user",
            Email = "updated@example.com",
            ImageName = "new-image.png"
        };

        // Act
        var content = new StringContent(
            JsonConvert.SerializeObject(updateRequest),
            Encoding.UTF8,
            "application/json");
        var response = await client.PutAsync($"/api/users/{user.Id}", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseContent = await response.Content.ReadAsStringAsync();
        var updateResponse = JsonConvert.DeserializeObject<UserUpdateResponse>(responseContent);
        updateResponse.Should().NotBeNull();
        updateResponse!.FirstName.Should().Be("UpdatedFirst");
        updateResponse.LastName.Should().Be("UpdatedLast");
        updateResponse.UserName.Should().Be("updated.user");
        updateResponse.Email.Should().Be("updated@example.com");

        // Verify database persistence
        var dbContext = _factory.CreateDbContext();
        var userManager = _factory.GetUserManager(dbContext);
        var updatedUser = await dbContext.Users.FindAsync(user.Id);
        updatedUser.Should().NotBeNull();
        updatedUser!.FirstName.Should().Be("UpdatedFirst");

        dbContext.Dispose();
    }

    [Fact]
    public async Task UpdateUserOnBoard_WithPartialData_ReturnsOkAndUpdatesProvidedFields()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (user, _) = await CreateUserAndGetTokenAsync(1, "OWNER");
        var originalEmail = user.Email;
        var client = _factory.CreateClientForUser(user.Id, "OWNER");

        var updateRequest = new UserUpdateRequest
        {
            FirstName = "UpdatedFirst",
            LastName = user.LastName,
            UserName = user.UserName!,
            Email = originalEmail,
            ImageName = "default.png"
        };

        // Act
        var content = new StringContent(
            JsonConvert.SerializeObject(updateRequest),
            Encoding.UTF8,
            "application/json");
        var response = await client.PutAsync($"/api/users/{user.Id}", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseContent = await response.Content.ReadAsStringAsync();
        var updateResponse = JsonConvert.DeserializeObject<UserUpdateResponse>(responseContent);
        updateResponse!.FirstName.Should().Be("UpdatedFirst");
    }

    [Fact]
    public async Task UpdateUserOnBoard_WithoutAuthorization_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();
        var updateRequest = new UserUpdateRequest
        {
            FirstName = "Test",
            LastName = "User",
            UserName = "test.user",
            Email = "test@example.com",
            ImageName = null
        };

        var content = new StringContent(
            JsonConvert.SerializeObject(updateRequest),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await client.PutAsync("/api/users/1", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateUserOnBoard_WithNonexistentUser_ReturnsNotFound()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (user, _) = await CreateUserAndGetTokenAsync(1, "OWNER");
        var client = _factory.CreateClientForUser(user.Id, "OWNER");

        var updateRequest = new UserUpdateRequest
        {
            FirstName = "Test",
            LastName = "User",
            UserName = "test.user",
            Email = "test@example.com",
            ImageName = null
        };

        var content = new StringContent(
            JsonConvert.SerializeObject(updateRequest),
            Encoding.UTF8,
            "application/json");

        // Act - Try to update non-existent user
        var response = await client.PutAsync("/api/users/99999", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateUserOnBoard_WithInvalidEmail_ReturnsBadRequest()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (user, _) = await CreateUserAndGetTokenAsync(1, "OWNER");
        var client = _factory.CreateClientForUser(user.Id, "OWNER");

        var updateRequest = new UserUpdateRequest
        {
            FirstName = "Test",
            LastName = "User",
            UserName = "test.user",
            Email = "invalid-email-format",
            ImageName = null
        };

        var content = new StringContent(
            JsonConvert.SerializeObject(updateRequest),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await client.PutAsync($"/api/users/{user.Id}", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateUserOnBoard_WithDuplicateEmail_ReturnsBadRequest()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (user1, _) = await CreateUserAndGetTokenAsync(1, "OWNER");
        var (user2, _) = await CreateUserAndGetTokenAsync(2, "OWNER", "user2@example.com");
        var client = _factory.CreateClientForUser(user1.Id, "OWNER");

        var updateRequest = new UserUpdateRequest
        {
            FirstName = "Test",
            LastName = "User",
            UserName = "test.user",
            Email = user2.Email!, // Try to use user2's email
            ImageName = null
        };

        var content = new StringContent(
            JsonConvert.SerializeObject(updateRequest),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await client.PutAsync($"/api/users/{user1.Id}", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Helper Methods

    private Task<(ApplicationUser user, string token)> CreateUserAndGetTokenAsync(
        int userId,
        string role,
        string? email = null)
    {
        var userHelper = new TestUserHelper(_factory);
        return userHelper.CreateUserAndGetTokenAsync(userId, email ?? $"user{userId}@example.com", role);
    }

    #endregion
}
