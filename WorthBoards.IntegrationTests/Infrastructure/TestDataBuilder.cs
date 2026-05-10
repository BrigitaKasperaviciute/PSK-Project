using WorthBoards.Business.Dtos.Identity;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Common.Enums;

namespace WorthBoards.IntegrationTests.Infrastructure;

public class TestDataBuilder
{
    private static int _userCounter = 1;
    private static int _boardCounter = 1;
    private static int _taskCounter = 1;

    public static UserRegisterRequest CreateUserRegisterRequest(string? userName = null, string? email = null)
    {
        var counter = _userCounter++;
        return new UserRegisterRequest(
            FirstName: $"TestUser{counter}First",
            LastName: $"TestUser{counter}Last",
            UserName: userName ?? $"testuser{counter}",
            Email: email ?? $"testuser{counter}@example.com",
            Password: "TestPassword123!"
        );
    }

    public static UserLoginRequest CreateUserLoginRequest(string userName = "testuser", string password = "TestPassword123!")
    {
        return new UserLoginRequest(UserName: userName, Password: password);
    }

    public static BoardRequest CreateBoardRequest(string? title = null, string? description = null, string? imageName = null)
    {
        var counter = _boardCounter++;
        return new BoardRequest
        {
            Title = title ?? $"Test Board {counter}",
            Description = description ?? $"Test Board Description {counter}",
            ImageName = imageName ?? "default-board.jpg"
        };
    }

    public static BoardUpdateRequest CreateBoardUpdateRequest(string? title = null, string? description = null)
    {
        var counter = _boardCounter++;
        return new BoardUpdateRequest
        {
            Title = title ?? $"Updated Board {counter}",
            Description = description ?? $"Updated Description {counter}",
            ImageName = "default-board.jpg"
        };
    }

    public static BoardTaskRequest CreateBoardTaskRequest(string? title = null, string? description = null, TaskStatusEnum? status = null)
    {
        var counter = _taskCounter++;
        return new BoardTaskRequest
        {
            Title = title ?? $"Test Task {counter}",
            Description = description ?? $"Test Task Description {counter}",
            DeadlineEnd = DateTime.UtcNow.AddDays(7),
            TaskStatus = status ?? TaskStatusEnum.PENDING
        };
    }

    public static BoardTaskUpdateRequest CreateBoardTaskUpdateRequest(string? title = null, TaskStatusEnum? status = null)
    {
        var counter = _taskCounter++;
        return new BoardTaskUpdateRequest
        {
            Title = title ?? $"Updated Task {counter}",
            Description = "Updated Description",
            DeadlineEnd = DateTime.UtcNow.AddDays(14),
            TaskStatus = status ?? TaskStatusEnum.IN_PROGRESS
        };
    }

    public static CommentRequest CreateCommentRequest(string? content = null)
    {
        return new CommentRequest
        {
            Content = content ?? $"Test comment at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}"
        };
    }

    public static CommentUpdateRequest CreateCommentUpdateRequest(string? content = null)
    {
        return new CommentUpdateRequest
        {
            Content = content ?? $"Updated comment at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}"
        };
    }

    public static InvitationRequest CreateInvitationRequest(int userId, UserRoleEnum role = UserRoleEnum.VIEWER)
    {
        return new InvitationRequest
        {
            UserId = userId,
            Role = role
        };
    }

    public static LinkUserToBoardRequest CreateLinkUserToBoardRequest(UserRoleEnum role = UserRoleEnum.EDITOR)
    {
        return new LinkUserToBoardRequest
        {
            UserRole = role
        };
    }

    public static LinkUserToTaskRequest CreateLinkUserToTaskRequest(int userId)
    {
        return new LinkUserToTaskRequest
        {
            UserId = userId
        };
    }

    public static UserUpdateRequest CreateUserUpdateRequest(
        string? firstName = null,
        string? lastName = null,
        string? userName = null,
        string? email = null)
    {
        var counter = _userCounter++;
        return new UserUpdateRequest
        {
            FirstName = firstName ?? $"UpdatedFirst{counter}",
            LastName = lastName ?? $"UpdatedLast{counter}",
            UserName = userName ?? $"updateduser{counter}",
            Email = email ?? $"updateduser{counter}@example.com",
            ImageName = "updated-image.jpg"
        };
    }

    public static void ResetCounters()
    {
        _userCounter = 1;
        _boardCounter = 1;
        _taskCounter = 1;
    }
}
