using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;
using Moq;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Common.Enums;
using WorthBoards.IntegrationTests.Infrastructure;

namespace WorthBoards.IntegrationTests.Tests;

public class TaskOnUserControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public TaskOnUserControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetAllMocks();
    }

    private HttpClient AuthClient(int userId = 1) => _factory.CreateClient().WithAuth(userId);

    private static StringContent Json(object body) =>
        new(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

    private void SetupOwnerRole() =>
        _factory.BoardServiceMock
            .Setup(s => s.GetUserRoleByBoardIdAndUserIdAsync(It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(UserRoleEnum.OWNER);

    private void SetupNoRole() =>
        _factory.BoardServiceMock
            .Setup(s => s.GetUserRoleByBoardIdAndUserIdAsync(It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync((UserRoleEnum?)null);

    // ── LinkUsersToTask ───────────────────────────────────────────────────

    [Fact]
    public async Task LinkUsersToTask_EditorRole_ReturnsOk()
    {
        SetupOwnerRole();
        _factory.TaskOnUserServiceMock
            .Setup(s => s.LinkUsersToTaskAsync(1, 1, It.IsAny<IEnumerable<LinkUserToTaskRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LinkUserToTaskResponse> { new() { UserId = 2, AssignedAt = DateTime.UtcNow } });
        _factory.NotificationServiceMock
            .Setup(s => s.NotifyTaskAssigned(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Link user 2 (different from token user 1) → triggers notification
        var body = new[] { new { userId = 2 } };
        var response = await AuthClient(userId: 1).PostAsync("/api/boards/1/tasks/1/users/link", Json(body));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task LinkUsersToTask_InsufficientRole_ReturnsForbidden()
    {
        SetupNoRole();

        var body = new[] { new { userId = 2 } };
        var response = await AuthClient().PostAsync("/api/boards/1/tasks/1/users/link", Json(body));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task LinkUsersToTask_SelfLink_ReturnsOkWithoutNotification()
    {
        SetupOwnerRole();
        // response.UserId == caller userId → if-branch is false → NotifyTaskAssigned NOT called
        _factory.TaskOnUserServiceMock
            .Setup(s => s.LinkUsersToTaskAsync(1, 1, It.IsAny<IEnumerable<LinkUserToTaskRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LinkUserToTaskResponse> { new() { UserId = 1, AssignedAt = DateTime.UtcNow } });

        var body = new[] { new { userId = 1 } };
        var response = await AuthClient(userId: 1).PostAsync("/api/boards/1/tasks/1/users/link", Json(body));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        _factory.NotificationServiceMock.Verify(
            s => s.NotifyTaskAssigned(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── UnlinkUsersFromTask ───────────────────────────────────────────────

    [Fact]
    public async Task UnlinkUsersFromTask_EditorRole_ReturnsOk()
    {
        SetupOwnerRole();
        _factory.TaskOnUserServiceMock
            .Setup(s => s.UnlinkUsersFromTaskAsync(1, 1, It.IsAny<IEnumerable<LinkUserToTaskRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LinkUserToTaskResponse>());

        var body = new[] { new { userId = 2 } };
        var client = AuthClient();
        var request = new HttpRequestMessage(HttpMethod.Delete, "/api/boards/1/tasks/1/users/unlink")
        {
            Content = Json(body)
        };
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UnlinkUsersFromTask_InsufficientRole_ReturnsForbidden()
    {
        SetupNoRole();

        var body = new[] { new { userId = 2 } };
        // Use SendAsync so we can include a body on DELETE
        var request = new HttpRequestMessage(HttpMethod.Delete, "/api/boards/1/tasks/1/users/unlink")
        {
            Content = Json(body)
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer", JwtTokenHelper.GenerateToken(userId: 1));

        var response = await _factory.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── GetUsersLinkedToTask ──────────────────────────────────────────────

    [Fact]
    public async Task GetUsersLinkedToTask_ViewerRole_ReturnsOk()
    {
        SetupOwnerRole();
        _factory.TaskOnUserServiceMock
            .Setup(s => s.GetUsersLinkedToTaskAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LinkedUserToTaskResponse>());

        var response = await AuthClient().GetAsync("/api/boards/1/tasks/1/users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetUsersLinkedToTask_InsufficientRole_ReturnsForbidden()
    {
        SetupNoRole();

        var response = await AuthClient().GetAsync("/api/boards/1/tasks/1/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}