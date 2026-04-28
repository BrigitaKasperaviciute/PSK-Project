using System.Net;
using FluentAssertions;
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
        var fileBytes = new byte[] { 137, 80, 78, 71 }; // PNG header bytes
        content.Add(new ByteArrayContent(fileBytes), "image", "test.png");

        var response = await client.PostAsync("/api/upload/image", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UploadImage_Unauthenticated_ReturnsUnauthorized()
    {
        using var content = new MultipartFormDataContent();
        var fileBytes = new byte[] { 137, 80, 78, 71 };
        content.Add(new ByteArrayContent(fileBytes), "image", "test.png");

        var response = await CreateAnonymousClient().PostAsync("/api/upload/image", content);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
