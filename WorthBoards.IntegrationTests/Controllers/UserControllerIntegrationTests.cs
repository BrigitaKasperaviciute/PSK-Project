using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using System.Text.Json;
using Xunit;
using WorthBoards.IntegrationTests.Infrastructure;

namespace WorthBoards.IntegrationTests.Controllers;

public class UserControllerIntegrationTests : IntegrationTestBase
{
    [Fact]
    public async Task GetUserById_ValidUserId_ReturnsOkWithUserData()
    {
        // Arrange
        await ResetDatabaseAsync();
        var helper = new TestHelper(HttpClient, this);
        var (userId, token, _) = await helper.CreateAndLoginUserAsync();
        
        SetBearerToken(token);

        // Act
        var response = await HttpClient.GetAsync($"api/users/{userId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        result.GetProperty("id").GetInt32().Should().Be(userId);
    }

    [Fact]
    public async Task GetUserById_WithoutUserId_ReturnsSelfData()
    {
        // Arrange
        await ResetDatabaseAsync();
        var helper = new TestHelper(HttpClient, this);
        var (userId, token, _) = await helper.CreateAndLoginUserAsync();
        
        SetBearerToken(token);

        // Act
        var response = await HttpClient.GetAsync("api/users/0");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetUserById_NonExistentUser_ReturnsNotFound()
    {
        // Arrange
        await ResetDatabaseAsync();
        var helper = new TestHelper(HttpClient, this);
        var (userId, token, _) = await helper.CreateAndLoginUserAsync();
        
        SetBearerToken(token);

        // Act
        var response = await HttpClient.GetAsync("api/users/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetUserById_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        await ResetDatabaseAsync();

        // Act
        var response = await HttpClient.GetAsync("api/users/1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateUserOnBoard_ValidRequest_ReturnsOkWithUpdatedUser()
    {
        // Arrange
        await ResetDatabaseAsync();
        var helper = new TestHelper(HttpClient, this);
        var (userId, token, _) = await helper.CreateAndLoginUserAsync();
        
        var updateRequest = TestDataBuilder.CreateUserUpdateRequest(
            firstName: "UpdatedFirstName",
            lastName: "UpdatedLastName"
        );
        SetBearerToken(token);

        // Act
        var response = await HttpClient.PutAsJsonAsync($"api/users/{userId}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        result.GetProperty("firstName").GetString().Should().Be("UpdatedFirstName");
        result.GetProperty("lastName").GetString().Should().Be("UpdatedLastName");
    }

    [Fact]
    public async Task UpdateUserOnBoard_InvalidRequest_ReturnsBadRequest()
    {
        // Arrange
        await ResetDatabaseAsync();
        var helper = new TestHelper(HttpClient, this);
        var (userId, token, _) = await helper.CreateAndLoginUserAsync();
        
        var invalidRequest = new { firstName = "", lastName = "" };
        SetBearerToken(token);

        // Act
        var response = await HttpClient.PutAsJsonAsync($"api/users/{userId}", invalidRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateUserOnBoard_NonExistentUser_ReturnsNotFound()
    {
        // Arrange
        await ResetDatabaseAsync();
        var helper = new TestHelper(HttpClient, this);
        var (userId, token, _) = await helper.CreateAndLoginUserAsync();
        
        var updateRequest = TestDataBuilder.CreateUserUpdateRequest();
        SetBearerToken(token);

        // Act
        var response = await HttpClient.PutAsJsonAsync("api/users/99999", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateUserOnBoard_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        await ResetDatabaseAsync();
        var updateRequest = TestDataBuilder.CreateUserUpdateRequest();

        // Act
        var response = await HttpClient.PutAsJsonAsync("api/users/1", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
