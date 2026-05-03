using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WorthBoards.Common.Constrants;
using WorthBoards.Common.Exceptions;
using WorthBoards.Data.Database;
using WorthBoards.IntegrationTests.Infrastructure;

namespace WorthBoards.IntegrationTests;

public sealed class UploadControllerTests(WorthBoardsApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task UploadImage_WithValidPngFile_ReturnsStoredFilenameAndLeavesDatabaseUnchanged()
    {
        // Arrange
        var client = await CreateAuthenticatedClientAsync("owner");
        var existingFiles = Directory.EnumerateFiles(ImageFiles.STATIC_IMAGE_DIR).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var content = new MultipartFormDataContent();
        var imageContent = new ByteArrayContent([1, 2, 3, 4]);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(imageContent, "image", "worthboards-upload.png");

        // Act
        var response = await client.PostAsync("/api/upload/image", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var storedFileName = (await response.Content.ReadAsStringAsync()).Trim('"');
        storedFileName.Should().NotBeNullOrWhiteSpace();
        Path.GetExtension(storedFileName).Should().Be(".png");

        var savedFilePath = Path.Combine(ImageFiles.STATIC_IMAGE_DIR, storedFileName!);
        File.Exists(savedFilePath).Should().BeTrue();

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await context.Users.CountAsync()).Should().Be(5);
            var owner = await context.Users.SingleAsync(user => user.Id == Seed.OwnerUserId);
            owner.UserName.Should().Be("owner");
            owner.ImageName.Should().BeNull();
        }

        File.Delete(savedFilePath);
        Directory.EnumerateFiles(ImageFiles.STATIC_IMAGE_DIR).ToHashSet(StringComparer.OrdinalIgnoreCase)
            .Should().BeEquivalentTo(existingFiles);
    }

    [Fact]
    public async Task UploadImage_WithUnsupportedExtension_ReturnsBadRequestAndLeavesDatabaseUnchanged()
    {
        // Arrange
        var client = await CreateAuthenticatedClientAsync("owner");
        var existingFiles = Directory.EnumerateFiles(ImageFiles.STATIC_IMAGE_DIR).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent([1, 2, 3, 4]);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "image", "worthboards-upload.txt");

        // Act
        var response = await client.PostAsync("/api/upload/image", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await ReadJsonAsync<ErrorDetails>(response);
        error.Should().NotBeNull();
        error!.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
        error.Details.Should().Contain("Supported image formats");

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await context.Users.CountAsync()).Should().Be(5);
            var owner = await context.Users.SingleAsync(user => user.Id == Seed.OwnerUserId);
            owner.UserName.Should().Be("owner");
        }

        Directory.EnumerateFiles(ImageFiles.STATIC_IMAGE_DIR).ToHashSet(StringComparer.OrdinalIgnoreCase)
            .Should().BeEquivalentTo(existingFiles);
    }
}