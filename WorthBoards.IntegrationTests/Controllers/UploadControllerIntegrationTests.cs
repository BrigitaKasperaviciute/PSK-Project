using System.Net;
using FluentAssertions;
using Xunit;
using WorthBoards.IntegrationTests.Infrastructure;

namespace WorthBoards.IntegrationTests.Controllers;

public class UploadControllerIntegrationTests : IntegrationTestBase
{
    [Fact]
    public async Task UploadImage_ValidImageFile_ReturnsOkWithFileName()
    {
        // Arrange
        await ResetDatabaseAsync();
        var helper = new TestHelper(HttpClient, this);
        var (userId, token, _) = await helper.CreateAndLoginUserAsync();
        
        SetBearerToken(token);
        
        var imageContent = new ByteArrayContent(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }); // PNG header
        var multipartContent = new MultipartFormDataContent();
        multipartContent.Add(imageContent, "image", "test-image.png");

        // Act
        var response = await HttpClient.PostAsync("api/upload/image", multipartContent);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var fileName = await response.Content.ReadAsStringAsync();
        fileName.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task UploadImage_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        await ResetDatabaseAsync();
        
        var imageContent = new ByteArrayContent(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }); // PNG header
        var multipartContent = new MultipartFormDataContent();
        multipartContent.Add(imageContent, "image", "test-image.png");

        // Act
        var response = await HttpClient.PostAsync("api/upload/image", multipartContent);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UploadImage_WithDifferentFileType_ReturnsOk()
    {
        // Arrange
        await ResetDatabaseAsync();
        var helper = new TestHelper(HttpClient, this);
        var (userId, token, _) = await helper.CreateAndLoginUserAsync();
        
        SetBearerToken(token);
        
        var imageContent = new ByteArrayContent(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }); // JPEG header
        var multipartContent = new MultipartFormDataContent();
        multipartContent.Add(imageContent, "image", "test-image.jpg");

        // Act
        var response = await HttpClient.PostAsync("api/upload/image", multipartContent);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
