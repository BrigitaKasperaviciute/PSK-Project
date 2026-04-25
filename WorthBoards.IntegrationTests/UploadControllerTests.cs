using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using WorthBoards.IntegrationTests.Infrastructure;
using Xunit;

namespace WorthBoards.IntegrationTests;

public class UploadControllerTests(WorthBoardsWebApplicationFactory factory)
    : IClassFixture<WorthBoardsWebApplicationFactory>
{
    // Minimal valid 1×1 white PNG (67 bytes)
    private static readonly byte[] MinimalPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");

    private HttpClient AuthorizedClient(int userId, string userName, string email)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTokenHelper.GenerateToken(userId, userName, email));
        return client;
    }

    // ── POST /api/upload/image ───────────────────────────────────────────────

    [Fact]
    public async Task UploadImage_ValidImageAuthenticated_ReturnsOkWithFilename()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var user = await seeder.CreateUserAsync();
        var client = AuthorizedClient(user.Id, user.UserName!, user.Email!);

        using var multipart = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(MinimalPng);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
        multipart.Add(fileContent, "image", "test.png");

        // Act
        var response = await client.PostAsync("/api/upload/image", multipart);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var filename = await response.Content.ReadAsStringAsync();
        filename.Should().NotBeNullOrWhiteSpace();
        filename.Should().EndWith(".png");
    }

    [Fact]
    public async Task UploadImage_Unauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var client = factory.CreateClient();

        using var multipart = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(MinimalPng);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
        multipart.Add(fileContent, "image", "test.png");

        // Act
        var response = await client.PostAsync("/api/upload/image", multipart);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
