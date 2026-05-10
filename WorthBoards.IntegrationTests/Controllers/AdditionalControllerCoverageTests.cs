using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.JsonPatch.Operations;
using Microsoft.AspNetCore.Identity.Data;
using System.Net;
using System.Text;
using System.Net.Http.Headers;
using WorthBoards.Business.Dtos.Identity;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Common.Enums;
using WorthBoards.Common.Exceptions.Custom;

namespace WorthBoards.IntegrationTests.Controllers;

public sealed class AdditionalControllerCoverageTests
{
    [Fact]
    public async Task ForgotPassword_ReturnsOk_WhenServiceSucceeds()
    {
        await using var factory = new WorthBoardsApiFactory();
        var client = factory.CreateAuthorizedClient();

        factory.AuthServiceMock
            .Setup(service => service.ForgotPasswordAsync(It.IsAny<ForgotPasswordRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PasswordRecoveryResponse("Sent"));

        var response = await client.SendAsync(TestRequestFactory.CreateJsonRequest(
            HttpMethod.Post,
            "/forgot-password",
            new ForgotPasswordRequest { Email = "jane@example.com" },
            userId: null,
            email: null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ForgotPassword_ReturnsBadRequest_WhenServiceThrows()
    {
        await using var factory = new WorthBoardsApiFactory();
        var client = factory.CreateAuthorizedClient();

        factory.AuthServiceMock
            .Setup(service => service.ForgotPasswordAsync(It.IsAny<ForgotPasswordRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BadRequestException("Invalid email"));

        var response = await client.SendAsync(TestRequestFactory.CreateJsonRequest(
            HttpMethod.Post,
            "/forgot-password",
            new ForgotPasswordRequest { Email = "bad" },
            userId: null,
            email: null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_ReturnsOk_WhenServiceSucceeds()
    {
        await using var factory = new WorthBoardsApiFactory();
        var client = factory.CreateAuthorizedClient();

        factory.AuthServiceMock
            .Setup(service => service.ResetPasswordAsync(It.IsAny<ResetPasswordRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PasswordRecoveryResponse("Changed"));

        var response = await client.SendAsync(TestRequestFactory.CreateJsonRequest(
            HttpMethod.Post,
            "/reset-password",
            new ResetPasswordRequest { Email = "jane@example.com", ResetCode = "123456", NewPassword = "NewP@ssw0rd1" },
            userId: null,
            email: null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task LoginUser_ReturnsUnauthorized_WhenServiceThrowsNotFoundException()
    {
        await using var factory = new WorthBoardsApiFactory();
        var client = factory.CreateAuthorizedClient();

        factory.AuthServiceMock
            .Setup(service => service.LoginUserAsync(It.IsAny<UserLoginRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("User not found"));

        var response = await client.SendAsync(TestRequestFactory.CreateJsonRequest(
            HttpMethod.Post,
            "/login",
            new UserLoginRequest("missing.user", "bad"),
            userId: null,
            email: null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAllBoardToUserLinks_ReturnsOk_WhenViewerOrHigher()
    {
        await using var factory = new WorthBoardsApiFactory();
        var client = factory.CreateAuthorizedClient();

        factory.BoardOnUserServiceMock
            .Setup(service => service.GetAllBoardToUserLinks(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new LinkUserToBoardResponse { BoardId = 5, UserId = 7, AddedAt = DateTime.UtcNow, UserRole = UserRoleEnum.OWNER }
            });

        var response = await client.SendAsync(TestRequestFactory.CreateJsonRequest(HttpMethod.Get, "/api/boards/5/links", userId: 7));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetBoardToUserLink_ReturnsNotFound_WhenServiceThrows()
    {
        await using var factory = new WorthBoardsApiFactory();
        var client = factory.CreateAuthorizedClient();

        factory.BoardOnUserServiceMock
            .Setup(service => service.GetBoardToUserLink(5, 12, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Link not found"));

        var response = await client.SendAsync(TestRequestFactory.CreateJsonRequest(HttpMethod.Get, "/api/boards/5/link/12", userId: 7));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetBoardToUserLink_ReturnsOk_WhenServiceSucceeds()
    {
        await using var factory = new WorthBoardsApiFactory();
        var client = factory.CreateAuthorizedClient();

        factory.BoardOnUserServiceMock
            .Setup(service => service.GetBoardToUserLink(5, 12, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LinkUserToBoardResponse
            {
                BoardId = 5,
                UserId = 12,
                AddedAt = DateTime.UtcNow,
                UserRole = UserRoleEnum.EDITOR
            });

        var response = await client.SendAsync(TestRequestFactory.CreateJsonRequest(HttpMethod.Get, "/api/boards/5/link/12", userId: 7));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task LinkUserToBoard_ReturnsCreated_WhenServiceSucceeds()
    {
        await using var factory = new WorthBoardsApiFactory();
        var client = factory.CreateAuthorizedClient();

        factory.BoardOnUserServiceMock
            .Setup(service => service.LinkUserToBoard(5, 12, It.IsAny<LinkUserToBoardRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LinkUserToBoardResponse { BoardId = 5, UserId = 12, AddedAt = DateTime.UtcNow, UserRole = UserRoleEnum.EDITOR });

        var response = await client.SendAsync(TestRequestFactory.CreateJsonRequest(
            HttpMethod.Post,
            "/api/boards/5/link/12",
            new LinkUserToBoardRequest { UserRole = UserRoleEnum.EDITOR },
            userId: 7));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task UpdateUserOnBoard_ReturnsOk_WhenServiceSucceeds()
    {
        await using var factory = new WorthBoardsApiFactory();
        var client = factory.CreateAuthorizedClient();

        factory.BoardOnUserServiceMock
            .Setup(service => service.UpdateUserOnBoard(5, 12, It.IsAny<LinkUserToBoardRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LinkUserToBoardResponse { BoardId = 5, UserId = 12, AddedAt = DateTime.UtcNow, UserRole = UserRoleEnum.VIEWER });

        var response = await client.SendAsync(TestRequestFactory.CreateJsonRequest(
            HttpMethod.Put,
            "/api/boards/5/link/12",
            new LinkUserToBoardRequest { UserRole = UserRoleEnum.VIEWER },
            userId: 7));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PatchUserOnBoard_ReturnsOk_WhenServiceSucceeds()
    {
        await using var factory = new WorthBoardsApiFactory();
        var client = factory.CreateAuthorizedClient();

        factory.BoardOnUserServiceMock
            .Setup(service => service.PatchUserOnBoard(5, 12, It.IsAny<JsonPatchDocument<LinkUserToBoardRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LinkUserToBoardResponse { BoardId = 5, UserId = 12, AddedAt = DateTime.UtcNow, UserRole = UserRoleEnum.VIEWER });

        var request = new HttpRequestMessage(HttpMethod.Patch, "/api/boards/5/link/12");
        request.Headers.Add("X-Test-UserId", "7");
        request.Headers.Add("X-Test-Email", "tester@example.com");
        request.Content = new StringContent("[{\"op\":\"replace\",\"path\":\"/userRole\",\"value\":2}]", Encoding.UTF8);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json-patch+json");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetUsersLinkedToBoard_ReturnsOk_WhenServiceSucceeds()
    {
        await using var factory = new WorthBoardsApiFactory();
        var client = factory.CreateAuthorizedClient();

        factory.BoardOnUserServiceMock
            .Setup(service => service.GetUsersLinkedToBoardAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new LinkedUserToBoardResponse { Id = 12, UserName = "john", UserRole = UserRoleEnum.VIEWER, AddedAt = DateTime.UtcNow }
            });

        var response = await client.SendAsync(TestRequestFactory.CreateJsonRequest(HttpMethod.Get, "/api/boards/5/users", userId: 7));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetUsersByUserName_ReturnsOk_WhenServiceSucceeds()
    {
        await using var factory = new WorthBoardsApiFactory();
        var client = factory.CreateAuthorizedClient();

        factory.BoardOnUserServiceMock
            .Setup(service => service.GetUsersByUserNameAsync(5, "jo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserResponse>
            {
                new() { Id = 12, FirstName = "John", LastName = "Doe", UserName = "john", Email = "john@example.com", CreationDate = DateTime.UtcNow }
            });

        var response = await client.SendAsync(TestRequestFactory.CreateJsonRequest(HttpMethod.Get, "/api/boards/5/collaborators?userName=jo", userId: 7));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RemoveUser_ReturnsNoContent_WhenServiceSucceeds()
    {
        await using var factory = new WorthBoardsApiFactory();
        var client = factory.CreateAuthorizedClient();

        factory.BoardOnUserServiceMock
            .Setup(service => service.UnlinkUserFromBoard(5, 12, 7, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await client.SendAsync(TestRequestFactory.CreateJsonRequest(HttpMethod.Post, "/api/boards/5/remove/12", userId: 7));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task GetAllBoardTaskComments_ReturnsOk_WhenServiceSucceeds()
    {
        await using var factory = new WorthBoardsApiFactory();
        var client = factory.CreateAuthorizedClient();

        factory.CommentServiceMock
            .Setup(service => service.GetAllBoardTaskCommentsAsync(33, It.IsAny<CancellationToken>(), 0, 10))
            .ReturnsAsync((new List<CommentResponse>
            {
                new() { Id = 44, TaskId = 33, UserId = 7, Content = "c1", CreationDate = DateTime.UtcNow, Edited = false, Version = 1 }
            }, 1));

        var response = await client.SendAsync(TestRequestFactory.CreateJsonRequest(HttpMethod.Get, "/api/boards/5/tasks/33/comments?pageNum=0&pageSize=10", userId: 7));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetAllBoardTaskComments_ReturnsOk_WhenTotalCountIsZero()
    {
        await using var factory = new WorthBoardsApiFactory();
        var client = factory.CreateAuthorizedClient();

        factory.CommentServiceMock
            .Setup(service => service.GetAllBoardTaskCommentsAsync(33, It.IsAny<CancellationToken>(), 0, 10))
            .ReturnsAsync((new List<CommentResponse>(), 0));

        var response = await client.SendAsync(TestRequestFactory.CreateJsonRequest(HttpMethod.Get, "/api/boards/5/tasks/33/comments?pageNum=0&pageSize=10", userId: 7));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetCommentById_ReturnsOk_WhenServiceSucceeds()
    {
        await using var factory = new WorthBoardsApiFactory();
        var client = factory.CreateAuthorizedClient();

        factory.CommentServiceMock
            .Setup(service => service.GetCommentByIdAsync(33, 44, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommentResponse { Id = 44, TaskId = 33, UserId = 7, Content = "c1", CreationDate = DateTime.UtcNow, Edited = false, Version = 1 });

        var response = await client.SendAsync(TestRequestFactory.CreateJsonRequest(HttpMethod.Get, "/api/boards/5/tasks/33/comments/44", userId: 7));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UpdateComment_ReturnsOk_WhenServiceSucceeds()
    {
        await using var factory = new WorthBoardsApiFactory();
        var client = factory.CreateAuthorizedClient();

        factory.CommentServiceMock
            .Setup(service => service.UpdateCommentAsync(44, It.IsAny<CommentUpdateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommentResponse { Id = 44, TaskId = 33, UserId = 7, Content = "updated", CreationDate = DateTime.UtcNow, Edited = true, Version = 2 });

        var response = await client.SendAsync(TestRequestFactory.CreateJsonRequest(
            HttpMethod.Put,
            "/api/boards/5/tasks/33/comments/44",
            new CommentUpdateRequest { Content = "updated", Version = 1 },
            userId: 7));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DeleteComment_ReturnsNoContent_WhenServiceSucceeds()
    {
        await using var factory = new WorthBoardsApiFactory();
        var client = factory.CreateAuthorizedClient();

        factory.CommentServiceMock
            .Setup(service => service.DeleteCommentAsync(44, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await client.SendAsync(TestRequestFactory.CreateJsonRequest(HttpMethod.Delete, "/api/boards/5/tasks/33/comments/44", userId: 7));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task GetAllActiveBoardTasks_ReturnsOk_WhenServiceSucceeds()
    {
        await using var factory = new WorthBoardsApiFactory();
        var client = factory.CreateAuthorizedClient();

        factory.BoardTaskServiceMock
            .Setup(service => service.GetBoardTasks(9, It.IsAny<IEnumerable<TaskStatusEnum>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new BoardTaskResponse { Id = 33, BoardId = 9, Title = "t", Description = "d", CreationDate = DateTime.UtcNow, TaskStatus = TaskStatusEnum.PENDING, Version = 1 }
            });

        var response = await client.SendAsync(TestRequestFactory.CreateJsonRequest(HttpMethod.Get, "/api/boards/9/tasks", userId: 7));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetAllArchivedBoardTasks_ReturnsOk_WhenServiceSucceeds()
    {
        await using var factory = new WorthBoardsApiFactory();
        var client = factory.CreateAuthorizedClient();

        factory.BoardTaskServiceMock
            .Setup(service => service.GetBoardTasks(9, It.IsAny<IEnumerable<TaskStatusEnum>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<BoardTaskResponse>());

        var response = await client.SendAsync(TestRequestFactory.CreateJsonRequest(HttpMethod.Get, "/api/boards/9/tasks/archived", userId: 7));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetBoardTaskById_ReturnsOk_WhenServiceSucceeds()
    {
        await using var factory = new WorthBoardsApiFactory();
        var client = factory.CreateAuthorizedClient();

        factory.BoardTaskServiceMock
            .Setup(service => service.GetBoardTaskById(9, 33, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BoardTaskResponse { Id = 33, BoardId = 9, Title = "t", Description = "d", CreationDate = DateTime.UtcNow, TaskStatus = TaskStatusEnum.PENDING, Version = 1 });

        var response = await client.SendAsync(TestRequestFactory.CreateJsonRequest(HttpMethod.Get, "/api/boards/9/tasks/33", userId: 7));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DeleteBoardTask_ReturnsNoContent_WhenServiceSucceeds()
    {
        await using var factory = new WorthBoardsApiFactory();
        var client = factory.CreateAuthorizedClient();

        factory.BoardTaskServiceMock
            .Setup(service => service.DeleteBoardTask(9, 33, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await client.SendAsync(TestRequestFactory.CreateJsonRequest(HttpMethod.Delete, "/api/boards/9/tasks/33", userId: 7));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteArchivedBoardTasks_ReturnsNoContent_WhenServiceSucceeds()
    {
        await using var factory = new WorthBoardsApiFactory();
        var client = factory.CreateAuthorizedClient();

        factory.BoardTaskServiceMock
            .Setup(service => service.DeleteArchivedBoardTasks(9, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await client.SendAsync(TestRequestFactory.CreateJsonRequest(HttpMethod.Delete, "/api/boards/9/tasks/archived", userId: 7));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task PatchBoardTask_ReturnsOk_WhenServiceSucceeds()
    {
        await using var factory = new WorthBoardsApiFactory();
        var client = factory.CreateAuthorizedClient();

        factory.BoardTaskServiceMock
            .Setup(service => service.PatchBoardTask(9, 33, It.IsAny<JsonPatchDocument<BoardTaskUpdateRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BoardTaskResponse { Id = 33, BoardId = 9, Title = "patched", Description = "d", CreationDate = DateTime.UtcNow, TaskStatus = TaskStatusEnum.PENDING, Version = 2 });

        var request = new HttpRequestMessage(HttpMethod.Patch, "/api/boards/9/tasks/33");
        request.Headers.Add("X-Test-UserId", "7");
        request.Headers.Add("X-Test-Email", "tester@example.com");
        request.Content = new StringContent("[{\"op\":\"replace\",\"path\":\"/title\",\"value\":\"patched\"}]", Encoding.UTF8);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json-patch+json");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UnlinkUsersFromTask_ReturnsOk_WhenServiceSucceeds()
    {
        await using var factory = new WorthBoardsApiFactory();
        var client = factory.CreateAuthorizedClient();

        factory.TaskOnUserServiceMock
            .Setup(service => service.UnlinkUsersFromTaskAsync(9, 33, It.IsAny<IEnumerable<LinkUserToTaskRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new LinkUserToTaskResponse { UserId = 12, AssignedAt = DateTime.UtcNow } });

        var response = await client.SendAsync(TestRequestFactory.CreateJsonRequest(
            HttpMethod.Delete,
            "/api/boards/9/tasks/33/users/unlink",
            new[] { new LinkUserToTaskRequest { UserId = 12 } },
            userId: 7));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetUsersLinkedToTask_ReturnsOk_WhenServiceSucceeds()
    {
        await using var factory = new WorthBoardsApiFactory();
        var client = factory.CreateAuthorizedClient();

        factory.TaskOnUserServiceMock
            .Setup(service => service.GetUsersLinkedToTaskAsync(9, 33, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new LinkedUserToTaskResponse { Id = 12, UserName = "john", AssignedAt = DateTime.UtcNow } });

        var response = await client.SendAsync(TestRequestFactory.CreateJsonRequest(HttpMethod.Get, "/api/boards/9/tasks/33/users", userId: 7));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DeleteNotification_ReturnsOk_WhenServiceSucceeds()
    {
        await using var factory = new WorthBoardsApiFactory();
        var client = factory.CreateAuthorizedClient();

        factory.NotificationServiceMock
            .Setup(service => service.UnlinkNotification(7, 55, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await client.SendAsync(TestRequestFactory.CreateJsonRequest(HttpMethod.Delete, "/api/notifications/55", userId: 7));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RegisterUser_ReturnsBadRequest_WhenInvalidJsonBodyTriggersModelBindingFailure()
    {
        await using var factory = new WorthBoardsApiFactory();
        var client = factory.CreateAuthorizedClient();

        var invalidJson = "{\"firstName\":\"Jane\",\"email\":";
        var response = await client.SendAsync(TestRequestFactory.CreateRawJsonRequest(HttpMethod.Post, "/register?email=jane@example.com", invalidJson, userId: null, email: null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
