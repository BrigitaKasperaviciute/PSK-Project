using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using WorthBoards.Business.Dtos.Identity;
using WorthBoards.IntegrationTests.Infrastructure;
using Xunit;

namespace WorthBoards.IntegrationTests;

public class UserControllerTests(WorthBoardsWebApplicationFactory factory)
    : IClassFixture<WorthBoardsWebApplicationFactory>
{
    private HttpClient AuthorizedClient(int userId, string userName, string email)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTokenHelper.GenerateToken(userId, userName, email));
        return client;
    }

    // ── GET /api/users/{userId} ──────────────────────────────────────────────

    [Fact]
    public async Task GetUserById_ExistingUser_ReturnsOkWithUser()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var user = await seeder.CreateUserAsync();
        var client = AuthorizedClient(user.Id, user.UserName!, user.Email!);

        // Act
        var response = await client.GetAsync($"/api/users/{user.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UserResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(user.Id);
        body.Email.Should().Be(user.Email);
    }

    [Fact]
    public async Task GetUserById_Unauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/users/1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── PUT /api/users/{userId} ──────────────────────────────────────────────

    [Fact]
    public async Task UpdateUser_ValidRequest_ReturnsOk()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var user = await seeder.CreateUserAsync();
        var client = AuthorizedClient(user.Id, user.UserName!, user.Email!);
        var request = new UserUpdateRequest
        {
            FirstName = "Updated",
            LastName = "Name",
            UserName = user.UserName!,
            Email = user.Email!,
            ImageName = "new_avatar.jpg"
        };

        // Act
        var response = await client.PutAsJsonAsync($"/api/users/{user.Id}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UserResponse>();
        body.Should().NotBeNull();
        body!.FirstName.Should().Be("Updated");
    }

    [Fact]
    public async Task UpdateUser_Unauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var client = factory.CreateClient();
        var request = new UserUpdateRequest
        {
            FirstName = "Hacker",
            LastName = "Hacker",
            UserName = "hacker",
            Email = "hacker@evil.com",
            ImageName = "x.jpg"
        };

        // Act
        var response = await client.PutAsJsonAsync("/api/users/1", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
