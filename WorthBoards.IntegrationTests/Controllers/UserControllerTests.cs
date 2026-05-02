using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using WorthBoards.Business.Dtos.Identity;
using WorthBoards.IntegrationTests.Infrastructure;

namespace WorthBoards.IntegrationTests.Controllers;

public class UserControllerTests(CustomWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    // ── GET /api/users/{userId} ──────────────────────────────────────────────

    [Fact]
    public async Task GetUserById_ExistingUser_ReturnsUserResponse()
    {
        // Arrange
        var (userId, _, client) = await RegisterAndLoginAsync();

        // Act
        var response = await client.GetAsync($"/api/users/{userId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var user = await response.Content.ReadFromJsonAsync<UserResponse>();
        user.Should().NotBeNull();
        user!.Id.Should().Be(userId);
    }

    [Fact]
    public async Task GetUserById_Unauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var (userId, _, authClient) = await RegisterAndLoginAsync();
        var anonClient = Factory.CreateClient();

        // Act
        var response = await anonClient.GetAsync($"/api/users/{userId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetUserById_NonExistentUser_ReturnsNotFound()
    {
        // Arrange
        var (_, _, client) = await RegisterAndLoginAsync();

        // Act
        var response = await client.GetAsync("/api/users/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── PUT /api/users/{userId} ──────────────────────────────────────────────

    [Fact]
    public async Task UpdateUser_ValidRequest_ReturnsUpdatedUser()
    {
        // Arrange
        var (userId, _, client) = await RegisterAndLoginAsync();
        var id = Guid.NewGuid().ToString("N")[..8];

        var updateRequest = new UserUpdateRequest
        {
            FirstName = "Updated",
            LastName = "Name",
            UserName = $"updated{id}",
            Email = $"updated{id}@example.com",
            ImageName = ""
        };

        // Act
        var response = await client.PutAsJsonAsync($"/api/users/{userId}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<UserUpdateResponse>();
        updated.Should().NotBeNull();
        updated!.FirstName.Should().Be("Updated");
        updated.LastName.Should().Be("Name");

        using var db = GetDbContext();
        db.Users.Find(userId)!.FirstName.Should().Be("Updated");
    }

    [Fact]
    public async Task UpdateUser_Unauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var (userId, _, _) = await RegisterAndLoginAsync();
        var anonClient = Factory.CreateClient();

        var updateRequest = new UserUpdateRequest
        {
            FirstName = "Hacker",
            LastName = "Attack",
            UserName = "hacker",
            Email = "hacker@evil.com",
            ImageName = ""
        };

        // Act
        var response = await anonClient.PutAsJsonAsync($"/api/users/{userId}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
