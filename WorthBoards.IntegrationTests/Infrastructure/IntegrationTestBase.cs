using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Security.Claims;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Xunit;
using WorthBoards.Common.Enums;
using WorthBoards.Data.Database;

namespace WorthBoards.IntegrationTests.Infrastructure;

public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected HttpClient HttpClient = null!;
    protected WorthBoardsWebApplicationFactory Factory = null!;
    private SqliteConnection? _sqliteConnection;

    public async Task InitializeAsync()
    {
        Environment.SetEnvironmentVariable("WBJwtKey", "your-secret-key-must-be-at-least-32-characters-long!!!");
        Environment.SetEnvironmentVariable("WBIssuer", "WorthBoards");
        Environment.SetEnvironmentVariable("WBAudience", "WorthBoards");

        _sqliteConnection = new SqliteConnection("Data Source=:memory:");
        await _sqliteConnection.OpenAsync();

        // Create factory with an isolated in-memory SQLite database for this test instance
        Factory = new WorthBoardsWebApplicationFactory(_sqliteConnection);

        // Initialize database
        using (var scope = Factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await dbContext.Database.EnsureCreatedAsync();
        }

        HttpClient = Factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        HttpClient?.Dispose();
        Factory?.Dispose();

        if (_sqliteConnection != null)
        {
            await _sqliteConnection.CloseAsync();
            await _sqliteConnection.DisposeAsync();
        }
    }

    public string GenerateJwtToken(int userId, string email, UserRoleEnum role = UserRoleEnum.OWNER)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("your-secret-key-must-be-at-least-32-characters-long!!!"));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Email, email),
            new Claim(ClaimTypes.Role, role.ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: "WorthBoards",
            audience: "WorthBoards",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public void SetBearerToken(string token)
    {
        HttpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    protected async Task ResetDatabaseAsync()
    {
        using (var scope = Factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            await dbContext.Database.EnsureDeletedAsync();
            await dbContext.Database.EnsureCreatedAsync();
        }
    }
}
