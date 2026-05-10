using System.Net;
using WorthBoards.Business.Dtos.Identity;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Common.Enums;

namespace WorthBoards.IntegrationTests.Controllers;

public sealed class BoardCollaborationControllerTests
{
    [Fact]
    public async Task InviteUser_ReturnsNoContent_ForOwner()
    {
        await using var factory = new WorthBoardsApiFactory();
        var client = factory.CreateAuthorizedClient();

        factory.BoardServiceMock
            .Setup(service => service.GetUserRoleByBoardIdAndUserIdAsync(5, 7))
            .ReturnsAsync(UserRoleEnum.OWNER);

        factory.NotificationServiceMock
            .Setup(service => service.NotifyBoardInvitation(5, 12, 7, UserRoleEnum.EDITOR, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await client.SendAsync(TestRequestFactory.CreateJsonRequest(
            HttpMethod.Post,
            "/api/boards/5/invite",
            new InvitationRequest { UserId = 12, Role = UserRoleEnum.EDITOR },
            userId: 7));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task InviteUser_ReturnsForbidden_ForEditor()
    {
        await using var factory = new WorthBoardsApiFactory();
        var client = factory.CreateAuthorizedClient();

        factory.BoardServiceMock
            .Setup(service => service.GetUserRoleByBoardIdAndUserIdAsync(5, 7))
            .ReturnsAsync(UserRoleEnum.EDITOR);

        var response = await client.SendAsync(TestRequestFactory.CreateJsonRequest(
            HttpMethod.Post,
            "/api/boards/5/invite",
            new InvitationRequest { UserId = 12, Role = UserRoleEnum.EDITOR },
            userId: 7));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TransferOwnership_ReturnsNoContent_ForOwner()
    {
        await using var factory = new WorthBoardsApiFactory();
        var client = factory.CreateAuthorizedClient();

        factory.BoardServiceMock
            .Setup(service => service.GetUserRoleByBoardIdAndUserIdAsync(5, 7))
            .ReturnsAsync(UserRoleEnum.OWNER);

        factory.BoardOnUserServiceMock
            .Setup(service => service.TransferOwnershipAsync(5, 7, 12, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await client.SendAsync(TestRequestFactory.CreateJsonRequest(
            HttpMethod.Post,
            "/api/boards/5/collaborators/12",
            userId: 7));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task TransferOwnership_ReturnsForbidden_ForViewer()
    {
        await using var factory = new WorthBoardsApiFactory();
        var client = factory.CreateAuthorizedClient();

        factory.BoardServiceMock
            .Setup(service => service.GetUserRoleByBoardIdAndUserIdAsync(5, 7))
            .ReturnsAsync(UserRoleEnum.VIEWER);

        var response = await client.SendAsync(TestRequestFactory.CreateJsonRequest(
            HttpMethod.Post,
            "/api/boards/5/collaborators/12",
            userId: 7));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}