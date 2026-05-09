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
using WorthBoards.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using WorthBoards.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace WorthBoards.IntegrationTests.Controllers;

[Collection(nameof(ApiCollection))]
public class NotificationControllerIntegrationTests
{
    private readonly ApiFactory _factory;

    public NotificationControllerIntegrationTests(ApiFactory factory)
    {
        _factory = factory;
    }

    #region GetNotificationsByUserId Tests

    [Fact]
    public async Task GetNotificationsByUserId_WithExistingNotifications_ReturnsOkAndNotificationList()
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

        // Create notifications for the viewer
        var notification1 = new Notification
        {
            SenderId = owner.Id,
            SendDate = DateTime.UtcNow,
            NotificationType = NotificationEventTypeEnum.USER_ADDED_TO_BOARD,
            BoardId = board.Id,
            SubjectUserId = viewer.Id,
        };
        var notification2 = new Notification
        {
            SenderId = owner.Id,
            SendDate = DateTime.UtcNow.AddHours(-1),
            NotificationType = NotificationEventTypeEnum.INVITATION,
            BoardId = board.Id,
            InvitationRole = UserRoleEnum.VIEWER,
        };

        dbContext.Notifications.AddRange(notification1, notification2);
        await dbContext.SaveChangesAsync();

        var notificationOnUser1 = new { NotificationId = notification1.Id, UserId = viewer.Id };
        var notificationOnUser2 = new { NotificationId = notification2.Id, UserId = viewer.Id };
        await dbContext.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"NotificationsOnUsers\" (\"NotificationId\", \"UserId\") VALUES ({0}, {1})",
            notification1.Id, viewer.Id);
        await dbContext.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"NotificationsOnUsers\" (\"NotificationId\", \"UserId\") VALUES ({0}, {1})",
            notification2.Id, viewer.Id);

        // Act
        var response = await client.GetAsync("/api/notifications");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var notifications = JsonConvert.DeserializeObject<List<NotificationResponse>>(content);
        notifications.Should().NotBeNull();
        notifications!.Should().HaveCountGreaterThanOrEqualTo(2);

        dbContext.Dispose();
    }

    [Fact]
    public async Task GetNotificationsByUserId_WithNoNotifications_ReturnsOkAndEmptyList()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (user, _) = await CreateUserAsync(1);
        var client = _factory.CreateClientForUser(user.Id, "VIEWER");

        // Act
        var response = await client.GetAsync("/api/notifications");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var notifications = JsonConvert.DeserializeObject<List<NotificationResponse>>(content);
        notifications.Should().NotBeNull();
        notifications.Should().BeEmpty();
    }

    [Fact]
    public async Task GetNotificationsByUserId_WithoutAuthorization_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/notifications");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region AcceptInvitation Tests

    [Fact]
    public async Task AcceptInvitation_WithValidInvitationNotification_ReturnsOkAndAddsUserToBoard()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1);
        var (invitee, _) = await CreateUserAsync(2);
        var client = _factory.CreateClientForUser(invitee.Id, "VIEWER");
        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id);

        // Create invitation notification
        var invitationNotification = new Notification
        {
            SenderId = owner.Id,
            SendDate = DateTime.UtcNow,
            NotificationType = NotificationEventTypeEnum.INVITATION,
            SubjectUserId = invitee.Id,
            BoardId = board.Id,
            InvitationRole = UserRoleEnum.EDITOR,
        };
        dbContext.Notifications.Add(invitationNotification);
        await dbContext.SaveChangesAsync();

        await dbContext.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"NotificationsOnUsers\" (\"NotificationId\", \"UserId\") VALUES ({0}, {1})",
            invitationNotification.Id, invitee.Id);

        // Act
        var response = await client.PostAsync(
            $"/api/notifications/{invitationNotification.Id}/accept", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify user is added to board with correct role
        var userRole = helper.GetUserBoardRole(board.Id, invitee.Id);
        userRole.Should().Be(UserRoleEnum.EDITOR);

        dbContext.Dispose();
    }

    [Fact]
    public async Task AcceptInvitation_WithNonexistentNotification_ReturnsNotFound()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (user, _) = await CreateUserAsync(1);
        var client = _factory.CreateClientForUser(user.Id, "VIEWER");

        // Act
        var response = await client.PostAsync("/api/notifications/99999/accept", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AcceptInvitation_WithNonInvitationNotification_ReturnsBadRequest()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1);
        var (user, _) = await CreateUserAsync(2);
        var client = _factory.CreateClientForUser(user.Id, "VIEWER");
        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id);
        var task = await helper.CreateBoardTaskAsync(board.Id);

        // Create non-invitation notification
        var taskNotification = new Notification
        {
            SenderId = owner.Id,
            SendDate = DateTime.UtcNow,
            NotificationType = NotificationEventTypeEnum.TASK_CREATED,
            BoardId = board.Id,
            TaskId = task.Id,
        };
        dbContext.Notifications.Add(taskNotification);
        await dbContext.SaveChangesAsync();

        await dbContext.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"NotificationsOnUsers\" (\"NotificationId\", \"UserId\") VALUES ({0}, {1})",
            taskNotification.Id, user.Id);

        // Act
        var response = await client.PostAsync($"/api/notifications/{taskNotification.Id}/accept", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        dbContext.Dispose();
    }

    [Fact]
    public async Task AcceptInvitation_ByUnauthorizedUser_ReturnsForbidden()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1);
        var (invitee, _) = await CreateUserAsync(2);
        var (outsider, _) = await CreateUserAsync(3);
        var client = _factory.CreateClientForUser(outsider.Id, "VIEWER");
        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id);

        // Create invitation for invitee, not outsider
        var invitationNotification = new Notification
        {
            SenderId = owner.Id,
            SendDate = DateTime.UtcNow,
            NotificationType = NotificationEventTypeEnum.INVITATION,
            SubjectUserId = invitee.Id,
            BoardId = board.Id,
            InvitationRole = UserRoleEnum.VIEWER,
        };
        dbContext.Notifications.Add(invitationNotification);
        await dbContext.SaveChangesAsync();

        // Only add to invitee
        await dbContext.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"NotificationsOnUsers\" (\"NotificationId\", \"UserId\") VALUES ({0}, {1})",
            invitationNotification.Id, invitee.Id);

        // Act - Outsider tries to accept
        var response = await client.PostAsync(
            $"/api/notifications/{invitationNotification.Id}/accept", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        dbContext.Dispose();
    }

    #endregion

    #region DeleteNotification Tests

    [Fact]
    public async Task DeleteNotification_WithValidNotification_ReturnsNoContent()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1);
        var (user, _) = await CreateUserAsync(2);
        var client = _factory.CreateClientForUser(user.Id, "VIEWER");
        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id);

        var notification = new Notification
        {
            SenderId = owner.Id,
            SendDate = DateTime.UtcNow,
            NotificationType = NotificationEventTypeEnum.USER_ADDED_TO_BOARD,
            BoardId = board.Id,
            SubjectUserId = user.Id,
        };
        dbContext.Notifications.Add(notification);
        await dbContext.SaveChangesAsync();

        await dbContext.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"NotificationsOnUsers\" (\"NotificationId\", \"UserId\") VALUES ({0}, {1})",
            notification.Id, user.Id);

        // Act
        var response = await client.DeleteAsync($"/api/notifications/{notification.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify notification is deleted for user
        var notificationOnUser = await dbContext.NotificationsOnUsers
            .Where(n => n.NotificationId == notification.Id && n.UserId == user.Id)
            .ToListAsync();
        notificationOnUser.Should().BeEmpty();

        dbContext.Dispose();
    }

    [Fact]
    public async Task DeleteNotification_ByUnauthorizedUser_ReturnsForbidden()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1);
        var (recipient, _) = await CreateUserAsync(2);
        var (outsider, _) = await CreateUserAsync(3);
        var client = _factory.CreateClientForUser(outsider.Id, "VIEWER");
        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id);

        var notification = new Notification
        {
            SenderId = owner.Id,
            SendDate = DateTime.UtcNow,
            NotificationType = NotificationEventTypeEnum.USER_ADDED_TO_BOARD,
            BoardId = board.Id,
            SubjectUserId = recipient.Id,
        };
        dbContext.Notifications.Add(notification);
        await dbContext.SaveChangesAsync();

        // Only add to recipient
        dbContext.NotificationsOnUsers.Add(new WorthBoards.Domain.Entities.NotificationOnUser { NotificationId = notification.Id, UserId = recipient.Id });
        await dbContext.SaveChangesAsync();

        // Act - Outsider tries to delete
        var response = await client.DeleteAsync($"/api/notifications/{notification.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        dbContext.Dispose();
    }

    [Fact]
    public async Task DeleteNotification_WithNonexistentNotification_ReturnsNotFound()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (user, _) = await CreateUserAsync(1);
        var client = _factory.CreateClientForUser(user.Id, "VIEWER");

        // Act
        var response = await client.DeleteAsync("/api/notifications/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region DeleteAllNotifications Tests

    [Fact]
    public async Task DeleteAllNotifications_WithMultipleNotifications_ReturnsNoContent()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (owner, _) = await CreateUserAsync(1);
        var (user, _) = await CreateUserAsync(2);
        var client = _factory.CreateClientForUser(user.Id, "VIEWER");
        var dbContext = _factory.CreateDbContext();
        var helper = new TestDataHelper(dbContext, GetUserManager(dbContext));

        var board = await helper.CreateBoardAsync(owner.Id);

        // Create multiple notifications
        var notification1 = new Notification
        {
            SenderId = owner.Id,
            SendDate = DateTime.UtcNow,
            NotificationType = NotificationEventTypeEnum.USER_ADDED_TO_BOARD,
            BoardId = board.Id,
        };
        var notification2 = new Notification
        {
            SenderId = owner.Id,
            SendDate = DateTime.UtcNow,
            NotificationType = NotificationEventTypeEnum.INVITATION,
            BoardId = board.Id,
            InvitationRole = UserRoleEnum.VIEWER,
        };
        dbContext.Notifications.AddRange(notification1, notification2);
        await dbContext.SaveChangesAsync();

        dbContext.NotificationsOnUsers.AddRange(new WorthBoards.Domain.Entities.NotificationOnUser { NotificationId = notification1.Id, UserId = user.Id },
            new WorthBoards.Domain.Entities.NotificationOnUser { NotificationId = notification2.Id, UserId = user.Id });
        await dbContext.SaveChangesAsync();

        // Act
        var response = await client.DeleteAsync("/api/notifications");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify all notifications are deleted for user
        var dbContext2 = _factory.CreateDbContext();
        var remainingNotifications = await dbContext2.NotificationsOnUsers
            .Where(n => n.UserId == user.Id)
            .ToListAsync();
        remainingNotifications.Should().BeEmpty();

        dbContext.Dispose();
        dbContext2.Dispose();
    }

    [Fact]
    public async Task DeleteAllNotifications_WithNoNotifications_ReturnsNoContent()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (user, _) = await CreateUserAsync(1);
        var client = _factory.CreateClientForUser(user.Id, "VIEWER");

        // Act
        var response = await client.DeleteAsync("/api/notifications");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeleteAllNotifications_WithoutAuthorization_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.DeleteAsync("/api/notifications");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
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
