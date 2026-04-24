using System.Net;
using Moq;
using Xunit;
using WorthBoards.IntegrationTests.Infrastructure;

namespace WorthBoards.IntegrationTests.Tests;

public class UploadControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public UploadControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetAllMocks();
    }

    // ── UploadImage ───────────────────────────────────────────────────────

    [Fact]
    public async Task UploadImage_Authenticated_ReturnsOk()
    {
        _factory.FileServiceMock
            .Setup(s => s.UploadImage(It.IsAny<Microsoft.AspNetCore.Http.IFormFile>()))
            .ReturnsAsync("image.png");

        var content = new MultipartFormDataContent();
        var fileBytes = "fake-image-content"u8.ToArray();
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "image", "test.png");

        var client = _factory.CreateClient().WithAuth(userId: 1);
        var response = await client.PostAsync("/api/upload/image", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UploadImage_NoToken_ReturnsUnauthorized()
    {
        var content = new MultipartFormDataContent();
        var fileBytes = "fake-image-content"u8.ToArray();
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "image", "test.png");

        var response = await _factory.CreateClient().PostAsync("/api/upload/image", content);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}