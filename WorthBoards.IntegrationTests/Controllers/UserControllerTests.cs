using Microsoft.EntityFrameworkCore;

namespace WorthBoards.IntegrationTests.Controllers;

[Collection(nameof(ApiCollection))]
[Trait("Category", "Integration")]
public sealed class UserControllerTests(CustomWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    // ── GET /api/users/{userId} ───────────────────────────────────────────────

    [Fact]
    public async Task GetUserById_WhenAuthenticated_ReturnsOkWithUser()
    {
        // Arrange
        var (userId, token) = await RegisterAndLoginAsync("getuser", "getuser@test.com");
        SetAuthToken(token);

        // Act
        var response = await Client.GetAsync($"/api/users/{userId}");

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<UserResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(userId);
        body.UserName.Should().Be("getuser");
        body.Email.Should().Be("getuser@test.com");
    }

    [Fact]
    public async Task GetUserById_WhenUnauthenticated_ReturnsUnauthorized()
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
    public async Task GetUserById_WithNonexistentId_ReturnsNotFound()
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
    public async Task UpdateUser_WhenAuthenticated_ReturnsOkAndPersistsChanges()
    {
        // Arrange
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (userId, token) = await RegisterAndLoginAsync(
            $"updateme{suffix}", $"updateme{suffix}@test.com");
        SetAuthToken(token);

        var updateRequest = new UserUpdateRequest
        {
            FirstName = "Updated",
            LastName  = "Name",
            UserName  = $"updated{suffix}",
            Email     = $"updateme{suffix}@test.com", // keep same email to avoid normalisation issues
            ImageName = "profile.png"
        };

        // Act
        var response = await Client.PutAsJsonAsync($"/api/users/{userId}", updateRequest);

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<UserUpdateResponse>();
        body.Should().NotBeNull();
        body!.FirstName.Should().Be("Updated");
        body.LastName.Should().Be("Name");

        // Assert – database state
        await using var db = CreateDbContext();
        var persisted = await db.Users.AsNoTracking()
            .SingleOrDefaultAsync(u => u.Id == userId);
        persisted.Should().NotBeNull();
        persisted!.FirstName.Should().Be("Updated");
        persisted.LastName.Should().Be("Name");
    }

    [Fact]
    public async Task UpdateUser_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var (userId, _) = await RegisterAndLoginAsync();
        ClearAuthToken();

        var updateRequest = new UserUpdateRequest
        {
            FirstName = "Hacked",
            LastName  = "User",
            UserName  = "hackeduser",
            Email     = "hacked@test.com",
            ImageName = "evil.png"
        };

        // Act
        var response = await Client.PutAsJsonAsync($"/api/users/{userId}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
