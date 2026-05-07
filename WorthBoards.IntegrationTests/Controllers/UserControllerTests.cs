using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WorthBoards.Business.Dtos.Identity;
using WorthBoards.IntegrationTests.Infrastructure;
using Xunit;

namespace WorthBoards.IntegrationTests.Controllers;

[Collection(nameof(ApiCollection))]
[Trait("Category", "Integration")]
public sealed class UserControllerTests : IAsyncLifetime
{
    private readonly ApiFactory _factory;

    public UserControllerTests(ApiFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        await using var db = _factory.CreateDbContext();
        await DatabaseSeeder.CleanDatabaseAsync(db);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<(int UserId, HttpClient Client)> CreateUserWithClientAsync(string? username = null, string? email = null)
    {
        await using var db = _factory.CreateDbContext();
        var userManager = _factory.Services.CreateScope().ServiceProvider
            .GetRequiredService<UserManager<Data.Identity.ApplicationUser>>();
        var user = await DatabaseSeeder.CreateUserAsync(db, userManager, username, email);
        return (user.Id, _factory.CreateAuthenticatedClient(user.Id, user.UserName!, user.Email!));
    }

    // --- GET /api/users/{userId} ---

    [Fact]
    public async Task GetUserById_WhenUserExists_ReturnsOkWithUser()
    {
        // Arrange
        var (userId, client) = await CreateUserWithClientAsync("get_user", "get_user@test.com");

        // Act
        var response = await client.GetAsync($"/api/users/{userId}");

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - response body
        var body = await response.Content.ReadFromJsonAsync<UserResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(userId);
        body.UserName.Should().Be("get_user");
        body.Email.Should().Be("get_user@test.com");

        // Assert - database state
        await using var verifyDb = _factory.CreateDbContext();
        var persisted = await verifyDb.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId);
        persisted.Should().NotBeNull();
    }

    [Fact]
    public async Task GetUserById_WhenUserNotFound_ReturnsNotFound()
    {
        // Arrange
        var (_, client) = await CreateUserWithClientAsync();

        // Act
        var response = await client.GetAsync("/api/users/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetUserById_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/users/1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --- PUT /api/users/{userId} ---

    [Fact]
    public async Task UpdateUser_WithValidRequest_ReturnsOkWithUpdatedUser()
    {
        // Arrange
        var (userId, client) = await CreateUserWithClientAsync("upd_user", "upd_user@test.com");

        var request = new UserUpdateRequest
        {
            FirstName = "UpdatedFirst",
            LastName = "UpdatedLast",
            UserName = "updated_username",
            Email = "updated@test.com",
            ImageName = "updated_avatar.jpg"
        };

        // Act
        var response = await client.PutAsJsonAsync($"/api/users/{userId}", request);

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - response body
        var body = await response.Content.ReadFromJsonAsync<UserUpdateResponse>();
        body.Should().NotBeNull();
        body!.FirstName.Should().Be("UpdatedFirst");
        body.LastName.Should().Be("UpdatedLast");

        // Assert - database state
        await using var verifyDb = _factory.CreateDbContext();
        var persisted = await verifyDb.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId);
        persisted.Should().NotBeNull();
        persisted!.FirstName.Should().Be("UpdatedFirst");
    }

    [Fact]
    public async Task UpdateUser_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new UserUpdateRequest
        {
            FirstName = "Hacker",
            LastName = "Attempt",
            UserName = "hacker",
            Email = "hacker@evil.com",
            ImageName = "hack.jpg"
        };

        // Act
        var response = await client.PutAsJsonAsync("/api/users/1", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateUser_WhenUserNotFound_ReturnsBadRequest()
    {
        // Arrange
        var (_, client) = await CreateUserWithClientAsync();
        var request = new UserUpdateRequest
        {
            FirstName = "Missing",
            LastName = "User",
            UserName = "missing_user",
            Email = "missing@test.com",
            ImageName = "missing.jpg"
        };

        // Act
        var response = await client.PutAsJsonAsync("/api/users/99999", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
