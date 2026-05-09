using System.Net;
using FluentAssertions;
using WorthBoards.Data.Identity;
using Microsoft.Extensions.DependencyInjection;
using WorthBoards.IntegrationTests.Infrastructure;
using Xunit;

namespace WorthBoards.IntegrationTests.Controllers;

[Collection(nameof(ApiCollection))]
public class UploadControllerIntegrationTests
{
    private readonly ApiFactory _factory;

    public UploadControllerIntegrationTests(ApiFactory factory)
    {
        _factory = factory;
    }

    #region UploadImage Tests

    [Fact]
    public async Task UploadImage_WithValidImageFile_ReturnsOkAndFilename()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (user, _) = await CreateUserAsync(1, "uploader@test.com");
        var client = _factory.CreateClientForUser(user.Id, "OWNER");

        // Create a simple test image (1x1 PNG)
        var pngBytes = new byte[] 
        { 
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
            0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
            0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
            0x89, 0x00, 0x00, 0x00, 0x0A, 0x49, 0x44, 0x41,
            0x54, 0x08, 0xD8, 0x63, 0xF8, 0xFF, 0xFF, 0x3F,
            0x00, 0x05, 0xFE, 0x02, 0xFE, 0xA1, 0x33, 0x81,
            0x84, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E,
            0x44, 0xAE, 0x42, 0x60, 0x82
        };

        var memoryStream = new MemoryStream(pngBytes);
        var streamContent = new StreamContent(memoryStream);
        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");

        using (var content = new MultipartFormDataContent())
        {
            content.Add(streamContent, "image", "test-image.png");

            // Act
            var response = await client.PostAsync("/api/upload/image", content);

            // Assert - HTTP layer
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            // Assert - response body contains filename
            var filename = await response.Content.ReadAsStringAsync();
            filename.Should().NotBeNullOrEmpty();
            filename.Should().EndWith(".png");
        }
    }

    [Fact]
    public async Task UploadImage_WithJpegImage_ReturnsOkAndFilename()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (user, _) = await CreateUserAsync(2, "jpeguploader@test.com");
        var client = _factory.CreateClientForUser(user.Id, "OWNER");

        // Create a minimal valid JPEG (start marker + end marker)
        var jpegBytes = new byte[] 
        { 
            0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46,
            0x49, 0x46, 0x00, 0x01, 0x01, 0x00, 0x00, 0x01,
            0x00, 0x01, 0x00, 0x00, 0xFF, 0xD9
        };

        var memoryStream = new MemoryStream(jpegBytes);
        var streamContent = new StreamContent(memoryStream);
        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");

        using (var content = new MultipartFormDataContent())
        {
            content.Add(streamContent, "image", "test-image.jpg");

            // Act
            var response = await client.PostAsync("/api/upload/image", content);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var filename = await response.Content.ReadAsStringAsync();
            filename.Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public async Task UploadImage_WithoutAuthorization_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        var memoryStream = new MemoryStream(new byte[] { 0x89, 0x50, 0x4E, 0x47 });
        var streamContent = new StreamContent(memoryStream);
        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");

        using (var content = new MultipartFormDataContent())
        {
            content.Add(streamContent, "image", "test-image.png");

            // Act
            var response = await client.PostAsync("/api/upload/image", content);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
    }

    [Fact]
    public async Task UploadImage_WithoutFormFile_ReturnsBadRequest()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (user, _) = await CreateUserAsync(3, "nofile@test.com");
        var client = _factory.CreateClientForUser(user.Id, "OWNER");

        using (var content = new MultipartFormDataContent())
        {
            // Not adding the image file at all

            // Act
            var response = await client.PostAsync("/api/upload/image", content);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
    }

    [Fact]
    public async Task UploadImage_WithEmptyFile_ReturnsBadRequest()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (user, _) = await CreateUserAsync(4, "emptyfile@test.com");
        var client = _factory.CreateClientForUser(user.Id, "OWNER");

        var memoryStream = new MemoryStream(Array.Empty<byte>());
        var streamContent = new StreamContent(memoryStream);
        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");

        using (var content = new MultipartFormDataContent())
        {
            content.Add(streamContent, "image", "empty.png");

            // Act
            var response = await client.PostAsync("/api/upload/image", content);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
    }

    [Fact]
    public async Task UploadImage_WithInvalidImageType_ReturnsBadRequest()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (user, _) = await CreateUserAsync(5, "invalidtype@test.com");
        var client = _factory.CreateClientForUser(user.Id, "OWNER");

        var textBytes = System.Text.Encoding.UTF8.GetBytes("This is a text file, not an image");
        var memoryStream = new MemoryStream(textBytes);
        var streamContent = new StreamContent(memoryStream);
        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");

        using (var content = new MultipartFormDataContent())
        {
            content.Add(streamContent, "image", "notanimage.txt");

            // Act
            var response = await client.PostAsync("/api/upload/image", content);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
    }

    [Fact]
    public async Task UploadImage_WithGifImage_ReturnsOkAndFilename()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (user, _) = await CreateUserAsync(6, "gifuploader@test.com");
        var client = _factory.CreateClientForUser(user.Id, "OWNER");

        // Minimal valid GIF (1x1 transparent)
        var gifBytes = new byte[]
        {
            0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0x01, 0x00,
            0x01, 0x00, 0x80, 0x00, 0x00, 0x00, 0x00, 0x00,
            0xFF, 0xFF, 0xFF, 0x21, 0xF9, 0x04, 0x01, 0x00,
            0x00, 0x00, 0x00, 0x2C, 0x00, 0x00, 0x00, 0x00,
            0x01, 0x00, 0x01, 0x00, 0x00, 0x02, 0x02, 0x44,
            0x01, 0x00, 0x3B
        };

        var memoryStream = new MemoryStream(gifBytes);
        var streamContent = new StreamContent(memoryStream);
        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/gif");

        using (var content = new MultipartFormDataContent())
        {
            content.Add(streamContent, "image", "test-image.gif");

            // Act
            var response = await client.PostAsync("/api/upload/image", content);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var filename = await response.Content.ReadAsStringAsync();
            filename.Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public async Task UploadImage_MultipleUploads_ReturnUniqueFilenames()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (user, _) = await CreateUserAsync(7, "multiupload@test.com");
        var client = _factory.CreateClientForUser(user.Id, "OWNER");

        var pngBytes = new byte[] 
        { 
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
            0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
            0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
            0x89, 0x00, 0x00, 0x00, 0x0A, 0x49, 0x44, 0x41,
            0x54, 0x08, 0xD8, 0x63, 0xF8, 0xFF, 0xFF, 0x3F,
            0x00, 0x05, 0xFE, 0x02, 0xFE, 0xA1, 0x33, 0x81,
            0x84, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E,
            0x44, 0xAE, 0x42, 0x60, 0x82
        };

        // Act - Upload first image
        string filename1;
        using (var content1 = new MultipartFormDataContent())
        {
            var stream1 = new MemoryStream(pngBytes);
            var streamContent1 = new StreamContent(stream1);
            streamContent1.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            content1.Add(streamContent1, "image", "test1.png");

            var response1 = await client.PostAsync("/api/upload/image", content1);
            response1.StatusCode.Should().Be(HttpStatusCode.OK);
            filename1 = await response1.Content.ReadAsStringAsync();
        }

        // Act - Upload second image
        string filename2;
        using (var content2 = new MultipartFormDataContent())
        {
            var stream2 = new MemoryStream(pngBytes);
            var streamContent2 = new StreamContent(stream2);
            streamContent2.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            content2.Add(streamContent2, "image", "test2.png");

            var response2 = await client.PostAsync("/api/upload/image", content2);
            response2.StatusCode.Should().Be(HttpStatusCode.OK);
            filename2 = await response2.Content.ReadAsStringAsync();
        }

        // Assert - Filenames should be different (not overwritten)
        filename1.Should().NotBe(filename2);
    }

    #endregion

    #region Helper Methods

    private async Task<(ApplicationUser User, string Password)> CreateUserAsync(
        int userId,
        string email,
        string userName = null!,
        string password = "DefaultPassword123!")
    {
        userName ??= $"user{userId}";

        var dbContext = _factory.CreateDbContext();
        var userManager = _factory.Services.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>();

        var user = new ApplicationUser
        {
            Id = userId,
            FirstName = $"User",
            LastName = $"Number{userId}",
            UserName = userName.Length > 30 ? userName.Substring(0, 30) : userName,
            Email = email,
            NormalizedEmail = email.ToUpper(),
            NormalizedUserName = userName.ToUpper(),
            SecurityStamp = Guid.NewGuid().ToString(),
            CreationDate = DateTime.UtcNow
        };

        await userManager.CreateAsync(user, password);
        dbContext.Dispose();

        return (user, password);
    }

    #endregion
}
