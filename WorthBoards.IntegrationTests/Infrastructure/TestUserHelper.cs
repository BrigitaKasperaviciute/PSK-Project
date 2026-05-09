using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using WorthBoards.Data.Identity;
using WorthBoards.Domain.Entities;

namespace WorthBoards.IntegrationTests.Infrastructure;

/// <summary>
/// Helper class for creating test users properly using UserManager from DI container.
/// </summary>
internal sealed class TestUserHelper
{
    private readonly ApiFactory _factory;

    public TestUserHelper(ApiFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Creates a user with the specified email and gets a JWT token for testing.
    /// </summary>
    public async Task<(ApplicationUser user, string token)> CreateUserAndGetTokenAsync(
        int userId,
        string email,
        string role = "USER",
        string password = "TestPassword123!")
    {
        var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = new ApplicationUserBuilder()
            .WithEmail(email)
            .WithUserName($"user{userId}")
            .Build();

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to create test user: {errors}");
        }

        var token = TestJwtTokenGenerator.GenerateToken(user.Id, role);
        return (user, token);
    }

    /// <summary>
    /// Creates a user without generating a token.
    /// </summary>
    public async Task<ApplicationUser> CreateUserAsync(
        string email,
        string password = "TestPassword123!")
    {
        var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = new ApplicationUserBuilder()
            .WithEmail(email)
            .Build();

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to create test user: {errors}");
        }

        return user;
    }
}
