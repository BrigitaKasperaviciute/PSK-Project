using System.Net;
using System.Net.Http.Json;
using WorthBoards.Business.Dtos.Identity;
using WorthBoards.Data.Identity;
using Xunit;

namespace WorthBoards.Api.IntegrationTests.Controllers;

public class UserControllerTests : IntegrationTestBase
{
    public UserControllerTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task GetUserById_WhenUserExists_ReturnsUser()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = 1,
            FirstName = "Test",
            LastName = "User",
            UserName = "testuser",
            Email = "test@example.com",
            CreationDate = DateTime.UtcNow
        };
        DbContext.Users.Add(user);

        await DbContext.SaveChangesAsync();

        var client = CreateAuthenticatedClient("1", "test@example.com");

        // Act
        var response = await client.GetAsync("/api/users/1");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.NotNull(result);
        Assert.Equal(1, result.Id);
        Assert.Equal("Test", result.FirstName);
        Assert.Equal("User", result.LastName);
    }

    [Fact]
    public async Task GetUserById_WhenUserDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = 1,
            FirstName = "Test",
            LastName = "User",
            UserName = "testuser",
            Email = "test@example.com",
            CreationDate = DateTime.UtcNow
        };
        DbContext.Users.Add(user);

        await DbContext.SaveChangesAsync();

        var client = CreateAuthenticatedClient("1", "test@example.com");

        // Act
        var response = await client.GetAsync("/api/users/999");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
