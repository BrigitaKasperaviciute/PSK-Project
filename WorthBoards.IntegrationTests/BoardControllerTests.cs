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

public class BoardControllerTests(WorthBoardsWebApplicationFactory factory)
    : IClassFixture<WorthBoardsWebApplicationFactory>
{
    private HttpClient AuthorizedClient(int userId, string userName, string email)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTokenHelper.GenerateToken(userId, userName, email));
        return client;
    }

    // ── GET /api/boards ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllCurrentUserBoards_AuthenticatedUser_ReturnsOkWithBoards()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync(owner);
        var client = AuthorizedClient(owner.Id, owner.UserName!, owner.Email!);

        // Act
        var response = await client.GetAsync("/api/boards");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResult<BoardResponse>>();
        body.Should().NotBeNull();
        body!.Items.Should().Contain(b => b.Title == board.Title);
    }

    [Fact]
    public async Task GetAllCurrentUserBoards_Unauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/boards");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── GET /api/boards/{boardId} ────────────────────────────────────────────

    [Fact]
    public async Task GetBoardById_ExistingBoard_ReturnsOkWithBoard()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync(owner, "UniqueGetBoardTitle");
        var client = AuthorizedClient(owner.Id, owner.UserName!, owner.Email!);

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<BoardResponse>();
        body.Should().NotBeNull();
        body!.Title.Should().Be("UniqueGetBoardTitle");
    }

    [Fact]
    public async Task GetBoardById_NonExistingId_ReturnsNotFound()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var client = AuthorizedClient(owner.Id, owner.UserName!, owner.Email!);

        // Act
        var response = await client.GetAsync("/api/boards/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── POST /api/boards ─────────────────────────────────────────────────────

    [Fact]
    public async Task CreateBoard_ValidRequest_ReturnsCreated()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var client = AuthorizedClient(owner.Id, owner.UserName!, owner.Email!);
        var request = new BoardRequest { Title = "New Board", Description = "Desc", ImageName = "img.jpg" };

        // Act
        var response = await client.PostAsJsonAsync("/api/boards", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<BoardResponse>();
        body!.Title.Should().Be("New Board");

        using var dbScope = factory.Services.CreateScope();
        var db = dbScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Boards.Any(b => b.Title == "New Board").Should().BeTrue();
    }

    [Fact]
    public async Task CreateBoard_Unauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var client = factory.CreateClient();
        var request = new BoardRequest { Title = "Board", Description = "Desc", ImageName = "img.jpg" };

        // Act
        var response = await client.PostAsJsonAsync("/api/boards", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── DELETE /api/boards/{boardId} ─────────────────────────────────────────

    [Fact]
    public async Task DeleteBoard_AsOwner_ReturnsNoContent()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync(owner);
        var client = AuthorizedClient(owner.Id, owner.UserName!, owner.Email!);

        // Act
        var response = await client.DeleteAsync($"/api/boards/{board.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var dbScope = factory.Services.CreateScope();
        var db = dbScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Boards.Any(b => b.Id == board.Id).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteBoard_AsViewer_ReturnsForbidden()
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
        var response = await client.DeleteAsync($"/api/boards/{board.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── PUT /api/boards/{boardId} ────────────────────────────────────────────

    [Fact]
    public async Task UpdateBoard_AsEditor_ReturnsOk()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var editor = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync(owner);
        await seeder.LinkUserToBoardAsync(board, editor, UserRoleEnum.EDITOR);
        var client = AuthorizedClient(editor.Id, editor.UserName!, editor.Email!);
        var request = new BoardUpdateRequest { Title = "Updated Title", Description = "New desc", ImageName = "new.jpg", Version = 0 };

        // Act
        var response = await client.PutAsJsonAsync($"/api/boards/{board.Id}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<BoardResponse>();
        body!.Title.Should().Be("Updated Title");
    }

    [Fact]
    public async Task UpdateBoard_AsViewer_ReturnsForbidden()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var viewer = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync(owner);
        await seeder.LinkUserToBoardAsync(board, viewer, UserRoleEnum.VIEWER);
        var client = AuthorizedClient(viewer.Id, viewer.UserName!, viewer.Email!);
        var request = new BoardUpdateRequest { Title = "Hacked", Description = "Hacked", ImageName = "x.jpg", Version = 0 };

        // Act
        var response = await client.PutAsJsonAsync($"/api/boards/{board.Id}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── PATCH /api/boards/{boardId} ──────────────────────────────────────────

    [Fact]
    public async Task PatchBoard_AsEditor_ReturnsOk()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync(owner, "PatchMe");
        var client = AuthorizedClient(owner.Id, owner.UserName!, owner.Email!);

        var patchDoc = Newtonsoft.Json.JsonConvert.SerializeObject(new[]
        {
            new { op = "replace", path = "/title", value = "Patched Title" }
        });
        var content = new StringContent(patchDoc, Encoding.UTF8, "application/json-patch+json");

        // Act
        var response = await client.PatchAsync($"/api/boards/{board.Id}", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<BoardResponse>();
        body!.Title.Should().Be("Patched Title");
    }

    [Fact]
    public async Task PatchBoard_AsViewer_ReturnsForbidden()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var viewer = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync(owner);
        await seeder.LinkUserToBoardAsync(board, viewer, UserRoleEnum.VIEWER);
        var client = AuthorizedClient(viewer.Id, viewer.UserName!, viewer.Email!);

        var patchDoc = Newtonsoft.Json.JsonConvert.SerializeObject(new[]
        {
            new { op = "replace", path = "/title", value = "Hacked" }
        });
        var content = new StringContent(patchDoc, Encoding.UTF8, "application/json-patch+json");

        // Act
        var response = await client.PatchAsync($"/api/boards/{board.Id}", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}

// Helper to deserialize paged responses from BoardController
file record PagedResult<T>(IEnumerable<T> Items, int PageNumber, int PageCount, int PageSize);
