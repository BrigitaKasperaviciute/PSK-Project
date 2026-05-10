using System.Net;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Common.Enums;

namespace WorthBoards.IntegrationTests.Controllers;

public sealed class CommentAndNotificationControllerTests
{
    [Fact]
    public async Task CreateComment_ReturnsCreated_WhenUserClaimIsPresent()
    {
        await using var factory = new WorthBoardsApiFactory();
        var client = factory.CreateAuthorizedClient();

        factory.CommentServiceMock
            .Setup(service => service.CreateCommentAsync(7, 33, It.IsAny<CommentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommentResponse
            {
                Id = 44,
                TaskId = 33,
                UserId = 7,
                Content = "Looks good",
                CreationDate = DateTime.UtcNow,
                Edited = false,
                Version = 1
            });

        var response = await client.SendAsync(TestRequestFactory.CreateJsonRequest(
            HttpMethod.Post,
            "/api/boards/5/tasks/33/comments",
            new CommentRequest { Content = "Looks good" },
            userId: 7));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CreateComment_ReturnsUnauthorized_WhenClaimIsMissing()
    {
        await using var factory = new WorthBoardsApiFactory();
        var client = factory.CreateAuthorizedClient();

        var response = await client.SendAsync(TestRequestFactory.CreateJsonRequest(
            HttpMethod.Post,
            "/api/boards/5/tasks/33/comments",
            new CommentRequest { Content = "Looks good" },
            userId: null,
            email: null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AcceptInvitation_ReturnsNoContent_WhenUserClaimIsPresent()
    {
        await using var factory = new WorthBoardsApiFactory();
        var client = factory.CreateAuthorizedClient();

        factory.NotificationServiceMock
            .Setup(service => service.AcceptInvitation(55, 7, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await client.SendAsync(TestRequestFactory.CreateJsonRequest(
            HttpMethod.Post,
            "/api/notifications/55/accept",
            userId: 7));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task AcceptInvitation_ReturnsUnauthorized_WhenClaimIsMissing()
    {
        await using var factory = new WorthBoardsApiFactory();
        var client = factory.CreateAuthorizedClient();

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/notifications/55/accept");
        request.Headers.Add("X-Test-Email", "tester@example.com");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetNotificationsByUserId_ReturnsOk_WhenServiceSucceeds()
    {
        await using var factory = new WorthBoardsApiFactory();
        var client = factory.CreateAuthorizedClient();

        factory.NotificationServiceMock
            .Setup(service => service.GetNotificationsByUserId(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<NotificationResponse>
            {
                new() { Id = 1, SendDate = DateTime.UtcNow, Title = "Invitation", Description = "You were invited", BoardId = 5, TaskId = null, Type = NotificationEventTypeEnum.INVITATION }
            });

        var response = await client.SendAsync(TestRequestFactory.CreateJsonRequest(
            HttpMethod.Get,
            "/api/notifications",
            userId: 7));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DeleteAllNotifications_ReturnsOk_WhenServiceSucceeds()
    {
        await using var factory = new WorthBoardsApiFactory();
        var client = factory.CreateAuthorizedClient();

        factory.NotificationServiceMock
            .Setup(service => service.UnlinkAllNotifications(7, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await client.SendAsync(TestRequestFactory.CreateJsonRequest(
            HttpMethod.Delete,
            "/api/notifications",
            userId: 7));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}