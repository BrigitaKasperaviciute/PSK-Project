using System.Net;
using FluentAssertions;
using WorthBoards.Common.Constrants;
using WorthBoards.IntegrationTests.Experiment1.TestInfrastructure;
using Xunit;

namespace WorthBoards.IntegrationTests.Experiment1;

public class UploadControllerIntegrationTests(IntegrationTestWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    [Fact]
    public async Task UploadImage_ValidImage_ReturnsOkAndCreatesPhysicalFile()
    {
        // Arrange
        var session = await CreateAuthenticatedUserAsync("upload_happy");
        Directory.CreateDirectory(ImageFiles.STATIC_IMAGE_DIR);
        var filesBefore = Directory.GetFiles(ImageFiles.STATIC_IMAGE_DIR).Length;

        using var stream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5, 6 });
        using var content = new MultipartFormDataContent();
        content.Add(new StreamContent(stream), "image", "photo.png");

        // Act
        var response = await session.Client.PostAsync("/api/upload/image", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var filename = (await response.Content.ReadAsStringAsync()).Trim('"');
        filename.Should().EndWith(".png");

        var uploadedPath = Path.Combine(ImageFiles.STATIC_IMAGE_DIR, filename);
        File.Exists(uploadedPath).Should().BeTrue();

        var filesAfter = Directory.GetFiles(ImageFiles.STATIC_IMAGE_DIR).Length;
        filesAfter.Should().Be(filesBefore + 1);

        File.Delete(uploadedPath);
    }

    [Fact]
    public async Task UploadImage_NoAuthentication_ReturnsUnauthorizedAndDoesNotCreateFile()
    {
        // Arrange
        var client = CreateAnonymousClient();
        Directory.CreateDirectory(ImageFiles.STATIC_IMAGE_DIR);
        var filesBefore = Directory.GetFiles(ImageFiles.STATIC_IMAGE_DIR).Length;

        using var stream = new MemoryStream(new byte[] { 7, 8, 9, 10 });
        using var content = new MultipartFormDataContent();
        content.Add(new StreamContent(stream), "image", "unauthorized.png");

        // Act
        var response = await client.PostAsync("/api/upload/image", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().BeNullOrEmpty();

        var filesAfter = Directory.GetFiles(ImageFiles.STATIC_IMAGE_DIR).Length;
        filesAfter.Should().Be(filesBefore);
    }
}
