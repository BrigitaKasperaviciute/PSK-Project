using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using WorthBoards.Api.IntegrationTests.Infrastructure;
using Xunit;

namespace WorthBoards.Api.IntegrationTests;

public class UploadControllerTests : IClassFixture<IntegrationTestFactory>
{
    private readonly IntegrationTestFactory _factory;

    public UploadControllerTests(IntegrationTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task UploadImage_ReturnsFileName_WhenAuthorized()
    {
        var client = _factory.CreateAuthenticatedClient();
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent([1, 2, 3]);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "image", "test.png");

        var response = await client.PostAsync("/api/upload/image", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var filename = await response.Content.ReadAsStringAsync();
        filename.Should().Contain(FakeFileService.StoredFileName);
    }

    [Fact]
    public async Task UploadImage_ReturnsUnauthorized_WhenMissingAuthHeader()
    {
        var client = _factory.CreateClient();
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent([1, 2, 3]);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "image", "test.png");

        var response = await client.PostAsync("/api/upload/image", content);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
