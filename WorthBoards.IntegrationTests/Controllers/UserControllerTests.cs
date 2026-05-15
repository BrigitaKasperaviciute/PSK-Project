using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using WorthBoards.Business.Dtos.Identity;
using WorthBoards.Common.Exceptions;
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

    // ---------------------------------------------------------------------------
    // GET /api/users/{userId}  [Authorize]
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetUserById_WhenAuthenticated_ReturnsUser()
    {
        // Arrange
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var user = await seeder.CreateUserAsync(userName: "jane.doe", email: "jane.doe@example.com");

        var client = _factory.CreateAuthenticatedClient(user.Id, user.UserName!, user.Email!);

        // Act
        var response = await client.GetAsync($"/api/users/{user.Id}");

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - response body
        var body = await response.Content.ReadFromJsonAsync<UserResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(user.Id);
        body.UserName.Should().Be("jane.doe");
        body.Email.Should().Be("jane.doe@example.com");
        body.FirstName.Should().Be("Test");
        body.LastName.Should().Be("User");
    }

    [Fact]
    public async Task GetUserById_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var user = await seeder.CreateUserAsync();
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync($"/api/users/{user.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetUserById_WithNonExistentUserId_ReturnsNotFound()
    {
        // Arrange
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var user = await seeder.CreateUserAsync();
        var client = _factory.CreateAuthenticatedClient(user.Id, user.UserName!, user.Email!);

        // Act
        var response = await client.GetAsync("/api/users/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------------------------------------------------------------------------
    // PUT /api/users/{userId}  [Authorize]
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task UpdateUser_WithValidRequest_ReturnsOkWithUpdatedUser()
    {
        // Arrange
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var user = await seeder.CreateUserAsync(userName: "john.original", email: "john.original@example.com");

        var client = _factory.CreateAuthenticatedClient(user.Id, user.UserName!, user.Email!);

        var request = new UserUpdateRequest
        {
            FirstName = "John",
            LastName = "Updated",
            UserName = "john.updated",
            Email = "john.updated@example.com",
            ImageName = "avatar.jpg"
        };

        // Act
        var response = await client.PutAsJsonAsync($"/api/users/{user.Id}", request);

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - response body
        var body = await response.Content.ReadFromJsonAsync<UserUpdateResponse>();
        body.Should().NotBeNull();
        body!.FirstName.Should().Be("John");
        body.LastName.Should().Be("Updated");
        body.UserName.Should().Be("john.updated");
    }

    [Fact]
    public async Task UpdateUser_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var user = await seeder.CreateUserAsync();
        var client = _factory.CreateClient();

        var request = new UserUpdateRequest
        {
            FirstName = "Should",
            LastName = "Fail",
            UserName = "should.fail",
            Email = "should.fail@example.com"
        };

        // Act
        var response = await client.PutAsJsonAsync($"/api/users/{user.Id}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
