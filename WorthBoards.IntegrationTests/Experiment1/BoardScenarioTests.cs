using System.Net;

namespace WorthBoards.IntegrationTests.Experiment1;

public class BoardScenarioTests(TestApplicationFactory factory) : IClassFixture<TestApplicationFactory>
{
    [Fact]
    public async Task Scenario_Board_Happy_Create_Then_List_ReturnsCreatedBoard()
    {
        var session = await TestAuthHelpers.RegisterAndLoginAsync(factory, "boardhappy");

        var createdBoardId = await TestAuthHelpers.CreateBoardAsync(session.Client, "Happy Board");

        var listResponse = await session.Client.GetAsync("/api/boards?pageNum=0&pageSize=10");
        listResponse.EnsureSuccessStatusCode();

        var listBody = await listResponse.Content.ReadAsStringAsync();

        Assert.True(createdBoardId > 0);
        Assert.Contains(createdBoardId.ToString(), listBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Scenario_Board_Negative_List_Without_Token_ReturnsUnauthorized()
    {
        var anonymousClient = factory.CreateClient();

        var response = await anonymousClient.GetAsync("/api/boards?pageNum=0&pageSize=10");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
