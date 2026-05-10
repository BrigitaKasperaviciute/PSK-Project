using System.Net;

namespace WorthBoards.IntegrationTests.Controllers;

public sealed class UploadControllerTests
{
    [Fact]
    public async Task UploadImage_ReturnsOk_WhenFileServiceSucceeds()
    {
        await using var factory = new WorthBoardsApiFactory();
        var client = factory.CreateAuthorizedClient();

        factory.FileServiceMock
            .Setup(service => service.UploadImage(It.IsAny<IFormFile>()))
            .ReturnsAsync("image-1.png");

        var response = await client.SendAsync(TestRequestFactory.CreateMultipartImageRequest("/api/upload/image"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UploadImage_ReturnsInternalServerError_WhenFileServiceThrows()
    {
        await using var factory = new WorthBoardsApiFactory();
        var client = factory.CreateAuthorizedClient();

        factory.FileServiceMock
            .Setup(service => service.UploadImage(It.IsAny<IFormFile>()))
            .ThrowsAsync(new Exception("storage failure"));

        var response = await client.SendAsync(TestRequestFactory.CreateMultipartImageRequest("/api/upload/image"));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }
}