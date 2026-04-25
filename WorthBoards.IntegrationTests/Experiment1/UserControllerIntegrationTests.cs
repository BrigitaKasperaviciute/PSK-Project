using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WorthBoards.Business.Dtos.Identity;
using WorthBoards.IntegrationTests.Experiment1.TestInfrastructure;
using Xunit;

namespace WorthBoards.IntegrationTests.Experiment1;

public class UserControllerIntegrationTests(IntegrationTestWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    [Fact]
    public async Task UpdateUser_ValidRequest_UpdatesPersistedUser()
    {
        // Arrange
        var session = await CreateAuthenticatedUserAsync("user_update");
        var request = new UserUpdateRequest
        {
            FirstName = "Updated",
            LastName = "Person",
            UserName = $"upd_{Guid.NewGuid():N}"[..20],
            Email = $"updated_{Guid.NewGuid():N}@example.com"[..30] + "@x.com",
            ImageName = "avatar.png"
        };

        // Act
        var response = await session.Client.PutAsJsonAsync($"/api/users/{session.UserId}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseBody = await response.Content.ReadAsStringAsync();
        responseBody.Should().Contain("Updated");

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WorthBoards.Data.Database.ApplicationDbContext>();
        var persistedUser = await dbContext.Users.SingleAsync(u => u.Id == session.UserId);
        persistedUser.FirstName.Should().Be(request.FirstName);
        persistedUser.LastName.Should().Be(request.LastName);
        persistedUser.UserName.Should().Be(request.UserName);
    }

    [Fact]
    public async Task GetUserById_NonExistingUser_ReturnsNotFoundAndKeepsDatabaseUnchanged()
    {
        // Arrange
        var session = await CreateAuthenticatedUserAsync("user_get_negative");

        using var arrangeScope = Factory.Services.CreateScope();
        var arrangeDb = arrangeScope.ServiceProvider.GetRequiredService<WorthBoards.Data.Database.ApplicationDbContext>();
        var usersBefore = await arrangeDb.Users.CountAsync();

        // Act
        var response = await session.Client.GetAsync("/api/users/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var responseBody = await response.Content.ReadAsStringAsync();
        responseBody.Should().ContainEquivalentOf("not found");

        using var assertScope = Factory.Services.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<WorthBoards.Data.Database.ApplicationDbContext>();
        var usersAfter = await assertDb.Users.CountAsync();
        usersAfter.Should().Be(usersBefore);
    }

    [Fact]
    public async Task GetUserById_ExistingUser_ReturnsUserResponse()
    {
        // Arrange
        var session = await CreateAuthenticatedUserAsync("user_get_happy");

        // Act
        var response = await session.Client.GetAsync($"/api/users/{session.UserId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var user = await response.Content.ReadFromJsonAsync<UserResponse>();
        user.Should().NotBeNull();
        user!.Id.Should().Be(session.UserId);
    }
}
