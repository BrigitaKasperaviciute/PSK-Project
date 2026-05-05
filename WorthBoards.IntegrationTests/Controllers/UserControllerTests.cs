using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorthBoards.Business.Dtos.Identity;
using WorthBoards.Common.Exceptions;
using WorthBoards.IntegrationTests.Infrastructure;
using Xunit;

namespace WorthBoards.IntegrationTests.Controllers;

[Collection(nameof(IntegrationCollection))]
public sealed class UserControllerTests(IntegrationTestAppFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task GetAndUpdateUser_WithValidToken_ReturnsCurrentUserAndPersistsChanges()
    {
        // Arrange
        var user = await Factory.CreateUserAsync("Xena", "Cole", "user-controller");
        var client = Factory.CreateAuthenticatedClient(user);

        // Act - get
        var getResponse = await client.GetAsync($"/api/users/{user.Id}");

        // Assert - get
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var getBody = await getResponse.Content.ReadFromJsonAsync<UserResponse>();
        getBody.Should().NotBeNull();
        getBody!.Id.Should().Be(user.Id);
        getBody.UserName.Should().Be(user.UserName);

        // Act - update
        var updateRequest = new UserUpdateRequest
        {
            FirstName = "Xavier",
            LastName = "Coleman",
            UserName = user.UserName!,
            Email = user.Email!,
            ImageName = "profile.png"
        };

        var updateResponse = await client.PutAsJsonAsync($"/api/users/{user.Id}", updateRequest);

        // Assert - update
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updateBody = await updateResponse.Content.ReadFromJsonAsync<UserUpdateResponse>();
        updateBody.Should().NotBeNull();
        updateBody!.FirstName.Should().Be(updateRequest.FirstName);
        updateBody.LastName.Should().Be(updateRequest.LastName);

        await Factory.WithDbContextAsync(async db =>
        {
            var persisted = await db.Users.AsNoTracking().SingleAsync(item => item.Id == user.Id);
            persisted.FirstName.Should().Be(updateRequest.FirstName);
            persisted.LastName.Should().Be(updateRequest.LastName);
            persisted.ImageName.Should().Be(updateRequest.ImageName);
        });
    }

    [Fact]
    public async Task UpdateUser_WhenUserMissing_ReturnsBadRequestAndDoesNotPersist()
    {
        // Arrange
        var user = await Factory.CreateUserAsync("Yuri", "Fox", "user-controller-negative");
        var client = Factory.CreateAuthenticatedClient(user);
        var request = new UserUpdateRequest
        {
            FirstName = "Updated",
            LastName = "User",
            UserName = user.UserName!,
            Email = user.Email!,
            ImageName = "updated.png"
        };

        // Act
        var response = await client.PutAsJsonAsync("/api/users/9999", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<ErrorDetails>();
        error.Should().NotBeNull();
        error!.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);

        await Factory.WithDbContextAsync(async db =>
        {
            var persisted = await db.Users.AsNoTracking().SingleAsync(item => item.Id == user.Id);
            persisted.FirstName.Should().Be(user.FirstName);
            persisted.ImageName.Should().BeNull();
        });
    }
}