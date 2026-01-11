using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using WorthBoards.Api.Tests.Infrastructure;
using WorthBoards.Business.Dtos.Identity;

namespace WorthBoards.Api.Tests.Controllers;

public class UserControllerTests : IntegrationTestBase
{
    [Fact]
    public async Task GetUserById_WithExistingUser_ReturnsUser()
    {
        // Arrange
        await ClearDatabaseAsync();
        var user = await TestHelpers.CreateTestUserAsync(Factory.Services);

        var token = GetJwtToken(user.Id, user.Email!);
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await Client.GetAsync($"/api/users/{user.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var userResponse = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.NotNull(userResponse);
        Assert.Equal(user.Id, userResponse.Id);
        Assert.Equal(user.Email, userResponse.Email);
    }

    [Fact]
    public async Task GetUserById_WithNonExistentUser_ReturnsNotFound()
    {
        // Arrange
        await ClearDatabaseAsync();
        var user = await TestHelpers.CreateTestUserAsync(Factory.Services);

        var token = GetJwtToken(user.Id, user.Email!);
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var nonExistentUserId = 99999;

        // Act
        var response = await Client.GetAsync($"/api/users/{nonExistentUserId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
