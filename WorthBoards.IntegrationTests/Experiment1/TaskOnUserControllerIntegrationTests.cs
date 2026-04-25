using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Common.Enums;
using WorthBoards.IntegrationTests.Experiment1.TestInfrastructure;
using Xunit;

namespace WorthBoards.IntegrationTests.Experiment1;

public class TaskOnUserControllerIntegrationTests(IntegrationTestWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    [Fact]
    public async Task LinkUsersToTask_EditorRequest_CreatesTaskUserLinks()
    {
        // Arrange
        var owner = await CreateAuthenticatedUserAsync("tasklink_owner");
        var assignee = await CreateAuthenticatedUserAsync("tasklink_user");
        var boardId = await CreateBoardAsync(owner.Client, "Task Link Board");

        await owner.Client.PostAsJsonAsync($"/api/boards/{boardId}/link/{assignee.UserId}", new LinkUserToBoardRequest
        {
            UserRole = UserRoleEnum.VIEWER
        });

        var taskId = await CreateBoardTaskAsync(owner.Client, boardId, "Assign me");

        var request = new[]
        {
            new LinkUserToTaskRequest { UserId = assignee.UserId }
        };

        // Act
        var response = await owner.Client.PostAsJsonAsync($"/api/boards/{boardId}/tasks/{taskId}/users/link", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain(assignee.UserId.ToString());

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WorthBoards.Data.Database.ApplicationDbContext>();
        var link = await dbContext.TasksOnUsers.SingleAsync(t => t.BoardTaskId == taskId && t.UserId == assignee.UserId);
        link.Should().NotBeNull();
    }

    [Fact]
    public async Task LinkUsersToTask_TaskNotFound_ReturnsNotFoundAndDoesNotCreateLinks()
    {
        // Arrange
        var owner = await CreateAuthenticatedUserAsync("tasklink_negative_owner");
        var assignee = await CreateAuthenticatedUserAsync("tasklink_negative_user");
        var boardId = await CreateBoardAsync(owner.Client, "Task Link Validation Board");

        using var arrangeScope = Factory.Services.CreateScope();
        var arrangeDb = arrangeScope.ServiceProvider.GetRequiredService<WorthBoards.Data.Database.ApplicationDbContext>();
        var linksBefore = await arrangeDb.TasksOnUsers.CountAsync();

        var request = new[]
        {
            new LinkUserToTaskRequest { UserId = assignee.UserId }
        };

        // Act
        var response = await owner.Client.PostAsJsonAsync($"/api/boards/{boardId}/tasks/999999/users/link", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("NotFound");

        using var assertScope = Factory.Services.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<WorthBoards.Data.Database.ApplicationDbContext>();
        var linksAfter = await assertDb.TasksOnUsers.CountAsync();
        linksAfter.Should().Be(linksBefore);
    }

    [Fact]
    public async Task GetUsersLinkedToTask_LinkedUsersExist_ReturnsUsers()
    {
        // Arrange
        var owner = await CreateAuthenticatedUserAsync("taskusers_get_owner");
        var user = await CreateAuthenticatedUserAsync("taskusers_get_user");
        var boardId = await CreateBoardAsync(owner.Client, "Task users board");
        await owner.Client.PostAsJsonAsync($"/api/boards/{boardId}/link/{user.UserId}", new LinkUserToBoardRequest { UserRole = UserRoleEnum.VIEWER });
        var taskId = await CreateBoardTaskAsync(owner.Client, boardId, "Task users task");
        await owner.Client.PostAsJsonAsync($"/api/boards/{boardId}/tasks/{taskId}/users/link", new[] { new LinkUserToTaskRequest { UserId = user.UserId } });

        // Act
        var response = await owner.Client.GetAsync($"/api/boards/{boardId}/tasks/{taskId}/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain(user.UserName);
    }

    [Fact]
    public async Task UnlinkUsersFromTask_ExistingLink_RemovesTaskUserLink()
    {
        // Arrange
        var owner = await CreateAuthenticatedUserAsync("taskusers_unlink_owner");
        var user = await CreateAuthenticatedUserAsync("taskusers_unlink_user");
        var boardId = await CreateBoardAsync(owner.Client, "Task unlink board");
        await owner.Client.PostAsJsonAsync($"/api/boards/{boardId}/link/{user.UserId}", new LinkUserToBoardRequest { UserRole = UserRoleEnum.VIEWER });
        var taskId = await CreateBoardTaskAsync(owner.Client, boardId, "Task unlink task");
        await owner.Client.PostAsJsonAsync($"/api/boards/{boardId}/tasks/{taskId}/users/link", new[] { new LinkUserToTaskRequest { UserId = user.UserId } });

        // Act
        var response = await owner.Client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/boards/{boardId}/tasks/{taskId}/users/unlink")
        {
            Content = JsonContent.Create(new[] { new LinkUserToTaskRequest { UserId = user.UserId } })
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WorthBoards.Data.Database.ApplicationDbContext>();
        var exists = await db.TasksOnUsers.AnyAsync(x => x.BoardTaskId == taskId && x.UserId == user.UserId);
        exists.Should().BeFalse();
    }
}
