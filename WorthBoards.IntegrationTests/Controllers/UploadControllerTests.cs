using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Moq;
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

    // ---------------------------------------------------------------------------
    // POST /api/upload/image  [Authorize]
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task UploadImage_WhenAuthenticated_ReturnsOkWithFilename()
    {
        // Arrange
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var user = await seeder.CreateUserAsync();

        var client = _factory.CreateAuthenticatedClient(user.Id, user.UserName!, user.Email!);

        using var form = new MultipartFormDataContent();
        var imageBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }; // minimal JPEG header
        form.Add(new ByteArrayContent(imageBytes) { Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg") } }, "image", "test.jpg");

        // Act
        var response = await client.PostAsync("/api/upload/image", form);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var filename = await response.Content.ReadAsStringAsync();
        filename.Should().Contain("test-image.jpg");
    }

    [Fact]
    public async Task UploadImage_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        using var form = new MultipartFormDataContent();
        var imageBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };
        form.Add(new ByteArrayContent(imageBytes) { Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg") } }, "image", "test.jpg");

        // Act
        var response = await client.PostAsync("/api/upload/image", form);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
