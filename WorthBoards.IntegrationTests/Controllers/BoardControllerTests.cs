using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Common.Enums;
using WorthBoards.Common.Exceptions;
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

    // ---------------------------------------------------------------------------
    // GET /api/boards
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetAllCurrentUserBoards_WhenAuthenticated_ReturnsPagedBoards()
    {
        // Arrange - create a user with two boards
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var user = await seeder.CreateUserAsync();
        var board1 = await seeder.CreateBoardAsync("Board One");
        var board2 = await seeder.CreateBoardAsync("Board Two");
        await seeder.LinkUserToBoardAsync(board1.Id, user.Id, UserRoleEnum.OWNER);
        await seeder.LinkUserToBoardAsync(board2.Id, user.Id, UserRoleEnum.VIEWER);

        var client = _factory.CreateAuthenticatedClient(user.Id, user.UserName!, user.Email!);

        // Act
        var response = await client.GetAsync("/api/boards");

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - response body contains both boards
        var body = await response.Content.ReadFromJsonAsync<PagedResult<BoardResponse>>();
        body.Should().NotBeNull();
        body!.Items.Should().HaveCount(2);
        body.Items.Should().Contain(b => b.Title == "Board One");
        body.Items.Should().Contain(b => b.Title == "Board Two");
    }

    [Fact]
    public async Task GetAllCurrentUserBoards_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/boards");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------------------------------------------------------------------------
    // GET /api/boards/{boardId}
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetBoardById_WithExistingBoard_ReturnsBoard()
    {
        // Arrange
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var user = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync("My Board");
        await seeder.LinkUserToBoardAsync(board.Id, user.Id, UserRoleEnum.OWNER);

        var client = _factory.CreateAuthenticatedClient(user.Id, user.UserName!, user.Email!);

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}");

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - response body
        var body = await response.Content.ReadFromJsonAsync<BoardResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(board.Id);
        body.Title.Should().Be("My Board");
    }

    [Fact]
    public async Task GetBoardById_WithNonExistentBoard_ReturnsNotFound()
    {
        // Arrange
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var user = await seeder.CreateUserAsync();
        var client = _factory.CreateAuthenticatedClient(user.Id, user.UserName!, user.Email!);

        // Act
        var response = await client.GetAsync("/api/boards/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------------------------------------------------------------------------
    // POST /api/boards
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task CreateBoard_WhenAuthenticated_ReturnsCreatedAndPersistsBoard()
    {
        // Arrange
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var user = await seeder.CreateUserAsync();
        var client = _factory.CreateAuthenticatedClient(user.Id, user.UserName!, user.Email!);

        var request = new BoardRequest
        {
            Title = "New Project Board",
            Description = "A board for our new project",
            ImageName = "project.jpg"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/boards", request);

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        // Assert - response body
        var body = await response.Content.ReadFromJsonAsync<BoardResponse>();
        body.Should().NotBeNull();
        body!.Title.Should().Be(request.Title);
        body.Description.Should().Be(request.Description);
        body.Id.Should().BeGreaterThan(0);

        // Assert - database state
        await using var db = _factory.CreateDbContext();
        var persisted = await db.Boards.AsNoTracking()
            .SingleOrDefaultAsync(b => b.Id == body.Id);
        persisted.Should().NotBeNull();
        persisted!.Title.Should().Be(request.Title);
    }

    [Fact]
    public async Task CreateBoard_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new BoardRequest
        {
            Title = "Should Fail",
            Description = "desc",
            ImageName = "img.jpg"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/boards", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------------------------------------------------------------------------
    // DELETE /api/boards/{boardId}  [AuthorizeRole(OWNER)]
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task DeleteBoard_AsOwner_ReturnsNoContentAndRemovesBoard()
    {
        // Arrange - owner creates a board
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var owner = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync("Board to Delete");
        await seeder.LinkUserToBoardAsync(board.Id, owner.Id, UserRoleEnum.OWNER);

        var client = _factory.CreateAuthenticatedClient(owner.Id, owner.UserName!, owner.Email!);

        // Act
        var response = await client.DeleteAsync($"/api/boards/{board.Id}");

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert - database state
        await using var db = _factory.CreateDbContext();
        var deleted = await db.Boards.AsNoTracking()
            .SingleOrDefaultAsync(b => b.Id == board.Id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteBoard_AsEditor_ReturnsForbidden()
    {
        // Arrange - editor does not have permission to delete boards
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var editor = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync("Protected Board");
        await seeder.LinkUserToBoardAsync(board.Id, editor.Id, UserRoleEnum.EDITOR);

        var client = _factory.CreateAuthenticatedClient(editor.Id, editor.UserName!, editor.Email!);

        // Act
        var response = await client.DeleteAsync($"/api/boards/{board.Id}");

        // Assert - editor role (1) > required OWNER role (0)
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Assert - board still exists
        await using var db = _factory.CreateDbContext();
        var stillExists = await db.Boards.AsNoTracking()
            .AnyAsync(b => b.Id == board.Id);
        stillExists.Should().BeTrue();
    }

    // ---------------------------------------------------------------------------
    // PUT /api/boards/{boardId}  [AuthorizeRole(EDITOR)]
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task UpdateBoard_AsEditor_ReturnsOkWithUpdatedBoard()
    {
        // Arrange
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var editor = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync("Original Title");
        await seeder.LinkUserToBoardAsync(board.Id, editor.Id, UserRoleEnum.EDITOR);

        var client = _factory.CreateAuthenticatedClient(editor.Id, editor.UserName!, editor.Email!);

        // Fetch current board to get version token for optimistic concurrency
        var getResponse = await client.GetAsync($"/api/boards/{board.Id}");
        var currentBoard = await getResponse.Content.ReadFromJsonAsync<BoardResponse>();

        var request = new BoardUpdateRequest
        {
            Title = "Updated Title",
            Description = "Updated description",
            ImageName = "updated.jpg",
            Version = currentBoard!.Version
        };

        // Act
        var response = await client.PutAsJsonAsync($"/api/boards/{board.Id}", request);

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - response body
        var body = await response.Content.ReadFromJsonAsync<BoardResponse>();
        body.Should().NotBeNull();
        body!.Title.Should().Be("Updated Title");
        body.Description.Should().Be("Updated description");

        // Assert - database state
        await using var db = _factory.CreateDbContext();
        var persisted = await db.Boards.AsNoTracking().SingleAsync(b => b.Id == board.Id);
        persisted.Title.Should().Be("Updated Title");
    }

    [Fact]
    public async Task UpdateBoard_AsViewer_ReturnsForbidden()
    {
        // Arrange
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var viewer = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync("Protected Board");
        await seeder.LinkUserToBoardAsync(board.Id, viewer.Id, UserRoleEnum.VIEWER);

        var client = _factory.CreateAuthenticatedClient(viewer.Id, viewer.UserName!, viewer.Email!);

        var request = new BoardUpdateRequest
        {
            Title = "Attempted Update",
            Description = "desc",
            ImageName = "img.jpg",
            Version = 0
        };

        // Act
        var response = await client.PutAsJsonAsync($"/api/boards/{board.Id}", request);

        // Assert - viewer role (2) > required EDITOR role (1)
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---------------------------------------------------------------------------
    // PATCH /api/boards/{boardId}  [AuthorizeRole(EDITOR)]
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task PatchBoard_AsEditor_ReturnsOkWithPatchedTitle()
    {
        // Arrange
        using var scope = _factory.CreateServiceScope();
        var seeder = new DatabaseSeeder(scope);
        var editor = await seeder.CreateUserAsync();
        var board = await seeder.CreateBoardAsync("Original Patch Title");
        await seeder.LinkUserToBoardAsync(board.Id, editor.Id, UserRoleEnum.EDITOR);

        var client = _factory.CreateAuthenticatedClient(editor.Id, editor.UserName!, editor.Email!);

        // Fetch current version for optimistic concurrency
        var getResponse = await client.GetAsync($"/api/boards/{board.Id}");
        var current = await getResponse.Content.ReadFromJsonAsync<BoardResponse>();

        var patchDoc = new object[]
        {
            new { op = "replace", path = "/title", value = (object)"Patched Title" },
            new { op = "replace", path = "/version", value = (object)current!.Version }
        };
        var content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(patchDoc),
            System.Text.Encoding.UTF8,
            "application/json-patch+json");

        // Act
        var response = await client.PatchAsync($"/api/boards/{board.Id}", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<BoardResponse>();
        body!.Title.Should().Be("Patched Title");
    }

    // ---------------------------------------------------------------------------
    // Helper record for deserializing paged responses
    // ---------------------------------------------------------------------------
    private record PagedResult<T>(IEnumerable<T> Items, int PageNumber, int PageCount, int PageSize);
}
