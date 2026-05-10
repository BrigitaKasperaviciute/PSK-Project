using System.Net;
using System.Net.Http.Headers;

namespace WorthBoards.IntegrationTests.Experiment1;

public class UploadScenarioTests(TestApplicationFactory factory) : IClassFixture<TestApplicationFactory>
{
    [Fact]
    public async Task Scenario_Upload_Happy_ValidImage_ReturnsGeneratedFilename()
    {
        var session = await TestAuthHelpers.RegisterAndLoginAsync(factory, "upload_happy");

        using var content = new MultipartFormDataContent();
        using var image = new ByteArrayContent(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x00, 0x01, 0x02, 0x03 });
        image.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(image, "image", "sample.png");

        var response = await session.Client.PostAsync("/api/upload/image", content);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(".png", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Scenario_Upload_Negative_WithoutAuth_ReturnsUnauthorized()
    {
        var anonymousClient = factory.CreateClient();

        using var content = new MultipartFormDataContent();
        using var image = new ByteArrayContent(new byte[] { 0x01, 0x02, 0x03 });
        image.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(image, "image", "sample.png");

        var response = await anonymousClient.PostAsync("/api/upload/image", content);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
