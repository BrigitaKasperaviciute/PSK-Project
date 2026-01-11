using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using WorthBoards.Api.Tests.Infrastructure;
using WorthBoards.Business.Dtos.Identity;
using Xunit.Abstractions;

namespace WorthBoards.Api.Tests.Performance;

public class UserControllerPerformanceTests : PerformanceTestBase
{
    private readonly ITestOutputHelper _output;

    public UserControllerPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task GetUserById_WithExistingUser_PerformanceTest()
    {
        var result = await RunPerformanceTestAsync(async () =>
        {
            var user = await TestHelpers.CreateTestUserAsync(Factory.Services, $"user_{Guid.NewGuid()}@test.com");

            var token = GetJwtToken(user.Id, user.Email!);
            Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await Client.GetAsync($"/api/users/{user.Id}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var userResponse = await response.Content.ReadFromJsonAsync<UserResponse>();
            Assert.NotNull(userResponse);
        });

        _output.WriteLine(result.ToString());
    }

    [Fact]
    public async Task GetUserById_WithNonExistentUser_PerformanceTest()
    {
        var result = await RunPerformanceTestAsync(async () =>
        {
            var user = await TestHelpers.CreateTestUserAsync(Factory.Services, $"user_{Guid.NewGuid()}@test.com");

            var token = GetJwtToken(user.Id, user.Email!);
            Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await Client.GetAsync($"/api/users/99999");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        });

        _output.WriteLine(result.ToString());
    }
}
