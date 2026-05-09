using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Http;
using Moq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using WorthBoards.Business.Dtos.Identity;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Common.Enums;
using WorthBoards.Common.Exceptions.Custom;
using WorthBoards.IntegrationTests.TestHost;
using Xunit;

namespace WorthBoards.IntegrationTests;

public sealed class ApiCoverageScenariosTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ApiCoverageScenariosTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AuthScenario_Happy_Register_ReturnsOk()
    {
        _factory.Mocks.ResetAll();
        var client = _factory.CreateClient();

        _factory.Mocks.AuthService
            .Setup(x => x.RegisterUserAsync(It.IsAny<UserRegisterRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserResponse
            {
                Id = 1,
                FirstName = "Test",
                LastName = "User",
                UserName = "testuser",
                Email = "test@worthboards.test",
                CreationDate = DateTime.UtcNow
            });

        var response = await client.PostAsJsonAsync("/register", new UserRegisterRequest("Test", "User", "testuser", "test@worthboards.test", "Passw0rd!"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AuthScenario_Negative_LoginUnauthorized_ReturnsUnauthorized()
    {
        _factory.Mocks.ResetAll();
        var client = _factory.CreateClient();

        _factory.Mocks.AuthService
            .Setup(x => x.LoginUserAsync(It.IsAny<UserLoginRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedException("Invalid credentials"));

        var response = await client.PostAsJsonAsync("/login", new UserLoginRequest("testuser", "wrong-pass"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task BoardScenario_Happy_GetBoards_ReturnsOk()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient();

        _factory.Mocks.BoardService
            .Setup(x => x.GetUserBoardsAsync(1, 0, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<BoardResponse> { BuildBoardResponse(1) }, 1));

        var response = await client.GetAsync("/api/boards");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task BoardScenario_Negative_GetBoardsWithoutAuth_ReturnsUnauthorized()
    {
        _factory.Mocks.ResetAll();
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/boards");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task BoardAuthorizationScenario_Happy_DeleteBoardAsOwner_ReturnsNoContent()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "1");

        _factory.Mocks.BoardService
            .Setup(x => x.GetUserRoleByBoardIdAndUserIdAsync(1, 1))
            .ReturnsAsync(UserRoleEnum.OWNER);

        _factory.Mocks.BoardService
            .Setup(x => x.DeleteBoardAsync(1, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await client.DeleteAsync("/api/boards/1");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task BoardAuthorizationScenario_Negative_DeleteBoardAsViewer_ReturnsForbidden()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "1");

        _factory.Mocks.BoardService
            .Setup(x => x.GetUserRoleByBoardIdAndUserIdAsync(1, 1))
            .ReturnsAsync(UserRoleEnum.VIEWER);

        var response = await client.DeleteAsync("/api/boards/1");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task BoardTaskScenario_Happy_CreateTaskAsEditor_ReturnsCreated()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "1");

        _factory.Mocks.BoardService
            .Setup(x => x.GetUserRoleByBoardIdAndUserIdAsync(1, 1))
            .ReturnsAsync(UserRoleEnum.EDITOR);

        _factory.Mocks.BoardTaskService
            .Setup(x => x.CreateBoardTask(1, It.IsAny<BoardTaskRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildBoardTaskResponse(5, 1, TaskStatusEnum.PENDING));

        _factory.Mocks.NotificationService
            .Setup(x => x.NotifyTaskCreated(1, 5, 1, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var request = new BoardTaskRequest
        {
            Title = "New task",
            Description = "Task description",
            DeadlineEnd = DateTime.UtcNow.AddDays(1),
            TaskStatus = TaskStatusEnum.PENDING
        };

        var response = await client.PostAsJsonAsync("/api/boards/1/tasks", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task BoardTaskScenario_Negative_CreateTaskAsViewer_ReturnsForbidden()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "1");

        _factory.Mocks.BoardService
            .Setup(x => x.GetUserRoleByBoardIdAndUserIdAsync(1, 1))
            .ReturnsAsync(UserRoleEnum.VIEWER);

        var request = new BoardTaskRequest
        {
            Title = "New task",
            TaskStatus = TaskStatusEnum.PENDING
        };

        var response = await client.PostAsJsonAsync("/api/boards/1/tasks", request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task BoardOnUserScenario_Happy_GetLinksAsViewer_ReturnsOk()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "1");

        _factory.Mocks.BoardService
            .Setup(x => x.GetUserRoleByBoardIdAndUserIdAsync(1, 1))
            .ReturnsAsync(UserRoleEnum.VIEWER);

        _factory.Mocks.BoardOnUserService
            .Setup(x => x.GetAllBoardToUserLinks(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LinkUserToBoardResponse>
            {
                new() { BoardId = 1, UserId = 1, AddedAt = DateTime.UtcNow, UserRole = UserRoleEnum.OWNER }
            });

        var response = await client.GetAsync("/api/boards/1/links");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task BoardOnUserScenario_Negative_GetLinksForbidden_ReturnsForbidden()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "1");

        _factory.Mocks.BoardService
            .Setup(x => x.GetUserRoleByBoardIdAndUserIdAsync(1, 1))
            .ReturnsAsync((UserRoleEnum?)null);

        var response = await client.GetAsync("/api/boards/1/links");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CommentScenario_Happy_CreateComment_ReturnsCreated()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "1");

        _factory.Mocks.CommentService
            .Setup(x => x.CreateCommentAsync(1, 9, It.IsAny<CommentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommentResponse
            {
                Id = 3,
                TaskId = 9,
                UserId = 1,
                Content = "hello",
                CreationDate = DateTime.UtcNow,
                Edited = false,
                Version = 1
            });

        var response = await client.PostAsJsonAsync("/api/boards/1/tasks/9/comments", new CommentRequest { Content = "hello" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CommentScenario_Negative_InvalidClaimUserId_ReturnsUnauthorized()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "not-an-int");

        var response = await client.PostAsJsonAsync("/api/boards/1/tasks/9/comments", new CommentRequest { Content = "hello" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task NotificationScenario_Happy_GetNotifications_ReturnsOk()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "1");

        _factory.Mocks.NotificationService
            .Setup(x => x.GetNotificationsByUserId(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<NotificationResponse>
            {
                new() { Id = 1, Title = "Invite", Description = "Board invite", SendDate = DateTime.UtcNow }
            });

        var response = await client.GetAsync("/api/notifications");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task NotificationScenario_Negative_AcceptInvitationWithInvalidClaim_ReturnsUnauthorized()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "invalid");

        var response = await client.PostAsync("/api/notifications/1/accept", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TaskOnUserScenario_Happy_LinkUsersAsEditor_ReturnsOkAndNotifiesOthers()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "1");

        _factory.Mocks.BoardService
            .Setup(x => x.GetUserRoleByBoardIdAndUserIdAsync(1, 1))
            .ReturnsAsync(UserRoleEnum.EDITOR);

        _factory.Mocks.TaskOnUserService
            .Setup(x => x.LinkUsersToTaskAsync(1, 2, It.IsAny<IEnumerable<LinkUserToTaskRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LinkUserToTaskResponse>
            {
                new() { UserId = 1, AssignedAt = DateTime.UtcNow },
                new() { UserId = 7, AssignedAt = DateTime.UtcNow }
            });

        _factory.Mocks.NotificationService
            .Setup(x => x.NotifyTaskAssigned(1, 2, 7, 1, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var request = new List<LinkUserToTaskRequest>
        {
            new() { UserId = 1 },
            new() { UserId = 7 }
        };

        var response = await client.PostAsJsonAsync("/api/boards/1/tasks/2/users/link", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        _factory.Mocks.NotificationService.Verify(x => x.NotifyTaskAssigned(1, 2, 7, 1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TaskOnUserScenario_Negative_LinkUsersAsViewer_ReturnsForbidden()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "1");

        _factory.Mocks.BoardService
            .Setup(x => x.GetUserRoleByBoardIdAndUserIdAsync(1, 1))
            .ReturnsAsync(UserRoleEnum.VIEWER);

        var response = await client.PostAsJsonAsync("/api/boards/1/tasks/2/users/link", new List<LinkUserToTaskRequest> { new() { UserId = 2 } });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UploadScenario_Happy_UploadImage_ReturnsOk()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "1");

        _factory.Mocks.FileService
            .Setup(x => x.UploadImage(It.IsAny<IFormFile>()))
            .ReturnsAsync("mocked-file.png");

        using var content = new MultipartFormDataContent();
        var imageContent = new ByteArrayContent(new byte[] { 1, 2, 3, 4 });
        imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(imageContent, "image", "image.png");

        var response = await client.PostAsync("/api/upload/image", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UploadScenario_Negative_UploadImageWithoutAuth_ReturnsUnauthorized()
    {
        _factory.Mocks.ResetAll();
        var client = _factory.CreateClient();

        using var content = new MultipartFormDataContent();
        var imageContent = new ByteArrayContent(new byte[] { 1, 2, 3, 4 });
        imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(imageContent, "image", "image.png");

        var response = await client.PostAsync("/api/upload/image", content);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UserScenario_Happy_GetUser_ReturnsOk()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "1");

        _factory.Mocks.UserService
            .Setup(x => x.GetUserById(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserResponse
            {
                Id = 1,
                FirstName = "Test",
                LastName = "User",
                UserName = "testuser",
                Email = "test@worthboards.test",
                CreationDate = DateTime.UtcNow
            });

        var response = await client.GetAsync("/api/users/1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UserScenario_Negative_GetUserWithoutAuth_ReturnsUnauthorized()
    {
        _factory.Mocks.ResetAll();
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/users/1");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ExceptionScenario_Happy_GetBoardById_ReturnsOk()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "1");

        _factory.Mocks.BoardService
            .Setup(x => x.GetBoardByIdAsync(11, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildBoardResponse(11));

        var response = await client.GetAsync("/api/boards/11");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ExceptionScenario_Negative_NotFoundExceptionMappedTo404()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "1");

        _factory.Mocks.BoardService
            .Setup(x => x.GetBoardByIdAsync(404, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Board not found"));

        var response = await client.GetAsync("/api/boards/404");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PasswordRecoveryScenario_Happy_ForgotPassword_ReturnsOk()
    {
        _factory.Mocks.ResetAll();
        var client = _factory.CreateClient();

        _factory.Mocks.AuthService
            .Setup(x => x.ForgotPasswordAsync(It.IsAny<Microsoft.AspNetCore.Identity.Data.ForgotPasswordRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PasswordRecoveryResponse("Email sent"));

        var response = await client.PostAsJsonAsync("/forgot-password", new { email = "test@worthboards.test" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PasswordRecoveryScenario_Negative_ResetPasswordBadRequest_ReturnsBadRequest()
    {
        _factory.Mocks.ResetAll();
        var client = _factory.CreateClient();

        _factory.Mocks.AuthService
            .Setup(x => x.ResetPasswordAsync(It.IsAny<Microsoft.AspNetCore.Identity.Data.ResetPasswordRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BadRequestException("Invalid reset token"));

        var response = await client.PostAsJsonAsync("/reset-password", new
        {
            email = "test@worthboards.test",
            resetCode = "invalid",
            newPassword = "Passw0rd!"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task BoardModificationScenario_Happy_CreateBoard_ReturnsCreated()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "1");

        _factory.Mocks.BoardService
            .Setup(x => x.CreateBoardAsync(1, It.IsAny<BoardRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildBoardResponse(21));

        var request = new BoardRequest { Title = "Created board", Description = "desc", ImageName = "board.png" };

        var response = await client.PostAsJsonAsync("/api/boards?userId=1", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task BoardModificationScenario_Negative_PatchBoardAsViewer_ReturnsForbidden()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "1");

        _factory.Mocks.BoardService
            .Setup(x => x.GetUserRoleByBoardIdAndUserIdAsync(1, 1))
            .ReturnsAsync(UserRoleEnum.VIEWER);

        using var patchContent = new StringContent("[{\"op\":\"replace\",\"path\":\"/title\",\"value\":\"updated\"}]", Encoding.UTF8, "application/json-patch+json");
        var response = await client.PatchAsync("/api/boards/1", patchContent);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task BoardTaskManagementScenario_Happy_GetArchivedTasks_ReturnsOk()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "1");

        _factory.Mocks.BoardService
            .Setup(x => x.GetUserRoleByBoardIdAndUserIdAsync(1, 1))
            .ReturnsAsync(UserRoleEnum.VIEWER);

        _factory.Mocks.BoardTaskService
            .Setup(x => x.GetBoardTasks(1, It.IsAny<IEnumerable<TaskStatusEnum>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BoardTaskResponse> { BuildBoardTaskResponse(3, 1, TaskStatusEnum.ARCHIVED) });

        var response = await client.GetAsync("/api/boards/1/tasks/archived");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task BoardTaskManagementScenario_Negative_DeleteTaskAsViewer_ReturnsForbidden()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "1");

        _factory.Mocks.BoardService
            .Setup(x => x.GetUserRoleByBoardIdAndUserIdAsync(1, 1))
            .ReturnsAsync(UserRoleEnum.VIEWER);

        var response = await client.DeleteAsync("/api/boards/1/tasks/5");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task BoardTaskUpdateScenario_Happy_UpdateWithStatusChange_ReturnsOk()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "1");

        _factory.Mocks.BoardService
            .Setup(x => x.GetUserRoleByBoardIdAndUserIdAsync(1, 1))
            .ReturnsAsync(UserRoleEnum.EDITOR);

        _factory.Mocks.BoardTaskService
            .Setup(x => x.GetBoardTaskById(1, 9, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildBoardTaskResponse(9, 1, TaskStatusEnum.PENDING));

        _factory.Mocks.BoardTaskService
            .Setup(x => x.UpdateBoardTask(1, 9, It.IsAny<BoardTaskUpdateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildBoardTaskResponse(9, 1, TaskStatusEnum.IN_PROGRESS));

        _factory.Mocks.NotificationService
            .Setup(x => x.NotifyTaskStatusChange(1, 9, 1, TaskStatusEnum.PENDING, TaskStatusEnum.IN_PROGRESS, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var request = new BoardTaskUpdateRequest
        {
            Title = "Task",
            Description = "Task",
            DeadlineEnd = DateTime.UtcNow.AddDays(2),
            TaskStatus = TaskStatusEnum.IN_PROGRESS,
            Version = 1
        };

        var response = await client.PutAsJsonAsync("/api/boards/1/tasks/9", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task BoardTaskUpdateScenario_Negative_UpdateAsViewer_ReturnsForbidden()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "1");

        _factory.Mocks.BoardService
            .Setup(x => x.GetUserRoleByBoardIdAndUserIdAsync(1, 1))
            .ReturnsAsync(UserRoleEnum.VIEWER);

        var request = new BoardTaskUpdateRequest
        {
            Title = "Task",
            TaskStatus = TaskStatusEnum.PENDING,
            Version = 1
        };

        var response = await client.PutAsJsonAsync("/api/boards/1/tasks/9", request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task BoardCollaborationScenario_Happy_LinkUserToBoard_ReturnsCreated()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "1");

        _factory.Mocks.BoardOnUserService
            .Setup(x => x.LinkUserToBoard(1, 7, It.IsAny<LinkUserToBoardRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LinkUserToBoardResponse
            {
                BoardId = 1,
                UserId = 7,
                AddedAt = DateTime.UtcNow,
                UserRole = UserRoleEnum.VIEWER
            });

        var response = await client.PostAsJsonAsync("/api/boards/1/link/7", new LinkUserToBoardRequest { UserRole = UserRoleEnum.VIEWER });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task BoardCollaborationScenario_Negative_GetCollaboratorsAsViewer_ReturnsForbidden()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "1");

        _factory.Mocks.BoardService
            .Setup(x => x.GetUserRoleByBoardIdAndUserIdAsync(1, 1))
            .ReturnsAsync(UserRoleEnum.VIEWER);

        var response = await client.GetAsync("/api/boards/1/collaborators?userName=a");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CommentReadUpdateScenario_Happy_GetCommentById_ReturnsOk()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "1");

        _factory.Mocks.CommentService
            .Setup(x => x.GetCommentByIdAsync(9, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommentResponse
            {
                Id = 3,
                TaskId = 9,
                UserId = 1,
                Content = "text",
                CreationDate = DateTime.UtcNow,
                Edited = false,
                Version = 1
            });

        var response = await client.GetAsync("/api/boards/1/tasks/9/comments/3");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CommentReadUpdateScenario_Negative_PatchCommentNotFound_ReturnsNotFound()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "1");

        _factory.Mocks.CommentService
            .Setup(x => x.PatchCommentAsync(3, It.IsAny<Microsoft.AspNetCore.JsonPatch.JsonPatchDocument<CommentUpdateRequest>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Comment not found"));

        using var patchContent = new StringContent("[{\"op\":\"replace\",\"path\":\"/content\",\"value\":\"updated\"}]", Encoding.UTF8, "application/json-patch+json");
        var response = await client.PatchAsync("/api/boards/1/tasks/9/comments/3", patchContent);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task NotificationCleanupScenario_Happy_DeleteNotification_ReturnsOk()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "1");

        _factory.Mocks.NotificationService
            .Setup(x => x.UnlinkNotification(1, 9, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await client.DeleteAsync("/api/notifications/9");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task NotificationCleanupScenario_Negative_DeleteAllWithoutAuth_ReturnsUnauthorized()
    {
        _factory.Mocks.ResetAll();
        var client = _factory.CreateClient();

        var response = await client.DeleteAsync("/api/notifications");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TaskAssignmentScenario_Happy_UnlinkUsers_ReturnsOk()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "1");

        _factory.Mocks.BoardService
            .Setup(x => x.GetUserRoleByBoardIdAndUserIdAsync(1, 1))
            .ReturnsAsync(UserRoleEnum.EDITOR);

        _factory.Mocks.TaskOnUserService
            .Setup(x => x.UnlinkUsersFromTaskAsync(1, 2, It.IsAny<IEnumerable<LinkUserToTaskRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LinkUserToTaskResponse> { new() { UserId = 7, AssignedAt = DateTime.UtcNow } });

        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/boards/1/tasks/2/users/unlink")
        {
            Content = JsonContent.Create(new List<LinkUserToTaskRequest> { new() { UserId = 7 } })
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TaskAssignmentScenario_Negative_GetLinkedUsersForbidden_ReturnsForbidden()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "1");

        _factory.Mocks.BoardService
            .Setup(x => x.GetUserRoleByBoardIdAndUserIdAsync(1, 1))
            .ReturnsAsync((UserRoleEnum?)null);

        var response = await client.GetAsync("/api/boards/1/tasks/2/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UserUpdateScenario_Happy_UpdateUser_ReturnsOk()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "1");

        _factory.Mocks.UserService
            .Setup(x => x.UpdateUser(1, It.IsAny<UserUpdateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserUpdateResponse
            {
                FirstName = "Updated",
                LastName = "User",
                UserName = "updated-user",
                Email = "updated@worthboards.test",
                ImageURL = "user.png"
            });

        var request = new UserUpdateRequest
        {
            FirstName = "Updated",
            LastName = "User",
            UserName = "updated-user",
            Email = "updated@worthboards.test",
            ImageName = "user.png"
        };

        var response = await client.PutAsJsonAsync("/api/users/1", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UserUpdateScenario_Negative_UpdateUserWithoutAuth_ReturnsUnauthorized()
    {
        _factory.Mocks.ResetAll();
        var client = _factory.CreateClient();

        var response = await client.PutAsJsonAsync("/api/users/1", new UserUpdateRequest
        {
            FirstName = "Updated",
            LastName = "User",
            UserName = "updated-user",
            Email = "updated@worthboards.test",
            ImageName = "user.png"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ChangePasswordScenario_Happy_ChangePassword_ReturnsOk()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "1", email: "owner@worthboards.test");

        _factory.Mocks.AuthService
            .Setup(x => x.ChangePasswordAsync(It.IsAny<ChangePasswordRequest>(), "owner@worthboards.test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChangePasswordResponse("Password changed"));

        var response = await client.PostAsJsonAsync("/change-password", new ChangePasswordRequest { OldPassword = "OldPass1!", NewPassword = "NewPass1!" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ChangePasswordScenario_Negative_ChangePasswordWithoutAuth_ReturnsUnauthorized()
    {
        _factory.Mocks.ResetAll();
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/change-password", new ChangePasswordRequest { OldPassword = "OldPass1!", NewPassword = "NewPass1!" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task BoardAdvancedScenario_Happy_UpdateAndPatchBoard_ReturnsOkForBoth()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "1");

        _factory.Mocks.BoardService
            .Setup(x => x.GetUserRoleByBoardIdAndUserIdAsync(1, 1))
            .ReturnsAsync(UserRoleEnum.EDITOR);

        _factory.Mocks.BoardService
            .Setup(x => x.UpdateBoardAsync(1, It.IsAny<BoardUpdateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildBoardResponse(1));

        _factory.Mocks.BoardService
            .Setup(x => x.PatchBoardAsync(1, It.IsAny<Microsoft.AspNetCore.JsonPatch.JsonPatchDocument<BoardUpdateRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildBoardResponse(1));

        var putResponse = await client.PutAsJsonAsync("/api/boards/1", new BoardUpdateRequest
        {
            Title = "Updated",
            Description = "Updated",
            ImageName = "updated.png",
            Version = 1
        });

        using var patchContent = new StringContent("[{\"op\":\"replace\",\"path\":\"/title\",\"value\":\"patched\"}]", Encoding.UTF8, "application/json-patch+json");
        var patchResponse = await client.PatchAsync("/api/boards/1", patchContent);

        Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, patchResponse.StatusCode);
    }

    [Fact]
    public async Task BoardAdvancedScenario_Negative_UpdateBoardAsViewer_ReturnsForbidden()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "1");

        _factory.Mocks.BoardService
            .Setup(x => x.GetUserRoleByBoardIdAndUserIdAsync(1, 1))
            .ReturnsAsync(UserRoleEnum.VIEWER);

        var response = await client.PutAsJsonAsync("/api/boards/1", new BoardUpdateRequest
        {
            Title = "Updated",
            Description = "Updated",
            ImageName = "updated.png",
            Version = 1
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task BoardOnUserAdvancedScenario_Happy_ExerciseBoardUserEndpoints_ReturnsSuccess()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "1");

        _factory.Mocks.BoardService
            .Setup(x => x.GetUserRoleByBoardIdAndUserIdAsync(1, 1))
            .ReturnsAsync(UserRoleEnum.OWNER);

        _factory.Mocks.NotificationService
            .Setup(x => x.NotifyBoardInvitation(1, 7, 1, UserRoleEnum.VIEWER, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _factory.Mocks.BoardOnUserService
            .Setup(x => x.UnlinkUserFromBoard(1, 7, 1, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _factory.Mocks.BoardOnUserService
            .Setup(x => x.GetBoardToUserLink(1, 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LinkUserToBoardResponse { BoardId = 1, UserId = 7, AddedAt = DateTime.UtcNow, UserRole = UserRoleEnum.VIEWER });

        _factory.Mocks.BoardOnUserService
            .Setup(x => x.GetUsersLinkedToBoardAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LinkedUserToBoardResponse> { new() { Id = 7, UserName = "u7", UserRole = UserRoleEnum.VIEWER, AddedAt = DateTime.UtcNow } });

        _factory.Mocks.BoardOnUserService
            .Setup(x => x.GetUsersByUserNameAsync(1, "u", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserResponse> { new() { Id = 7, FirstName = "A", LastName = "B", UserName = "u7", Email = "u7@w.test", CreationDate = DateTime.UtcNow } });

        _factory.Mocks.BoardOnUserService
            .Setup(x => x.UpdateUserOnBoard(1, 7, It.IsAny<LinkUserToBoardRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LinkUserToBoardResponse { BoardId = 1, UserId = 7, AddedAt = DateTime.UtcNow, UserRole = UserRoleEnum.EDITOR });

        _factory.Mocks.BoardOnUserService
            .Setup(x => x.PatchUserOnBoard(1, 7, It.IsAny<Microsoft.AspNetCore.JsonPatch.JsonPatchDocument<LinkUserToBoardRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LinkUserToBoardResponse { BoardId = 1, UserId = 7, AddedAt = DateTime.UtcNow, UserRole = UserRoleEnum.EDITOR });

        _factory.Mocks.BoardOnUserService
            .Setup(x => x.TransferOwnershipAsync(1, 1, 7, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var inviteResponse = await client.PostAsJsonAsync("/api/boards/1/invite", new InvitationRequest { UserId = 7, Role = UserRoleEnum.VIEWER });
        var removeResponse = await client.PostAsync("/api/boards/1/remove/7", content: null);
        var getLinkResponse = await client.GetAsync("/api/boards/1/link/7");
        var getUsersResponse = await client.GetAsync("/api/boards/1/users");
        var collaboratorsResponse = await client.GetAsync("/api/boards/1/collaborators?userName=u");
        var putLinkResponse = await client.PutAsJsonAsync("/api/boards/1/link/7", new LinkUserToBoardRequest { UserRole = UserRoleEnum.EDITOR });
        using var patchLink = new StringContent("[{\"op\":\"replace\",\"path\":\"/userRole\",\"value\":1}]", Encoding.UTF8, "application/json-patch+json");
        var patchLinkResponse = await client.PatchAsync("/api/boards/1/link/7", patchLink);
        var transferResponse = await client.PostAsync("/api/boards/1/collaborators/7", content: null);

        Assert.Equal(HttpStatusCode.NoContent, inviteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, removeResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, getLinkResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, getUsersResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, collaboratorsResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, putLinkResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, patchLinkResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, transferResponse.StatusCode);
    }

    [Fact]
    public async Task BoardOnUserAdvancedScenario_Negative_OwnerEndpointsAsViewer_ReturnsForbidden()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "1");

        _factory.Mocks.BoardService
            .Setup(x => x.GetUserRoleByBoardIdAndUserIdAsync(1, 1))
            .ReturnsAsync(UserRoleEnum.VIEWER);

        var response = await client.PostAsJsonAsync("/api/boards/1/invite", new InvitationRequest { UserId = 7, Role = UserRoleEnum.VIEWER });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task BoardTaskAdvancedScenario_Happy_ExerciseBoardTaskEndpoints_ReturnsSuccess()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "1");

        _factory.Mocks.BoardService
            .Setup(x => x.GetUserRoleByBoardIdAndUserIdAsync(1, 1))
            .ReturnsAsync(UserRoleEnum.EDITOR);

        _factory.Mocks.BoardTaskService
            .Setup(x => x.GetBoardTasks(1, It.IsAny<IEnumerable<TaskStatusEnum>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BoardTaskResponse> { BuildBoardTaskResponse(2, 1, TaskStatusEnum.PENDING) });

        _factory.Mocks.BoardTaskService
            .Setup(x => x.GetBoardTaskById(1, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildBoardTaskResponse(2, 1, TaskStatusEnum.PENDING));

        _factory.Mocks.BoardTaskService
            .Setup(x => x.DeleteBoardTask(1, 2, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _factory.Mocks.BoardTaskService
            .Setup(x => x.DeleteArchivedBoardTasks(1, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _factory.Mocks.BoardTaskService
            .Setup(x => x.PatchBoardTask(1, 2, It.IsAny<Microsoft.AspNetCore.JsonPatch.JsonPatchDocument<BoardTaskUpdateRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildBoardTaskResponse(2, 1, TaskStatusEnum.IN_PROGRESS));

        var activeResponse = await client.GetAsync("/api/boards/1/tasks");
        var byIdResponse = await client.GetAsync("/api/boards/1/tasks/2");
        var deleteResponse = await client.DeleteAsync("/api/boards/1/tasks/2");
        var deleteArchivedResponse = await client.DeleteAsync("/api/boards/1/tasks/archived");
        using var patchContent = new StringContent("[{\"op\":\"replace\",\"path\":\"/taskStatus\",\"value\":1}]", Encoding.UTF8, "application/json-patch+json");
        var patchResponse = await client.PatchAsync("/api/boards/1/tasks/2", patchContent);

        Assert.Equal(HttpStatusCode.OK, activeResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, byIdResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, deleteArchivedResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, patchResponse.StatusCode);
    }

    [Fact]
    public async Task BoardTaskAdvancedScenario_Negative_EditorEndpointsAsViewer_ReturnsForbidden()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "1");

        _factory.Mocks.BoardService
            .Setup(x => x.GetUserRoleByBoardIdAndUserIdAsync(1, 1))
            .ReturnsAsync(UserRoleEnum.VIEWER);

        var response = await client.DeleteAsync("/api/boards/1/tasks/2");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CommentAdvancedScenario_Happy_GetAllDeleteUpdatePatch_ReturnsSuccess()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "1");

        _factory.Mocks.CommentService
            .Setup(x => x.GetAllBoardTaskCommentsAsync(9, It.IsAny<CancellationToken>(), 0, 10))
            .ReturnsAsync((new List<CommentResponse> { new() { Id = 1, TaskId = 9, UserId = 1, Content = "c", CreationDate = DateTime.UtcNow, Edited = false, Version = 1 } }, 1));

        _factory.Mocks.CommentService
            .Setup(x => x.DeleteCommentAsync(1, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _factory.Mocks.CommentService
            .Setup(x => x.UpdateCommentAsync(1, It.IsAny<CommentUpdateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommentResponse { Id = 1, TaskId = 9, UserId = 1, Content = "updated", CreationDate = DateTime.UtcNow, Edited = true, Version = 2 });

        _factory.Mocks.CommentService
            .Setup(x => x.PatchCommentAsync(1, It.IsAny<Microsoft.AspNetCore.JsonPatch.JsonPatchDocument<CommentUpdateRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommentResponse { Id = 1, TaskId = 9, UserId = 1, Content = "patched", CreationDate = DateTime.UtcNow, Edited = true, Version = 2 });

        var getAllResponse = await client.GetAsync("/api/boards/1/tasks/9/comments");
        var deleteResponse = await client.DeleteAsync("/api/boards/1/tasks/9/comments/1");
        var putResponse = await client.PutAsJsonAsync("/api/boards/1/tasks/9/comments/1", new CommentUpdateRequest { Content = "updated", Version = 1 });
        using var patchContent = new StringContent("[{\"op\":\"replace\",\"path\":\"/content\",\"value\":\"patched\"}]", Encoding.UTF8, "application/json-patch+json");
        var patchResponse = await client.PatchAsync("/api/boards/1/tasks/9/comments/1", patchContent);

        Assert.Equal(HttpStatusCode.OK, getAllResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, patchResponse.StatusCode);
    }

    [Fact]
    public async Task CommentAdvancedScenario_Negative_UpdateThrowsGenericException_ReturnsInternalServerError()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "1");

        _factory.Mocks.CommentService
            .Setup(x => x.UpdateCommentAsync(1, It.IsAny<CommentUpdateRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("unexpected"));

        var response = await client.PutAsJsonAsync("/api/boards/1/tasks/9/comments/1", new CommentUpdateRequest { Content = "updated", Version = 1 });

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task NotificationAndTaskQueryScenario_Happy_DeleteAllAndGetTaskUsers_ReturnsOk()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "1");

        _factory.Mocks.NotificationService
            .Setup(x => x.UnlinkAllNotifications(1, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _factory.Mocks.BoardService
            .Setup(x => x.GetUserRoleByBoardIdAndUserIdAsync(1, 1))
            .ReturnsAsync(UserRoleEnum.VIEWER);

        _factory.Mocks.TaskOnUserService
            .Setup(x => x.GetUsersLinkedToTaskAsync(1, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LinkedUserToTaskResponse> { new() { Id = 7, UserName = "u7", AssignedAt = DateTime.UtcNow } });

        var deleteAllResponse = await client.DeleteAsync("/api/notifications");
        var getUsersResponse = await client.GetAsync("/api/boards/1/tasks/2/users");

        Assert.Equal(HttpStatusCode.OK, deleteAllResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, getUsersResponse.StatusCode);
    }

    [Fact]
    public async Task NotificationAndTaskQueryScenario_Negative_GetTaskUsersForbidden_ReturnsForbidden()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "1");

        _factory.Mocks.BoardService
            .Setup(x => x.GetUserRoleByBoardIdAndUserIdAsync(1, 1))
            .ReturnsAsync((UserRoleEnum?)null);

        var response = await client.GetAsync("/api/boards/1/tasks/2/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AuthLoginBranchScenario_Happy_LoginReturnsOk()
    {
        _factory.Mocks.ResetAll();
        var client = _factory.CreateClient();

        _factory.Mocks.AuthService
            .Setup(x => x.LoginUserAsync(It.IsAny<UserLoginRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserLoginResponse(1, "testuser", "token"));

        var response = await client.PostAsJsonAsync("/login", new UserLoginRequest("testuser", "Passw0rd!"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AuthLoginBranchScenario_Negative_NotFoundAndUnhandledErrors_ReturnStatuses()
    {
        _factory.Mocks.ResetAll();
        var client = _factory.CreateClient();

        _factory.Mocks.AuthService
            .SetupSequence(x => x.LoginUserAsync(It.IsAny<UserLoginRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("missing"))
            .ThrowsAsync(new Exception("boom"));

        var notFoundResponse = await client.PostAsJsonAsync("/login", new UserLoginRequest("missing", "Passw0rd!"));
        var unhandledResponse = await client.PostAsJsonAsync("/login", new UserLoginRequest("error", "Passw0rd!"));

        Assert.Equal(HttpStatusCode.Unauthorized, notFoundResponse.StatusCode);
        Assert.Equal(HttpStatusCode.InternalServerError, unhandledResponse.StatusCode);
    }

    private HttpClient CreateAuthenticatedClient(string userId = "1", string email = "owner@worthboards.test")
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        client.DefaultRequestHeaders.Add("X-Test-Auth", "true");
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId);
        client.DefaultRequestHeaders.Add("X-Test-Email", email);

        return client;
    }

    private static BoardResponse BuildBoardResponse(int id)
    {
        return new BoardResponse
        {
            Id = id,
            Title = "Board",
            Description = "Board description",
            ImageURL = "board.png",
            CreationDate = DateTime.UtcNow,
            Version = 1
        };
    }

    private static BoardTaskResponse BuildBoardTaskResponse(int id, int boardId, TaskStatusEnum status)
    {
        return new BoardTaskResponse
        {
            Id = id,
            BoardId = boardId,
            Title = "Task",
            Description = "Task description",
            DeadlineEnd = DateTime.UtcNow.AddDays(1),
            CreationDate = DateTime.UtcNow,
            TaskStatus = status,
            Version = 1
        };
    }

    // ==================== ADDITIONAL COVERAGE TESTS ====================
    
    #region Board Pagination and Filtering
    
    [Fact]
    public async Task BoardPaginationScenario_Happy_GetBoardsWithPagination_ReturnsPagedResult()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "2");

        _factory.Mocks.BoardService
            .Setup(x => x.GetUserBoardsAsync(2, 0, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<BoardResponse> 
            { 
                BuildBoardResponse(1), 
                BuildBoardResponse(2), 
                BuildBoardResponse(3),
                BuildBoardResponse(4),
                BuildBoardResponse(5)
            }, 25));

        var response = await client.GetAsync("/api/boards?pageNum=0&pageSize=5");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task BoardPaginationScenario_Negative_GetBoardsWithoutUserIdNullFallback_ReturnsOk()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "3");

        _factory.Mocks.BoardService
            .Setup(x => x.GetUserBoardsAsync(3, 0, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<BoardResponse> { BuildBoardResponse(1) }, 1));

        // Omit userId parameter - should fall back to authenticated user
        var response = await client.GetAsync("/api/boards");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    #endregion

    #region Comment Operations
    
    [Fact]
    public async Task CommentGetAllScenario_Happy_GetAllComments_ReturnsPagedComments()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "1");

        _factory.Mocks.CommentService
            .Setup(x => x.GetAllBoardTaskCommentsAsync(5, It.IsAny<CancellationToken>(), 0, 10))
            .ReturnsAsync((new List<CommentResponse>
            {
                new() { Id = 1, TaskId = 5, UserId = 1, Content = "comment1", CreationDate = DateTime.UtcNow, Edited = false, Version = 1 },
                new() { Id = 2, TaskId = 5, UserId = 2, Content = "comment2", CreationDate = DateTime.UtcNow, Edited = false, Version = 1 }
            }, 2));

        var response = await client.GetAsync("/api/boards/1/tasks/5/comments");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CommentDeleteScenario_Happy_DeleteComment_ReturnsNoContent()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "1");

        _factory.Mocks.CommentService
            .Setup(x => x.DeleteCommentAsync(3, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await client.DeleteAsync("/api/boards/1/tasks/5/comments/3");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task CommentUpdateScenario_Happy_UpdateCommentContent_ReturnsUpdatedComment()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "1");

        _factory.Mocks.CommentService
            .Setup(x => x.UpdateCommentAsync(2, It.IsAny<CommentUpdateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommentResponse
            {
                Id = 2,
                TaskId = 5,
                UserId = 1,
                Content = "updated comment",
                CreationDate = DateTime.UtcNow,
                Edited = true,
                Version = 2
            });

        var response = await client.PutAsJsonAsync("/api/boards/1/tasks/5/comments/2", 
            new CommentUpdateRequest { Content = "updated comment", Version = 1 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CommentDeleteScenario_Negative_DeleteNonExistentComment_ReturnsNotFound()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "1");

        _factory.Mocks.CommentService
            .Setup(x => x.DeleteCommentAsync(999, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Comment not found"));

        var response = await client.DeleteAsync("/api/boards/1/tasks/5/comments/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #endregion

    #region Notification Operations
    
    [Fact]
    public async Task NotificationAcceptanceScenario_Happy_AcceptInvitation_ReturnsNoContent()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "2");

        _factory.Mocks.NotificationService
            .Setup(x => x.AcceptInvitation(5, 2, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await client.PostAsync("/api/notifications/5/accept", content: null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task NotificationAcceptanceScenario_Negative_AcceptWithInvalidNotification_ReturnsNotFound()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "2");

        _factory.Mocks.NotificationService
            .Setup(x => x.AcceptInvitation(999, 2, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Notification not found"));

        var response = await client.PostAsync("/api/notifications/999/accept", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task NotificationDeleteAllScenario_Happy_DeleteAllNotifications_ReturnsOk()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "1");

        _factory.Mocks.NotificationService
            .Setup(x => x.UnlinkAllNotifications(1, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await client.DeleteAsync("/api/notifications");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    #endregion

    #region Task User Assignment Operations
    
    [Fact]
    public async Task TaskUserAssignmentGetScenario_Happy_GetLinkedUsers_ReturnsUserList()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "1");

        _factory.Mocks.BoardService
            .Setup(x => x.GetUserRoleByBoardIdAndUserIdAsync(1, 1))
            .ReturnsAsync(UserRoleEnum.VIEWER);

        _factory.Mocks.TaskOnUserService
            .Setup(x => x.GetUsersLinkedToTaskAsync(1, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LinkedUserToTaskResponse>
            {
                new() { Id = 1, UserName = "user1", AssignedAt = DateTime.UtcNow },
                new() { Id = 2, UserName = "user2", AssignedAt = DateTime.UtcNow }
            });

        var response = await client.GetAsync("/api/boards/1/tasks/3/users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TaskUserUnlinkScenario_Negative_UnlinkAsViewer_ReturnsForbidden()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "1");

        _factory.Mocks.BoardService
            .Setup(x => x.GetUserRoleByBoardIdAndUserIdAsync(1, 1))
            .ReturnsAsync(UserRoleEnum.VIEWER);

        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/boards/1/tasks/3/users/unlink")
        {
            Content = JsonContent.Create(new List<LinkUserToTaskRequest> { new() { UserId = 2 } })
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TaskUserMultipleAssignmentScenario_Happy_AssignMultipleUsers_ReturnsLinkedUsers()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "1");

        _factory.Mocks.BoardService
            .Setup(x => x.GetUserRoleByBoardIdAndUserIdAsync(2, 1))
            .ReturnsAsync(UserRoleEnum.EDITOR);

        _factory.Mocks.TaskOnUserService
            .Setup(x => x.LinkUsersToTaskAsync(2, 4, It.IsAny<IEnumerable<LinkUserToTaskRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LinkUserToTaskResponse>
            {
                new() { UserId = 2, AssignedAt = DateTime.UtcNow },
                new() { UserId = 3, AssignedAt = DateTime.UtcNow },
                new() { UserId = 4, AssignedAt = DateTime.UtcNow }
            });

        _factory.Mocks.NotificationService
            .Setup(x => x.NotifyTaskAssigned(2, 4, It.IsAny<int>(), 1, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var request = new List<LinkUserToTaskRequest>
        {
            new() { UserId = 2 },
            new() { UserId = 3 },
            new() { UserId = 4 }
        };

        var response = await client.PostAsJsonAsync("/api/boards/2/tasks/4/users/link", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    #endregion

    #region Board-User Advanced Operations
    
    [Fact]
    public async Task BoardUserInviteScenario_Happy_InviteUserToBoard_ReturnsNoContent()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "1");

        _factory.Mocks.BoardService
            .Setup(x => x.GetUserRoleByBoardIdAndUserIdAsync(2, 1))
            .ReturnsAsync(UserRoleEnum.OWNER);

        _factory.Mocks.NotificationService
            .Setup(x => x.NotifyBoardInvitation(2, 3, 1, UserRoleEnum.EDITOR, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await client.PostAsJsonAsync("/api/boards/2/invite", 
            new InvitationRequest { UserId = 3, Role = UserRoleEnum.EDITOR });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task BoardUserRemovalScenario_Happy_RemoveUserFromBoard_ReturnsNoContent()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "1");

        _factory.Mocks.BoardService
            .Setup(x => x.GetUserRoleByBoardIdAndUserIdAsync(2, 1))
            .ReturnsAsync(UserRoleEnum.EDITOR);

        _factory.Mocks.BoardOnUserService
            .Setup(x => x.UnlinkUserFromBoard(2, 3, 1, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await client.PostAsync("/api/boards/2/remove/3", content: null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task BoardUserRemovalScenario_Negative_RemoveUserNotFound_ReturnsNotFound()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "1");

        _factory.Mocks.BoardOnUserService
            .Setup(x => x.UnlinkUserFromBoard(2, 3, 1, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("User is not linked to this board"));

        var response = await client.PostAsync("/api/boards/2/remove/3", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task BoardLinkOperationsScenario_Happy_GetSpecificBoardUserLink_ReturnsLink()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "1");

        _factory.Mocks.BoardService
            .Setup(x => x.GetUserRoleByBoardIdAndUserIdAsync(2, 1))
            .ReturnsAsync(UserRoleEnum.VIEWER);

        _factory.Mocks.BoardOnUserService
            .Setup(x => x.GetBoardToUserLink(2, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LinkUserToBoardResponse 
            { 
                BoardId = 2, 
                UserId = 3, 
                AddedAt = DateTime.UtcNow, 
                UserRole = UserRoleEnum.EDITOR 
            });

        var response = await client.GetAsync("/api/boards/2/link/3");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task BoardPatchUserRoleScenario_Happy_PatchUserRoleChange_ReturnsUpdatedLink()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "1");

        _factory.Mocks.BoardService
            .Setup(x => x.GetUserRoleByBoardIdAndUserIdAsync(2, 1))
            .ReturnsAsync(UserRoleEnum.EDITOR);

        _factory.Mocks.BoardOnUserService
            .Setup(x => x.PatchUserOnBoard(2, 3, It.IsAny<Microsoft.AspNetCore.JsonPatch.JsonPatchDocument<LinkUserToBoardRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LinkUserToBoardResponse 
            { 
                BoardId = 2, 
                UserId = 3, 
                AddedAt = DateTime.UtcNow, 
                UserRole = UserRoleEnum.VIEWER 
            });

        using var patchContent = new StringContent("[{\"op\":\"replace\",\"path\":\"/userRole\",\"value\":0}]", Encoding.UTF8, "application/json-patch+json");
        var response = await client.PatchAsync("/api/boards/2/link/3", patchContent);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task BoardUpdateUserRoleScenario_Happy_UpdateUserRoleViaPost_ReturnsUpdatedLink()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "1");

        _factory.Mocks.BoardOnUserService
            .Setup(x => x.LinkUserToBoard(2, 3, It.IsAny<LinkUserToBoardRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LinkUserToBoardResponse 
            { 
                BoardId = 2, 
                UserId = 3, 
                AddedAt = DateTime.UtcNow, 
                UserRole = UserRoleEnum.VIEWER 
            });

        var response = await client.PostAsJsonAsync("/api/boards/2/link/3", 
            new LinkUserToBoardRequest { UserRole = UserRoleEnum.VIEWER });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task BoardUpdateUserRoleViaMethodScenario_Happy_UpdateUserRolePut_ReturnsUpdatedLink()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "1");

        _factory.Mocks.BoardService
            .Setup(x => x.GetUserRoleByBoardIdAndUserIdAsync(2, 1))
            .ReturnsAsync(UserRoleEnum.EDITOR);

        _factory.Mocks.BoardOnUserService
            .Setup(x => x.UpdateUserOnBoard(2, 3, It.IsAny<LinkUserToBoardRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LinkUserToBoardResponse 
            { 
                BoardId = 2, 
                UserId = 3, 
                AddedAt = DateTime.UtcNow, 
                UserRole = UserRoleEnum.EDITOR 
            });

        var response = await client.PutAsJsonAsync("/api/boards/2/link/3", 
            new LinkUserToBoardRequest { UserRole = UserRoleEnum.EDITOR });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task BoardGetUsersScenario_Happy_GetAllUsersLinkedToBoard_ReturnsUserList()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "1");

        _factory.Mocks.BoardService
            .Setup(x => x.GetUserRoleByBoardIdAndUserIdAsync(2, 1))
            .ReturnsAsync(UserRoleEnum.VIEWER);

        _factory.Mocks.BoardOnUserService
            .Setup(x => x.GetUsersLinkedToBoardAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LinkedUserToBoardResponse>
            {
                new() { Id = 1, UserName = "owner", UserRole = UserRoleEnum.OWNER, AddedAt = DateTime.UtcNow },
                new() { Id = 3, UserName = "editor", UserRole = UserRoleEnum.EDITOR, AddedAt = DateTime.UtcNow }
            });

        var response = await client.GetAsync("/api/boards/2/users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task BoardTransferOwnershipScenario_Happy_TransferOwnershipToNewOwner_ReturnsNoContent()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "1");

        _factory.Mocks.BoardService
            .Setup(x => x.GetUserRoleByBoardIdAndUserIdAsync(2, 1))
            .ReturnsAsync(UserRoleEnum.OWNER);

        _factory.Mocks.BoardOnUserService
            .Setup(x => x.TransferOwnershipAsync(2, 1, 3, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await client.PostAsync("/api/boards/2/collaborators/3", content: null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task BoardTransferOwnershipScenario_Negative_TransferAsEditor_ReturnsForbidden()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "1");

        _factory.Mocks.BoardService
            .Setup(x => x.GetUserRoleByBoardIdAndUserIdAsync(2, 1))
            .ReturnsAsync(UserRoleEnum.EDITOR);

        var response = await client.PostAsync("/api/boards/2/collaborators/3", content: null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    #endregion

    #region Authentication Edge Cases and Validation
    
    [Fact]
    public async Task AuthenticationEdgeCaseScenario_Happy_RegisterNewUser_ReturnsOk()
    {
        _factory.Mocks.ResetAll();
        var client = _factory.CreateClient();

        _factory.Mocks.AuthService
            .Setup(x => x.RegisterUserAsync(It.IsAny<UserRegisterRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserResponse
            {
                Id = 5,
                FirstName = "New",
                LastName = "User",
                UserName = "newuser",
                Email = "new@worthboards.test",
                CreationDate = DateTime.UtcNow
            });

        var response = await client.PostAsJsonAsync("/register", 
            new UserRegisterRequest("New", "User", "newuser", "new@worthboards.test", "SecurePass123!"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AuthenticationValidationScenario_Negative_RegisterWithDuplicateUsername_ReturnsBadRequest()
    {
        _factory.Mocks.ResetAll();
        var client = _factory.CreateClient();

        _factory.Mocks.AuthService
            .Setup(x => x.RegisterUserAsync(It.IsAny<UserRegisterRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BadRequestException("Username already exists"));

        var response = await client.PostAsJsonAsync("/register", 
            new UserRegisterRequest("Test", "User", "testuser", "test@worthboards.test", "Passw0rd!"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AuthenticationForgotPasswordScenario_Negative_ForgotPasswordInvalidEmail_ReturnsBadRequest()
    {
        _factory.Mocks.ResetAll();
        var client = _factory.CreateClient();

        _factory.Mocks.AuthService
            .Setup(x => x.ForgotPasswordAsync(It.IsAny<Microsoft.AspNetCore.Identity.Data.ForgotPasswordRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BadRequestException("Email not found"));

        var response = await client.PostAsJsonAsync("/forgot-password", 
            new { email = "nonexistent@worthboards.test" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AuthenticationChangePasswordScenario_Negative_ChangePasswordWrongOldPassword_ReturnsBadRequest()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "1", email: "user@worthboards.test");

        _factory.Mocks.AuthService
            .Setup(x => x.ChangePasswordAsync(It.IsAny<ChangePasswordRequest>(), "user@worthboards.test", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BadRequestException("Invalid old password"));

        var response = await client.PostAsJsonAsync("/change-password", 
            new ChangePasswordRequest { OldPassword = "WrongPass1!", NewPassword = "NewPass1!" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region Board Error Handling
    
    [Fact]
    public async Task BoardExceptionHandlingScenario_Negative_GetNonExistentBoard_ReturnsNotFound()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "1");

        _factory.Mocks.BoardService
            .Setup(x => x.GetBoardByIdAsync(999, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Board not found"));

        var response = await client.GetAsync("/api/boards/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task BoardCreationValidationScenario_Negative_CreateBoardWithoutTitle_ReturnsBadRequest()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "1");

        _factory.Mocks.BoardService
            .Setup(x => x.CreateBoardAsync(1, It.IsAny<BoardRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BadRequestException("Board title is required"));

        var response = await client.PostAsJsonAsync("/api/boards?userId=1", 
            new BoardRequest { Title = "", Description = "desc", ImageName = "board.png" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region Task Error Handling
    
    [Fact]
    public async Task TaskExceptionHandlingScenario_Negative_GetNonExistentTask_ReturnsNotFound()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "1");

        _factory.Mocks.BoardService
            .Setup(x => x.GetUserRoleByBoardIdAndUserIdAsync(1, 1))
            .ReturnsAsync(UserRoleEnum.VIEWER);

        _factory.Mocks.BoardTaskService
            .Setup(x => x.GetBoardTaskById(1, 999, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Task not found"));

        var response = await client.GetAsync("/api/boards/1/tasks/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TaskCreationValidationScenario_Negative_CreateTaskWithoutTitle_ReturnsBadRequest()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "1");

        _factory.Mocks.BoardService
            .Setup(x => x.GetUserRoleByBoardIdAndUserIdAsync(1, 1))
            .ReturnsAsync(UserRoleEnum.EDITOR);

        _factory.Mocks.BoardTaskService
            .Setup(x => x.CreateBoardTask(1, It.IsAny<BoardTaskRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BadRequestException("Task title is required"));

        var response = await client.PostAsJsonAsync("/api/boards/1/tasks", 
            new BoardTaskRequest { Title = "", Description = "desc", TaskStatus = TaskStatusEnum.PENDING });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TaskUpdateConflictScenario_Negative_UpdateTaskWithConflict_ReturnsConflict()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "1");

        _factory.Mocks.BoardService
            .Setup(x => x.GetUserRoleByBoardIdAndUserIdAsync(1, 1))
            .ReturnsAsync(UserRoleEnum.EDITOR);

        _factory.Mocks.BoardTaskService
            .Setup(x => x.GetBoardTaskById(1, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildBoardTaskResponse(5, 1, TaskStatusEnum.PENDING));

        _factory.Mocks.BoardTaskService
            .Setup(x => x.UpdateBoardTask(1, 5, It.IsAny<BoardTaskUpdateRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BadRequestException("Version conflict: task has been modified"));

        var response = await client.PutAsJsonAsync("/api/boards/1/tasks/5", 
            new BoardTaskUpdateRequest 
            { 
                Title = "Updated", 
                TaskStatus = TaskStatusEnum.PENDING, 
                Version = 1  // Wrong version
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region User Validation
    
    [Fact]
    public async Task UserProfileScenario_Happy_GetUserProfile_ReturnsUserData()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "2");

        _factory.Mocks.UserService
            .Setup(x => x.GetUserById(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserResponse
            {
                Id = 2,
                FirstName = "John",
                LastName = "Doe",
                UserName = "johndoe",
                Email = "john@worthboards.test",
                CreationDate = DateTime.UtcNow.AddDays(-30)
            });

        var response = await client.GetAsync("/api/users/2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UserProfileScenario_Negative_GetNonExistentUser_ReturnsNotFound()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "1");

        _factory.Mocks.UserService
            .Setup(x => x.GetUserById(999, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("User not found"));

        var response = await client.GetAsync("/api/users/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UserUpdateValidationScenario_Negative_UpdateUserWithInvalidEmail_ReturnsBadRequest()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "1");

        _factory.Mocks.UserService
            .Setup(x => x.UpdateUser(1, It.IsAny<UserUpdateRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BadRequestException("Invalid email format"));

        var response = await client.PutAsJsonAsync("/api/users/1", 
            new UserUpdateRequest 
            { 
                FirstName = "John", 
                LastName = "Doe", 
                UserName = "johndoe", 
                Email = "invalid-email", 
                ImageName = "user.png" 
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region Upload Operations
    
    [Fact]
    public async Task UploadValidationScenario_Negative_UploadImageInvalidFormat_ReturnsBadRequest()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "1");

        _factory.Mocks.FileService
            .Setup(x => x.UploadImage(It.IsAny<IFormFile>()))
            .ThrowsAsync(new BadRequestException("Invalid file format. Only PNG, JPG, and GIF are allowed"));

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(new byte[] { 1, 2, 3 });
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "image", "document.pdf");

        var response = await client.PostAsync("/api/upload/image", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadErrorScenario_Negative_UploadImageServerError_ReturnsInternalServerError()
    {
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "1");

        _factory.Mocks.FileService
            .Setup(x => x.UploadImage(It.IsAny<IFormFile>()))
            .ThrowsAsync(new Exception("File storage service unavailable"));

        using var content = new MultipartFormDataContent();
        var imageContent = new ByteArrayContent(new byte[] { 1, 2, 3, 4 });
        imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(imageContent, "image", "image.png");

        var response = await client.PostAsync("/api/upload/image", content);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    #endregion

    #region Complex Scenario Integration Tests
    
    [Fact]
    public async Task CompleteWorkflowScenario_Happy_FullBoardLifecycle_SucceedsAllOperations()
    {
        // This test exercises multiple operations in sequence to simulate a full workflow
        _factory.Mocks.ResetAll();
        var client = CreateAuthenticatedClient(userId: "1");

        // Setup: User creates a board
        _factory.Mocks.BoardService
            .Setup(x => x.CreateBoardAsync(1, It.IsAny<BoardRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildBoardResponse(10));

        var createBoardResponse = await client.PostAsJsonAsync("/api/boards?userId=1", 
            new BoardRequest { Title = "Project Board", Description = "A new project", ImageName = "board.png" });

        Assert.Equal(HttpStatusCode.Created, createBoardResponse.StatusCode);

        // Get created board
        _factory.Mocks.BoardService
            .Setup(x => x.GetBoardByIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildBoardResponse(10));

        var getBoardResponse = await client.GetAsync("/api/boards/10");
        Assert.Equal(HttpStatusCode.OK, getBoardResponse.StatusCode);

        // Create task
        _factory.Mocks.BoardService
            .Setup(x => x.GetUserRoleByBoardIdAndUserIdAsync(10, 1))
            .ReturnsAsync(UserRoleEnum.OWNER);

        _factory.Mocks.BoardTaskService
            .Setup(x => x.CreateBoardTask(10, It.IsAny<BoardTaskRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildBoardTaskResponse(20, 10, TaskStatusEnum.PENDING));

        _factory.Mocks.NotificationService
            .Setup(x => x.NotifyTaskCreated(10, 20, 1, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var createTaskResponse = await client.PostAsJsonAsync("/api/boards/10/tasks", 
            new BoardTaskRequest { Title = "Task 1", Description = "First task", TaskStatus = TaskStatusEnum.PENDING });

        Assert.Equal(HttpStatusCode.Created, createTaskResponse.StatusCode);

        // Add comment to task
        _factory.Mocks.CommentService
            .Setup(x => x.CreateCommentAsync(1, 20, It.IsAny<CommentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommentResponse 
            { 
                Id = 30, 
                TaskId = 20, 
                UserId = 1, 
                Content = "Initial assessment", 
                CreationDate = DateTime.UtcNow, 
                Edited = false, 
                Version = 1 
            });

        var createCommentResponse = await client.PostAsJsonAsync("/api/boards/10/tasks/20/comments", 
            new CommentRequest { Content = "Initial assessment" });

        Assert.Equal(HttpStatusCode.Created, createCommentResponse.StatusCode);
    }

    [Fact]
    public async Task RoleBasedAccessScenario_Mixed_DifferentRolesAccess_ReturnAppropriateStatuses()
    {
        // This test validates role-based access control for different user roles
        _factory.Mocks.ResetAll();

        // OWNER accessing endpoints
        var ownerClient = CreateAuthenticatedClient(userId: "1");
        _factory.Mocks.BoardService
            .Setup(x => x.GetUserRoleByBoardIdAndUserIdAsync(1, 1))
            .ReturnsAsync(UserRoleEnum.OWNER);

        _factory.Mocks.NotificationService
            .Setup(x => x.NotifyBoardInvitation(1, 7, 1, UserRoleEnum.VIEWER, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var ownerResponse = await ownerClient.PostAsJsonAsync("/api/boards/1/invite", 
            new InvitationRequest { UserId = 7, Role = UserRoleEnum.VIEWER });
        Assert.Equal(HttpStatusCode.NoContent, ownerResponse.StatusCode);

        // EDITOR accessing the same endpoint (should fail)
        var editorClient = CreateAuthenticatedClient(userId: "2");
        _factory.Mocks.BoardService
            .Setup(x => x.GetUserRoleByBoardIdAndUserIdAsync(1, 2))
            .ReturnsAsync(UserRoleEnum.EDITOR);

        var editorResponse = await editorClient.PostAsJsonAsync("/api/boards/1/invite", 
            new InvitationRequest { UserId = 7, Role = UserRoleEnum.VIEWER });
        Assert.Equal(HttpStatusCode.Forbidden, editorResponse.StatusCode);

        // VIEWER accessing the same endpoint (should fail)
        var viewerClient = CreateAuthenticatedClient(userId: "3");
        _factory.Mocks.BoardService
            .Setup(x => x.GetUserRoleByBoardIdAndUserIdAsync(1, 3))
            .ReturnsAsync(UserRoleEnum.VIEWER);

        var viewerResponse = await viewerClient.PostAsJsonAsync("/api/boards/1/invite", 
            new InvitationRequest { UserId = 7, Role = UserRoleEnum.VIEWER });
        Assert.Equal(HttpStatusCode.Forbidden, viewerResponse.StatusCode);
    }

    #endregion
}
