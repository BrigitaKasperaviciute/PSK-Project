using System.Net;
using System.Net.Http.Headers;
using Xunit;

namespace WorthBoards.Api.IntegrationTests.Controllers;

public class UploadControllerTests : IntegrationTestBase
{
    public UploadControllerTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task UploadImage_WhenAuthorized_ReturnsOk()
    {
        // Arrange
        var client = CreateAuthenticatedClient("1", "test@example.com");

        // Create a simple image file content
        var imageContent = new ByteArrayContent(new byte[] { 0x89, 0x50, 0x4E, 0x47 }); // PNG header
        imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");

        var formData = new MultipartFormDataContent
        {
            { imageContent, "image", "test.png" }
        };

        // Act
        var response = await client.PostAsync("/api/upload/image", formData);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UploadImage_WhenUnauthorized_ReturnsUnauthorized()
    {
        // Arrange
        var client = CreateUnauthenticatedClient();

        // Create a simple image file content
        var imageContent = new ByteArrayContent(new byte[] { 0x89, 0x50, 0x4E, 0x47 }); // PNG header
        imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");

        var formData = new MultipartFormDataContent
        {
            { imageContent, "image", "test.png" }
        };

        // Act
        var response = await client.PostAsync("/api/upload/image", formData);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
