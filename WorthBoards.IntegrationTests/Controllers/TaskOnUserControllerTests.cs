using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Common.Enums;
using WorthBoards.IntegrationTests.Infrastructure;

namespace WorthBoards.IntegrationTests.Controllers;

public class TaskOnUserControllerTests(CustomWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    // ── POST /api/boards/{boardId}/tasks/{taskId}/users/link ──────────────────

    [Fact]
    public async Task LinkUsersToTask_EditorUser_ReturnsOkAndPersistsLinkToDb()
    {
        // Arrange
        var (ownerId, ownerToken) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(ownerToken);
        var task = await CreateTaskAsync(board.Id, ownerToken);

        var (memberId, _) = await RegisterAndLoginAsync();
        await LinkUserToBoardDirectlyAsync(board.Id, memberId, UserRoleEnum.EDITOR);

        SetAuthToken(ownerToken);
        var linkList = new List<LinkUserToTaskRequest> { new() { UserId = memberId } };

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/users/link", linkList);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<IEnumerable<LinkUserToTaskResponse>>();
        body.Should().NotBeNull();
        body!.Should().Contain(l => l.UserId == memberId);

        var dbLink = await QueryAsync(db =>
            db.TasksOnUsers.FirstOrDefaultAsync(t => t.BoardTaskId == task.Id && t.UserId == memberId));
        dbLink.Should().NotBeNull();
    }

    [Fact]
    public async Task LinkUsersToTask_ViewerUser_ReturnsForbidden()
    {
        // Arrange
        var (_, ownerToken) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(ownerToken);
        var task = await CreateTaskAsync(board.Id, ownerToken);

        var (viewerId, viewerToken) = await RegisterAndLoginAsync();
        await LinkUserToBoardDirectlyAsync(board.Id, viewerId, UserRoleEnum.VIEWER);
        SetAuthToken(viewerToken);

        var linkList = new List<LinkUserToTaskRequest> { new() { UserId = viewerId } };

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/users/link", linkList);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── DELETE /api/boards/{boardId}/tasks/{taskId}/users/unlink ──────────────

    [Fact]
    public async Task UnlinkUsersFromTask_EditorUser_ReturnsOkAndRemovesLinkFromDb()
    {
        // Arrange
        var (_, ownerToken) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(ownerToken);
        var task = await CreateTaskAsync(board.Id, ownerToken);

        var (memberId, _) = await RegisterAndLoginAsync();
        await LinkUserToBoardDirectlyAsync(board.Id, memberId, UserRoleEnum.EDITOR);

        // First link
        SetAuthToken(ownerToken);
        var linkList = new List<LinkUserToTaskRequest> { new() { UserId = memberId } };
        await Client.PostAsJsonAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/users/link", linkList);

        // Act — unlink
        var response = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Delete,
            $"/api/boards/{board.Id}/tasks/{task.Id}/users/unlink")
        {
            Content = JsonContent.Create(linkList)
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var dbLink = await QueryAsync(db =>
            db.TasksOnUsers.FirstOrDefaultAsync(t => t.BoardTaskId == task.Id && t.UserId == memberId));
        dbLink.Should().BeNull();
    }

    [Fact]
    public async Task UnlinkUsersFromTask_ViewerUser_ReturnsForbidden()
    {
        // Arrange
        var (_, ownerToken) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(ownerToken);
        var task = await CreateTaskAsync(board.Id, ownerToken);

        var (viewerId, viewerToken) = await RegisterAndLoginAsync();
        await LinkUserToBoardDirectlyAsync(board.Id, viewerId, UserRoleEnum.VIEWER);
        SetAuthToken(viewerToken);

        var linkList = new List<LinkUserToTaskRequest> { new() { UserId = viewerId } };

        // Act
        var response = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Delete,
            $"/api/boards/{board.Id}/tasks/{task.Id}/users/unlink")
        {
            Content = JsonContent.Create(linkList)
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── GET /api/boards/{boardId}/tasks/{taskId}/users ────────────────────────

    [Fact]
    public async Task GetUsersLinkedToTask_BoardMember_ReturnsOkWithLinkedUsers()
    {
        // Arrange
        var (ownerId, ownerToken) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(ownerToken);
        var task = await CreateTaskAsync(board.Id, ownerToken);

        SetAuthToken(ownerToken);
        var linkList = new List<LinkUserToTaskRequest> { new() { UserId = ownerId } };
        await Client.PostAsJsonAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/users/link", linkList);

        // Act
        var response = await Client.GetAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<IEnumerable<LinkedUserToTaskResponse>>();
        body.Should().NotBeNull();
        body!.Should().Contain(u => u.Id == ownerId);
    }

    [Fact]
    public async Task GetUsersLinkedToTask_NonMember_ReturnsForbidden()
    {
        // Arrange
        var (_, ownerToken) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(ownerToken);
        var task = await CreateTaskAsync(board.Id, ownerToken);

        var (_, outsiderToken) = await RegisterAndLoginAsync();
        SetAuthToken(outsiderToken);

        // Act
        var response = await Client.GetAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
