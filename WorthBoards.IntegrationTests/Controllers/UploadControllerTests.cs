using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using WorthBoards.IntegrationTests.Infrastructure;

namespace WorthBoards.IntegrationTests.Controllers;

public class UploadControllerTests(CustomWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    // ── POST /api/upload/image ───────────────────────────────────────────────

    [Fact]
    public async Task UploadImage_AuthenticatedWithValidFile_ReturnsFilename()
    {
        // Arrange
        var (_, _, client) = await RegisterAndLoginAsync();
        var multipart = new MultipartFormDataContent();
        var fileBytes = new ByteArrayContent(new byte[] { 0x89, 0x50, 0x4E, 0x47 }); // PNG magic bytes
        fileBytes.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        multipart.Add(fileBytes, "image", "test.png");

        // Act
        var response = await client.PostAsync("/api/upload/image", multipart);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var filename = await response.Content.ReadAsStringAsync();
        filename.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task UploadImage_Unauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var anonClient = Factory.CreateClient();
        var multipart = new MultipartFormDataContent();
        var fileBytes = new ByteArrayContent(new byte[] { 1, 2, 3 });
        fileBytes.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        multipart.Add(fileBytes, "image", "test.png");

        // Act
        var response = await anonClient.PostAsync("/api/upload/image", multipart);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
