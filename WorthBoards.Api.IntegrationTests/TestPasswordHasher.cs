using Microsoft.AspNetCore.Identity;
using System.Security.Cryptography;
using System.Text;

namespace WorthBoards.Api.IntegrationTests;

/// <summary>
/// Fast password hasher for testing purposes only.
/// Uses simple SHA256 instead of BCrypt to speed up tests.
/// WARNING: Never use this in production!
/// </summary>
public class TestPasswordHasher<TUser> : IPasswordHasher<TUser> where TUser : class
{
    public string HashPassword(TUser user, string password)
    {
        // Use simple SHA256 for testing - much faster than BCrypt
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(password);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    public PasswordVerificationResult VerifyHashedPassword(TUser user, string hashedPassword, string providedPassword)
    {
        var hashOfProvidedPassword = HashPassword(user, providedPassword);
        return hashedPassword == hashOfProvidedPassword
            ? PasswordVerificationResult.Success
            : PasswordVerificationResult.Failed;
    }
}
