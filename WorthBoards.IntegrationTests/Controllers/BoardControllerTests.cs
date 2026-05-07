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
public sealed class BoardControllerTests : IAsyncLifetime
{
    private readonly ApiFactory _factory;

    public BoardControllerTests(ApiFactory factory)
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
        var client = _factory.CreateAuthenticatedClient(user.Id, user.UserName!, user.Email!);
        return (user.Id, client);
    }

    // --- GET /api/boards ---

    [Fact]
    public async Task GetAllCurrentUserBoards_WhenAuthenticated_ReturnsOkWithBoards()
    {
        // Arrange
        var (userId, client) = await CreateUserWithClientAsync();
        await using var db = _factory.CreateDbContext();
        await DatabaseSeeder.CreateBoardAsync(db, userId, title: "My Board");

        // Act
        var response = await client.GetAsync("/api/boards");

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - response body
        var body = await response.Content.ReadFromJsonAsync<PagedBoardsResponse>();
        body.Should().NotBeNull();
        body!.Items.Should().HaveCountGreaterThanOrEqualTo(1);
        body.Items.Should().Contain(b => b.Title == "My Board");
    }

    [Fact]
    public async Task GetAllCurrentUserBoards_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/boards");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --- GET /api/boards/{boardId} ---

    [Fact]
    public async Task GetBoardById_WhenBoardExists_ReturnsOkWithBoard()
    {
        // Arrange
        var (userId, client) = await CreateUserWithClientAsync();
        await using var db = _factory.CreateDbContext();
        var board = await DatabaseSeeder.CreateBoardAsync(db, userId, title: "Details Board");

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}");

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - response body
        var body = await response.Content.ReadFromJsonAsync<BoardResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(board.Id);
        body.Title.Should().Be("Details Board");

        // Assert - database state
        await using var verifyDb = _factory.CreateDbContext();
        var persisted = await verifyDb.Boards.AsNoTracking().SingleOrDefaultAsync(b => b.Id == board.Id);
        persisted.Should().NotBeNull();
    }

    [Fact]
    public async Task GetBoardById_WhenBoardNotFound_ReturnsNotFound()
    {
        // Arrange
        var (_, client) = await CreateUserWithClientAsync();

        // Act
        var response = await client.GetAsync("/api/boards/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- POST /api/boards ---

    [Fact]
    public async Task CreateBoard_WithValidRequest_ReturnsCreatedAndPersistsBoard()
    {
        // Arrange
        var (userId, client) = await CreateUserWithClientAsync();
        var request = new BoardRequest
        {
            Title = "New Board",
            Description = "Board description",
            ImageName = "board.jpg"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/boards", request);

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        // Assert - response body
        var body = await response.Content.ReadFromJsonAsync<BoardResponse>();
        body.Should().NotBeNull();
        body!.Title.Should().Be("New Board");
        body.Description.Should().Be("Board description");
        body.Id.Should().BeGreaterThan(0);

        // Assert - database state: board exists and creator is OWNER
        await using var db = _factory.CreateDbContext();
        var persisted = await db.Boards.AsNoTracking().SingleOrDefaultAsync(b => b.Id == body.Id);
        persisted.Should().NotBeNull();
        var ownerLink = await db.BoardOnUsers.AsNoTracking()
            .SingleOrDefaultAsync(bou => bou.BoardId == body.Id && bou.UserId == userId);
        ownerLink.Should().NotBeNull();
        ownerLink!.UserRole.Should().Be(UserRoleEnum.OWNER);
    }

    [Fact]
    public async Task CreateBoard_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new BoardRequest { Title = "Board", Description = "Desc", ImageName = "img.jpg" };

        // Act
        var response = await client.PostAsJsonAsync("/api/boards", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --- DELETE /api/boards/{boardId} ---

    [Fact]
    public async Task DeleteBoard_AsOwner_ReturnsNoContentAndRemovesBoard()
    {
        // Arrange
        var (userId, client) = await CreateUserWithClientAsync();
        await using var db = _factory.CreateDbContext();
        var board = await DatabaseSeeder.CreateBoardAsync(db, userId, UserRoleEnum.OWNER);

        // Act
        var response = await client.DeleteAsync($"/api/boards/{board.Id}");

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert - database state
        await using var verifyDb = _factory.CreateDbContext();
        var deleted = await verifyDb.Boards.AsNoTracking().SingleOrDefaultAsync(b => b.Id == board.Id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteBoard_AsEditor_ReturnsForbidden()
    {
        // Arrange - seed board owner, then create editor user
        var (ownerId, _) = await CreateUserWithClientAsync("owner_del", "owner_del@test.com");
        var (editorId, editorClient) = await CreateUserWithClientAsync("editor_del", "editor_del@test.com");
        await using var db = _factory.CreateDbContext();
        var board = await DatabaseSeeder.CreateBoardAsync(db, ownerId, UserRoleEnum.OWNER);
        await DatabaseSeeder.AddUserToBoardAsync(db, board.Id, editorId, UserRoleEnum.EDITOR);

        // Act
        var response = await editorClient.DeleteAsync($"/api/boards/{board.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Assert - board still exists
        await using var verifyDb = _factory.CreateDbContext();
        var stillThere = await verifyDb.Boards.AsNoTracking().SingleOrDefaultAsync(b => b.Id == board.Id);
        stillThere.Should().NotBeNull();
    }

    // --- PUT /api/boards/{boardId} ---

    [Fact]
    public async Task UpdateBoard_AsOwner_ReturnsOkWithUpdatedBoard()
    {
        // Arrange
        var (userId, client) = await CreateUserWithClientAsync();
        await using var db = _factory.CreateDbContext();
        var board = await DatabaseSeeder.CreateBoardAsync(db, userId, UserRoleEnum.OWNER, title: "Old Title");

        var request = new BoardUpdateRequest
        {
            Title = "Updated Title",
            Description = "Updated Description",
            ImageName = "updated.jpg",
            Version = board.Version
        };

        // Act
        var response = await client.PutAsJsonAsync($"/api/boards/{board.Id}", request);

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - response body
        var body = await response.Content.ReadFromJsonAsync<BoardResponse>();
        body.Should().NotBeNull();
        body!.Title.Should().Be("Updated Title");

        // Assert - database state
        await using var verifyDb = _factory.CreateDbContext();
        var persisted = await verifyDb.Boards.AsNoTracking().SingleOrDefaultAsync(b => b.Id == board.Id);
        persisted!.Title.Should().Be("Updated Title");
    }

    [Fact]
    public async Task UpdateBoard_AsViewer_ReturnsForbidden()
    {
        // Arrange
        var (ownerId, _) = await CreateUserWithClientAsync("owner_upd", "owner_upd@test.com");
        var (viewerId, viewerClient) = await CreateUserWithClientAsync("viewer_upd", "viewer_upd@test.com");
        await using var db = _factory.CreateDbContext();
        var board = await DatabaseSeeder.CreateBoardAsync(db, ownerId, UserRoleEnum.OWNER);
        await DatabaseSeeder.AddUserToBoardAsync(db, board.Id, viewerId, UserRoleEnum.VIEWER);

        var request = new BoardUpdateRequest
        {
            Title = "Hacked Title",
            Description = "Hacked",
            ImageName = "hack.jpg",
            Version = board.Version
        };

        // Act
        var response = await viewerClient.PutAsJsonAsync($"/api/boards/{board.Id}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // --- PATCH /api/boards/{boardId} ---

    [Fact]
    public async Task PatchBoard_AsOwner_ReturnsOkWithPatchedBoard()
    {
        // Arrange
        var (userId, client) = await CreateUserWithClientAsync();
        await using var db = _factory.CreateDbContext();
        var board = await DatabaseSeeder.CreateBoardAsync(db, userId, UserRoleEnum.OWNER, title: "Patch Board");

        var patchDoc = new[]
        {
            new { op = "replace", path = "/title", value = (object)"Patched Title" },
            new { op = "replace", path = "/version", value = (object)board.Version }
        };

        var request = new System.Net.Http.StringContent(
            System.Text.Json.JsonSerializer.Serialize(patchDoc),
            System.Text.Encoding.UTF8,
            "application/json-patch+json"
        );

        // Act
        var response = await client.PatchAsync($"/api/boards/{board.Id}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<BoardResponse>();
        body.Should().NotBeNull();
        body!.Title.Should().Be("Patched Title");
    }

    [Fact]
    public async Task PatchBoard_AsViewer_ReturnsForbidden()
    {
        // Arrange
        var (ownerId, _) = await CreateUserWithClientAsync("patch_owner", "patch_owner@test.com");
        var (viewerId, viewerClient) = await CreateUserWithClientAsync("patch_viewer", "patch_viewer@test.com");
        await using var db = _factory.CreateDbContext();
        var board = await DatabaseSeeder.CreateBoardAsync(db, ownerId, UserRoleEnum.OWNER, title: "Patch Board");
        await DatabaseSeeder.AddUserToBoardAsync(db, board.Id, viewerId, UserRoleEnum.VIEWER);

        var patchDoc = new[]
        {
            new { op = "replace", path = "/title", value = (object)"Blocked Title" },
            new { op = "replace", path = "/version", value = (object)board.Version }
        };

        var request = new System.Net.Http.StringContent(
            System.Text.Json.JsonSerializer.Serialize(patchDoc),
            System.Text.Encoding.UTF8,
            "application/json-patch+json"
        );

        // Act
        var response = await viewerClient.PatchAsync($"/api/boards/{board.Id}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // Helper record to deserialize paginated response
    private record PagedBoardsResponse(
        IEnumerable<BoardResponse> Items,
        int PageNumber,
        int PageCount,
        int PageSize
    );
}
