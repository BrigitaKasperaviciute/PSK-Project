using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using WorthBoards.Api.Tests.Infrastructure;
using WorthBoards.Business.Dtos.Responses;
using Xunit.Abstractions;

namespace WorthBoards.Api.Tests.Performance;

public class BoardTaskControllerPerformanceTests : PerformanceTestBase
{
    private readonly ITestOutputHelper _output;

    public BoardTaskControllerPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task GetBoardTaskById_WithExistingTask_PerformanceTest()
    {
        var result = await RunPerformanceTestAsync(async () =>
        {
            var user = await TestHelpers.CreateTestUserAsync(Factory.Services, $"user_{Guid.NewGuid()}@test.com");
            var board = await TestHelpers.CreateTestBoardAsync(Factory.Services, user.Id);
            var task = await TestHelpers.CreateTestTaskAsync(Factory.Services, board.Id);

            var token = GetJwtToken(user.Id, user.Email!);
            Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await Client.GetAsync($"/api/boards/{board.Id}/tasks/{task.Id}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var taskResponse = await response.Content.ReadFromJsonAsync<BoardTaskResponse>();
            Assert.NotNull(taskResponse);
        });

        _output.WriteLine(result.ToString());
    }

    [Fact]
    public async Task GetBoardTaskById_WithNonExistentTask_PerformanceTest()
    {
        var result = await RunPerformanceTestAsync(async () =>
        {
            var user = await TestHelpers.CreateTestUserAsync(Factory.Services, $"user_{Guid.NewGuid()}@test.com");
            var board = await TestHelpers.CreateTestBoardAsync(Factory.Services, user.Id);

            var token = GetJwtToken(user.Id, user.Email!);
            Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await Client.GetAsync($"/api/boards/{board.Id}/tasks/99999");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        });

        _output.WriteLine(result.ToString());
    }
}
