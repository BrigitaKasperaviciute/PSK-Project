using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Common.Enums;
using WorthBoards.IntegrationTests.Infrastructure;
using Xunit;

namespace WorthBoards.IntegrationTests;

public class TaskOnUserControllerTests(WorthBoardsWebApplicationFactory factory)
    : IClassFixture<WorthBoardsWebApplicationFactory>
{
    private HttpClient AuthorizedClient(int userId, string userName, string email)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTokenHelper.GenerateToken(userId, userName, email));
        return client;
    }

    // ── POST link users ──────────────────────────────────────────────────────

    [Fact]
    public async Task LinkUsersToTask_AsEditor_ReturnsOk()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var assignee = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync(owner);
        await seeder.LinkUserToBoardAsync(board, assignee, UserRoleEnum.VIEWER);
        var task = await seeder.CreateTaskAsync(board);
        var client = AuthorizedClient(owner.Id, owner.UserName!, owner.Email!);
        var linkList = new[] { new LinkUserToTaskRequest { UserId = assignee.Id } };

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/users/link", linkList);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IEnumerable<LinkUserToTaskResponse>>();
        body.Should().Contain(l => l.UserId == assignee.Id);
    }

    [Fact]
    public async Task LinkUsersToTask_AsViewer_ReturnsForbidden()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var viewer = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync(owner);
        await seeder.LinkUserToBoardAsync(board, viewer, UserRoleEnum.VIEWER);
        var task = await seeder.CreateTaskAsync(board);
        var client = AuthorizedClient(viewer.Id, viewer.UserName!, viewer.Email!);
        var linkList = new[] { new LinkUserToTaskRequest { UserId = viewer.Id } };

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/users/link", linkList);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── DELETE unlink users ──────────────────────────────────────────────────

    [Fact]
    public async Task UnlinkUsersFromTask_AsEditor_ReturnsOk()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var assignee = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync(owner);
        await seeder.LinkUserToBoardAsync(board, assignee, UserRoleEnum.VIEWER);
        var task = await seeder.CreateTaskAsync(board);
        var client = AuthorizedClient(owner.Id, owner.UserName!, owner.Email!);

        // First link the user
        await client.PostAsJsonAsync($"/api/boards/{board.Id}/tasks/{task.Id}/users/link",
            new[] { new LinkUserToTaskRequest { UserId = assignee.Id } });

        var unlinkList = new[] { new LinkUserToTaskRequest { UserId = assignee.Id } };

        // Act
        var request = new HttpRequestMessage(HttpMethod.Delete,
            $"/api/boards/{board.Id}/tasks/{task.Id}/users/unlink")
        {
            Content = JsonContent.Create(unlinkList)
        };
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UnlinkUsersFromTask_AsViewer_ReturnsForbidden()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var viewer = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync(owner);
        await seeder.LinkUserToBoardAsync(board, viewer, UserRoleEnum.VIEWER);
        var task = await seeder.CreateTaskAsync(board);
        var client = AuthorizedClient(viewer.Id, viewer.UserName!, viewer.Email!);
        var unlinkList = new[] { new LinkUserToTaskRequest { UserId = viewer.Id } };

        // Act
        var request = new HttpRequestMessage(HttpMethod.Delete,
            $"/api/boards/{board.Id}/tasks/{task.Id}/users/unlink")
        {
            Content = JsonContent.Create(unlinkList)
        };
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── GET users on task ────────────────────────────────────────────────────

    [Fact]
    public async Task GetUsersLinkedToTask_AsViewer_ReturnsOk()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync(owner);
        var task = await seeder.CreateTaskAsync(board);
        var client = AuthorizedClient(owner.Id, owner.UserName!, owner.Email!);

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}/tasks/{task.Id}/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IEnumerable<LinkedUserToTaskResponse>>();
        body.Should().NotBeNull();
    }

    [Fact]
    public async Task GetUsersLinkedToTask_NotBoardMember_ReturnsForbidden()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var outsider = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync(owner);
        var task = await seeder.CreateTaskAsync(board);
        var client = AuthorizedClient(outsider.Id, outsider.UserName!, outsider.Email!);

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}/tasks/{task.Id}/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
