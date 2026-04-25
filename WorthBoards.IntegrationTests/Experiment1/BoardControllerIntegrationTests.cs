using System.Net;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.IntegrationTests.Experiment1.TestInfrastructure;
using Xunit;

namespace WorthBoards.IntegrationTests.Experiment1;

public class BoardControllerIntegrationTests(IntegrationTestWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    [Fact]
    public async Task CreateBoard_AuthenticatedRequest_CreatesBoardAndOwnerLink()
    {
        // Arrange
        var session = await CreateAuthenticatedUserAsync("board_happy");
        var request = new BoardRequest
        {
            Title = "Integration Board",
            Description = "Board for integration test",
            ImageName = "demo.png"
        };

        // Act
        var response = await session.Client.PostAsJsonAsync("/api/boards", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var bodyText = await response.Content.ReadAsStringAsync();
        bodyText.Should().Contain("Integration Board");

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WorthBoards.Data.Database.ApplicationDbContext>();
        var persistedBoard = await dbContext.Boards.SingleAsync(b => b.Title == request.Title);
        persistedBoard.Description.Should().Be(request.Description);

        var ownerLink = await dbContext.BoardOnUsers.SingleAsync(b => b.BoardId == persistedBoard.Id && b.UserId == session.UserId);
        ownerLink.UserRole.Should().Be(WorthBoards.Common.Enums.UserRoleEnum.OWNER);
    }

    [Fact]
    public async Task CreateBoard_MissingTitle_ReturnsBadRequestAndDoesNotPersistBoard()
    {
        // Arrange
        var session = await CreateAuthenticatedUserAsync("board_negative");
        var request = new BoardRequest
        {
            Title = string.Empty,
            Description = "Invalid board",
            ImageName = "invalid.png"
        };

        using var arrangeScope = Factory.Services.CreateScope();
        var arrangeDb = arrangeScope.ServiceProvider.GetRequiredService<WorthBoards.Data.Database.ApplicationDbContext>();
        var boardCountBefore = await arrangeDb.Boards.CountAsync();

        // Act
        var response = await session.Client.PostAsJsonAsync("/api/boards", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Title");

        using var assertScope = Factory.Services.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<WorthBoards.Data.Database.ApplicationDbContext>();
        var boardCountAfter = await assertDb.Boards.CountAsync();
        boardCountAfter.Should().Be(boardCountBefore);
    }

    [Fact]
    public async Task GetAllCurrentUserBoards_ExistingBoards_ReturnsPaginatedItems()
    {
        // Arrange
        var session = await CreateAuthenticatedUserAsync("board_list");
        await CreateBoardAsync(session.Client, "B1");
        await CreateBoardAsync(session.Client, "B2");

        // Act
        var response = await session.Client.GetAsync("/api/boards?pageNum=0&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("items");
        body.Should().Contain("B1");
    }

    [Fact]
    public async Task UpdateBoard_ValidVersion_UpdatesPersistedBoard()
    {
        // Arrange
        var session = await CreateAuthenticatedUserAsync("board_update");
        var boardId = await CreateBoardAsync(session.Client, "Before Update");
        var current = await session.Client.GetFromJsonAsync<BoardResponse>($"/api/boards/{boardId}");

        var request = new BoardUpdateRequest
        {
            Title = "After Update",
            Description = "Updated description",
            ImageName = "updated.png",
            Version = current!.Version
        };

        // Act
        var response = await session.Client.PutAsJsonAsync($"/api/boards/{boardId}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WorthBoards.Data.Database.ApplicationDbContext>();
        var board = await db.Boards.SingleAsync(b => b.Id == boardId);
        board.Title.Should().Be("After Update");
    }

    [Fact]
    public async Task PatchBoard_ValidPatch_UpdatesBoardTitle()
    {
        // Arrange
        var session = await CreateAuthenticatedUserAsync("board_patch");
        var boardId = await CreateBoardAsync(session.Client, "Patch Before");
        var patchBody = "[{\"op\":\"replace\",\"path\":\"/title\",\"value\":\"Patch After\"}]";
        using var content = new StringContent(patchBody, Encoding.UTF8, "application/json-patch+json");

        // Act
        var response = await session.Client.PatchAsync($"/api/boards/{boardId}", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WorthBoards.Data.Database.ApplicationDbContext>();
        var board = await db.Boards.SingleAsync(b => b.Id == boardId);
        board.Title.Should().Be("Patch After");
    }

    [Fact]
    public async Task DeleteBoard_OwnerRequest_RemovesBoardFromDatabase()
    {
        // Arrange
        var session = await CreateAuthenticatedUserAsync("board_delete");
        var boardId = await CreateBoardAsync(session.Client, "Delete Me");

        // Act
        var response = await session.Client.DeleteAsync($"/api/boards/{boardId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WorthBoards.Data.Database.ApplicationDbContext>();
        var exists = await db.Boards.AnyAsync(b => b.Id == boardId);
        exists.Should().BeFalse();
    }
}
