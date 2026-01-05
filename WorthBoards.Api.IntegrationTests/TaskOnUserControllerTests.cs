using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using WorthBoards.Api.IntegrationTests.Infrastructure;
using WorthBoards.Business.Dtos.Responses;
using Xunit;

namespace WorthBoards.Api.IntegrationTests;

public class TaskOnUserControllerTests : IClassFixture<IntegrationTestFactory>
{
    private readonly HttpClient _authorizedClient;

    public TaskOnUserControllerTests(IntegrationTestFactory factory)
    {
        _authorizedClient = factory.CreateAuthenticatedClient();
    }

    [Fact]
    public async Task GetUsersLinkedToTask_ReturnsUsers_WhenTaskExists()
    {
        var response = await _authorizedClient.GetAsync("/api/boards/1/tasks/1/users");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var users = await response.Content.ReadFromJsonAsync<IEnumerable<LinkedUserToTaskResponse>>();
        users.Should().NotBeNull();
        users!.Should().Contain(u => u.Id == 1);
    }

    [Fact]
    public async Task GetUsersLinkedToTask_ReturnsNotFound_ForUnknownTask()
    {
        var response = await _authorizedClient.GetAsync("/api/boards/1/tasks/999/users");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
