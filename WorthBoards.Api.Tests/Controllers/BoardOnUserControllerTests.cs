using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using WorthBoards.Api.Tests.Infrastructure;
using WorthBoards.Business.Dtos.Responses;

namespace WorthBoards.Api.Tests.Controllers;

public class BoardOnUserControllerTests : IntegrationTestBase
{
    [Fact]
    public async Task GetBoardToUserLink_WithExistingLink_ReturnsLink()
    {
        // Arrange
        await ClearDatabaseAsync();
        var user = await TestHelpers.CreateTestUserAsync(Factory.Services);
        var board = await TestHelpers.CreateTestBoardAsync(Factory.Services, user.Id);

        var token = GetJwtToken(user.Id, user.Email!);
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await Client.GetAsync($"/api/boards/{board.Id}/link/{user.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var linkResponse = await response.Content.ReadFromJsonAsync<LinkUserToBoardResponse>();
        Assert.NotNull(linkResponse);
        Assert.Equal(user.Id, linkResponse.UserId);
        Assert.Equal(board.Id, linkResponse.BoardId);
    }

    [Fact]
    public async Task GetBoardToUserLink_WithNonExistentLink_ReturnsNotFound()
    {
        // Arrange
        await ClearDatabaseAsync();
        var user = await TestHelpers.CreateTestUserAsync(Factory.Services);
        var board = await TestHelpers.CreateTestBoardAsync(Factory.Services, user.Id);

        var token = GetJwtToken(user.Id, user.Email!);
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var nonExistentUserId = 99999;

        // Act
        var response = await Client.GetAsync($"/api/boards/{board.Id}/link/{nonExistentUserId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
