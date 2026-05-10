using System.Net;

namespace WorthBoards.IntegrationTests.Experiment1;

public class UserScenarioTests(TestApplicationFactory factory) : IClassFixture<TestApplicationFactory>
{
    [Fact]
    public async Task Scenario_User_Happy_GetOwnProfile_ReturnsUser()
    {
        var session = await TestAuthHelpers.RegisterAndLoginAsync(factory, "userhappy");

        var response = await session.Client.GetAsync($"/api/users/{session.UserId}");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(session.UserName, body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Scenario_User_Negative_GetProfile_WithoutAuth_ReturnsUnauthorized()
    {
        var anonymousClient = factory.CreateClient();

        var response = await anonymousClient.GetAsync("/api/users/1");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
