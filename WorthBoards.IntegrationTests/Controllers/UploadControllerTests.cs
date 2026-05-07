using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using WorthBoards.Common.Constrants;
using WorthBoards.IntegrationTests.Infrastructure;
using Xunit;

namespace WorthBoards.IntegrationTests.Controllers;

[Collection(nameof(ApiCollection))]
[Trait("Category", "Integration")]
public sealed class UploadControllerTests : IAsyncLifetime
{
    private readonly ApiFactory _factory;

    public UploadControllerTests(ApiFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        await using var db = _factory.CreateDbContext();
        await DatabaseSeeder.CleanDatabaseAsync(db);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<(int UserId, HttpClient Client)> CreateUserWithClientAsync(string? username = null, string? email = null)
    {
        await using var db = _factory.CreateDbContext();
        var userManager = _factory.Services.CreateScope().ServiceProvider
            .GetRequiredService<UserManager<Data.Identity.ApplicationUser>>();
        var user = await DatabaseSeeder.CreateUserAsync(db, userManager, username, email);
        return (user.Id, _factory.CreateAuthenticatedClient(user.Id, user.UserName!, user.Email!));
    }

    [Fact]
    public async Task UploadImage_WithValidImage_ReturnsOkAndStoresFile()
    {
        // Arrange
        var (_, client) = await CreateUserWithClientAsync("upload_user", "upload_user@test.com");
        var fileName = $"upload-{Guid.NewGuid():N}.png";
        var imageBytes = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(imageBytes);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
        content.Add(fileContent, "image", fileName);

        Directory.CreateDirectory(ImageFiles.STATIC_IMAGE_DIR);

        // Act
        var response = await client.PostAsync("/api/upload/image", content);

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - response body
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain(".png");

        // Assert - file persisted to disk
        var savedFileName = body.Trim('"');
        var savedFilePath = Path.Combine(ImageFiles.STATIC_IMAGE_DIR, savedFileName);
        File.Exists(savedFilePath).Should().BeTrue();

        File.Delete(savedFilePath);
    }

    [Fact]
    public async Task UploadImage_WithInvalidExtension_ReturnsBadRequest()
    {
        // Arrange
        var (_, client) = await CreateUserWithClientAsync("upload_bad_ext", "upload_bad_ext@test.com");
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(new byte[] { 1, 2, 3 });
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("text/plain");
        content.Add(fileContent, "image", "bad.txt");

        // Act
        var response = await client.PostAsync("/api/upload/image", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UploadImage_WithOversizedFile_ReturnsBadRequest()
    {
        // Arrange
        var (_, client) = await CreateUserWithClientAsync("upload_big", "upload_big@test.com");
        var oversizedBytes = new byte[ImageFiles.IMAGE_MAX_SIZE + 1];
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(oversizedBytes);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
        content.Add(fileContent, "image", "big.png");

        // Act
        var response = await client.PostAsync("/api/upload/image", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}