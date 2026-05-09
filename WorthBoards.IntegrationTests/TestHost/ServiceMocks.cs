using Moq;
using WorthBoards.Business.Services.Interfaces;
using WorthBoards.Common.Enums;

namespace WorthBoards.IntegrationTests.TestHost;

public sealed class ServiceMocks
{
    public Mock<IAuthService> AuthService { get; } = new();
    public Mock<IBoardService> BoardService { get; } = new();
    public Mock<IBoardTaskService> BoardTaskService { get; } = new();
    public Mock<IBoardOnUserService> BoardOnUserService { get; } = new();
    public Mock<ICommentService> CommentService { get; } = new();
    public Mock<INotificationService> NotificationService { get; } = new();
    public Mock<ITaskOnUserService> TaskOnUserService { get; } = new();
    public Mock<IUserService> UserService { get; } = new();
    public Mock<IFileService> FileService { get; } = new();

    public ServiceMocks()
    {
        SetupDefaults();
    }

    public void ResetAll()
    {
        AuthService.Reset();
        BoardService.Reset();
        BoardTaskService.Reset();
        BoardOnUserService.Reset();
        CommentService.Reset();
        NotificationService.Reset();
        TaskOnUserService.Reset();
        UserService.Reset();
        FileService.Reset();
        SetupDefaults();
    }

    private void SetupDefaults()
    {
        BoardService
            .Setup(x => x.GetUserRoleByBoardIdAndUserIdAsync(It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(UserRoleEnum.OWNER);
    }
}
