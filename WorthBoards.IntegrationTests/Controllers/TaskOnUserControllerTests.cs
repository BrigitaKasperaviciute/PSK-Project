using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Common.Enums;
using WorthBoards.IntegrationTests.Infrastructure;

namespace WorthBoards.IntegrationTests.Controllers;

public class TaskOnUserControllerTests(CustomWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    // ── POST link users to task ──────────────────────────────────────────────

    [Fact]
    public async Task LinkUsersToTask_AsEditor_ReturnsLinkedUsersAndPersists()
    {
        // Arrange
        var (_, _, ownerClient) = await RegisterAndLoginAsync();
        var (memberId, _, _) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(ownerClient);
        var task = await CreateTaskAsync(ownerClient, board.Id);
        await LinkUserToBoardDirectlyAsync(board.Id, memberId, UserRoleEnum.EDITOR);

        var linkList = new[] { new LinkUserToTaskRequest { UserId = memberId } };

        // Act
        var response = await ownerClient.PostAsJsonAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/users/link", linkList);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var links = await response.Content.ReadFromJsonAsync<IEnumerable<LinkUserToTaskResponse>>();
        links.Should().Contain(l => l.UserId == memberId);

        using var db = GetDbContext();
        db.TasksOnUsers.Any(tou => tou.BoardTaskId == task.Id && tou.UserId == memberId).Should().BeTrue();
    }

    [Fact]
    public async Task LinkUsersToTask_AsViewer_ReturnsForbidden()
    {
        // Arrange
        var (_, _, ownerClient) = await RegisterAndLoginAsync();
        var (viewerId, _, viewerClient) = await RegisterAndLoginAsync();
        var (targetId, _, _) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(ownerClient);
        var task = await CreateTaskAsync(ownerClient, board.Id);
        await LinkUserToBoardDirectlyAsync(board.Id, viewerId, UserRoleEnum.VIEWER);
        await LinkUserToBoardDirectlyAsync(board.Id, targetId, UserRoleEnum.VIEWER);

        var linkList = new[] { new LinkUserToTaskRequest { UserId = targetId } };

        // Act
        var response = await viewerClient.PostAsJsonAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/users/link", linkList);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── DELETE unlink users from task ────────────────────────────────────────

    [Fact]
    public async Task UnlinkUsersFromTask_AsEditor_ReturnsOkAndRemovesLink()
    {
        // Arrange
        var (_, _, ownerClient) = await RegisterAndLoginAsync();
        var (memberId, _, _) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(ownerClient);
        var task = await CreateTaskAsync(ownerClient, board.Id);
        await LinkUserToBoardDirectlyAsync(board.Id, memberId, UserRoleEnum.EDITOR);

        // First link the member to the task
        await ownerClient.PostAsJsonAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/users/link",
            new[] { new LinkUserToTaskRequest { UserId = memberId } });

        var unlinkList = new[] { new LinkUserToTaskRequest { UserId = memberId } };

        // Act
        var response = await ownerClient.SendAsync(new HttpRequestMessage(HttpMethod.Delete,
            $"/api/boards/{board.Id}/tasks/{task.Id}/users/unlink")
        {
            Content = JsonContent.Create(unlinkList)
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var db = GetDbContext();
        db.TasksOnUsers.Any(tou => tou.BoardTaskId == task.Id && tou.UserId == memberId).Should().BeFalse();
    }

    [Fact]
    public async Task UnlinkUsersFromTask_AsViewer_ReturnsForbidden()
    {
        // Arrange
        var (_, _, ownerClient) = await RegisterAndLoginAsync();
        var (viewerId, _, viewerClient) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(ownerClient);
        var task = await CreateTaskAsync(ownerClient, board.Id);
        await LinkUserToBoardDirectlyAsync(board.Id, viewerId, UserRoleEnum.VIEWER);

        var unlinkList = new[] { new LinkUserToTaskRequest { UserId = viewerId } };

        // Act
        var response = await viewerClient.SendAsync(new HttpRequestMessage(HttpMethod.Delete,
            $"/api/boards/{board.Id}/tasks/{task.Id}/users/unlink")
        {
            Content = JsonContent.Create(unlinkList)
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── GET users linked to task ─────────────────────────────────────────────

    [Fact]
    public async Task GetUsersLinkedToTask_AsMember_ReturnsLinkedUsers()
    {
        // Arrange
        var (ownerId, _, ownerClient) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(ownerClient);
        var task = await CreateTaskAsync(ownerClient, board.Id);

        // Link the owner to the task
        await ownerClient.PostAsJsonAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/users/link",
            new[] { new LinkUserToTaskRequest { UserId = ownerId } });

        // Act
        var response = await ownerClient.GetAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var users = await response.Content.ReadFromJsonAsync<IEnumerable<LinkedUserToTaskResponse>>();
        users.Should().Contain(u => u.Id == ownerId);
    }

    [Fact]
    public async Task GetUsersLinkedToTask_AsNonMember_ReturnsForbidden()
    {
        // Arrange
        var (_, _, ownerClient) = await RegisterAndLoginAsync();
        var (_, _, nonMemberClient) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(ownerClient);
        var task = await CreateTaskAsync(ownerClient, board.Id);

        // Act
        var response = await nonMemberClient.GetAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
