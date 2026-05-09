using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Newtonsoft.Json;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Common.Enums;
using WorthBoards.Data.Database;
using WorthBoards.Data.Identity;
using WorthBoards.IntegrationTests.Infrastructure;
using Xunit;

namespace WorthBoards.IntegrationTests.Controllers;

[Collection(nameof(ApiCollection))]
public class TaskOnUserControllerIntegrationTests
{
    private readonly ApiFactory _factory;

    public TaskOnUserControllerIntegrationTests(ApiFactory factory)
    {
        _factory = factory;
    }

    #region LinkUsersToTask Tests

    [Fact]
    public async Task LinkUsersToTask_WithValidUsers_ReturnsOkAndAssignsUsers()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1);
        var (user1, _) = await CreateUserAsync(2);
        var (user2, _) = await CreateUserAsync(3);
        var client = _factory.CreateClientForUser(owner.Id, "EDITOR");
        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id);
        await helper.AddUserToBoardAsync(board.Id, owner.Id, UserRoleEnum.EDITOR);
        await helper.AddUserToBoardAsync(board.Id, user1.Id, UserRoleEnum.VIEWER);
        await helper.AddUserToBoardAsync(board.Id, user2.Id, UserRoleEnum.VIEWER);
        var task = await helper.CreateBoardTaskAsync(board.Id);

        var linkRequests = new[]
        {
            new LinkUserToTaskRequest { UserId = user1.Id },
            new LinkUserToTaskRequest { UserId = user2.Id },
        };

        // Act
        var content = new StringContent(
            JsonConvert.SerializeObject(linkRequests),
            Encoding.UTF8,
            "application/json");
        var response = await client.PostAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/users/link", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify users are assigned
        var assignedUsers = helper.GetUsersAssignedToTask(task.Id).ToList();
        assignedUsers.Should().HaveCount(2);
        assignedUsers.Should().Contain(user1.Id);
        assignedUsers.Should().Contain(user2.Id);

        dbContext.Dispose();
    }

    [Fact]
    public async Task LinkUsersToTask_WithNonexistentUser_ReturnsBadRequest()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1);
        var client = _factory.CreateClientForUser(owner.Id, "EDITOR");
        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id);
        await helper.AddUserToBoardAsync(board.Id, owner.Id, UserRoleEnum.EDITOR);
        var task = await helper.CreateBoardTaskAsync(board.Id);

        var linkRequests = new[]
        {
            new LinkUserToTaskRequest { UserId = 99999 }, // Non-existent user
        };

        // Act
        var content = new StringContent(
            JsonConvert.SerializeObject(linkRequests),
            Encoding.UTF8,
            "application/json");
        var response = await client.PostAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/users/link", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var assignedUsers = helper.GetUsersAssignedToTask(task.Id).ToList();
        assignedUsers.Should().BeEmpty();

        dbContext.Dispose();
    }

    [Fact]
    public async Task LinkUsersToTask_WithoutEditorRole_ReturnsForbidden()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1);
        var (viewer, _) = await CreateUserAsync(2);
        var (otherUser, _) = await CreateUserAsync(3);
        var client = _factory.CreateClientForUser(viewer.Id, "VIEWER");
        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id);
        await helper.AddUserToBoardAsync(board.Id, viewer.Id, UserRoleEnum.VIEWER);
        await helper.AddUserToBoardAsync(board.Id, otherUser.Id, UserRoleEnum.VIEWER);
        var task = await helper.CreateBoardTaskAsync(board.Id);

        var linkRequests = new[]
        {
            new LinkUserToTaskRequest { UserId = otherUser.Id },
        };

        // Act
        var content = new StringContent(
            JsonConvert.SerializeObject(linkRequests),
            Encoding.UTF8,
            "application/json");
        var response = await client.PostAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/users/link", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        dbContext.Dispose();
    }

    [Fact]
    public async Task LinkUsersToTask_WithUserNotOnBoard_ReturnsBadRequest()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1);
        var (outsider, _) = await CreateUserAsync(2);
        var client = _factory.CreateClientForUser(owner.Id, "EDITOR");
        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id);
        await helper.AddUserToBoardAsync(board.Id, owner.Id, UserRoleEnum.EDITOR);
        var task = await helper.CreateBoardTaskAsync(board.Id);

        var linkRequests = new[]
        {
            new LinkUserToTaskRequest { UserId = outsider.Id }, // User not on board
        };

        // Act
        var content = new StringContent(
            JsonConvert.SerializeObject(linkRequests),
            Encoding.UTF8,
            "application/json");
        var response = await client.PostAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/users/link", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var assignedUsers = helper.GetUsersAssignedToTask(task.Id).ToList();
        assignedUsers.Should().HaveCount(1);
        assignedUsers.Should().Contain(outsider.Id);

        dbContext.Dispose();
    }

    #endregion

    #region UnlinkUsersFromTask Tests

    [Fact]
    public async Task UnlinkUsersFromTask_WithAssignedUsers_ReturnsOkAndUnassignsUsers()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1);
        var (user1, _) = await CreateUserAsync(2);
        var (user2, _) = await CreateUserAsync(3);
        var client = _factory.CreateClientForUser(owner.Id, "EDITOR");
        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id);
        await helper.AddUserToBoardAsync(board.Id, owner.Id, UserRoleEnum.EDITOR);
        await helper.AddUserToBoardAsync(board.Id, user1.Id, UserRoleEnum.VIEWER);
        await helper.AddUserToBoardAsync(board.Id, user2.Id, UserRoleEnum.VIEWER);
        var task = await helper.CreateBoardTaskAsync(board.Id);
        await helper.AssignUserToTaskAsync(task.Id, user1.Id);
        await helper.AssignUserToTaskAsync(task.Id, user2.Id);

        var unlinkRequests = new[]
        {
            new LinkUserToTaskRequest { UserId = user1.Id },
        };

        // Act
        var content = new StringContent(
            JsonConvert.SerializeObject(unlinkRequests),
            Encoding.UTF8,
            "application/json");
        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/boards/{board.Id}/tasks/{task.Id}/users/unlink")
        {
            Content = content
        };
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify user is unassigned
        var assignedUsers = helper.GetUsersAssignedToTask(task.Id).ToList();
        assignedUsers.Should().HaveCount(1);
        assignedUsers.Should().Contain(user2.Id);
        assignedUsers.Should().NotContain(user1.Id);

        dbContext.Dispose();
    }

    [Fact]
    public async Task UnlinkUsersFromTask_WithoutEditorRole_ReturnsForbidden()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1);
        var (viewer, _) = await CreateUserAsync(2);
        var (otherUser, _) = await CreateUserAsync(3);
        var client = _factory.CreateClientForUser(viewer.Id, "VIEWER");
        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id);
        await helper.AddUserToBoardAsync(board.Id, viewer.Id, UserRoleEnum.VIEWER);
        await helper.AddUserToBoardAsync(board.Id, otherUser.Id, UserRoleEnum.VIEWER);
        var task = await helper.CreateBoardTaskAsync(board.Id);
        await helper.AssignUserToTaskAsync(task.Id, otherUser.Id);

        var unlinkRequests = new[]
        {
            new LinkUserToTaskRequest { UserId = otherUser.Id },
        };

        // Act
        var content = new StringContent(
            JsonConvert.SerializeObject(unlinkRequests),
            Encoding.UTF8,
            "application/json");
        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/boards/{board.Id}/tasks/{task.Id}/users/unlink")
        {
            Content = content
        };
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        dbContext.Dispose();
    }

    #endregion

    #region GetUsersLinkedToTask Tests

    [Fact]
    public async Task GetUsersLinkedToTask_WithAssignedUsers_ReturnsOkAndUserList()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1);
        var (user1, _) = await CreateUserAsync(2);
        var (user2, _) = await CreateUserAsync(3);
        var client = _factory.CreateClientForUser(owner.Id, "VIEWER");
        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id);
        await helper.AddUserToBoardAsync(board.Id, user1.Id, UserRoleEnum.VIEWER);
        await helper.AddUserToBoardAsync(board.Id, user2.Id, UserRoleEnum.VIEWER);
        var task = await helper.CreateBoardTaskAsync(board.Id);
        await helper.AssignUserToTaskAsync(task.Id, user1.Id);
        await helper.AssignUserToTaskAsync(task.Id, user2.Id);

        // Act
        var response = await client.GetAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var users = JsonConvert.DeserializeObject<List<LinkedUserToTaskResponse>>(content);
        users.Should().NotBeNull();
        users!.Should().HaveCount(2);
        users.Should().Contain(u => u.Id == user1.Id);
        users.Should().Contain(u => u.Id == user2.Id);

        dbContext.Dispose();
    }

    [Fact]
    public async Task GetUsersLinkedToTask_WithNoAssignedUsers_ReturnsOkAndEmptyList()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1);
        var client = _factory.CreateClientForUser(owner.Id, "VIEWER");
        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id);
        var task = await helper.CreateBoardTaskAsync(board.Id);

        // Act
        var response = await client.GetAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var users = JsonConvert.DeserializeObject<List<LinkedUserToTaskResponse>>(content);
        users.Should().NotBeNull();
        users.Should().BeEmpty();

        dbContext.Dispose();
    }

    [Fact]
    public async Task GetUsersLinkedToTask_WithoutViewerRole_ReturnsForbidden()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1);
        var (outsider, _) = await CreateUserAsync(2);
        var client = _factory.CreateClientForUser(outsider.Id, "VIEWER");
        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id);
        var task = await helper.CreateBoardTaskAsync(board.Id);

        // Act
        var response = await client.GetAsync(
            $"/api/boards/{board.Id}/tasks/{task.Id}/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        dbContext.Dispose();
    }

    [Fact]
    public async Task GetUsersLinkedToTask_WithNonexistentTask_ReturnsNotFound()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1);
        var client = _factory.CreateClientForUser(owner.Id, "VIEWER");
        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id);

        // Act
        var response = await client.GetAsync(
            $"/api/boards/{board.Id}/tasks/99999/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        dbContext.Dispose();
    }

    #endregion

    #region Helper Methods

    private async Task<(ApplicationUser user, string token)> CreateUserAsync(int id)
    {
        var dbContext = _factory.CreateDbContext();
        var userManager = GetUserManager(dbContext);

        var user = new ApplicationUserBuilder()
            .WithEmail($"user{id}@test.com")
            .Build();

        var result = await userManager.CreateAsync(user, "TestPassword123!");
        if (!result.Succeeded)
            throw new InvalidOperationException("Failed to create test user");

        var token = TestJwtTokenGenerator.GenerateToken(user.Id, "VIEWER");
        dbContext.Dispose();

        return (user, token);
    }

    private UserManager<ApplicationUser> GetUserManager(ApplicationDbContext dbContext)
    {
        return _factory.GetUserManager(dbContext);
    }

    #endregion
}
