using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Common.Enums;
using WorthBoards.IntegrationTests.Infrastructure;
using Xunit;

namespace WorthBoards.IntegrationTests.Controllers;

[Collection(nameof(ApiCollection))]
[Trait("Category", "Integration")]
public sealed class TaskOnUserControllerTests : IAsyncLifetime
{
    private readonly ApiFactory _factory;

    public TaskOnUserControllerTests(ApiFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        await using var db = _factory.CreateDbContext();
        await DatabaseSeeder.CleanDatabaseAsync(db);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<(int UserId, HttpClient Client)> CreateUserWithClientAsync(string? username = null, string? email = null)
    {
        await using var db = _factory.CreateDbContext();
        var userManager = _factory.Services.CreateScope().ServiceProvider
            .GetRequiredService<UserManager<Data.Identity.ApplicationUser>>();
        var user = await DatabaseSeeder.CreateUserAsync(db, userManager, username, email);
        return (user.Id, _factory.CreateAuthenticatedClient(user.Id, user.UserName!, user.Email!));
    }

    // --- POST /api/boards/{boardId}/tasks/{taskId}/users/link ---

    [Fact]
    public async Task LinkUsersToTask_AsEditor_ReturnsOkAndPersistsLink()
    {
        // Arrange
        var (editorId, editorClient) = await CreateUserWithClientAsync("editor_link", "editor_link@test.com");
        var (targetUserId, _) = await CreateUserWithClientAsync("target_link", "target_link@test.com");
        await using var db = _factory.CreateDbContext();
        var board = await DatabaseSeeder.CreateBoardAsync(db, editorId, UserRoleEnum.EDITOR);
        await DatabaseSeeder.AddUserToBoardAsync(db, board.Id, targetUserId, UserRoleEnum.VIEWER);
        var task = await DatabaseSeeder.CreateTaskAsync(db, board.Id, "Assignable Task");

        var request = new[] { new LinkUserToTaskRequest { UserId = targetUserId } };

        // Act
        var response = await editorClient.PostAsJsonAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/users/link", request);

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - response body
        var body = await response.Content.ReadFromJsonAsync<IEnumerable<LinkedUserToTaskResponse>>();
        body.Should().NotBeNull();
        body.Should().HaveCountGreaterThanOrEqualTo(1);

        // Assert - database state
        await using var verifyDb = _factory.CreateDbContext();
        var link = await verifyDb.TasksOnUsers.AsNoTracking()
            .SingleOrDefaultAsync(tou => tou.BoardTaskId == task.Id && tou.UserId == targetUserId);
        link.Should().NotBeNull();
    }

    [Fact]
    public async Task LinkUsersToTask_AsViewer_ReturnsForbidden()
    {
        // Arrange
        var (ownerId, _) = await CreateUserWithClientAsync("task_owner", "task_owner@test.com");
        var (viewerId, viewerClient) = await CreateUserWithClientAsync("task_viewer", "task_viewer@test.com");
        await using var db = _factory.CreateDbContext();
        var board = await DatabaseSeeder.CreateBoardAsync(db, ownerId, UserRoleEnum.OWNER);
        await DatabaseSeeder.AddUserToBoardAsync(db, board.Id, viewerId, UserRoleEnum.VIEWER);
        var task = await DatabaseSeeder.CreateTaskAsync(db, board.Id, "Protected Task");

        var request = new[] { new LinkUserToTaskRequest { UserId = viewerId } };

        // Act
        var response = await viewerClient.PostAsJsonAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/users/link", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // --- DELETE /api/boards/{boardId}/tasks/{taskId}/users/unlink ---

    [Fact]
    public async Task UnlinkUsersFromTask_AsEditor_ReturnsOkAndRemovesLink()
    {
        // Arrange
        var (editorId, editorClient) = await CreateUserWithClientAsync("unlink_editor", "unlink_editor@test.com");
        var (assigneeId, _) = await CreateUserWithClientAsync("unlink_assignee", "unlink_assignee@test.com");
        await using var db = _factory.CreateDbContext();
        var board = await DatabaseSeeder.CreateBoardAsync(db, editorId, UserRoleEnum.EDITOR);
        await DatabaseSeeder.AddUserToBoardAsync(db, board.Id, assigneeId, UserRoleEnum.VIEWER);
        var task = await DatabaseSeeder.CreateTaskAsync(db, board.Id, "Task with Assignee");

        // Seed the task-user link directly
        await DatabaseSeeder.AddUserToTaskAsync(db, task.Id, assigneeId);

        var requestBody = new[] { new LinkUserToTaskRequest { UserId = assigneeId } };

        // Act
        var httpRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/boards/{board.Id}/tasks/{task.Id}/users/unlink")
        {
            Content = System.Net.Http.Json.JsonContent.Create(requestBody)
        };
        var response = await editorClient.SendAsync(httpRequest);

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - database state
        await using var verifyDb = _factory.CreateDbContext();
        var link = await verifyDb.TasksOnUsers.AsNoTracking()
            .SingleOrDefaultAsync(tou => tou.BoardTaskId == task.Id && tou.UserId == assigneeId);
        link.Should().BeNull();
    }

    [Fact]
    public async Task UnlinkUsersFromTask_WhenTaskNotFound_ReturnsNotFound()
    {
        // Arrange
        var (editorId, editorClient) = await CreateUserWithClientAsync("unlink_notfound_editor", "unlink_notfound_editor@test.com");
        await using var db = _factory.CreateDbContext();
        var board = await DatabaseSeeder.CreateBoardAsync(db, editorId, UserRoleEnum.EDITOR);

        var requestBody = new[] { new LinkUserToTaskRequest { UserId = editorId } };

        // Act
        var httpRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/boards/{board.Id}/tasks/99999/users/unlink")
        {
            Content = System.Net.Http.Json.JsonContent.Create(requestBody)
        };
        var response = await editorClient.SendAsync(httpRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- GET /api/boards/{boardId}/tasks/{taskId}/users ---

    [Fact]
    public async Task GetUsersLinkedToTask_AsViewer_ReturnsOkWithAssignedUsers()
    {
        // Arrange
        var (ownerId, _) = await CreateUserWithClientAsync("get_task_owner", "get_task_owner@test.com");
        var (viewerId, viewerClient) = await CreateUserWithClientAsync("get_task_viewer", "get_task_viewer@test.com");
        await using var db = _factory.CreateDbContext();
        var board = await DatabaseSeeder.CreateBoardAsync(db, ownerId, UserRoleEnum.OWNER);
        await DatabaseSeeder.AddUserToBoardAsync(db, board.Id, viewerId, UserRoleEnum.VIEWER);
        var task = await DatabaseSeeder.CreateTaskAsync(db, board.Id, "Task with Users");

        // Seed a task-user assignment
        await DatabaseSeeder.AddUserToTaskAsync(db, task.Id, viewerId);

        // Act
        var response = await viewerClient.GetAsync($"/api/boards/{board.Id}/tasks/{task.Id}/users");

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - response body
        var body = await response.Content.ReadFromJsonAsync<IEnumerable<LinkedUserToTaskResponse>>();
        body.Should().NotBeNull();
        body.Should().HaveCountGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task GetUsersLinkedToTask_WhenNotOnBoard_ReturnsForbidden()
    {
        // Arrange
        var (ownerId, _) = await CreateUserWithClientAsync("fbd_task_owner", "fbd_task_owner@test.com");
        var (otherId, otherClient) = await CreateUserWithClientAsync("fbd_task_other", "fbd_task_other@test.com");
        await using var db = _factory.CreateDbContext();
        var board = await DatabaseSeeder.CreateBoardAsync(db, ownerId, UserRoleEnum.OWNER);
        var task = await DatabaseSeeder.CreateTaskAsync(db, board.Id, "Protected Task");

        // Act - user not on board
        var response = await otherClient.GetAsync($"/api/boards/{board.Id}/tasks/{task.Id}/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
