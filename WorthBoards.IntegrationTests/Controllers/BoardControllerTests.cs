using System.Net;
using System.Net.Http.Headers;
using System.Text;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Common.Exceptions.Custom;

namespace WorthBoards.IntegrationTests.Controllers;

public sealed class BoardControllerTests
{
    [Fact]
    public async Task GetAllCurrentUserBoards_ReturnsOk_WhenUserClaimIsPresent()
    {
        await using var factory = new WorthBoardsApiFactory();
        var client = factory.CreateAuthorizedClient();

        factory.BoardServiceMock
            .Setup(service => service.GetUserBoardsAsync(7, 0, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<BoardResponse>
            {
                new() { Id = 1, Title = "Board A", Description = "Alpha", ImageURL = null!, CreationDate = DateTime.UtcNow, Version = 1 }
            }, 1));

        var response = await client.GetAsync("/api/boards?userId=7&pageNum=0&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetAllCurrentUserBoards_ReturnsUnauthorized_WhenClaimIsMissing()
    {
        await using var factory = new WorthBoardsApiFactory();
        var client = factory.CreateAuthorizedClient();

        var response = await client.GetAsync("/api/boards?pageNum=0&pageSize=10");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAllCurrentUserBoards_ReturnsOk_WhenUserIdComesFromClaim_AndTotalCountIsZero()
    {
        await using var factory = new WorthBoardsApiFactory();
        var client = factory.CreateAuthorizedClient();

        factory.BoardServiceMock
            .Setup(service => service.GetUserBoardsAsync(7, 0, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<BoardResponse>(), 0));

        var response = await client.SendAsync(TestRequestFactory.CreateJsonRequest(
            HttpMethod.Get,
            "/api/boards?pageNum=0&pageSize=10",
            userId: 7));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreateBoard_ReturnsCreated_WhenUserClaimIsPresent()
    {
        await using var factory = new WorthBoardsApiFactory();
        var client = factory.CreateAuthorizedClient();

        factory.BoardServiceMock
            .Setup(service => service.CreateBoardAsync(7, It.IsAny<BoardRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BoardResponse
            {
                Id = 11,
                Title = "New board",
                Description = "Created through integration test",
                ImageURL = null!,
                CreationDate = DateTime.UtcNow,
                Version = 1
            });

        var response = await client.SendAsync(TestRequestFactory.CreateJsonRequest(
            HttpMethod.Post,
            "/api/boards?userId=7",
            new BoardRequest { Title = "New board", Description = "Created through integration test", ImageName = "board.png" }));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CreateBoard_ReturnsUnauthorized_WhenClaimIsMissing()
    {
        await using var factory = new WorthBoardsApiFactory();
        var client = factory.CreateAuthorizedClient();

        var response = await client.SendAsync(TestRequestFactory.CreateJsonRequest(
            HttpMethod.Post,
            "/api/boards",
            new BoardRequest { Title = "New board", Description = "Created through integration test", ImageName = "board.png" },
            userId: null,
            email: null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateBoard_ReturnsCreated_WhenUserIdComesFromClaim()
    {
        await using var factory = new WorthBoardsApiFactory();
        var client = factory.CreateAuthorizedClient();

        factory.BoardServiceMock
            .Setup(service => service.CreateBoardAsync(7, It.IsAny<BoardRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BoardResponse
            {
                Id = 12,
                Title = "Claim board",
                Description = "Created from user claim",
                ImageURL = null!,
                CreationDate = DateTime.UtcNow,
                Version = 1
            });

        var response = await client.SendAsync(TestRequestFactory.CreateJsonRequest(
            HttpMethod.Post,
            "/api/boards",
            new BoardRequest { Title = "Claim board", Description = "Created from user claim", ImageName = "board.png" },
            userId: 7));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task GetBoardById_ReturnsOk_WhenServiceSucceeds()
    {
        await using var factory = new WorthBoardsApiFactory();
        var client = factory.CreateAuthorizedClient();

        factory.BoardServiceMock
            .Setup(service => service.GetBoardByIdAsync(11, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BoardResponse
            {
                Id = 11,
                Title = "Board A",
                Description = "Alpha",
                ImageURL = null!,
                CreationDate = DateTime.UtcNow,
                Version = 1
            });

        var response = await client.GetAsync("/api/boards/11");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetBoardById_ReturnsNotFound_WhenServiceThrowsNotFoundException()
    {
        await using var factory = new WorthBoardsApiFactory();
        var client = factory.CreateAuthorizedClient();

        factory.BoardServiceMock
            .Setup(service => service.GetBoardByIdAsync(11, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Board was not found"));

        var response = await client.GetAsync("/api/boards/11");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateBoard_ReturnsOk_ForEditorRole()
    {
        await using var factory = new WorthBoardsApiFactory();
        var client = factory.CreateAuthorizedClient();

        factory.BoardServiceMock
            .Setup(service => service.GetUserRoleByBoardIdAndUserIdAsync(11, 7))
            .ReturnsAsync(WorthBoards.Common.Enums.UserRoleEnum.EDITOR);

        factory.BoardServiceMock
            .Setup(service => service.UpdateBoardAsync(11, It.IsAny<BoardUpdateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BoardResponse
            {
                Id = 11,
                Title = "Updated board",
                Description = "Updated",
                ImageURL = null!,
                CreationDate = DateTime.UtcNow,
                Version = 2
            });

        var response = await client.SendAsync(TestRequestFactory.CreateJsonRequest(
            HttpMethod.Put,
            "/api/boards/11",
            new BoardUpdateRequest { Title = "Updated board", Description = "Updated", ImageName = "board.png", Version = 1 },
            userId: 7));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DeleteBoard_ReturnsForbidden_ForViewerRole()
    {
        await using var factory = new WorthBoardsApiFactory();
        var client = factory.CreateAuthorizedClient();

        factory.BoardServiceMock
            .Setup(service => service.GetUserRoleByBoardIdAndUserIdAsync(11, 7))
            .ReturnsAsync(WorthBoards.Common.Enums.UserRoleEnum.VIEWER);

        var response = await client.SendAsync(TestRequestFactory.CreateJsonRequest(
            HttpMethod.Delete,
            "/api/boards/11",
            userId: 7));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteBoard_ReturnsNoContent_ForOwnerRole()
    {
        await using var factory = new WorthBoardsApiFactory();
        var client = factory.CreateAuthorizedClient();

        factory.BoardServiceMock
            .Setup(service => service.GetUserRoleByBoardIdAndUserIdAsync(11, 7))
            .ReturnsAsync(WorthBoards.Common.Enums.UserRoleEnum.OWNER);

        factory.BoardServiceMock
            .Setup(service => service.DeleteBoardAsync(11, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await client.SendAsync(TestRequestFactory.CreateJsonRequest(
            HttpMethod.Delete,
            "/api/boards/11",
            userId: 7));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task PatchBoard_ReturnsOk_ForEditorRole()
    {
        await using var factory = new WorthBoardsApiFactory();
        var client = factory.CreateAuthorizedClient();

        factory.BoardServiceMock
            .Setup(service => service.GetUserRoleByBoardIdAndUserIdAsync(11, 7))
            .ReturnsAsync(WorthBoards.Common.Enums.UserRoleEnum.EDITOR);

        factory.BoardServiceMock
            .Setup(service => service.PatchBoardAsync(11, It.IsAny<Microsoft.AspNetCore.JsonPatch.JsonPatchDocument<BoardUpdateRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BoardResponse
            {
                Id = 11,
                Title = "Patched board",
                Description = "Patched",
                ImageURL = null!,
                CreationDate = DateTime.UtcNow,
                Version = 2
            });

        var request = new HttpRequestMessage(HttpMethod.Patch, "/api/boards/11");
        request.Headers.Add("X-Test-UserId", "7");
        request.Headers.Add("X-Test-Email", "tester@example.com");
        request.Content = new StringContent("[{\"op\":\"replace\",\"path\":\"/title\",\"value\":\"Patched board\"}]", Encoding.UTF8);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json-patch+json");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}