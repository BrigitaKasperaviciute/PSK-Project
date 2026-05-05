using Microsoft.EntityFrameworkCore;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Common.Enums;
using WorthBoards.IntegrationTests.Infrastructure;

namespace WorthBoards.IntegrationTests.Controllers;

public sealed class TaskOnUserControllerTests(CustomWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    // ── LinkUsersToTask ───────────────────────────────────────────────────────

    [Fact]
    public async Task LinkUsersToTask_AsEditor_ReturnsOkAndPersistsAssignment()
    {
        // Arrange
        var (_, ownerToken) = await RegisterAndLoginAsync();
        SetAuthToken(ownerToken);
        var board = await CreateBoardAsync();
        var task = await CreateTaskAsync(board.Id);

        var (memberId, _) = await RegisterAndLoginAsync();
        await LinkUserToBoardDirectlyAsync(board.Id, memberId, UserRoleEnum.VIEWER);

        var linkRequests = new[] { new LinkUserToTaskRequest { UserId = memberId } };

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/users/link", linkRequests);

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert – response body
        var body = await response.Content.ReadFromJsonAsync<IEnumerable<LinkUserToTaskResponse>>();
        body.Should().NotBeNull();
        body!.Should().Contain(r => r.UserId == memberId);

        // Assert – database state
        await using var db = Factory.CreateDbContext();
        var persisted = await db.TasksOnUsers.AsNoTracking()
            .SingleOrDefaultAsync(tou => tou.BoardTaskId == task.Id && tou.UserId == memberId);
        persisted.Should().NotBeNull();
    }

    [Fact]
    public async Task LinkUsersToTask_AsViewer_ReturnsForbidden()
    {
        // Arrange
        var (_, ownerToken) = await RegisterAndLoginAsync();
        SetAuthToken(ownerToken);
        var board = await CreateBoardAsync();
        var task = await CreateTaskAsync(board.Id);

        var (viewerId, viewerToken) = await RegisterAndLoginAsync();
        await LinkUserToBoardDirectlyAsync(board.Id, viewerId, UserRoleEnum.VIEWER);
        SetAuthToken(viewerToken);

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/users/link",
            new[] { new LinkUserToTaskRequest { UserId = viewerId } });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── UnlinkUsersFromTask ───────────────────────────────────────────────────

    [Fact]
    public async Task UnlinkUsersFromTask_AsEditor_ReturnsOkAndRemovesAssignment()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);
        var board = await CreateBoardAsync();
        var task = await CreateTaskAsync(board.Id);

        var (memberId, _) = await RegisterAndLoginAsync();
        await LinkUserToBoardDirectlyAsync(board.Id, memberId, UserRoleEnum.VIEWER);

        // Link member to task first
        await Client.PostAsJsonAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/users/link",
            new[] { new LinkUserToTaskRequest { UserId = memberId } });

        // Act
        var response = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Delete,
            $"/api/boards/{board.Id}/tasks/{task.Id}/users/unlink")
        {
            Content = JsonContent.Create(new[] { new LinkUserToTaskRequest { UserId = memberId } })
        });

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert – database state
        await using var db = Factory.CreateDbContext();
        var persisted = await db.TasksOnUsers.AsNoTracking()
            .SingleOrDefaultAsync(tou => tou.BoardTaskId == task.Id && tou.UserId == memberId);
        persisted.Should().BeNull();
    }

    [Fact]
    public async Task UnlinkUsersFromTask_AsViewer_ReturnsForbidden()
    {
        // Arrange
        var (_, ownerToken) = await RegisterAndLoginAsync();
        SetAuthToken(ownerToken);
        var board = await CreateBoardAsync();
        var task = await CreateTaskAsync(board.Id);

        var (viewerId, viewerToken) = await RegisterAndLoginAsync();
        await LinkUserToBoardDirectlyAsync(board.Id, viewerId, UserRoleEnum.VIEWER);
        SetAuthToken(viewerToken);

        // Act
        var response = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Delete,
            $"/api/boards/{board.Id}/tasks/{task.Id}/users/unlink")
        {
            Content = JsonContent.Create(new[] { new LinkUserToTaskRequest { UserId = viewerId } })
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── GetUsersLinkedToTask ──────────────────────────────────────────────────

    [Fact]
    public async Task GetUsersLinkedToTask_WithAssignedUsers_ReturnsOkWithUsers()
    {
        // Arrange
        var (_, ownerToken) = await RegisterAndLoginAsync();
        SetAuthToken(ownerToken);
        var board = await CreateBoardAsync();
        var task = await CreateTaskAsync(board.Id);

        var (memberId, _) = await RegisterAndLoginAsync();
        await LinkUserToBoardDirectlyAsync(board.Id, memberId, UserRoleEnum.VIEWER);
        await Client.PostAsJsonAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/users/link",
            new[] { new LinkUserToTaskRequest { UserId = memberId } });

        // Act
        var response = await Client.GetAsync($"/api/boards/{board.Id}/tasks/{task.Id}/users");

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert – response body
        var body = await response.Content.ReadFromJsonAsync<IEnumerable<LinkedUserToTaskResponse>>();
        body.Should().NotBeNull();
        body!.Should().Contain(u => u.Id == memberId);
    }

    [Fact]
    public async Task GetUsersLinkedToTask_AsNonMember_ReturnsForbidden()
    {
        // Arrange
        var (_, ownerToken) = await RegisterAndLoginAsync();
        SetAuthToken(ownerToken);
        var board = await CreateBoardAsync();
        var task = await CreateTaskAsync(board.Id);

        var (_, strangerToken) = await RegisterAndLoginAsync();
        SetAuthToken(strangerToken);

        // Act
        var response = await Client.GetAsync($"/api/boards/{board.Id}/tasks/{task.Id}/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
