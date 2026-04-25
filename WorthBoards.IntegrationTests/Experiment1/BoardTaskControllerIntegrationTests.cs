using System.Net;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Common.Enums;
using WorthBoards.IntegrationTests.Experiment1.TestInfrastructure;
using Xunit;

namespace WorthBoards.IntegrationTests.Experiment1;

public class BoardTaskControllerIntegrationTests(IntegrationTestWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    [Fact]
    public async Task CreateBoardTask_EditorRequest_CreatesTaskAndReturnsCreated()
    {
        // Arrange
        var session = await CreateAuthenticatedUserAsync("task_happy");
        var boardId = await CreateBoardAsync(session.Client, "Task Board");

        var request = new BoardTaskRequest
        {
            Title = "Prepare release",
            Description = "Deploy release candidate",
            DeadlineEnd = DateTime.UtcNow.AddDays(1),
            TaskStatus = TaskStatusEnum.PENDING
        };

        // Act
        var response = await session.Client.PostAsJsonAsync($"/api/boards/{boardId}/tasks", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Prepare release");

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WorthBoards.Data.Database.ApplicationDbContext>();
        var persistedTask = await dbContext.BoardTasks.SingleAsync(t => t.BoardId == boardId && t.Title == request.Title);
        persistedTask.TaskStatus.Should().Be(TaskStatusEnum.PENDING);
    }

    [Fact]
    public async Task CreateBoardTask_MissingTitle_ReturnsBadRequestAndDoesNotPersistTask()
    {
        // Arrange
        var session = await CreateAuthenticatedUserAsync("task_negative");
        var boardId = await CreateBoardAsync(session.Client, "Task Validation Board");

        using var arrangeScope = Factory.Services.CreateScope();
        var arrangeDb = arrangeScope.ServiceProvider.GetRequiredService<WorthBoards.Data.Database.ApplicationDbContext>();
        var taskCountBefore = await arrangeDb.BoardTasks.CountAsync();

        var request = new BoardTaskRequest
        {
            Title = string.Empty,
            Description = "Invalid",
            DeadlineEnd = DateTime.UtcNow.AddDays(1),
            TaskStatus = TaskStatusEnum.PENDING
        };

        // Act
        var response = await session.Client.PostAsJsonAsync($"/api/boards/{boardId}/tasks", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Title");

        using var assertScope = Factory.Services.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<WorthBoards.Data.Database.ApplicationDbContext>();
        var taskCountAfter = await assertDb.BoardTasks.CountAsync();
        taskCountAfter.Should().Be(taskCountBefore);
    }

    [Fact]
    public async Task GetAllActiveBoardTasks_ExistingTasks_ReturnsOnlyNonArchived()
    {
        // Arrange
        var session = await CreateAuthenticatedUserAsync("task_get_active");
        var boardId = await CreateBoardAsync(session.Client, "Task List Board");
        await CreateBoardTaskAsync(session.Client, boardId, "Active Task", TaskStatusEnum.PENDING);
        await CreateBoardTaskAsync(session.Client, boardId, "Archived Task", TaskStatusEnum.ARCHIVED);

        // Act
        var response = await session.Client.GetAsync($"/api/boards/{boardId}/tasks");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Active Task");
        body.Should().NotContain("Archived Task");
    }

    [Fact]
    public async Task GetBoardTaskById_ExistingTask_ReturnsTask()
    {
        // Arrange
        var session = await CreateAuthenticatedUserAsync("task_get_by_id");
        var boardId = await CreateBoardAsync(session.Client, "Task By Id Board");
        var taskId = await CreateBoardTaskAsync(session.Client, boardId, "Task Lookup");

        // Act
        var response = await session.Client.GetAsync($"/api/boards/{boardId}/tasks/{taskId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var task = await response.Content.ReadFromJsonAsync<BoardTaskResponse>();
        task.Should().NotBeNull();
        task!.Id.Should().Be(taskId);
    }

    [Fact]
    public async Task UpdateBoardTask_ValidRequest_UpdatesTaskStatus()
    {
        // Arrange
        var session = await CreateAuthenticatedUserAsync("task_update");
        var boardId = await CreateBoardAsync(session.Client, "Task Update Board");
        var taskId = await CreateBoardTaskAsync(session.Client, boardId, "Update Task", TaskStatusEnum.PENDING);
        var existing = await session.Client.GetFromJsonAsync<BoardTaskResponse>($"/api/boards/{boardId}/tasks/{taskId}");

        var request = new BoardTaskUpdateRequest
        {
            Title = existing!.Title,
            Description = existing.Description,
            DeadlineEnd = existing.DeadlineEnd,
            TaskStatus = TaskStatusEnum.IN_PROGRESS,
            Version = existing.Version
        };

        // Act
        var response = await session.Client.PutAsJsonAsync($"/api/boards/{boardId}/tasks/{taskId}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WorthBoards.Data.Database.ApplicationDbContext>();
        var task = await db.BoardTasks.SingleAsync(t => t.Id == taskId);
        task.TaskStatus.Should().Be(TaskStatusEnum.IN_PROGRESS);
    }

    [Fact]
    public async Task PatchBoardTask_ValidPatch_UpdatesTaskTitle()
    {
        // Arrange
        var session = await CreateAuthenticatedUserAsync("task_patch");
        var boardId = await CreateBoardAsync(session.Client, "Task Patch Board");
        var taskId = await CreateBoardTaskAsync(session.Client, boardId, "Before Patch");
        var patchBody = "[{\"op\":\"replace\",\"path\":\"/title\",\"value\":\"After Patch\"}]";
        using var content = new StringContent(patchBody, Encoding.UTF8, "application/json-patch+json");

        // Act
        var response = await session.Client.PatchAsync($"/api/boards/{boardId}/tasks/{taskId}", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WorthBoards.Data.Database.ApplicationDbContext>();
        var task = await db.BoardTasks.SingleAsync(t => t.Id == taskId);
        task.Title.Should().Be("After Patch");
    }

    [Fact]
    public async Task DeleteBoardTask_ExistingTask_RemovesTask()
    {
        // Arrange
        var session = await CreateAuthenticatedUserAsync("task_delete");
        var boardId = await CreateBoardAsync(session.Client, "Task Delete Board");
        var taskId = await CreateBoardTaskAsync(session.Client, boardId, "Delete Task");

        // Act
        var response = await session.Client.DeleteAsync($"/api/boards/{boardId}/tasks/{taskId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WorthBoards.Data.Database.ApplicationDbContext>();
        var exists = await db.BoardTasks.AnyAsync(t => t.Id == taskId);
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteArchivedBoardTasks_ArchivedItemsExist_RemovesArchivedOnly()
    {
        // Arrange
        var session = await CreateAuthenticatedUserAsync("task_delete_archived");
        var boardId = await CreateBoardAsync(session.Client, "Archived Delete Board");
        var archivedTaskId = await CreateBoardTaskAsync(session.Client, boardId, "Archived", TaskStatusEnum.ARCHIVED);
        await CreateBoardTaskAsync(session.Client, boardId, "Active", TaskStatusEnum.PENDING);

        // Act
        var response = await session.Client.DeleteAsync($"/api/boards/{boardId}/tasks/archived");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WorthBoards.Data.Database.ApplicationDbContext>();
        var archivedExists = await db.BoardTasks.AnyAsync(t => t.Id == archivedTaskId);
        archivedExists.Should().BeFalse();
        var activeCount = await db.BoardTasks.CountAsync(t => t.BoardId == boardId && t.TaskStatus != TaskStatusEnum.ARCHIVED);
        activeCount.Should().Be(1);
    }

    [Fact]
    public async Task GetAllArchivedBoardTasks_ArchivedTasksExist_ReturnsArchivedTasks()
    {
        // Arrange
        var session = await CreateAuthenticatedUserAsync("task_archived_list");
        var boardId = await CreateBoardAsync(session.Client, "Archived Board");
        await CreateBoardTaskAsync(session.Client, boardId, "Archived 1", TaskStatusEnum.ARCHIVED);
        await CreateBoardTaskAsync(session.Client, boardId, "Active 1", TaskStatusEnum.PENDING);

        // Act
        var response = await session.Client.GetAsync($"/api/boards/{boardId}/tasks/archived");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Archived 1");
        body.Should().NotContain("Active 1");
    }
}
