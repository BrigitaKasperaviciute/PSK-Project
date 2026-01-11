using System.Net;
using System.Net.Http.Json;
using WorthBoards.Api.Tests.Infrastructure;
using WorthBoards.Business.Dtos.Identity;
using Xunit.Abstractions;

namespace WorthBoards.Api.Tests.Performance;

public class AuthControllerPerformanceTests : PerformanceTestBase
{
    private readonly ITestOutputHelper _output;

    public AuthControllerPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task Login_WithValidCredentials_PerformanceTest()
    {
        var result = await RunPerformanceTestAsync(async () =>
        {
            var email = $"testuser_{Guid.NewGuid()}@example.com";
            var password = "TestPassword123!";

            await TestHelpers.CreateTestUserAsync(Factory.Services, email, password);

            var loginRequest = new UserLoginRequest(email, password);
            var response = await Client.PostAsJsonAsync("/login", loginRequest);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        });

        _output.WriteLine(result.ToString());
    }

    [Fact]
    public async Task Login_WithInvalidPassword_PerformanceTest()
    {
        var result = await RunPerformanceTestAsync(async () =>
        {
            var email = $"testuser_{Guid.NewGuid()}@example.com";
            var password = "TestPassword123!";

            await TestHelpers.CreateTestUserAsync(Factory.Services, email, password);

            var loginRequest = new UserLoginRequest(email, "WrongPassword123!");
            var response = await Client.PostAsJsonAsync("/login", loginRequest);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        });

        _output.WriteLine(result.ToString());
    }
}
