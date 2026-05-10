using System.Net;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Common.Enums;

namespace WorthBoards.IntegrationTests.Controllers;

public sealed class BoardTaskAndTaskOnUserControllerTests
{
    [Fact]
    public async Task CreateBoardTask_ReturnsCreated_AndNotifiesAssignee()
    {
        await using var factory = new WorthBoardsApiFactory();
        var client = factory.CreateAuthorizedClient();

        factory.BoardServiceMock
            .Setup(service => service.GetUserRoleByBoardIdAndUserIdAsync(9, 7))
            .ReturnsAsync(UserRoleEnum.EDITOR);

        factory.BoardTaskServiceMock
            .Setup(service => service.CreateBoardTask(9, It.IsAny<BoardTaskRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BoardTaskResponse
            {
                Id = 33,
                BoardId = 9,
                Title = "Task A",
                Description = "Created in test",
                DeadlineEnd = null,
                CreationDate = DateTime.UtcNow,
                TaskStatus = TaskStatusEnum.PENDING,
                Version = 1
            });

        factory.NotificationServiceMock
            .Setup(service => service.NotifyTaskCreated(9, 33, 7, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await client.SendAsync(TestRequestFactory.CreateJsonRequest(
            HttpMethod.Post,
            "/api/boards/9/tasks",
            new BoardTaskRequest { Title = "Task A", Description = "Created in test", DeadlineEnd = null, TaskStatus = TaskStatusEnum.PENDING },
            userId: 7));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CreateBoardTask_ReturnsForbidden_ForViewer()
    {
        await using var factory = new WorthBoardsApiFactory();
        var client = factory.CreateAuthorizedClient();

        factory.BoardServiceMock
            .Setup(service => service.GetUserRoleByBoardIdAndUserIdAsync(9, 7))
            .ReturnsAsync(UserRoleEnum.VIEWER);

        var response = await client.SendAsync(TestRequestFactory.CreateJsonRequest(
            HttpMethod.Post,
            "/api/boards/9/tasks",
            new BoardTaskRequest { Title = "Task A", Description = "Created in test", DeadlineEnd = null, TaskStatus = TaskStatusEnum.PENDING },
            userId: 7));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateBoardTask_ReturnsOk_WhenStatusChanges()
    {
        await using var factory = new WorthBoardsApiFactory();
        var client = factory.CreateAuthorizedClient();

        factory.BoardServiceMock
            .Setup(service => service.GetUserRoleByBoardIdAndUserIdAsync(9, 7))
            .ReturnsAsync(UserRoleEnum.EDITOR);

        factory.BoardTaskServiceMock
            .Setup(service => service.GetBoardTaskById(9, 33, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BoardTaskResponse
            {
                Id = 33,
                BoardId = 9,
                Title = "Task A",
                Description = "Created in test",
                DeadlineEnd = null,
                CreationDate = DateTime.UtcNow,
                TaskStatus = TaskStatusEnum.PENDING,
                Version = 1
            });

        factory.BoardTaskServiceMock
            .Setup(service => service.UpdateBoardTask(9, 33, It.IsAny<BoardTaskUpdateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BoardTaskResponse
            {
                Id = 33,
                BoardId = 9,
                Title = "Task A",
                Description = "Updated",
                DeadlineEnd = null,
                CreationDate = DateTime.UtcNow,
                TaskStatus = TaskStatusEnum.COMPLETED,
                Version = 2
            });

        factory.NotificationServiceMock
            .Setup(service => service.NotifyTaskStatusChange(9, 33, 7, TaskStatusEnum.PENDING, TaskStatusEnum.COMPLETED, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await client.SendAsync(TestRequestFactory.CreateJsonRequest(
            HttpMethod.Put,
            "/api/boards/9/tasks/33",
            new BoardTaskUpdateRequest { Title = "Task A", Description = "Updated", DeadlineEnd = null, TaskStatus = TaskStatusEnum.COMPLETED, Version = 1 },
            userId: 7));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UpdateBoardTask_ReturnsOk_WhenStatusIsUnchanged()
    {
        await using var factory = new WorthBoardsApiFactory();
        var client = factory.CreateAuthorizedClient();

        factory.BoardServiceMock
            .Setup(service => service.GetUserRoleByBoardIdAndUserIdAsync(9, 7))
            .ReturnsAsync(UserRoleEnum.EDITOR);

        factory.BoardTaskServiceMock
            .Setup(service => service.GetBoardTaskById(9, 33, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BoardTaskResponse
            {
                Id = 33,
                BoardId = 9,
                Title = "Task A",
                Description = "Created in test",
                DeadlineEnd = null,
                CreationDate = DateTime.UtcNow,
                TaskStatus = TaskStatusEnum.PENDING,
                Version = 1
            });

        factory.BoardTaskServiceMock
            .Setup(service => service.UpdateBoardTask(9, 33, It.IsAny<BoardTaskUpdateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BoardTaskResponse
            {
                Id = 33,
                BoardId = 9,
                Title = "Task A",
                Description = "Updated",
                DeadlineEnd = null,
                CreationDate = DateTime.UtcNow,
                TaskStatus = TaskStatusEnum.PENDING,
                Version = 2
            });

        var response = await client.SendAsync(TestRequestFactory.CreateJsonRequest(
            HttpMethod.Put,
            "/api/boards/9/tasks/33",
            new BoardTaskUpdateRequest { Title = "Task A", Description = "Updated", DeadlineEnd = null, TaskStatus = TaskStatusEnum.PENDING, Version = 1 },
            userId: 7));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task LinkUsersToTask_ReturnsOk_AndSkipsNotificationForCurrentUser()
    {
        await using var factory = new WorthBoardsApiFactory();
        var client = factory.CreateAuthorizedClient();

        factory.BoardServiceMock
            .Setup(service => service.GetUserRoleByBoardIdAndUserIdAsync(9, 7))
            .ReturnsAsync(UserRoleEnum.EDITOR);

        factory.TaskOnUserServiceMock
            .Setup(service => service.LinkUsersToTaskAsync(9, 33, It.IsAny<IEnumerable<LinkUserToTaskRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new LinkUserToTaskResponse { UserId = 7, AssignedAt = DateTime.UtcNow },
                new LinkUserToTaskResponse { UserId = 12, AssignedAt = DateTime.UtcNow }
            });

        factory.NotificationServiceMock
            .Setup(service => service.NotifyTaskAssigned(9, 33, 12, 7, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await client.SendAsync(TestRequestFactory.CreateJsonRequest(
            HttpMethod.Post,
            "/api/boards/9/tasks/33/users/link",
            new[] { new LinkUserToTaskRequest { UserId = 7 }, new LinkUserToTaskRequest { UserId = 12 } },
            userId: 7));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task LinkUsersToTask_ReturnsForbidden_ForViewer()
    {
        await using var factory = new WorthBoardsApiFactory();
        var client = factory.CreateAuthorizedClient();

        factory.BoardServiceMock
            .Setup(service => service.GetUserRoleByBoardIdAndUserIdAsync(9, 7))
            .ReturnsAsync(UserRoleEnum.VIEWER);

        var response = await client.SendAsync(TestRequestFactory.CreateJsonRequest(
            HttpMethod.Post,
            "/api/boards/9/tasks/33/users/link",
            new[] { new LinkUserToTaskRequest { UserId = 12 } },
            userId: 7));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}