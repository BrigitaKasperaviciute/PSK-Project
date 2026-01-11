using System.Net;
using System.Net.Http.Headers;
using WorthBoards.Api.Tests.Infrastructure;
using Xunit.Abstractions;

namespace WorthBoards.Api.Tests.Performance;

public class NotificationControllerPerformanceTests : PerformanceTestBase
{
    private readonly ITestOutputHelper _output;

    public NotificationControllerPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task DeleteNotification_WithExistingNotification_PerformanceTest()
    {
        var result = await RunPerformanceTestAsync(async () =>
        {
            var user = await TestHelpers.CreateTestUserAsync(Factory.Services, $"user_{Guid.NewGuid()}@test.com");
            var board = await TestHelpers.CreateTestBoardAsync(Factory.Services, user.Id);
            var notification = await TestHelpers.CreateTestNotificationAsync(Factory.Services, user.Id, board.Id);

            var token = GetJwtToken(user.Id, user.Email!);
            Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await Client.DeleteAsync($"/api/notifications/{notification.Id}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        });

        _output.WriteLine(result.ToString());
    }

    [Fact]
    public async Task DeleteNotification_WithNonExistentNotification_PerformanceTest()
    {
        var result = await RunPerformanceTestAsync(async () =>
        {
            var user = await TestHelpers.CreateTestUserAsync(Factory.Services, $"user_{Guid.NewGuid()}@test.com");

            var token = GetJwtToken(user.Id, user.Email!);
            Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await Client.DeleteAsync($"/api/notifications/99999");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        });

        _output.WriteLine(result.ToString());
    }
}
