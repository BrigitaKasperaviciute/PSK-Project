using System.Net;
using System.Net.Http.Headers;
using System.Text;
using WorthBoards.Api.Tests.Infrastructure;
using Xunit.Abstractions;

namespace WorthBoards.Api.Tests.Performance;

public class UploadControllerPerformanceTests : PerformanceTestBase
{
    private readonly ITestOutputHelper _output;

    public UploadControllerPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task UploadImage_WithAuthorization_PerformanceTest()
    {
        var result = await RunPerformanceTestAsync(async () =>
        {
            var user = await TestHelpers.CreateTestUserAsync(Factory.Services, $"user_{Guid.NewGuid()}@test.com");

            var token = GetJwtToken(user.Id, user.Email!);
            Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var content = new MultipartFormDataContent();
            var imageContent = new ByteArrayContent(Encoding.UTF8.GetBytes("fake image content"));
            imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            content.Add(imageContent, "image", "test.png");

            var response = await Client.PostAsync("/api/upload/image", content);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        });

        _output.WriteLine(result.ToString());
    }

    [Fact]
    public async Task UploadImage_WithoutAuthorization_PerformanceTest()
    {
        var result = await RunPerformanceTestAsync(async () =>
        {
            var content = new MultipartFormDataContent();
            var imageContent = new ByteArrayContent(Encoding.UTF8.GetBytes("fake image content"));
            imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            content.Add(imageContent, "image", "test.png");

            var response = await Client.PostAsync("/api/upload/image", content);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        });

        _output.WriteLine(result.ToString());
    }
}
