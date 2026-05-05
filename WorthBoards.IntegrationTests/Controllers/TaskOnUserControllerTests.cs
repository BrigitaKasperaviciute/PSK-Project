using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WorthBoards.Business.Dtos.Requests;
using WorthBoards.Common.Enums;
using WorthBoards.Data.Database;
using WorthBoards.IntegrationTests.Infrastructure;
using Xunit;

namespace WorthBoards.IntegrationTests.Controllers;

[Collection(nameof(IntegrationCollection))]
public sealed class TaskOnUserControllerTests(IntegrationTestAppFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task LinkUsersToTask_WithEditorRole_ReturnsAssignedUsersAndCreatesNotification()
    {
        // Arrange
        var owner = await Factory.CreateUserAsync("Tom", "Hale", "task-link-owner");
        var assignee = await Factory.CreateUserAsync("Uma", "Lake", "task-link-user");
        var board = await Factory.SeedBoardAsync(owner.Id, "Assignee board", "Task linking");
        var task = await Factory.SeedTaskAsync(board.Id, "Assign reviewers", null, TaskStatusEnum.IN_PROGRESS);
        var client = Factory.CreateAuthenticatedClient(owner);
        var request = new[] { new LinkUserToTaskRequest { UserId = assignee.Id } };

        // Act
        var response = await client.PostAsJsonAsync($"/api/boards/{board.Id}/tasks/{task.Id}/users/link", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await Factory.WithDbContextAsync(async db =>
        {
            var link = await db.TasksOnUsers.AsNoTracking()
                .SingleAsync(item => item.BoardTaskId == task.Id && item.UserId == assignee.Id);
            link.UserId.Should().Be(assignee.Id);

            var notifications = await db.Notifications.AsNoTracking()
                .Where(item => item.BoardId == board.Id && item.TaskId == task.Id)
                .ToListAsync();
            notifications.Should().ContainSingle(item => item.NotificationType == NotificationEventTypeEnum.TASK_ASSIGNED);
            notifications.Single().SubjectUserId.Should().Be(assignee.Id);
        });
    }

    [Fact]
    public async Task LinkUsersToTask_WithViewerRole_ReturnsForbiddenAndLeavesAssignmentsEmpty()
    {
        // Arrange
        var owner = await Factory.CreateUserAsync("Vera", "North", "task-link-owner-negative");
        var viewer = await Factory.CreateUserAsync("Will", "Baker", "task-link-viewer-negative");
        var board = await Factory.SeedBoardAsync(owner.Id, "Forbidden board", "Task linking");
        await Factory.SeedBoardUserAsync(board.Id, viewer.Id, UserRoleEnum.VIEWER);
        var task = await Factory.SeedTaskAsync(board.Id, "Do not assign", null, TaskStatusEnum.PENDING);
        var client = Factory.CreateAuthenticatedClient(viewer);

        // Act
        var response = await client.PostAsJsonAsync($"/api/boards/{board.Id}/tasks/{task.Id}/users/link", new[] { new LinkUserToTaskRequest { UserId = owner.Id } });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await Factory.WithDbContextAsync(async db =>
        {
            var links = await db.TasksOnUsers.AsNoTracking().Where(item => item.BoardTaskId == task.Id).ToListAsync();
            links.Should().BeEmpty();
        });
    }
}