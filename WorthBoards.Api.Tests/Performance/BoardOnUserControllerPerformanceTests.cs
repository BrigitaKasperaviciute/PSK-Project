using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using WorthBoards.Api.Tests.Infrastructure;
using WorthBoards.Business.Dtos.Responses;
using Xunit.Abstractions;

namespace WorthBoards.Api.Tests.Performance;

public class BoardOnUserControllerPerformanceTests : PerformanceTestBase
{
    private readonly ITestOutputHelper _output;

    public BoardOnUserControllerPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task GetBoardToUserLink_WithExistingLink_PerformanceTest()
    {
        var result = await RunPerformanceTestAsync(async () =>
        {
            var user = await TestHelpers.CreateTestUserAsync(Factory.Services, $"user_{Guid.NewGuid()}@test.com");
            var board = await TestHelpers.CreateTestBoardAsync(Factory.Services, user.Id);

            var token = GetJwtToken(user.Id, user.Email!);
            Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await Client.GetAsync($"/api/boards/{board.Id}/link/{user.Id}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var linkResponse = await response.Content.ReadFromJsonAsync<LinkUserToBoardResponse>();
            Assert.NotNull(linkResponse);
        });

        _output.WriteLine(result.ToString());
    }

    [Fact]
    public async Task GetBoardToUserLink_WithNonExistentLink_PerformanceTest()
    {
        var result = await RunPerformanceTestAsync(async () =>
        {
            var user = await TestHelpers.CreateTestUserAsync(Factory.Services, $"user_{Guid.NewGuid()}@test.com");
            var board = await TestHelpers.CreateTestBoardAsync(Factory.Services, user.Id);

            var token = GetJwtToken(user.Id, user.Email!);
            Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await Client.GetAsync($"/api/boards/{board.Id}/link/99999");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        });

        _output.WriteLine(result.ToString());
    }
}
