using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using WorthBoards.Business.Dtos.Responses;
using WorthBoards.Common.Enums;
using WorthBoards.Common.Exceptions;
using WorthBoards.Data.Database;
using WorthBoards.IntegrationTests.Infrastructure;
using Xunit;

namespace WorthBoards.IntegrationTests.Controllers;

[Collection(nameof(IntegrationCollection))]
public sealed class NotificationControllerTests(IntegrationTestAppFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task AcceptInvitation_WithValidInvitation_AddsBoardMembershipAndConsumesNotification()
    {
        // Arrange
        var owner = await Factory.CreateUserAsync("Quentin", "Stone", "notification-owner");
        var invitee = await Factory.CreateUserAsync("Rita", "Vale", "notification-invitee");
        var board = await Factory.SeedBoardAsync(owner.Id, "Notifications board", "Accept invitation");
        var invitation = await Factory.SeedNotificationAsync(
            board.Id,
            owner.Id,
            NotificationEventTypeEnum.INVITATION,
            subjectUserId: invitee.Id,
            invitationRole: UserRoleEnum.VIEWER,
            notificationUserIds: new[] { invitee.Id });
        var client = Factory.CreateAuthenticatedClient(invitee);

        // Act
        var response = await client.PostAsync($"/api/notifications/{invitation.Id}/accept", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await Factory.WithDbContextAsync(async db =>
        {
            var boardLink = await db.BoardOnUsers.AsNoTracking()
                .SingleAsync(item => item.BoardId == board.Id && item.UserId == invitee.Id);
            boardLink.UserRole.Should().Be(UserRoleEnum.VIEWER);

            var invitationRow = await db.Notifications.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == invitation.Id);
            invitationRow.Should().BeNull();

            var notifications = await db.Notifications.AsNoTracking()
                .Where(item => item.BoardId == board.Id)
                .ToListAsync();
            notifications.Should().ContainSingle(item => item.NotificationType == NotificationEventTypeEnum.USER_ADDED_TO_BOARD);
        });
    }

    [Fact]
    public async Task DeleteNotification_WhenNotificationMissing_ReturnsNotFoundAndKeepsExistingRows()
    {
        // Arrange
        var user = await Factory.CreateUserAsync("Sara", "Mills", "notification-delete");
        var board = await Factory.SeedBoardAsync(user.Id, "Delete notification board", "Negative flow");
        var notification = await Factory.SeedNotificationAsync(
            board.Id,
            user.Id,
            NotificationEventTypeEnum.TASK_CREATED,
            notificationUserIds: new[] { user.Id },
            taskId: null);
        var client = Factory.CreateAuthenticatedClient(user);

        // Act
        var response = await client.DeleteAsync($"/api/notifications/{notification.Id + 1000}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var error = await response.Content.ReadFromJsonAsync<ErrorDetails>();
        error.Should().NotBeNull();
        error!.StatusCode.Should().Be((int)HttpStatusCode.NotFound);

        await Factory.WithDbContextAsync(async db =>
        {
            var notifications = await db.Notifications.AsNoTracking().ToListAsync();
            notifications.Should().ContainSingle(item => item.Id == notification.Id);
        });
    }

    [Fact]
    public async Task GetDeleteAndDeleteAll_WithAuthenticatedUser_ExercisesNotificationLifecycle()
    {
        // Arrange
        var user = await Factory.CreateUserAsync("Tracy", "Vale", "notification-lifecycle");
        var board = await Factory.SeedBoardAsync(user.Id, "Lifecycle notifications board", "Notification reads and deletes");
        var task = await Factory.SeedTaskAsync(board.Id, "Notification task", "Used for notification formatting", TaskStatusEnum.PENDING);
        var first = await Factory.SeedNotificationAsync(
            board.Id,
            user.Id,
            NotificationEventTypeEnum.TASK_CREATED,
            taskId: task.Id,
            notificationUserIds: new[] { user.Id });
        var second = await Factory.SeedNotificationAsync(
            board.Id,
            user.Id,
            NotificationEventTypeEnum.TASK_CREATED,
            taskId: task.Id,
            notificationUserIds: new[] { user.Id });
        var client = Factory.CreateAuthenticatedClient(user);

        // Act - get all
        var getResponse = await client.GetAsync("/api/notifications");

        // Assert - get all
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var getBody = await getResponse.Content.ReadFromJsonAsync<List<NotificationResponse>>();
        getBody.Should().Contain(item => item.Id == first.Id);
        getBody.Should().Contain(item => item.Id == second.Id);

        // Act - delete one notification
        var deleteOneResponse = await client.DeleteAsync($"/api/notifications/{first.Id}");

        // Assert - delete one
        deleteOneResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act - delete all remaining notifications
        var deleteAllResponse = await client.DeleteAsync("/api/notifications");

        // Assert - delete all
        deleteAllResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        await Factory.WithDbContextAsync(async db =>
        {
            var links = await db.NotificationsOnUsers.AsNoTracking().ToListAsync();
            links.Should().BeEmpty();
        });
    }
}