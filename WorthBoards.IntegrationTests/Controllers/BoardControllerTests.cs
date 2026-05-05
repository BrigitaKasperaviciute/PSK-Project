using Microsoft.EntityFrameworkCore;

namespace WorthBoards.IntegrationTests.Controllers;

[Collection(nameof(ApiCollection))]
[Trait("Category", "Integration")]
public sealed class BoardControllerTests(CustomWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    // ── GET /api/boards ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllCurrentUserBoards_WhenAuthenticated_ReturnsOkWithBoards()
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

        var body = await response.Content.ReadFromJsonAsync<PagedResult<BoardResponse>>();
        body.Should().NotBeNull();
        body!.Items.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task GetAllCurrentUserBoards_WhenUnauthenticated_ReturnsUnauthorized()
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
    public async Task GetBoardById_WithExistingBoard_ReturnsOkWithBoard()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);
        var board = await CreateBoardAsync("Specific Board");

        // Act
        var response = await Client.GetAsync($"/api/boards/{board.Id}");

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<BoardResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(board.Id);
        body.Title.Should().Be("Specific Board");
    }

    [Fact]
    public async Task GetBoardById_WithNonexistentId_ReturnsNotFound()
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
    public async Task CreateBoard_WhenAuthenticated_ReturnsCreatedAndPersistsBoard()
    {
        // Arrange
        var (userId, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);

        var request = new BoardRequest
        {
            Title = "My New Board",
            Description = "Integration test board",
            ImageName = "board.png"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/boards", request);

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<BoardResponse>();
        body.Should().NotBeNull();
        body!.Title.Should().Be("My New Board");
        body.Id.Should().BeGreaterThan(0);

        // Assert – database state: board exists and creator is OWNER
        await using var db = CreateDbContext();
        var board = await db.Boards.AsNoTracking()
            .SingleOrDefaultAsync(b => b.Id == body.Id);
        board.Should().NotBeNull();

        var link = await db.BoardOnUsers.AsNoTracking()
            .SingleOrDefaultAsync(bou => bou.BoardId == body.Id && bou.UserId == userId);
        link.Should().NotBeNull();
        link!.UserRole.Should().Be(UserRoleEnum.OWNER);
    }

    [Fact]
    public async Task CreateBoard_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        ClearAuthToken();

        var request = new BoardRequest
        {
            Title = "Unauthorized Board",
            Description = "Should not be created",
            ImageName = "board.png"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/boards", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── DELETE /api/boards/{boardId} ──────────────────────────────────────────

    [Fact]
    public async Task DeleteBoard_AsOwner_ReturnsNoContentAndRemovesBoard()
    {
        // Arrange
        var (_, token) = await RegisterAndLoginAsync();
        SetAuthToken(token);
        var board = await CreateBoardAsync("Board to Delete");

        // Act
        var response = await Client.DeleteAsync($"/api/boards/{board.Id}");

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert – database state
        await using var db = CreateDbContext();
        var deleted = await db.Boards.AsNoTracking()
            .SingleOrDefaultAsync(b => b.Id == board.Id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteBoard_AsEditor_ReturnsForbidden()
    {
        // Arrange – owner creates board, editor tries to delete it
        var (_, ownerToken) = await RegisterAndLoginAsync();
        SetAuthToken(ownerToken);
        var board = await CreateBoardAsync("Protected Board");

        var (editorId, editorToken) = await RegisterAndLoginAsync();
        await LinkUserToBoardDirectlyAsync(board.Id, editorId, UserRoleEnum.EDITOR);
        SetAuthToken(editorToken);

        // Act
        var response = await Client.DeleteAsync($"/api/boards/{board.Id}");

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Assert – board still exists
        await using var db = CreateDbContext();
        var still = await db.Boards.AsNoTracking()
            .SingleOrDefaultAsync(b => b.Id == board.Id);
        still.Should().NotBeNull();
    }

    // ── PUT /api/boards/{boardId} ─────────────────────────────────────────────

    [Fact]
    public async Task UpdateBoard_AsEditor_ReturnsOkWithUpdatedBoard()
    {
        // Arrange
        var (_, ownerToken) = await RegisterAndLoginAsync();
        SetAuthToken(ownerToken);
        var board = await CreateBoardAsync("Original Title");

        var (editorId, editorToken) = await RegisterAndLoginAsync();
        await LinkUserToBoardDirectlyAsync(board.Id, editorId, UserRoleEnum.EDITOR);
        SetAuthToken(editorToken);

        var updateRequest = new BoardUpdateRequest
        {
            Title = "Updated Title",
            Description = "Updated Description",
            ImageName = "updated.png",
            Version = board.Version
        };

        // Act
        var response = await Client.PutAsJsonAsync($"/api/boards/{board.Id}", updateRequest);

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<BoardResponse>();
        body.Should().NotBeNull();
        body!.Title.Should().Be("Updated Title");

        // Assert – database state
        await using var db = CreateDbContext();
        var updated = await db.Boards.AsNoTracking()
            .SingleOrDefaultAsync(b => b.Id == board.Id);
        updated!.Title.Should().Be("Updated Title");
    }

    [Fact]
    public async Task UpdateBoard_AsViewer_ReturnsForbidden()
    {
        // Arrange
        var (_, ownerToken) = await RegisterAndLoginAsync();
        SetAuthToken(ownerToken);
        var board = await CreateBoardAsync("Viewer Board");

        var (viewerId, viewerToken) = await RegisterAndLoginAsync();
        await LinkUserToBoardDirectlyAsync(board.Id, viewerId, UserRoleEnum.VIEWER);
        SetAuthToken(viewerToken);

        var updateRequest = new BoardUpdateRequest
        {
            Title = "Attempted Update",
            Description = "Should fail",
            ImageName = "fail.png",
            Version = board.Version
        };

        // Act
        var response = await Client.PutAsJsonAsync($"/api/boards/{board.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── PATCH /api/boards/{boardId} ───────────────────────────────────────────

    [Fact]
    public async Task PatchBoard_AsEditor_ReturnsOkWithPatchedBoard()
    {
        // Arrange
        var (_, ownerToken) = await RegisterAndLoginAsync();
        SetAuthToken(ownerToken);
        var board = await CreateBoardAsync("Patch Me");

        var (editorId, editorToken) = await RegisterAndLoginAsync();
        await LinkUserToBoardDirectlyAsync(board.Id, editorId, UserRoleEnum.EDITOR);
        SetAuthToken(editorToken);

        var patch = new object[]
        {
            new { op = "replace", path = "/title",   value = (object)"Patched Title" },
            new { op = "replace", path = "/version", value = (object)board.Version }
        };

        // Act
        using var content = JsonContent.Create(patch);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json-patch+json");
        var response = await Client.PatchAsync($"/api/boards/{board.Id}", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<BoardResponse>();
        body!.Title.Should().Be("Patched Title");
    }

    [Fact]
    public async Task PatchBoard_WithoutBoardMembership_ReturnsForbidden()
    {
        // Arrange – owner creates board, a different user (not a board member) tries to patch
        var (_, ownerToken) = await RegisterAndLoginAsync();
        SetAuthToken(ownerToken);
        var board = await CreateBoardAsync("No Access Board");

        var (_, strangerToken) = await RegisterAndLoginAsync();
        SetAuthToken(strangerToken);

        var patch = new[] { new { op = "replace", path = "/title", value = "Hacked" } };

        using var content = JsonContent.Create(patch);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json-patch+json");

        // Act
        var response = await Client.PatchAsync($"/api/boards/{board.Id}", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}

// Minimal projection for the paginated response shape returned by GetAllCurrentUserBoards.
file record PagedResult<T>(
    List<T> Items,
    int PageNumber,
    int PageCount,
    int PageSize);
