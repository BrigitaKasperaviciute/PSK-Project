using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WorthBoards.Data.Identity;

namespace WorthBoards.IntegrationTests.Infrastructure;

internal static class TestUserManagerFactory
{
    public static UserManager<ApplicationUser> Create()
    {
        var services = new ServiceCollection().BuildServiceProvider();

        return new UserManager<ApplicationUser>(
            new NoOpUserStore(),
            Options.Create(new IdentityOptions()),
            new PasswordHasher<ApplicationUser>(),
            Array.Empty<IUserValidator<ApplicationUser>>(),
            Array.Empty<IPasswordValidator<ApplicationUser>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            services,
            NullLogger<UserManager<ApplicationUser>>.Instance);
    }

    private sealed class NoOpUserStore : IUserStore<ApplicationUser>
    {
        public Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken cancellationToken = default) =>
            Task.FromResult(IdentityResult.Success);

        public Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken cancellationToken = default) =>
            Task.FromResult(IdentityResult.Success);

        public Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken cancellationToken = default) =>
            Task.FromResult(IdentityResult.Success);

        public Task<ApplicationUser?> FindByIdAsync(string userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ApplicationUser?>(null);

        public Task<ApplicationUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken = default) =>
            Task.FromResult<ApplicationUser?>(null);

        public Task<string?> GetUserIdAsync(ApplicationUser user, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(user.Id.ToString());

        public Task<string?> GetUserNameAsync(ApplicationUser user, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(user.UserName);

        public Task SetUserNameAsync(ApplicationUser user, string? userName, CancellationToken cancellationToken = default)
        {
            user.UserName = userName;
            return Task.CompletedTask;
        }

        public Task<string?> GetNormalizedUserNameAsync(ApplicationUser user, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(user.NormalizedUserName);

        public Task SetNormalizedUserNameAsync(ApplicationUser user, string? normalizedName, CancellationToken cancellationToken = default)
        {
            user.NormalizedUserName = normalizedName;
            return Task.CompletedTask;
        }

        public void Dispose() { }
    }
}
