using WorthBoards.Common.Enums;
using WorthBoards.Domain.Entities;

namespace WorthBoards.IntegrationTests.Infrastructure;

/// <summary>
/// Builder for creating test Board instances with sensible defaults.
/// </summary>
internal sealed class BoardBuilder
{
    private string _title = "Test Board";
    private string _description = "Test Board Description";
    private string _imageName = "default.png";
    private DateTime _creationDate = DateTime.UtcNow;

    public BoardBuilder WithTitle(string title)
    {
        _title = title;
        return this;
    }

    public BoardBuilder WithDescription(string description)
    {
        _description = description;
        return this;
    }

    public BoardBuilder WithImageName(string imageName)
    {
        _imageName = imageName;
        return this;
    }

    public Board Build()
    {
        return new Board
        {
            Title = _title,
            Description = _description,
            ImageName = _imageName,
            CreationDate = _creationDate,
        };
    }
}

/// <summary>
/// Builder for creating test BoardTask instances with sensible defaults.
/// </summary>
internal sealed class BoardTaskBuilder
{
    private int _boardId = 1;
    private string _title = "Test Task";
    private string? _description = "Test Task Description";
    private DateTime? _deadlineEnd;
    private TaskStatusEnum _taskStatus = TaskStatusEnum.PENDING;
    private DateTime _creationDate = DateTime.UtcNow;

    public BoardTaskBuilder WithBoardId(int boardId)
    {
        _boardId = boardId;
        return this;
    }

    public BoardTaskBuilder WithTitle(string title)
    {
        _title = title;
        return this;
    }

    public BoardTaskBuilder WithDescription(string? description)
    {
        _description = description;
        return this;
    }

    public BoardTaskBuilder WithDeadlineEnd(DateTime? deadlineEnd)
    {
        _deadlineEnd = deadlineEnd;
        return this;
    }

    public BoardTaskBuilder WithStatus(TaskStatusEnum status)
    {
        _taskStatus = status;
        return this;
    }

    public BoardTask Build()
    {
        return new BoardTask
        {
            BoardId = _boardId,
            Title = _title,
            Description = _description,
            DeadlineEnd = _deadlineEnd,
            TaskStatus = _taskStatus,
            CreationDate = _creationDate,
        };
    }
}

/// <summary>
/// Builder for creating test Comment instances with sensible defaults.
/// </summary>
internal sealed class CommentBuilder
{
    private int _boardTaskId = 1;
    private int _userId = 1;
    private string _content = "Test comment";
    private bool _edited = false;
    private DateTime _creationDate = DateTime.UtcNow;

    public CommentBuilder WithBoardTaskId(int boardTaskId)
    {
        _boardTaskId = boardTaskId;
        return this;
    }

    public CommentBuilder WithUserId(int userId)
    {
        _userId = userId;
        return this;
    }

    public CommentBuilder WithContent(string content)
    {
        _content = content;
        return this;
    }

    public CommentBuilder AsEdited()
    {
        _edited = true;
        return this;
    }

    public Comment Build()
    {
        return new Comment
        {
            BoardTaskId = _boardTaskId,
            UserId = _userId,
            Content = _content,
            Edited = _edited,
            CreationDate = _creationDate,
        };
    }
}

/// <summary>
/// Builder for creating test BoardOnUser (role assignment) instances with sensible defaults.
/// </summary>
internal sealed class BoardOnUserBuilder
{
    private int _boardId = 1;
    private int _userId = 1;
    private DateTime _addedAt = DateTime.UtcNow;
    private UserRoleEnum _userRole = UserRoleEnum.VIEWER;

    public BoardOnUserBuilder WithBoardId(int boardId)
    {
        _boardId = boardId;
        return this;
    }

    public BoardOnUserBuilder WithUserId(int userId)
    {
        _userId = userId;
        return this;
    }

    public BoardOnUserBuilder WithRole(UserRoleEnum role)
    {
        _userRole = role;
        return this;
    }

    public BoardOnUser Build()
    {
        return new BoardOnUser
        {
            BoardId = _boardId,
            UserId = _userId,
            AddedAt = _addedAt,
            UserRole = _userRole,
        };
    }
}

/// <summary>
/// Builder for creating test TaskOnUser (task assignment) instances with sensible defaults.
/// </summary>
internal sealed class TaskOnUserBuilder
{
    private int _boardTaskId = 1;
    private int _userId = 1;
    private DateTime _assignedAt = DateTime.UtcNow;

    public TaskOnUserBuilder WithBoardTaskId(int boardTaskId)
    {
        _boardTaskId = boardTaskId;
        return this;
    }

    public TaskOnUserBuilder WithUserId(int userId)
    {
        _userId = userId;
        return this;
    }

    public TaskOnUser Build()
    {
        return new TaskOnUser
        {
            BoardTaskId = _boardTaskId,
            UserId = _userId,
            AssignedAt = _assignedAt,
        };
    }
}
