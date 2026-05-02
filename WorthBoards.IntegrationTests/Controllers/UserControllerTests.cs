using System.Net;
using System.Net.Http.Json;
using WorthBoards.Business.Dtos.Identity;
using WorthBoards.IntegrationTests.Infrastructure;

namespace WorthBoards.IntegrationTests.Controllers;

public class UserControllerTests : IntegrationTestBase
{
    public UserControllerTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task GetUserById_Authenticated_ReturnsOk()
    {
        var (token, userId) = await RegisterAndLoginAsync();
        var client = CreateAuthenticatedClient(token);

        var response = await client.GetAsync($"/api/users/{userId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UserResponse>();
        body!.Id.Should().Be(userId);
    }

    [Fact]
    public async Task GetUserById_Unauthenticated_ReturnsUnauthorized()
    {
        var response = await CreateAnonymousClient().GetAsync("/api/users/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateUser_Authenticated_ReturnsOk()
    {
        var (token, userId) = await RegisterAndLoginAsync();
        var client = CreateAuthenticatedClient(token);

        var suffix = Guid.NewGuid().ToString("N")[..10];
        var updateRequest = new UserUpdateRequest
        {
            FirstName = "Updated",
            LastName = "Name",
            UserName = $"updated_{suffix}",
            Email = $"updated_{suffix}@test.com",
            ImageName = "new-avatar.jpg"
        };
        var response = await client.PutAsJsonAsync($"/api/users/{userId}", updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdateUser_Unauthenticated_ReturnsUnauthorized()
    {
        var updateRequest = new UserUpdateRequest
        {
            FirstName = "Hacker",
            LastName = "Person",
            UserName = "hacker",
            Email = "hacker@test.com",
            ImageName = "hack.jpg"
        };
        var response = await CreateAnonymousClient().PutAsJsonAsync("/api/users/1", updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
