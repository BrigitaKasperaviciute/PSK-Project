using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Common.Enums;
using WorthBoards.Data.Database;
using WorthBoards.IntegrationTests.Infrastructure;
using Xunit;

namespace WorthBoards.IntegrationTests;

public class BoardTaskControllerTests(WorthBoardsWebApplicationFactory factory)
    : IClassFixture<WorthBoardsWebApplicationFactory>
{
    private HttpClient AuthorizedClient(int userId, string userName, string email)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTokenHelper.GenerateToken(userId, userName, email));
        return client;
    }

    // ── GET active tasks ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllActiveBoardTasks_AsViewer_ReturnsOkWithTasks()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync(owner);
        var task = await seeder.CreateTaskAsync(board, status: TaskStatusEnum.PENDING);
        var client = AuthorizedClient(owner.Id, owner.UserName!, owner.Email!);

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}/tasks");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IEnumerable<BoardTaskResponse>>();
        body.Should().Contain(t => t.Id == task.Id);
    }

    [Fact]
    public async Task GetAllActiveBoardTasks_NotBoardMember_ReturnsForbidden()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var outsider = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync(owner);
        var client = AuthorizedClient(outsider.Id, outsider.UserName!, outsider.Email!);

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}/tasks");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── GET archived tasks ───────────────────────────────────────────────────

    [Fact]
    public async Task GetAllArchivedBoardTasks_AsViewer_ReturnsOkWithArchivedTasks()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync(owner);
        var archived = await seeder.CreateTaskAsync(board, status: TaskStatusEnum.ARCHIVED);
        var client = AuthorizedClient(owner.Id, owner.UserName!, owner.Email!);

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}/tasks/archived");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IEnumerable<BoardTaskResponse>>();
        body.Should().Contain(t => t.Id == archived.Id);
    }

    [Fact]
    public async Task GetAllArchivedBoardTasks_Unauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/boards/1/tasks/archived");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── GET task by id ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetBoardTaskById_ExistingTask_ReturnsOk()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync(owner);
        var task = await seeder.CreateTaskAsync(board, "GetByIdTask");
        var client = AuthorizedClient(owner.Id, owner.UserName!, owner.Email!);

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}/tasks/{task.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<BoardTaskResponse>();
        body!.Title.Should().Be("GetByIdTask");
    }

    [Fact]
    public async Task GetBoardTaskById_NonExistingTask_ReturnsNotFound()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync(owner);
        var client = AuthorizedClient(owner.Id, owner.UserName!, owner.Email!);

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}/tasks/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── POST task ────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateBoardTask_AsEditor_ReturnsCreated()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync(owner);
        var client = AuthorizedClient(owner.Id, owner.UserName!, owner.Email!);
        var request = new BoardTaskRequest { Title = "New Task", Description = "Desc", TaskStatus = TaskStatusEnum.PENDING };

        // Act
        var response = await client.PostAsJsonAsync($"/api/boards/{board.Id}/tasks", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<BoardTaskResponse>();
        body!.Title.Should().Be("New Task");

        using var dbScope = factory.Services.CreateScope();
        var db = dbScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.BoardTasks.Any(t => t.Title == "New Task" && t.BoardId == board.Id).Should().BeTrue();
    }

    [Fact]
    public async Task CreateBoardTask_AsViewer_ReturnsForbidden()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var viewer = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync(owner);
        await seeder.LinkUserToBoardAsync(board, viewer, UserRoleEnum.VIEWER);
        var client = AuthorizedClient(viewer.Id, viewer.UserName!, viewer.Email!);
        var request = new BoardTaskRequest { Title = "Forbidden", Description = "x", TaskStatus = TaskStatusEnum.PENDING };

        // Act
        var response = await client.PostAsJsonAsync($"/api/boards/{board.Id}/tasks", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── DELETE task ──────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteBoardTask_AsEditor_ReturnsNoContent()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync(owner);
        var task = await seeder.CreateTaskAsync(board);
        var client = AuthorizedClient(owner.Id, owner.UserName!, owner.Email!);

        // Act
        var response = await client.DeleteAsync($"/api/boards/{board.Id}/tasks/{task.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var dbScope = factory.Services.CreateScope();
        var db = dbScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.BoardTasks.Any(t => t.Id == task.Id).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteBoardTask_AsViewer_ReturnsForbidden()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var viewer = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync(owner);
        var task = await seeder.CreateTaskAsync(board);
        await seeder.LinkUserToBoardAsync(board, viewer, UserRoleEnum.VIEWER);
        var client = AuthorizedClient(viewer.Id, viewer.UserName!, viewer.Email!);

        // Act
        var response = await client.DeleteAsync($"/api/boards/{board.Id}/tasks/{task.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── DELETE archived tasks ────────────────────────────────────────────────

    [Fact]
    public async Task DeleteArchivedBoardTasks_AsEditor_ReturnsNoContent()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync(owner);
        await seeder.CreateTaskAsync(board, status: TaskStatusEnum.ARCHIVED);
        var client = AuthorizedClient(owner.Id, owner.UserName!, owner.Email!);

        // Act
        var response = await client.DeleteAsync($"/api/boards/{board.Id}/tasks/archived");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteArchivedBoardTasks_AsViewer_ReturnsForbidden()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var viewer = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync(owner);
        await seeder.LinkUserToBoardAsync(board, viewer, UserRoleEnum.VIEWER);
        var client = AuthorizedClient(viewer.Id, viewer.UserName!, viewer.Email!);

        // Act
        var response = await client.DeleteAsync($"/api/boards/{board.Id}/tasks/archived");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── PUT task ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateBoardTask_AsEditor_ReturnsOk()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync(owner);
        var task = await seeder.CreateTaskAsync(board);
        var client = AuthorizedClient(owner.Id, owner.UserName!, owner.Email!);
        var request = new BoardTaskUpdateRequest
        {
            Title = "Updated Task",
            Description = "Updated",
            TaskStatus = TaskStatusEnum.IN_PROGRESS,
            Version = 0
        };

        // Act
        var response = await client.PutAsJsonAsync($"/api/boards/{board.Id}/tasks/{task.Id}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<BoardTaskResponse>();
        body!.Title.Should().Be("Updated Task");
        body.TaskStatus.Should().Be(TaskStatusEnum.IN_PROGRESS);
    }

    [Fact]
    public async Task UpdateBoardTask_AsViewer_ReturnsForbidden()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var viewer = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync(owner);
        var task = await seeder.CreateTaskAsync(board);
        await seeder.LinkUserToBoardAsync(board, viewer, UserRoleEnum.VIEWER);
        var client = AuthorizedClient(viewer.Id, viewer.UserName!, viewer.Email!);
        var request = new BoardTaskUpdateRequest { Title = "Hack", TaskStatus = TaskStatusEnum.PENDING, Version = 0 };

        // Act
        var response = await client.PutAsJsonAsync($"/api/boards/{board.Id}/tasks/{task.Id}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── PATCH task ───────────────────────────────────────────────────────────

    [Fact]
    public async Task PatchBoardTask_AsEditor_ReturnsOk()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync(owner);
        var task = await seeder.CreateTaskAsync(board, "PatchTask");
        var client = AuthorizedClient(owner.Id, owner.UserName!, owner.Email!);

        var patchDoc = Newtonsoft.Json.JsonConvert.SerializeObject(new[]
        {
            new { op = "replace", path = "/title", value = "Patched Task" }
        });
        var content = new StringContent(patchDoc, Encoding.UTF8, "application/json-patch+json");

        // Act
        var response = await client.PatchAsync($"/api/boards/{board.Id}/tasks/{task.Id}", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<BoardTaskResponse>();
        body!.Title.Should().Be("Patched Task");
    }

    [Fact]
    public async Task PatchBoardTask_AsViewer_ReturnsForbidden()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var viewer = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync(owner);
        var task = await seeder.CreateTaskAsync(board);
        await seeder.LinkUserToBoardAsync(board, viewer, UserRoleEnum.VIEWER);
        var client = AuthorizedClient(viewer.Id, viewer.UserName!, viewer.Email!);

        var patchDoc = Newtonsoft.Json.JsonConvert.SerializeObject(new[]
        {
            new { op = "replace", path = "/title", value = "Hacked" }
        });
        var content = new StringContent(patchDoc, Encoding.UTF8, "application/json-patch+json");

        // Act
        var response = await client.PatchAsync($"/api/boards/{board.Id}/tasks/{task.Id}", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
