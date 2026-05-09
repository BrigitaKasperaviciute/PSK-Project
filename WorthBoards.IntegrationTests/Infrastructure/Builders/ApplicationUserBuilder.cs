using WorthBoards.Data.Identity;

namespace WorthBoards.IntegrationTests.Infrastructure;

/// <summary>
/// Builder for creating test ApplicationUser instances with sensible defaults.
/// </summary>
internal sealed class ApplicationUserBuilder
{
    private string _userName = $"testuser_{Guid.NewGuid():N}".Substring(0, 30); // Max 30 chars
    private string _email = $"user-{Guid.NewGuid():N}@test.com";
    private string _firstName = "Test";
    private string _lastName = "User";
    private string? _imageName;
    private DateTime _creationDate = DateTime.UtcNow;

    public ApplicationUserBuilder WithUserName(string userName)
    {
        _userName = userName.Length > 30 ? userName.Substring(0, 30) : userName;
        return this;
    }

    public ApplicationUserBuilder WithEmail(string email)
    {
        _email = email;
        return this;
    }

    public ApplicationUserBuilder WithFirstName(string firstName)
    {
        _firstName = firstName;
        return this;
    }

    public ApplicationUserBuilder WithLastName(string lastName)
    {
        _lastName = lastName;
        return this;
    }

    public ApplicationUserBuilder WithImageName(string? imageName)
    {
        _imageName = imageName;
        return this;
    }

    public ApplicationUser Build()
    {
        return new ApplicationUser
        {
            UserName = _userName,
            Email = _email,
            FirstName = _firstName,
            LastName = _lastName,
            ImageName = _imageName,
            CreationDate = _creationDate,
            EmailConfirmed = true,
        };
    }
}
