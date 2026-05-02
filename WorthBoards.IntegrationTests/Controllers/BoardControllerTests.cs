using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Common.Enums;
using WorthBoards.IntegrationTests.Infrastructure;

namespace WorthBoards.IntegrationTests.Controllers;

public class BoardControllerTests(CustomWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    // ── GET /api/boards ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllCurrentUserBoards_AuthenticatedUser_ReturnsOwnBoards()
    {
        // Arrange
        var (_, _, client) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(client);

        // Act
        var response = await client.GetAsync("/api/boards");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PaginatedResponse<BoardResponse>>();
        body.Should().NotBeNull();
        body!.Items.Should().Contain(b => b.Id == board.Id);
    }

    [Fact]
    public async Task GetAllCurrentUserBoards_Unauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var client = Factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/boards");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── GET /api/boards/{boardId} ────────────────────────────────────────────

    [Fact]
    public async Task GetBoardById_ExistingBoard_ReturnsBoardResponse()
    {
        // Arrange
        var (_, _, client) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(client, "Specific Board");

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<BoardResponse>();
        result.Should().NotBeNull();
        result!.Id.Should().Be(board.Id);
        result.Title.Should().Be("Specific Board");
    }

    [Fact]
    public async Task GetBoardById_NonExistentBoard_ReturnsNotFound()
    {
        // Arrange
        var (_, _, client) = await RegisterAndLoginAsync();

        // Act
        var response = await client.GetAsync("/api/boards/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── POST /api/boards ─────────────────────────────────────────────────────

    [Fact]
    public async Task CreateBoard_ValidRequest_ReturnsBoardAndPersistsToDatabase()
    {
        // Arrange
        var (userId, _, client) = await RegisterAndLoginAsync();
        var request = new BoardRequest
        {
            Title = "My New Board",
            Description = "A useful board",
            ImageName = ""
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/boards", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var board = await response.Content.ReadFromJsonAsync<BoardResponse>();
        board.Should().NotBeNull();
        board!.Title.Should().Be("My New Board");
        board.Description.Should().Be("A useful board");

        using var db = GetDbContext();
        db.Boards.Any(b => b.Id == board.Id && b.Title == "My New Board").Should().BeTrue();
        db.BoardOnUsers.Any(bou => bou.BoardId == board.Id && bou.UserId == userId && bou.UserRole == UserRoleEnum.OWNER)
            .Should().BeTrue();
    }

    [Fact]
    public async Task CreateBoard_EmptyTitle_ReturnsBadRequest()
    {
        // Arrange
        var (_, _, client) = await RegisterAndLoginAsync();
        var request = new BoardRequest
        {
            Title = "",
            Description = "No title board",
            ImageName = ""
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/boards", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── DELETE /api/boards/{boardId} ─────────────────────────────────────────

    [Fact]
    public async Task DeleteBoard_AsOwner_ReturnsNoContentAndRemovesFromDatabase()
    {
        // Arrange
        var (_, _, client) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(client);

        // Act
        var response = await client.DeleteAsync($"/api/boards/{board.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var db = GetDbContext();
        db.Boards.Any(b => b.Id == board.Id).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteBoard_AsEditor_ReturnsForbidden()
    {
        // Arrange
        var (_, _, ownerClient) = await RegisterAndLoginAsync();
        var (editorId, _, editorClient) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(ownerClient);
        await LinkUserToBoardDirectlyAsync(board.Id, editorId, UserRoleEnum.EDITOR);

        // Act
        var response = await editorClient.DeleteAsync($"/api/boards/{board.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── PUT /api/boards/{boardId} ────────────────────────────────────────────

    [Fact]
    public async Task UpdateBoard_ValidRequestWithCorrectVersion_ReturnsUpdatedBoard()
    {
        // Arrange
        var (_, _, client) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(client, "Original Title");

        var updateRequest = new BoardUpdateRequest
        {
            Title = "Updated Title",
            Description = "Updated Desc",
            ImageName = "",
            Version = board.Version
        };

        // Act
        var response = await client.PutAsJsonAsync($"/api/boards/{board.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<BoardResponse>();
        updated!.Title.Should().Be("Updated Title");

        using var db = GetDbContext();
        db.Boards.Find(board.Id)!.Title.Should().Be("Updated Title");
    }

    [Fact]
    public async Task UpdateBoard_StaleVersion_ReturnsConflict()
    {
        // Arrange
        var (_, _, client) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(client);

        var updateRequest = new BoardUpdateRequest
        {
            Title = "Conflict Title",
            Description = "Desc",
            ImageName = "",
            Version = board.Version + 999  // stale version
        };

        // Act
        var response = await client.PutAsJsonAsync($"/api/boards/{board.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ── PATCH /api/boards/{boardId} ──────────────────────────────────────────

    [Fact]
    public async Task PatchBoard_ValidPatch_ReturnsUpdatedBoard()
    {
        // Arrange
        var (_, _, client) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(client, "Patch Me");

        var patchDoc = new PatchOp[]
        {
            new("replace", "/title", "Patched Title"),
            new("replace", "/version", board.Version)
        };

        // Act
        var response = await client.PatchAsJsonAsync($"/api/boards/{board.Id}", patchDoc);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var patched = await response.Content.ReadFromJsonAsync<BoardResponse>();
        patched!.Title.Should().Be("Patched Title");
    }

    [Fact]
    public async Task PatchBoard_AsViewer_ReturnsForbidden()
    {
        // Arrange
        var (_, _, ownerClient) = await RegisterAndLoginAsync();
        var (viewerId, _, viewerClient) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(ownerClient);
        await LinkUserToBoardDirectlyAsync(board.Id, viewerId, UserRoleEnum.VIEWER);

        var patchDoc = new PatchOp[]
        {
            new("replace", "/title", "Not Allowed"),
            new("replace", "/version", 0)
        };

        // Act
        var response = await viewerClient.PatchAsJsonAsync($"/api/boards/{board.Id}", patchDoc);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private record PaginatedResponse<T>(IEnumerable<T> Items, int PageNumber, int PageCount, int PageSize);
    private record PatchOp(string op, string path, object value);
}
