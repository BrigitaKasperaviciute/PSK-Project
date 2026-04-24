using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;
using Moq;
using WorthBoards.Business.Dtos.Identity;
using WorthBoards.Common.Exceptions.Custom;
using WorthBoards.IntegrationTests.Infrastructure;

namespace WorthBoards.IntegrationTests.Tests;

public class UserControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public UserControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetAllMocks();
    }

    private HttpClient AuthClient() => _factory.CreateClient().WithAuth(userId: 1);

    private static StringContent Json(object body) =>
        new(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

    // ── GetUserById ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetUserById_ExistingUser_ReturnsOk()
    {
        _factory.UserServiceMock
            .Setup(s => s.GetUserById(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserResponse { Id = 1, UserName = "alice", Email = "alice@test.com", FirstName = "Alice", LastName = "Smith", CreationDate = DateTime.UtcNow });

        var response = await AuthClient().GetAsync("/api/users/1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetUserById_NoToken_ReturnsUnauthorized()
    {
        var response = await _factory.CreateClient().GetAsync("/api/users/1");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── UpdateUser ────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateUser_ValidRequest_ReturnsOk()
    {
        _factory.UserServiceMock
            .Setup(s => s.UpdateUser(1, It.IsAny<UserUpdateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserUpdateResponse { FirstName = "Alice", LastName = "Smith", UserName = "alice", Email = "alice@test.com" });

        var response = await AuthClient().PutAsync("/api/users/1",
            Json(new { firstName = "Alice", lastName = "Smith", userName = "alice", email = "alice@test.com", imageName = "" }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UpdateUser_NoToken_ReturnsUnauthorized()
    {
        var response = await _factory.CreateClient().PutAsync("/api/users/1",
            Json(new { firstName = "Alice", lastName = "Smith", userName = "alice", email = "alice@test.com", imageName = "" }));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}