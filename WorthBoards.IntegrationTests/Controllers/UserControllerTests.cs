using Microsoft.EntityFrameworkCore;
using WorthBoards.Business.Dtos.Identity;
using WorthBoards.IntegrationTests.Infrastructure;

namespace WorthBoards.IntegrationTests.Controllers;

public sealed class UserControllerTests(CustomWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    // ── GetUserById ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetUserById_WithExistingUser_ReturnsOkWithUser()
    {
        // Arrange
        var (userId, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);

        // Act
        var response = await Client.GetAsync($"/api/users/{userId}");

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert – response body
        var body = await response.Content.ReadFromJsonAsync<UserResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(userId);
        body.FirstName.Should().Be("Test");
        body.LastName.Should().Be("User");
    }

    [Fact]
    public async Task GetUserById_WithNonExistentUser_ReturnsNotFound()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);

        // Act
        var response = await Client.GetAsync("/api/users/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetUserById_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var (userId, _) = await RegisterAndLoginAsync();
        ClearAuthToken();

        // Act
        var response = await Client.GetAsync($"/api/users/{userId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── UpdateUserOnBoard ─────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateUser_WithValidRequest_ReturnsOkAndUpdatesDb()
    {
        // Arrange
        var (userId, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);

        var uniqueSuffix = Guid.NewGuid().ToString("N")[..6];

        // Read the existing user's email/username so we only change first/last name
        // (changing email/username via raw mapper bypasses Identity normalization)
        await using var dbRead = Factory.CreateDbContext();
        var existingUser = await dbRead.Users.FindAsync(userId);

        var updateRequest = new UserUpdateRequest
        {
            FirstName = "Updated",
            LastName = "Name",
            UserName = existingUser!.UserName!,
            Email = existingUser.Email!,
            ImageName = "profile.jpg"
        };

        // Act
        var response = await Client.PutAsJsonAsync($"/api/users/{userId}", updateRequest);

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert – response body
        var body = await response.Content.ReadFromJsonAsync<UserUpdateResponse>();
        body.Should().NotBeNull();
        body!.FirstName.Should().Be(updateRequest.FirstName);
        body.LastName.Should().Be(updateRequest.LastName);

        // Assert – database state
        await using var db = Factory.CreateDbContext();
        var persisted = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId);
        persisted.Should().NotBeNull();
        persisted!.FirstName.Should().Be(updateRequest.FirstName);
        persisted.LastName.Should().Be(updateRequest.LastName);
    }

    [Fact]
    public async Task UpdateUser_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var (userId, _) = await RegisterAndLoginAsync();
        ClearAuthToken();

        var request = new UserUpdateRequest
        {
            FirstName = "Hacker",
            LastName = "Hacker",
            UserName = "hacker",
            Email = "hacker@example.com",
            ImageName = "hacker.jpg"
        };

        // Act
        var response = await Client.PutAsJsonAsync($"/api/users/{userId}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
