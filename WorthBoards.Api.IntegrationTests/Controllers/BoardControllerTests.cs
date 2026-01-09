using System.Net;
using System.Net.Http.Json;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Data.Identity;
using WorthBoards.Domain.Entities;
using Xunit;

namespace WorthBoards.Api.IntegrationTests.Controllers;

public class BoardControllerTests : IntegrationTestBase
{
    public BoardControllerTests(TestWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task GetBoardById_WhenBoardExists_ReturnsBoard()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = 1,
            FirstName = "Test",
            LastName = "User",
            UserName = "testuser",
            Email = "test@example.com",
            CreationDate = DateTime.UtcNow
        };
        DbContext.Users.Add(user);

        var board = new Board
        {
            Id = 1,
            Title = "Test Board",
            Description = "Test Description",
            ImageName = "test.png",
            CreationDate = DateTime.UtcNow
        };
        DbContext.Boards.Add(board);

        var boardOnUser = new BoardOnUser
        {
            BoardId = 1,
            UserId = 1,
            UserRole = Common.Enums.UserRoleEnum.OWNER,
            AddedAt = DateTime.UtcNow
        };
        DbContext.BoardOnUsers.Add(boardOnUser);

        await DbContext.SaveChangesAsync();

        var client = CreateAuthenticatedClient("1", "test@example.com");

        // Act
        var response = await client.GetAsync("/api/boards/1");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<BoardResponse>();
        Assert.NotNull(result);
        Assert.Equal(1, result.Id);
        Assert.Equal("Test Board", result.Title);
    }

    [Fact]
    public async Task GetBoardById_WhenBoardDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var client = CreateAuthenticatedClient("1", "test@example.com");

        // Act
        var response = await client.GetAsync("/api/boards/999");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
