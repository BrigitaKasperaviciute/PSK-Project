using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using WorthBoards.Business.Dtos.Identity;
using WorthBoards.Common.Exceptions;
using WorthBoards.IntegrationTests.Infrastructure;

namespace WorthBoards.IntegrationTests;

public sealed class UserControllerTests(WorthBoardsApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task GetUserById_WithCurrentUserToken_ReturnsStoredUser()
    {
        // Arrange
        var client = await CreateAuthenticatedClientAsync("viewer");

        // Act
        var response = await client.GetAsync($"/api/users/{Seed.ViewerUserId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var user = await ReadJsonAsync<UserResponse>(response);
        user.Should().NotBeNull();
        user!.UserName.Should().Be("viewer");
        user.Email.Should().Be("viewer@example.com");
    }

    [Fact]
    public async Task UpdateUserOnBoard_WithUnknownUserId_ReturnsBadRequestAndKeepsDatabaseState()
    {
        // Arrange
        var client = await CreateAuthenticatedClientAsync("viewer");
        var payload = new UserUpdateRequest
        {
            FirstName = "Updated",
            LastName = "Name",
            UserName = "viewer",
            Email = "viewer@example.com",
            ImageName = "viewer.png"
        };

        // Act
        var response = await client.PutAsJsonAsync("/api/users/9999", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await ReadJsonAsync<ErrorDetails>(response);
        error.Should().NotBeNull();
        error!.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);

        var currentUserResponse = await client.GetAsync($"/api/users/{Seed.ViewerUserId}");
        currentUserResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}