using Microsoft.EntityFrameworkCore;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.IntegrationTests.Infrastructure;

namespace WorthBoards.IntegrationTests.Controllers;

public sealed class BoardControllerTests(CustomWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    // ── GetAllCurrentUserBoards ───────────────────────────────────────────────

    [Fact]
    public async Task GetAllCurrentUserBoards_WhenUserHasBoards_ReturnsOkWithBoards()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);
        await CreateBoardAsync("Board A");
        await CreateBoardAsync("Board B");

        // Act
        var response = await Client.GetAsync("/api/boards");

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert – response body
        var body = await response.Content.ReadFromJsonAsync<PagedResult<BoardResponse>>();
        body.Should().NotBeNull();
        body!.Items.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task GetAllCurrentUserBoards_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        ClearAuthToken();

        // Act
        var response = await Client.GetAsync("/api/boards");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── GetBoardById ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetBoardById_WithExistingBoard_ReturnsOkWithBoard()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);
        var board = await CreateBoardAsync("My Board");

        // Act
        var response = await Client.GetAsync($"/api/boards/{board.Id}");

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert – response body
        var body = await response.Content.ReadFromJsonAsync<BoardResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(board.Id);
        body.Title.Should().Be("My Board");
    }

    [Fact]
    public async Task GetBoardById_WithNonExistentBoard_ReturnsNotFound()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);

        // Act
        var response = await Client.GetAsync("/api/boards/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── CreateBoard ───────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateBoard_WithValidRequest_ReturnsCreatedAndPersistsBoard()
    {
        // Arrange
        var (userId, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);
        var request = new BoardRequest
        {
            Title = "New Board",
            Description = "A great board",
            ImageName = "board.jpg"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/boards", request);

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        // Assert – response body
        var body = await response.Content.ReadFromJsonAsync<BoardResponse>();
        body.Should().NotBeNull();
        body!.Title.Should().Be(request.Title);
        body.Description.Should().Be(request.Description);
        body.Id.Should().BeGreaterThan(0);

        // Assert – database state
        await using var db = Factory.CreateDbContext();
        var persisted = await db.Boards.AsNoTracking().SingleOrDefaultAsync(b => b.Id == body.Id);
        persisted.Should().NotBeNull();
        persisted!.Title.Should().Be(request.Title);

        var link = await db.BoardOnUsers.AsNoTracking()
            .SingleOrDefaultAsync(bou => bou.BoardId == body.Id && bou.UserId == userId);
        link.Should().NotBeNull("owner link must be created automatically");
    }

    [Fact]
    public async Task CreateBoard_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        ClearAuthToken();
        var request = new BoardRequest { Title = "Board", Description = "Desc", ImageName = "img.jpg" };

        // Act
        var response = await Client.PostAsJsonAsync("/api/boards", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── DeleteBoard ───────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteBoard_AsOwner_ReturnsNoContentAndRemovesFromDb()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);
        var board = await CreateBoardAsync("Board to delete");

        // Act
        var response = await Client.DeleteAsync($"/api/boards/{board.Id}");

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert – database state
        await using var db = Factory.CreateDbContext();
        var persisted = await db.Boards.AsNoTracking().SingleOrDefaultAsync(b => b.Id == board.Id);
        persisted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteBoard_AsNonOwner_ReturnsForbidden()
    {
        // Arrange
        var (_, ownerToken) = await RegisterAndLoginAsync();
        SetAuthToken(ownerToken);
        var board = await CreateBoardAsync("Owner's Board");

        var (viewerId, viewerToken) = await RegisterAndLoginAsync();
        await LinkUserToBoardDirectlyAsync(board.Id, viewerId, Common.Enums.UserRoleEnum.VIEWER);
        SetAuthToken(viewerToken);

        // Act
        var response = await Client.DeleteAsync($"/api/boards/{board.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── UpdateBoard ───────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateBoard_AsEditor_ReturnsOkAndUpdatesDb()
    {
        // Arrange
        var (_, ownerToken) = await RegisterAndLoginAsync();
        SetAuthToken(ownerToken);
        var board = await CreateBoardAsync("Original Title");

        var (editorId, editorToken) = await RegisterAndLoginAsync();
        await LinkUserToBoardDirectlyAsync(board.Id, editorId, Common.Enums.UserRoleEnum.EDITOR);
        SetAuthToken(editorToken);

        var updateRequest = new BoardUpdateRequest
        {
            Title = "Updated Title",
            Description = "Updated description",
            ImageName = "updated.jpg"
        };

        // Act
        var response = await Client.PutAsJsonAsync($"/api/boards/{board.Id}", updateRequest);

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert – response body
        var body = await response.Content.ReadFromJsonAsync<BoardResponse>();
        body!.Title.Should().Be(updateRequest.Title);

        // Assert – database state
        await using var db = Factory.CreateDbContext();
        var persisted = await db.Boards.AsNoTracking().SingleOrDefaultAsync(b => b.Id == board.Id);
        persisted!.Title.Should().Be(updateRequest.Title);
    }

    [Fact]
    public async Task UpdateBoard_AsViewer_ReturnsForbidden()
    {
        // Arrange
        var (_, ownerToken) = await RegisterAndLoginAsync();
        SetAuthToken(ownerToken);
        var board = await CreateBoardAsync("Board");

        var (viewerId, viewerToken) = await RegisterAndLoginAsync();
        await LinkUserToBoardDirectlyAsync(board.Id, viewerId, Common.Enums.UserRoleEnum.VIEWER);
        SetAuthToken(viewerToken);

        // Act
        var response = await Client.PutAsJsonAsync($"/api/boards/{board.Id}",
            new BoardUpdateRequest { Title = "Hacked", Description = "Hacked", ImageName = "hacked.jpg" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── PatchBoard ────────────────────────────────────────────────────────────

    [Fact]
    public async Task PatchBoard_AsEditor_ReturnsOkAndUpdatesTitle()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);
        var board = await CreateBoardAsync("Patch Me");

        var patch = new[] { new { op = "replace", path = "/title", value = "Patched Title" } };

        // Act
        var response = await Client.PatchAsJsonAsync($"/api/boards/{board.Id}", patch);

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert – database state
        await using var db = Factory.CreateDbContext();
        var persisted = await db.Boards.AsNoTracking().SingleOrDefaultAsync(b => b.Id == board.Id);
        persisted!.Title.Should().Be("Patched Title");
    }

    [Fact]
    public async Task PatchBoard_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);
        var board = await CreateBoardAsync();
        ClearAuthToken();

        var patch = new[] { new { op = "replace", path = "/title", value = "Should Fail" } };

        // Act
        var response = await Client.PatchAsJsonAsync($"/api/boards/{board.Id}", patch);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}

file record PagedResult<T>(IEnumerable<T> Items, int PageNumber, int PageCount, int PageSize);
