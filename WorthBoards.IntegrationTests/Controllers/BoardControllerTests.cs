using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Common.Enums;
using WorthBoards.IntegrationTests.Infrastructure;

namespace WorthBoards.IntegrationTests.Controllers;

public class BoardControllerTests(CustomWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    // ── GET /api/boards ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllCurrentUserBoards_AuthenticatedUser_ReturnsOkWithBoardList()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(token);
        SetAuthToken(token);

        // Act
        var response = await Client.GetAsync("/api/boards");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<PagedResult<BoardResponse>>();
        body.Should().NotBeNull();
        body!.Items.Should().Contain(b => b.Id == board.Id);
    }

    [Fact]
    public async Task GetAllCurrentUserBoards_UnauthenticatedUser_ReturnsUnauthorized()
    {
        // Arrange
        ClearAuthToken();

        // Act
        var response = await Client.GetAsync("/api/boards");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── GET /api/boards/{boardId} ─────────────────────────────────────────────

    [Fact]
    public async Task GetBoardById_ExistingBoard_ReturnsOkWithBoard()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(token);
        SetAuthToken(token);

        // Act
        var response = await Client.GetAsync($"/api/boards/{board.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<BoardResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(board.Id);
        body.Title.Should().Be(board.Title);
    }

    [Fact]
    public async Task GetBoardById_NonExistingBoard_ReturnsNotFound()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);

        // Act
        var response = await Client.GetAsync("/api/boards/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── POST /api/boards ──────────────────────────────────────────────────────

    [Fact]
    public async Task CreateBoard_AuthenticatedUser_ReturnsCreatedWithBoardAndPersistsToDb()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);
        var request = new BoardRequest
        {
            Title = "My New Board",
            Description = "Test board description",
            ImageName = "board.jpg"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/boards", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<BoardResponse>();
        body.Should().NotBeNull();
        body!.Title.Should().Be("My New Board");
        body.Id.Should().BeGreaterThan(0);

        var dbBoard = await QueryAsync(db => db.Boards.FindAsync(body.Id).AsTask());
        dbBoard.Should().NotBeNull();
        dbBoard!.Title.Should().Be("My New Board");
    }

    [Fact]
    public async Task CreateBoard_UnauthenticatedUser_ReturnsUnauthorized()
    {
        // Arrange
        ClearAuthToken();
        var request = new BoardRequest
        {
            Title = "Unauthorized Board",
            Description = "Should not be created",
            ImageName = "board.jpg"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/boards", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── PUT /api/boards/{boardId} ─────────────────────────────────────────────

    [Fact]
    public async Task UpdateBoard_OwnerUser_ReturnsOkWithUpdatedBoard()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(token);
        SetAuthToken(token);

        var updateRequest = new BoardUpdateRequest
        {
            Title = "Updated Title",
            Description = "Updated description",
            ImageName = "updated.jpg",
            Version = board.Version
        };

        // Act
        var response = await Client.PutAsJsonAsync($"/api/boards/{board.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<BoardResponse>();
        body.Should().NotBeNull();
        body!.Title.Should().Be("Updated Title");

        var dbBoard = await QueryAsync(db => db.Boards.FindAsync(board.Id).AsTask());
        dbBoard!.Title.Should().Be("Updated Title");
    }

    [Fact]
    public async Task UpdateBoard_ViewerUser_ReturnsForbidden()
    {
        // Arrange
        var (_, ownerToken) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(ownerToken);

        var (viewerId, _) = await RegisterAndLoginAsync();
        await LinkUserToBoardDirectlyAsync(board.Id, viewerId, UserRoleEnum.VIEWER);

        var (_, viewerToken) = await RegisterAndLoginAsync();
        // re-login as viewer — get a fresh user and link them
        var (viewerId2, viewerToken2) = await RegisterAndLoginAsync();
        await LinkUserToBoardDirectlyAsync(board.Id, viewerId2, UserRoleEnum.VIEWER);
        SetAuthToken(viewerToken2);

        var updateRequest = new BoardUpdateRequest
        {
            Title = "Viewer Attempt",
            Description = "Should fail",
            ImageName = "board.jpg",
            Version = board.Version
        };

        // Act
        var response = await Client.PutAsJsonAsync($"/api/boards/{board.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── DELETE /api/boards/{boardId} ──────────────────────────────────────────

    [Fact]
    public async Task DeleteBoard_OwnerUser_ReturnsNoContentAndRemovesFromDb()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(token);
        SetAuthToken(token);

        // Act
        var response = await Client.DeleteAsync($"/api/boards/{board.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var dbBoard = await QueryAsync(db => db.Boards.FindAsync(board.Id).AsTask());
        dbBoard.Should().BeNull();
    }

    [Fact]
    public async Task DeleteBoard_ViewerUser_ReturnsForbidden()
    {
        // Arrange
        var (_, ownerToken) = await RegisterAndLoginAsync();
        var board = await CreateBoardAsync(ownerToken);

        var (viewerId, viewerToken) = await RegisterAndLoginAsync();
        await LinkUserToBoardDirectlyAsync(board.Id, viewerId, UserRoleEnum.VIEWER);
        SetAuthToken(viewerToken);

        // Act
        var response = await Client.DeleteAsync($"/api/boards/{board.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}

// Local helper record for deserializing paginated responses.
file record PagedResult<T>(IEnumerable<T> Items, int PageNumber, int PageCount, int PageSize);
