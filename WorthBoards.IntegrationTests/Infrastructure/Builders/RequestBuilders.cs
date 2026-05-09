using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Common.Enums;

namespace WorthBoards.IntegrationTests.Infrastructure;

/// <summary>
/// Builder for creating BoardRequest instances.
/// </summary>
internal sealed class BoardRequestBuilder
{
    private string _title = "Test Board";
    private string _description = "Test Board Description";
    private string _imageName = "default.png";

    public BoardRequestBuilder WithTitle(string title)
    {
        _title = title;
        return this;
    }

    public BoardRequestBuilder WithDescription(string description)
    {
        _description = description;
        return this;
    }

    public BoardRequestBuilder WithImageName(string imageName)
    {
        _imageName = imageName;
        return this;
    }

    public BoardRequest Build()
    {
        return new BoardRequest
        {
            Title = _title,
            Description = _description,
            ImageName = _imageName,
        };
    }
}

/// <summary>
/// Builder for creating BoardTaskRequest instances.
/// </summary>
internal sealed class BoardTaskRequestBuilder
{
    private string _title = "Test Task";
    private string? _description = "Test Task Description";
    private DateTime? _deadlineEnd;
    private TaskStatusEnum _taskStatus = TaskStatusEnum.PENDING;

    public BoardTaskRequestBuilder WithTitle(string title)
    {
        _title = title;
        return this;
    }

    public BoardTaskRequestBuilder WithDescription(string? description)
    {
        _description = description;
        return this;
    }

    public BoardTaskRequestBuilder WithDeadlineEnd(DateTime? deadlineEnd)
    {
        _deadlineEnd = deadlineEnd;
        return this;
    }

    public BoardTaskRequestBuilder WithStatus(TaskStatusEnum status)
    {
        _taskStatus = status;
        return this;
    }

    public BoardTaskRequest Build()
    {
        return new BoardTaskRequest
        {
            Title = _title,
            Description = _description,
            DeadlineEnd = _deadlineEnd,
            TaskStatus = _taskStatus,
        };
    }
}

/// <summary>
/// Builder for creating BoardTaskUpdateRequest instances.
/// </summary>
internal sealed class BoardTaskUpdateRequestBuilder
{
    private string _title = "Updated Task";
    private string? _description = "Updated Description";
    private DateTime? _deadlineEnd;
    private TaskStatusEnum _taskStatus = TaskStatusEnum.PENDING;
    private uint _version = 1;

    public BoardTaskUpdateRequestBuilder WithTitle(string title)
    {
        _title = title;
        return this;
    }

    public BoardTaskUpdateRequestBuilder WithDescription(string? description)
    {
        _description = description;
        return this;
    }

    public BoardTaskUpdateRequestBuilder WithDeadlineEnd(DateTime? deadlineEnd)
    {
        _deadlineEnd = deadlineEnd;
        return this;
    }

    public BoardTaskUpdateRequestBuilder WithStatus(TaskStatusEnum status)
    {
        _taskStatus = status;
        return this;
    }

    public BoardTaskUpdateRequestBuilder WithVersion(uint version)
    {
        _version = version;
        return this;
    }

    public BoardTaskUpdateRequest Build()
    {
        return new BoardTaskUpdateRequest
        {
            Title = _title,
            Description = _description,
            DeadlineEnd = _deadlineEnd,
            TaskStatus = _taskStatus,
            Version = _version,
        };
    }
}

/// <summary>
/// Builder for creating BoardUpdateRequest instances.
/// </summary>
internal sealed class BoardUpdateRequestBuilder
{
    private string _title = "Updated Board";
    private string _description = "Updated Description";
    private string _imageName = "updated.png";
    private uint _version = 1;

    public BoardUpdateRequestBuilder WithTitle(string title)
    {
        _title = title;
        return this;
    }

    public BoardUpdateRequestBuilder WithDescription(string description)
    {
        _description = description;
        return this;
    }

    public BoardUpdateRequestBuilder WithImageName(string imageName)
    {
        _imageName = imageName;
        return this;
    }

    public BoardUpdateRequestBuilder WithVersion(uint version)
    {
        _version = version;
        return this;
    }

    public BoardUpdateRequest Build()
    {
        return new BoardUpdateRequest
        {
            Title = _title,
            Description = _description,
            ImageName = _imageName,
            Version = _version,
        };
    }
}

/// <summary>
/// Builder for creating CommentRequest instances.
/// </summary>
internal sealed class CommentRequestBuilder
{
    private string _content = "Test comment";

    public CommentRequestBuilder WithContent(string content)
    {
        _content = content;
        return this;
    }

    public CommentRequest Build()
    {
        return new CommentRequest
        {
            Content = _content,
        };
    }
}

/// <summary>
/// Builder for creating CommentUpdateRequest instances.
/// </summary>
internal sealed class CommentUpdateRequestBuilder
{
    private string _content = "Updated comment";
    private uint _version = 1;

    public CommentUpdateRequestBuilder WithContent(string content)
    {
        _content = content;
        return this;
    }

    public CommentUpdateRequestBuilder WithVersion(uint version)
    {
        _version = version;
        return this;
    }

    public CommentUpdateRequest Build()
    {
        return new CommentUpdateRequest
        {
            Content = _content,
            Version = _version,
        };
    }
}

/// <summary>
/// Builder for creating LinkUserToTaskRequest instances.
/// </summary>
internal sealed class LinkUserToTaskRequestBuilder
{
    private int _userId = 1;

    public LinkUserToTaskRequestBuilder WithUserId(int userId)
    {
        _userId = userId;
        return this;
    }

    public LinkUserToTaskRequest Build()
    {
        return new LinkUserToTaskRequest
        {
            UserId = _userId,
        };
    }
}

/// <summary>
/// Builder for creating InvitationRequest instances.
/// </summary>
internal sealed class InvitationRequestBuilder
{
    private int _userId = 1;
    private UserRoleEnum _role = UserRoleEnum.VIEWER;

    public InvitationRequestBuilder WithUserId(int userId)
    {
        _userId = userId;
        return this;
    }

    public InvitationRequestBuilder WithRole(UserRoleEnum role)
    {
        _role = role;
        return this;
    }

    public InvitationRequest Build()
    {
        return new InvitationRequest
        {
            UserId = _userId,
            Role = _role,
        };
    }
}
