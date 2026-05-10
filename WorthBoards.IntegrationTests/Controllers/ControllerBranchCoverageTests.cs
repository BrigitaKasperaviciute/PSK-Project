using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using WorthBoards.Api.Controllers;
using WorthBoards.Api.Utils;
using WorthBoards.Business.Dtos.Identity;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Business.Services.Interfaces;

namespace WorthBoards.IntegrationTests.Controllers;

public sealed class ControllerBranchCoverageTests
{
    [Fact]
    public async Task UserController_GetUserById_ReturnsUnauthorized_WhenUserIdIsNull_AndClaimMissing()
    {
        var userServiceMock = new Mock<IUserService>(MockBehavior.Strict);
        var controller = new UserController(userServiceMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "no-id") }, "Test"))
                }
            }
        };

        var result = await controller.GetUserById(null, CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task UserController_GetUserById_UsesClaimWhenUserIdIsNull()
    {
        var userServiceMock = new Mock<IUserService>(MockBehavior.Strict);
        userServiceMock
            .Setup(service => service.GetUserById(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserResponse
            {
                Id = 7,
                FirstName = "Jane",
                LastName = "Doe",
                UserName = "jane.doe",
                Email = "jane@example.com",
                CreationDate = DateTime.UtcNow
            });

        var controller = new UserController(userServiceMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "7") }, "Test"))
                }
            }
        };

        var result = await controller.GetUserById(null, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var user = Assert.IsType<UserResponse>(okResult.Value);
        Assert.Equal(7, user.Id);
    }

    [Fact]
    public async Task TaskOnUserController_LinkUsersToTask_ReturnsUnauthorized_WhenClaimMissing()
    {
        var taskOnUserServiceMock = new Mock<ITaskOnUserService>(MockBehavior.Strict);
        var notificationServiceMock = new Mock<INotificationService>(MockBehavior.Strict);

        taskOnUserServiceMock
            .Setup(service => service.LinkUsersToTaskAsync(9, 33, It.IsAny<IEnumerable<LinkUserToTaskRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new LinkUserToTaskResponse { UserId = 12, AssignedAt = DateTime.UtcNow }
            });

        var controller = new TaskOnUserController(taskOnUserServiceMock.Object, notificationServiceMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "no-id") }, "Test"))
                }
            }
        };

        var result = await controller.LinkUsersToTask(
            9,
            33,
            new[] { new LinkUserToTaskRequest { UserId = 12 } },
            CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public void UserHelper_GetUsername_ReturnsAnonymous_WhenIdentityNameIsMissing()
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity())
        };

        var userName = UserHelper.GetUsername(httpContext);

        Assert.Equal("Anonymous", userName);
    }
}
