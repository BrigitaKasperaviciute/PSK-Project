using Microsoft.EntityFrameworkCore;

namespace WorthBoards.IntegrationTests.Controllers;

[Collection(nameof(ApiCollection))]
[Trait("Category", "Integration")]
public sealed class TaskOnUserControllerTests(CustomWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    // ── GET /api/boards/{boardId}/tasks/{taskId}/users ────────────────────────

    [Fact]
    public async Task GetUsersLinkedToTask_AsViewer_ReturnsOkWithUsers()
    {
        // Arrange
        var (userId, ownerToken) = await RegisterAndLoginAsync();
        SetAuthToken(ownerToken);
        var board = await CreateBoardAsync();
        var task = await CreateTaskAsync(board.Id);

        var (viewerId, viewerToken) = await RegisterAndLoginAsync();
        await LinkUserToBoardDirectlyAsync(board.Id, viewerId, UserRoleEnum.VIEWER);

        // Link owner to the task directly
        await using (var db = CreateDbContext())
        {
            db.TasksOnUsers.Add(new WorthBoards.Domain.Entities.TaskOnUser
            {
                BoardTaskId = task.Id,
                UserId = userId,
                AssignedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        SetAuthToken(viewerToken);

        // Act
        var response = await Client.GetAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<List<LinkedUserToTaskResponse>>();
        body.Should().NotBeNull();
        body!.Should().Contain(u => u.Id == userId);
    }

    [Fact]
    public async Task GetUsersLinkedToTask_WithoutBoardMembership_ReturnsForbidden()
    {
        // Arrange
        var (_, ownerToken) = await RegisterAndLoginAsync();
        SetAuthToken(ownerToken);
        var board = await CreateBoardAsync();
        var task = await CreateTaskAsync(board.Id);

        var (_, strangerToken) = await RegisterAndLoginAsync();
        SetAuthToken(strangerToken);

        // Act
        var response = await Client.GetAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── POST /api/boards/{boardId}/tasks/{taskId}/users/link ─────────────────

    [Fact]
    public async Task LinkUsersToTask_AsEditor_ReturnsOkAndPersistsLinks()
    {
        // Arrange
        var (_, ownerToken) = await RegisterAndLoginAsync();
        SetAuthToken(ownerToken);
        var board = await CreateBoardAsync();
        var task = await CreateTaskAsync(board.Id);

        var (editorId, editorToken) = await RegisterAndLoginAsync();
        await LinkUserToBoardDirectlyAsync(board.Id, editorId, UserRoleEnum.EDITOR);

        var (targetUserId, _) = await RegisterAndLoginAsync();
        SetAuthToken(editorToken);

        var linkList = new List<LinkUserToTaskRequest>
        {
            new LinkUserToTaskRequest { UserId = targetUserId }
        };

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/users/link", linkList);

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<List<LinkUserToTaskResponse>>();
        body.Should().NotBeNull();
        body!.Should().Contain(r => r.UserId == targetUserId);

        // Assert – database state
        await using var db = CreateDbContext();
        var persisted = await db.TasksOnUsers.AsNoTracking()
            .SingleOrDefaultAsync(tou => tou.BoardTaskId == task.Id
                                      && tou.UserId == targetUserId);
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

        var linkList = new List<LinkUserToTaskRequest>
        {
            new LinkUserToTaskRequest { UserId = viewerId }
        };

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/users/link", linkList);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task LinkUsersToTask_WithNonexistentTaskId_ReturnsNotFound()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);
        var board = await CreateBoardAsync();

        var linkList = new List<LinkUserToTaskRequest>
        {
            new LinkUserToTaskRequest { UserId = 1 }
        };

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/api/boards/{board.Id}/tasks/999999/users/link", linkList);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── DELETE /api/boards/{boardId}/tasks/{taskId}/users/unlink ─────────────

    [Fact]
    public async Task UnlinkUsersFromTask_AsEditor_ReturnsOkAndRemovesLinks()
    {
        // Arrange
        var (_, ownerToken) = await RegisterAndLoginAsync();
        SetAuthToken(ownerToken);
        var board = await CreateBoardAsync();
        var task = await CreateTaskAsync(board.Id);

        var (editorId, editorToken) = await RegisterAndLoginAsync();
        await LinkUserToBoardDirectlyAsync(board.Id, editorId, UserRoleEnum.EDITOR);

        var (targetUserId, _) = await RegisterAndLoginAsync();

        // Seed the task-user link directly
        await using (var db = CreateDbContext())
        {
            db.TasksOnUsers.Add(new WorthBoards.Domain.Entities.TaskOnUser
            {
                BoardTaskId = task.Id,
                UserId = targetUserId,
                AssignedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        SetAuthToken(editorToken);

        var unlinkList = new List<LinkUserToTaskRequest>
        {
            new LinkUserToTaskRequest { UserId = targetUserId }
        };

        // Act
        var response = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Delete,
            $"/api/boards/{board.Id}/tasks/{task.Id}/users/unlink")
        {
            Content = JsonContent.Create(unlinkList)
        });

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert – database state
        await using var db2 = CreateDbContext();
        var link = await db2.TasksOnUsers.AsNoTracking()
            .SingleOrDefaultAsync(tou => tou.BoardTaskId == task.Id
                                      && tou.UserId == targetUserId);
        link.Should().BeNull();
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

        var unlinkList = new List<LinkUserToTaskRequest>
        {
            new LinkUserToTaskRequest { UserId = viewerId }
        };

        // Act
        var response = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Delete,
            $"/api/boards/{board.Id}/tasks/{task.Id}/users/unlink")
        {
            Content = JsonContent.Create(unlinkList)
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
