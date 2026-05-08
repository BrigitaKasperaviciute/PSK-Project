using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Moq;
using Xunit;
using WorthBoards.Business.Dtos.Identity;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Common.Enums;
using WorthBoards.IntegrationTests.Infrastructure;

namespace WorthBoards.IntegrationTests.Tests;

public class LoggingCoverageTests : IClassFixture<LoggingWebApplicationFactory>
{
    private readonly LoggingWebApplicationFactory _factory;

    public LoggingCoverageTests(LoggingWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetAllMocks();
    }

    private HttpClient AuthClient() => _factory.CreateClient().WithAuth(userId: 1);

    private static StringContent Json(object body) =>
        new(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

    private void SetupOwnerRole() =>
        _factory.BoardServiceMock
            .Setup(s => s.GetUserRoleByBoardIdAndUserIdAsync(It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(UserRoleEnum.OWNER);

    // ── ControllerLoggingActionFilter: non-board endpoint (null boardId) ──

    [Fact]
    public async Task Filter_NonBoardEndpointRequest_LogsWithNullBoardId()
    {
        _factory.UserServiceMock
            .Setup(s => s.UpdateUser(1, It.IsAny<UserUpdateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserUpdateResponse { FirstName = "A", LastName = "B", UserName = "alice", Email = "alice@test.com" });

        // No boardId route key → GetRoleWithBoardIdAsync returns (null, null)
        var response = await AuthClient().PutAsync("/api/users/1",
            Json(new { firstName = "A", lastName = "B", userName = "alice", email = "alice@test.com", imageName = "" }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
