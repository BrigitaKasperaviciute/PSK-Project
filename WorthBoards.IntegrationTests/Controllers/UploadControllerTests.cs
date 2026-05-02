using System.Net;
using System.Net.Http.Headers;
using WorthBoards.IntegrationTests.Infrastructure;

namespace WorthBoards.IntegrationTests.Controllers;

public class UploadControllerTests : IntegrationTestBase
{
    public UploadControllerTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task UploadImage_Authenticated_ReturnsOk()
    {
        var (token, _) = await RegisterAndLoginAsync();
        var client = CreateAuthenticatedClient(token);

        using var content = new MultipartFormDataContent();
        var imageBytes = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };
        var fileContent = new ByteArrayContent(imageBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "image", "test.png");

        var response = await client.PostAsync("/api/upload/image", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var filename = await response.Content.ReadAsStringAsync();
        filename.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task UploadImage_Unauthenticated_ReturnsUnauthorized()
    {
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(new byte[] { 1, 2, 3 });
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "image", "test.png");

        var response = await CreateAnonymousClient().PostAsync("/api/upload/image", content);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
