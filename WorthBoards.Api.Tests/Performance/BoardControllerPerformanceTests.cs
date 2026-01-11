using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using WorthBoards.Api.Tests.Infrastructure;
using WorthBoards.Business.Dtos.Responses;
using Xunit.Abstractions;

namespace WorthBoards.Api.Tests.Performance;

public class BoardControllerPerformanceTests : PerformanceTestBase
{
    private readonly ITestOutputHelper _output;

    public BoardControllerPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task GetBoardById_WithExistingBoard_PerformanceTest()
    {
        var result = await RunPerformanceTestAsync(async () =>
        {
            var user = await TestHelpers.CreateTestUserAsync(Factory.Services, $"user_{Guid.NewGuid()}@test.com");
            var board = await TestHelpers.CreateTestBoardAsync(Factory.Services, user.Id);

            var token = GetJwtToken(user.Id, user.Email!);
            Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await Client.GetAsync($"/api/boards/{board.Id}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var boardResponse = await response.Content.ReadFromJsonAsync<BoardResponse>();
            Assert.NotNull(boardResponse);
        });

        _output.WriteLine(result.ToString());
    }

    [Fact]
    public async Task GetBoardById_WithNonExistentBoard_PerformanceTest()
    {
        var result = await RunPerformanceTestAsync(async () =>
        {
            var user = await TestHelpers.CreateTestUserAsync(Factory.Services, $"user_{Guid.NewGuid()}@test.com");

            var token = GetJwtToken(user.Id, user.Email!);
            Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await Client.GetAsync($"/api/boards/99999");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        });

        _output.WriteLine(result.ToString());
    }
}
