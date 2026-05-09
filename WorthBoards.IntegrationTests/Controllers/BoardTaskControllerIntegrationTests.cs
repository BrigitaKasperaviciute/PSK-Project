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
public class BoardTaskControllerIntegrationTests
{
    private readonly ApiFactory _factory;

    public BoardTaskControllerIntegrationTests(ApiFactory factory)
    {
        _factory = factory;
    }

    #region GetAllActiveBoardTasks Tests

    [Fact]
    public async Task GetAllActiveBoardTasks_WithValidBoardId_ReturnsOkAndActiveTasks()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1);
        var client = _factory.CreateClientForUser(owner.Id, "VIEWER");
        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id);
        var task1 = await helper.CreateBoardTaskAsync(board.Id, "Task 1", TaskStatusEnum.PENDING);
        var task2 = await helper.CreateBoardTaskAsync(board.Id, "Task 2", TaskStatusEnum.IN_PROGRESS);
        var task3 = await helper.CreateBoardTaskAsync(board.Id, "Task 3", TaskStatusEnum.ARCHIVED); // Should not appear

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}/tasks");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var tasks = JsonConvert.DeserializeObject<List<BoardTaskResponse>>(content);
        tasks.Should().HaveCount(2);
        tasks!.Should().Contain(t => t.Title == "Task 1" && t.TaskStatus == TaskStatusEnum.PENDING);
        tasks.Should().Contain(t => t.Title == "Task 2" && t.TaskStatus == TaskStatusEnum.IN_PROGRESS);

        dbContext.Dispose();
    }

    [Fact]
    public async Task GetAllActiveBoardTasks_WithUnauthorizedUser_ReturnsForbidden()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1);
        var (outsider, _) = await CreateUserAsync(2);
        var client = _factory.CreateClientForUser(outsider.Id, "VIEWER");
        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id);

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}/tasks");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        dbContext.Dispose();
    }

    #endregion

    #region GetAllArchivedBoardTasks Tests

    [Fact]
    public async Task GetAllArchivedBoardTasks_WithValidBoardId_ReturnsOkAndArchivedTasks()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1);
        var client = _factory.CreateClientForUser(owner.Id, "VIEWER");
        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id);
        var task1 = await helper.CreateBoardTaskAsync(board.Id, "Archived 1", TaskStatusEnum.ARCHIVED);
        var task2 = await helper.CreateBoardTaskAsync(board.Id, "Archived 2", TaskStatusEnum.ARCHIVED);
        var task3 = await helper.CreateBoardTaskAsync(board.Id, "Active", TaskStatusEnum.PENDING); // Should not appear

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}/tasks/archived");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var tasks = JsonConvert.DeserializeObject<List<BoardTaskResponse>>(content);
        tasks.Should().HaveCount(2);
        tasks!.Should().AllSatisfy(t => t.TaskStatus.Should().Be(WorthBoards.Common.Enums.TaskStatusEnum.ARCHIVED));

        dbContext.Dispose();
    }

    #endregion

    #region GetBoardTaskById Tests

    [Fact]
    public async Task GetBoardTaskById_WithValidIds_ReturnsOkAndTaskData()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1);
        var client = _factory.CreateClientForUser(owner.Id, "VIEWER");
        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id);
        var task = await helper.CreateBoardTaskAsync(board.Id, "Test Task", TaskStatusEnum.PENDING, "Description");

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}/tasks/{task.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var taskResponse = JsonConvert.DeserializeObject<BoardTaskResponse>(content);
        taskResponse.Should().NotBeNull();
        taskResponse!.Title.Should().Be("Test Task");
        taskResponse.Description.Should().Be("Description");

        dbContext.Dispose();
    }

    [Fact]
    public async Task GetBoardTaskById_WithNonexistentTask_ReturnsNotFound()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1);
        var client = _factory.CreateClientForUser(owner.Id, "VIEWER");
        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id);

        // Act
        var response = await client.GetAsync($"/api/boards/{board.Id}/tasks/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        dbContext.Dispose();
    }

    #endregion

    #region CreateBoardTask Tests

    [Fact]
    public async Task CreateBoardTask_WithValidData_ReturnsCreatedAndTaskData()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1);
        var client = _factory.CreateClientForUser(owner.Id, "EDITOR");
        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id);
        await helper.AddUserToBoardAsync(board.Id, owner.Id, UserRoleEnum.EDITOR);

        var deadline = DateTime.UtcNow.AddDays(7);
        var createRequest = new BoardTaskRequestBuilder()
            .WithTitle("New Task")
            .WithDescription("Task Description")
            .WithStatus(TaskStatusEnum.PENDING)
            .WithDeadlineEnd(deadline)
            .Build();

        // Act
        var content = new StringContent(
            JsonConvert.SerializeObject(createRequest),
            Encoding.UTF8,
            "application/json");
        var response = await client.PostAsync($"/api/boards/{board.Id}/tasks", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var responseContent = await response.Content.ReadAsStringAsync();
        var taskResponse = JsonConvert.DeserializeObject<BoardTaskResponse>(responseContent);
        taskResponse.Should().NotBeNull();
        taskResponse!.Title.Should().Be("New Task");
        taskResponse!.TaskStatus.Should().Be(WorthBoards.Common.Enums.TaskStatusEnum.PENDING);

        dbContext.Dispose();
    }

    [Fact]
    public async Task CreateBoardTask_WithoutEditorRole_ReturnsForbidden()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1);
        var (viewer, _) = await CreateUserAsync(2);
        var client = _factory.CreateClientForUser(viewer.Id, "VIEWER");
        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id);
        await helper.AddUserToBoardAsync(board.Id, viewer.Id, UserRoleEnum.VIEWER);

        var createRequest = new BoardTaskRequestBuilder().Build();

        // Act
        var content = new StringContent(
            JsonConvert.SerializeObject(createRequest),
            Encoding.UTF8,
            "application/json");
        var response = await client.PostAsync($"/api/boards/{board.Id}/tasks", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        dbContext.Dispose();
    }

    [Fact]
    public async Task CreateBoardTask_WithMissingTitle_ReturnsBadRequest()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1);
        var client = _factory.CreateClientForUser(owner.Id, "EDITOR");
        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id);
        await helper.AddUserToBoardAsync(board.Id, owner.Id, UserRoleEnum.EDITOR);

        var createRequest = new { Description = "Description", TaskStatus = "PENDING" };

        // Act
        var content = new StringContent(
            JsonConvert.SerializeObject(createRequest),
            Encoding.UTF8,
            "application/json");
        var response = await client.PostAsync($"/api/boards/{board.Id}/tasks", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        dbContext.Dispose();
    }

    #endregion

    #region UpdateBoardTask Tests

    [Fact]
    public async Task UpdateBoardTask_WithValidData_ReturnsOkAndUpdatedTask()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1);
        var client = _factory.CreateClientForUser(owner.Id, "EDITOR");
        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id);
        await helper.AddUserToBoardAsync(board.Id, owner.Id, UserRoleEnum.EDITOR);
        var task = await helper.CreateBoardTaskAsync(board.Id, "Original Task", TaskStatusEnum.PENDING);

        var updateRequest = new BoardTaskUpdateRequestBuilder()
            .WithTitle("Updated Task")
            .WithStatus(TaskStatusEnum.IN_PROGRESS)
            .WithVersion(task.Version)
            .Build();

        // Act
        var content = new StringContent(
            JsonConvert.SerializeObject(updateRequest),
            Encoding.UTF8,
            "application/json");
        var response = await client.PutAsync($"/api/boards/{board.Id}/tasks/{task.Id}", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseContent = await response.Content.ReadAsStringAsync();
        var taskResponse = JsonConvert.DeserializeObject<BoardTaskResponse>(responseContent);
        taskResponse.Should().NotBeNull();
        taskResponse!.Title.Should().Be("Updated Task");
        taskResponse.TaskStatus.Should().Be(WorthBoards.Common.Enums.TaskStatusEnum.IN_PROGRESS);

        dbContext.Dispose();
    }

    [Fact]
    public async Task UpdateBoardTask_WithInvalidVersion_ReturnsBadRequest()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1);
        var client = _factory.CreateClientForUser(owner.Id, "EDITOR");
        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id);
        await helper.AddUserToBoardAsync(board.Id, owner.Id, UserRoleEnum.EDITOR);
        var task = await helper.CreateBoardTaskAsync(board.Id, "Task", TaskStatusEnum.PENDING);

        var updateRequest = new BoardTaskUpdateRequestBuilder()
            .WithVersion(999) // Wrong version
            .Build();

        // Act
        var content = new StringContent(
            JsonConvert.SerializeObject(updateRequest),
            Encoding.UTF8,
            "application/json");
        var response = await client.PutAsync($"/api/boards/{board.Id}/tasks/{task.Id}", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        dbContext.Dispose();
    }

    #endregion

    #region DeleteBoardTask Tests

    [Fact]
    public async Task DeleteBoardTask_WithValidIds_ReturnsNoContent()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1);
        var client = _factory.CreateClientForUser(owner.Id, "EDITOR");
        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id);
        await helper.AddUserToBoardAsync(board.Id, owner.Id, UserRoleEnum.EDITOR);
        var task = await helper.CreateBoardTaskAsync(board.Id, "Task to Delete");

        // Act
        var response = await client.DeleteAsync($"/api/boards/{board.Id}/tasks/{task.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify deletion
        var deletedTask = dbContext.BoardTasks.FirstOrDefault(t => t.Id == task.Id);
        deletedTask.Should().BeNull();

        dbContext.Dispose();
    }

    [Fact]
    public async Task DeleteBoardTask_WithoutEditorRole_ReturnsForbidden()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1);
        var (viewer, _) = await CreateUserAsync(2);
        var client = _factory.CreateClientForUser(viewer.Id, "VIEWER");
        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id);
        await helper.AddUserToBoardAsync(board.Id, viewer.Id, UserRoleEnum.VIEWER);
        var task = await helper.CreateBoardTaskAsync(board.Id, "Task");

        // Act
        var response = await client.DeleteAsync($"/api/boards/{board.Id}/tasks/{task.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        dbContext.Dispose();
    }

    #endregion

    #region DeleteArchivedBoardTasks Tests

    [Fact]
    public async Task DeleteArchivedBoardTasks_WithValidBoard_DeletesOnlyArchivedTasks()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1);
        var client = _factory.CreateClientForUser(owner.Id, "EDITOR");
        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id);
        await helper.AddUserToBoardAsync(board.Id, owner.Id, UserRoleEnum.EDITOR);
        var archived1 = await helper.CreateBoardTaskAsync(board.Id, "Archived 1", TaskStatusEnum.ARCHIVED);
        var archived2 = await helper.CreateBoardTaskAsync(board.Id, "Archived 2", TaskStatusEnum.ARCHIVED);
        var pending = await helper.CreateBoardTaskAsync(board.Id, "Pending", TaskStatusEnum.PENDING);

        // Act
        var response = await client.DeleteAsync($"/api/boards/{board.Id}/tasks/archived");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify only archived tasks are deleted
        var remainingTasks = dbContext.BoardTasks.Where(t => t.BoardId == board.Id).ToList();
        remainingTasks.Should().HaveCount(1);
        remainingTasks[0].Id.Should().Be(pending.Id);

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

        var token = TestJwtTokenGenerator.GenerateToken(user.Id, "OWNER");
        dbContext.Dispose();

        return (user, token);
    }

    private UserManager<ApplicationUser> GetUserManager(ApplicationDbContext dbContext)
    {
        return _factory.GetUserManager(dbContext);
    }

    #endregion
}
