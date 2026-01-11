using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using WorthBoards.Api.Tests.Infrastructure;
using WorthBoards.Business.Dtos.Responses;
using Xunit.Abstractions;

namespace WorthBoards.Api.Tests.Performance;

public class CommentControllerPerformanceTests : PerformanceTestBase
{
    private readonly ITestOutputHelper _output;

    public CommentControllerPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task GetCommentById_WithExistingComment_PerformanceTest()
    {
        var result = await RunPerformanceTestAsync(async () =>
        {
            var user = await TestHelpers.CreateTestUserAsync(Factory.Services, $"user_{Guid.NewGuid()}@test.com");
            var board = await TestHelpers.CreateTestBoardAsync(Factory.Services, user.Id);
            var task = await TestHelpers.CreateTestTaskAsync(Factory.Services, board.Id);
            var comment = await TestHelpers.CreateTestCommentAsync(Factory.Services, task.Id, user.Id);

            var token = GetJwtToken(user.Id, user.Email!);
            Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await Client.GetAsync($"/api/boards/{board.Id}/tasks/{task.Id}/comments/{comment.Id}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var commentResponse = await response.Content.ReadFromJsonAsync<CommentResponse>();
            Assert.NotNull(commentResponse);
        });

        _output.WriteLine(result.ToString());
    }

    [Fact]
    public async Task GetCommentById_WithNonExistentComment_PerformanceTest()
    {
        var result = await RunPerformanceTestAsync(async () =>
        {
            var user = await TestHelpers.CreateTestUserAsync(Factory.Services, $"user_{Guid.NewGuid()}@test.com");
            var board = await TestHelpers.CreateTestBoardAsync(Factory.Services, user.Id);
            var task = await TestHelpers.CreateTestTaskAsync(Factory.Services, board.Id);

            var token = GetJwtToken(user.Id, user.Email!);
            Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await Client.GetAsync($"/api/boards/{board.Id}/tasks/{task.Id}/comments/99999");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        });

        _output.WriteLine(result.ToString());
    }
}
