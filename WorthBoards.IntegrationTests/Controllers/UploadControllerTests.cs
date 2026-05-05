using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WorthBoards.Common.Constrants;
using WorthBoards.Common.Exceptions;
using WorthBoards.IntegrationTests.Infrastructure;
using Xunit;

namespace WorthBoards.IntegrationTests.Controllers;

[Collection(nameof(IntegrationCollection))]
public sealed class UploadControllerTests(IntegrationTestAppFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task UploadImage_WithValidFile_ReturnsFilenameAndStoresFile()
    {
        // Arrange
        var user = await Factory.CreateUserAsync("Zoe", "Parker", "upload-user");
        var client = Factory.CreateAuthenticatedClient(user);
        var imageBytes = new byte[] { 1, 2, 3, 4 };
        await using var stream = new MemoryStream(imageBytes);
        var file = new StreamContent(stream);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");

        var content = new MultipartFormDataContent
        {
            { file, "image", "avatar.png" }
        };

        // Act
        var response = await client.PostAsync("/api/upload/image", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var filename = (await response.Content.ReadAsStringAsync()).Trim('"', '\r', '\n');
        filename.Should().NotBeNullOrWhiteSpace();
        Path.GetExtension(filename!).Should().Be(".png");

        var savedFile = Path.Combine(ImageFiles.STATIC_IMAGE_DIR, filename!);
        File.Exists(savedFile).Should().BeTrue();

        await Factory.WithDbContextAsync(async db =>
        {
            var persistedUser = await db.Users.AsNoTracking().SingleAsync(item => item.Id == user.Id);
            persistedUser.Email.Should().Be(user.Email);
        });

        if (File.Exists(savedFile))
        {
            File.Delete(savedFile);
        }
    }

    [Fact]
    public async Task UploadImage_WithUnsupportedExtension_ReturnsBadRequestAndDoesNotCreateFile()
    {
        // Arrange
        var user = await Factory.CreateUserAsync("Ava", "Stone", "upload-user-negative");
        var client = Factory.CreateAuthenticatedClient(user);
        var fileBytes = new byte[] { 9, 8, 7 };
        await using var stream = new MemoryStream(fileBytes);
        var file = new StreamContent(stream);
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");

        var content = new MultipartFormDataContent
        {
            { file, "image", "notes.txt" }
        };

        // Act
        var response = await client.PostAsync("/api/upload/image", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<ErrorDetails>();
        error.Should().NotBeNull();
        error!.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);

        await Factory.WithDbContextAsync(async db =>
        {
            var persistedUser = await db.Users.AsNoTracking().SingleAsync(item => item.Id == user.Id);
            persistedUser.Email.Should().Be(user.Email);
        });

        File.Exists(Path.Combine(ImageFiles.STATIC_IMAGE_DIR, "notes.txt")).Should().BeFalse();
    }
}