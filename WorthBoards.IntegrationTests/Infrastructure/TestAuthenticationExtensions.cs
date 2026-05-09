using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace WorthBoards.IntegrationTests.Infrastructure;

/// <summary>
/// Provides test authentication setup and JWT token generation for integration tests.
/// </summary>
public static class TestAuthenticationExtensions
{
    private const string TestKey = "this_is_a_test_secret_key_for_jwt_token_generation_in_tests_only";
    private const string TestIssuer = "WorthBoards.Tests";
    private const string TestAudience = "WorthBoards.Tests";

    public static IServiceCollection AddTestAuthentication(this IServiceCollection services)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestKey));

        // Check if authentication is already added
        var authBuilder = services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        });
        
        // Add or reconfigure JWT Bearer
        authBuilder.AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateIssuer = true,
                ValidIssuer = TestIssuer,
                ValidateAudience = true,
                ValidAudience = TestAudience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
            };
        });

        return services;
    }
}

/// <summary>
/// Generates test JWT tokens for use in integration tests.
/// </summary>
public static class TestJwtTokenGenerator
{
    private const string TestKey = "this_is_a_test_secret_key_for_jwt_token_generation_in_tests_only";
    private const string TestIssuer = "WorthBoards.Tests";
    private const string TestAudience = "WorthBoards.Tests";

    public static string GenerateToken(int userId, string userRole, TimeSpan? expiresIn = null)
    {
        return GenerateToken(userId, userRole, null, expiresIn);
    }

    public static string GenerateToken(int userId, string userRole, string? email, TimeSpan? expiresIn = null)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, userRole),
            new Claim("sub", userId.ToString()),
        };

        if (!string.IsNullOrWhiteSpace(email))
            claims.Add(new Claim(ClaimTypes.Email, email));

        var token = new JwtSecurityToken(
            issuer: TestIssuer,
            audience: TestAudience,
            claims: claims,
            expires: DateTime.UtcNow.Add(expiresIn ?? TimeSpan.FromHours(1)),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static string GenerateExpiredToken(int userId, string userRole)
    {
        return GenerateToken(userId, userRole, TimeSpan.FromSeconds(-1));
    }

    public static string GenerateInvalidToken()
    {
        return "invalid.token.here";
    }
}
