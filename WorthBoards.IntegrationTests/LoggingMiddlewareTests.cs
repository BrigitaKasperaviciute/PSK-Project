using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using WorthBoards.Business.Dtos.Identity;
using WorthBoards.IntegrationTests.Infrastructure;
using Xunit;

namespace WorthBoards.IntegrationTests;

public class LoggingMiddlewareTests(LoggingEnabledWebApplicationFactory factory)
    : IClassFixture<LoggingEnabledWebApplicationFactory>
{
    private HttpClient AuthorizedClient(int userId, string userName, string email)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTokenHelper.GenerateToken(userId, userName, email));
        return client;
    }

    // ── GET request with no body ─────────────────────────────────────────────
    // Covers: InvokeAsync, LogRequest ("No JSON body" branch), LogResponse, GetUsername (authenticated)

    [Fact]
    public async Task GetBoards_Authenticated_ThroughMiddleware_ReturnsOk()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        await seeder.CreateBoardAsync(owner);
        var client = AuthorizedClient(owner.Id, owner.UserName!, owner.Email!);

        // Act
        var response = await client.GetAsync("/api/boards");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── POST request with invalid JSON body ──────────────────────────────────
    // Covers: SanitizeBody catch block (JObject.Parse fails on invalid JSON)

    [Fact]
    public async Task PostLogin_WithInvalidJsonBody_ThroughMiddleware_ReturnsBadRequest()
    {
        // Arrange – send Content-Type: application/json with a body that is not valid JSON.
        // The middleware reads it, JObject.Parse throws, catch returns "Sanitation failure!".
        var client = factory.CreateClient();
        var invalidContent = new StringContent("not-valid-json{{", Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("/login", invalidContent);

        // Assert – model binding fails so the endpoint returns 400
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
