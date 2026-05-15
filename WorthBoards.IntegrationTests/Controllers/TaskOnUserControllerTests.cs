using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
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

    // ---------------------------------------------------------------------------
    // POST /api/boards/{boardId}/tasks/{taskId}/users/link  [AuthorizeRole(EDITOR)]
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task LinkUsersToTask_AsEditor_ReturnsOkAndPersistsAssignment()
    {
        // Arrange
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var editor = await seeder.CreateUserAsync();
        var assignee = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync();
        await seeder.LinkUserToBoardAsync(board.Id, editor.Id, UserRoleEnum.EDITOR);
        await seeder.LinkUserToBoardAsync(board.Id, assignee.Id, UserRoleEnum.VIEWER);
        var task = await seeder.CreateBoardTaskAsync(board.Id, "Task to Assign");

        var client = _factory.CreateAuthenticatedClient(editor.Id, editor.UserName!, editor.Email!);

        var linkList = new[] { new LinkUserToTaskRequest { UserId = assignee.Id } };

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/users/link", linkList);

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - response body
        var body = await response.Content.ReadFromJsonAsync<IEnumerable<LinkUserToTaskResponse>>();
        body.Should().NotBeNull();
        body!.Should().ContainSingle(r => r.UserId == assignee.Id);

        // Assert - database state
        await using var db = _factory.CreateDbContext();
        var persisted = await db.TasksOnUsers.AsNoTracking()
            .SingleOrDefaultAsync(t => t.BoardTaskId == task.Id && t.UserId == assignee.Id);
        persisted.Should().NotBeNull();
    }

    [Fact]
    public async Task LinkUsersToTask_AsViewer_ReturnsForbidden()
    {
        // Arrange
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var viewer = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync();
        await seeder.LinkUserToBoardAsync(board.Id, viewer.Id, UserRoleEnum.VIEWER);
        var task = await seeder.CreateBoardTaskAsync(board.Id);

        var client = _factory.CreateAuthenticatedClient(viewer.Id, viewer.UserName!, viewer.Email!);

        var linkList = new[] { new LinkUserToTaskRequest { UserId = viewer.Id } };

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/users/link", linkList);

        // Assert - viewer role (2) > required EDITOR role (1)
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Assert - no assignment was persisted
        await using var db = _factory.CreateDbContext();
        var notPersisted = await db.TasksOnUsers.AsNoTracking()
            .AnyAsync(t => t.BoardTaskId == task.Id && t.UserId == viewer.Id);
        notPersisted.Should().BeFalse();
    }

    // ---------------------------------------------------------------------------
    // DELETE /api/boards/{boardId}/tasks/{taskId}/users/unlink  [AuthorizeRole(EDITOR)]
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task UnlinkUsersFromTask_AsEditor_ReturnsOkAndRemovesAssignment()
    {
        // Arrange - pre-seed a task assignment
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var editor = await seeder.CreateUserAsync();
        var assignee = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync();
        await seeder.LinkUserToBoardAsync(board.Id, editor.Id, UserRoleEnum.EDITOR);
        await seeder.LinkUserToBoardAsync(board.Id, assignee.Id, UserRoleEnum.VIEWER);
        var task = await seeder.CreateBoardTaskAsync(board.Id);

        // Link assignee to task directly in DB (bypassing notifications)
        await using (var db = _factory.CreateDbContext())
        {
            db.TasksOnUsers.Add(new WorthBoards.Domain.Entities.TaskOnUser
            {
                BoardTaskId = task.Id,
                UserId = assignee.Id,
                AssignedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateAuthenticatedClient(editor.Id, editor.UserName!, editor.Email!);
        var unlinkList = new[] { new LinkUserToTaskRequest { UserId = assignee.Id } };

        // Act
        var response = await client.SendAsync(new HttpRequestMessage(
            HttpMethod.Delete, $"/api/boards/{board.Id}/tasks/{task.Id}/users/unlink")
        {
            Content = JsonContent.Create(unlinkList)
        });

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - database state: assignment removed
        await using var dbAssert = _factory.CreateDbContext();
        var removed = await dbAssert.TasksOnUsers.AsNoTracking()
            .SingleOrDefaultAsync(t => t.BoardTaskId == task.Id && t.UserId == assignee.Id);
        removed.Should().BeNull();
    }

    // ---------------------------------------------------------------------------
    // GET /api/boards/{boardId}/tasks/{taskId}/users  [AuthorizeRole(VIEWER)]
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetUsersLinkedToTask_AsViewer_ReturnsAssignedUsers()
    {
        // Arrange
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var viewer = await seeder.CreateUserAsync();
        var assignee = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync();
        await seeder.LinkUserToBoardAsync(board.Id, viewer.Id, UserRoleEnum.VIEWER);
        await seeder.LinkUserToBoardAsync(board.Id, assignee.Id, UserRoleEnum.VIEWER);
        var task = await seeder.CreateBoardTaskAsync(board.Id);

        // Seed a task assignment
        await using (var db = _factory.CreateDbContext())
        {
            db.TasksOnUsers.Add(new WorthBoards.Domain.Entities.TaskOnUser
            {
                BoardTaskId = task.Id,
                UserId = assignee.Id,
                AssignedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateAuthenticatedClient(viewer.Id, viewer.UserName!, viewer.Email!);

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}/tasks/{task.Id}/users");

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - response body contains the assigned user
        var users = await response.Content.ReadFromJsonAsync<IEnumerable<LinkedUserToTaskResponse>>();
        users.Should().NotBeNull();
        users!.Should().ContainSingle(u => u.Id == assignee.Id);
    }

    [Fact]
    public async Task GetUsersLinkedToTask_AsViewer_WhenNoAssignments_ReturnsEmptyList()
    {
        // Arrange
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var viewer = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync();
        await seeder.LinkUserToBoardAsync(board.Id, viewer.Id, UserRoleEnum.VIEWER);
        var task = await seeder.CreateBoardTaskAsync(board.Id);

        var client = _factory.CreateAuthenticatedClient(viewer.Id, viewer.UserName!, viewer.Email!);

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}/tasks/{task.Id}/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var users = await response.Content.ReadFromJsonAsync<IEnumerable<LinkedUserToTaskResponse>>();
        users.Should().NotBeNull().And.BeEmpty();
    }

    // ---------------------------------------------------------------------------
    // DELETE /api/boards/{boardId}/tasks/{taskId}/users/unlink — negative
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task UnlinkUsersFromTask_AsViewer_ReturnsForbidden()
    {
        // Arrange
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var viewer = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync();
        await seeder.LinkUserToBoardAsync(board.Id, viewer.Id, UserRoleEnum.VIEWER);
        var task = await seeder.CreateBoardTaskAsync(board.Id);

        var client = _factory.CreateAuthenticatedClient(viewer.Id, viewer.UserName!, viewer.Email!);
        var unlinkList = new[] { new LinkUserToTaskRequest { UserId = viewer.Id } };

        // Act
        var response = await client.SendAsync(new HttpRequestMessage(
            HttpMethod.Delete, $"/api/boards/{board.Id}/tasks/{task.Id}/users/unlink")
        {
            Content = JsonContent.Create(unlinkList)
        });

        // Assert - viewer role (2) > required EDITOR role (1)
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---------------------------------------------------------------------------
    // GET /api/boards/{boardId}/tasks/{taskId}/users — negative
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetUsersLinkedToTask_WhenNotBoardMember_ReturnsForbidden()
    {
        // Arrange
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var nonMember = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync();
        var task = await seeder.CreateBoardTaskAsync(board.Id);

        var client = _factory.CreateAuthenticatedClient(nonMember.Id, nonMember.UserName!, nonMember.Email!);

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}/tasks/{task.Id}/users");

        // Assert - not a board member, PermissionHandler fails
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
