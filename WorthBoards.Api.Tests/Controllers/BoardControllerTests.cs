using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using WorthBoards.Api.Tests.Infrastructure;
using WorthBoards.Business.Dtos.Responses;

namespace WorthBoards.Api.Tests.Controllers;

public class BoardControllerTests : IntegrationTestBase
{
    [Fact]
    public async Task GetBoardById_WithExistingBoard_ReturnsBoard()
    {
        // Arrange
        await ClearDatabaseAsync();
        var user = await TestHelpers.CreateTestUserAsync(Factory.Services);
        var board = await TestHelpers.CreateTestBoardAsync(Factory.Services, user.Id);

        var token = GetJwtToken(user.Id, user.Email!);
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await Client.GetAsync($"/api/boards/{board.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var boardResponse = await response.Content.ReadFromJsonAsync<BoardResponse>();
        Assert.NotNull(boardResponse);
        Assert.Equal(board.Id, boardResponse.Id);
        Assert.Equal("Test Board", boardResponse.Title);
    }

    [Fact]
    public async Task GetBoardById_WithNonExistentBoard_ReturnsNotFound()
    {
        // Arrange
        await ClearDatabaseAsync();
        var user = await TestHelpers.CreateTestUserAsync(Factory.Services);

        var token = GetJwtToken(user.Id, user.Email!);
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var nonExistentBoardId = 99999;

        // Act
        var response = await Client.GetAsync($"/api/boards/{nonExistentBoardId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
