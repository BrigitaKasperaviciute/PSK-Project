using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using WorthBoards.Business.Dtos.Identity;
using WorthBoards.IntegrationTests.Infrastructure;

namespace WorthBoards.IntegrationTests.Controllers;

public class UserControllerTests(CustomWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    // ── GET /api/users/{userId} ───────────────────────────────────────────────

    [Fact]
    public async Task GetUserById_AuthenticatedUser_ReturnsOkWithUserProfile()
    {
        // Arrange
        var (userId, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);

        // Act
        var response = await Client.GetAsync($"/api/users/{userId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<UserResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(userId);
        body.FirstName.Should().Be("Test");
        body.LastName.Should().Be("User");
    }

    [Fact]
    public async Task GetUserById_UnauthenticatedUser_ReturnsUnauthorized()
    {
        // Arrange
        var (userId, _) = await RegisterAndLoginAsync();
        ClearAuthToken();

        // Act
        var response = await Client.GetAsync($"/api/users/{userId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetUserById_NonExistingUser_ReturnsNotFound()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);

        // Act
        var response = await Client.GetAsync("/api/users/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── PUT /api/users/{userId} ───────────────────────────────────────────────

    [Fact]
    public async Task UpdateUser_AuthenticatedUser_ReturnsOkWithUpdatedProfile()
    {
        // Arrange
        var (userId, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);

        var id = Guid.NewGuid().ToString("N")[..8];
        var updateRequest = new UserUpdateRequest
        {
            FirstName = "UpdatedFirst",
            LastName = "UpdatedLast",
            UserName = $"updated_{id}",
            Email = $"updated_{id}@test.com",
            ImageName = "avatar.jpg"
        };

        // Act
        var response = await Client.PutAsJsonAsync($"/api/users/{userId}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<UserUpdateResponse>();
        body.Should().NotBeNull();
        body!.FirstName.Should().Be("UpdatedFirst");
        body.LastName.Should().Be("UpdatedLast");

        var dbUser = await QueryAsync(db => db.Users.FindAsync(userId).AsTask());
        dbUser.Should().NotBeNull();
        dbUser!.FirstName.Should().Be("UpdatedFirst");
    }

    [Fact]
    public async Task UpdateUser_UnauthenticatedUser_ReturnsUnauthorized()
    {
        // Arrange
        var (userId, _) = await RegisterAndLoginAsync();
        ClearAuthToken();

        var updateRequest = new UserUpdateRequest
        {
            FirstName = "Should",
            LastName = "Fail",
            UserName = "shouldfail",
            Email = "shouldfail@test.com",
            ImageName = "avatar.jpg"
        };

        // Act
        var response = await Client.PutAsJsonAsync($"/api/users/{userId}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
