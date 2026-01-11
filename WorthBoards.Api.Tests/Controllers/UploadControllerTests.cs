using System.Net;
using System.Net.Http.Headers;
using System.Text;
using WorthBoards.Api.Tests.Infrastructure;

namespace WorthBoards.Api.Tests.Controllers;

public class UploadControllerTests : IntegrationTestBase
{
    [Fact]
    public async Task UploadImage_WithAuthorization_ReturnsOk()
    {
        // Arrange
        await ClearDatabaseAsync();
        var user = await TestHelpers.CreateTestUserAsync(Factory.Services);

        var token = GetJwtToken(user.Id, user.Email!);
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Create a fake image file
        var content = new MultipartFormDataContent();
        var imageContent = new ByteArrayContent(Encoding.UTF8.GetBytes("fake image content"));
        imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(imageContent, "image", "test.png");

        // Act
        var response = await Client.PostAsync("/api/upload/image", content);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var fileName = await response.Content.ReadAsStringAsync();
        Assert.NotNull(fileName);
        Assert.NotEmpty(fileName);
    }

    [Fact]
    public async Task UploadImage_WithoutAuthorization_ReturnsUnauthorized()
    {
        // Arrange
        await ClearDatabaseAsync();

        // Create a fake image file
        var content = new MultipartFormDataContent();
        var imageContent = new ByteArrayContent(Encoding.UTF8.GetBytes("fake image content"));
        imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(imageContent, "image", "test.png");

        // Act
        var response = await Client.PostAsync("/api/upload/image", content);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
